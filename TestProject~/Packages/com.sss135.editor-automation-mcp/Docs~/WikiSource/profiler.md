# profiler

Read-only analysis of Unity Profiler data. These tools answer "where did the
frame time go", "how does this counter behave over the run", and "which frames
spiked" against whatever is currently in the Profiler's in-memory buffer — a
live recording **or** the last-loaded capture file. Everything here reads back
`UnityEditorInternal.ProfilerDriver` and the `UnityEditor.Profiling`
`*FrameDataView` family on the Editor main thread; nothing is recomputed, no
runtime instrumentation is required.

This namespace never controls recording. Starting and stopping a capture stays
entirely in your hands — use the Profiler window's Record button (or load a
saved file with `profiler.load-capture`). The agent only analyzes data Unity
already captured.

## Recording is the user's job

There is no `profiler.start` / `profiler.stop`, and deliberately so. Unity
captures everything natively the moment you arm the Profiler window's Record
button, so the only useful surface is analysis of an already-captured session.
Before calling any read tool, make sure a recording exists: either it is live
in the Profiler window now, or you have loaded a file with
`profiler.load-capture`. An empty buffer makes the read tools throw
`No profiler frames available` (except `profiler.describe`, which reports the
empty state instead).

Be careful about domain reloads. Recompiling the plugin clears the current live
capture, so re-record (or load a `.raw`/`.data` file) after any recompile before
trusting a read. See the recompile note on the [root wiki page](wiki.md).

## One global buffer

`ProfilerDriver` is a single editor-wide singleton, so there is **no per-call
`source` argument** — every read operates on the one buffer that is loaded right
now. `profiler.load-capture` is the only stateful tool: it calls
`ProfilerDriver.LoadProfile` and **replaces the global buffer**, which also
changes what the Profiler window shows and what every other `profiler.*` read
returns, until you re-record or load a different file. This is the deliberate
divergence from [`recorder`](recorder.md), which takes a `session` argument on
every call — the recorder owns its own session files, the profiler shares
Unity's single window-backed buffer.

## describe, then summarize, then drill

The intended flow funnels from an orientation call to a per-counter summary to a
single frame — it never dumps every frame's markers:

1. **`profiler.describe`** — the orientation call. Frame range, thread names, and
   the available counter list (name / category / unit). Counter names from here
   are the inputs to the other tools.
2. **Find the moment.** `profiler.summarize-counter` for one counter's
   min/max/mean/p95/p99 plus the **argmax frame** — that frame index is the
   drill handle. `profiler.find-spikes` lists the frames that break out of the
   counter's normal range. `profiler.get-series` returns a bounded,
   peak-preserving slice when you need the shape over a window.
3. **Drill into a frame.** Feed an argmax / spike frame into
   `profiler.frame-breakdown` for that frame's top markers (inverted + merged
   hierarchy: leaf functions aggregated, sorted by self time, total time, or GC
   bytes).

`profiler.query` is the C# escape hatch for ad-hoc questions the structured
tools can't express; it runs a read-only snippet against a `ProfilerQueryContext`
view of the buffer.

## Reading the numbers

- **Editor numbers are relative only.** The Profiler running inside the Editor
  measures the Editor process, not a clean player build — there is editor
  overhead in every frame and no IL2CPP. Use the figures to compare frames,
  counters, and code paths against *each other*; do not read them as the
  shipping-build cost. For absolute timings, profile a development player (out of
  scope for this surface).
- **Time counters are nanoseconds; GC is bytes.** Each counter's `unit` string
  says what its values mean — `"ns"` for time, `"bytes"` for memory, `"count"`,
  `"percent"`, `"hz"`, or `""` when unknown (common for a loaded capture whose
  counter metadata isn't live). `GC Allocated In Frame` is a *byte* count of
  managed allocation, not a time — high values mean GC pressure, not slow code.
  `frameTimeMs` on a breakdown and the `*Ms` marker columns are milliseconds.
- **Counters vs threads; finding CPU spikes.** The counter tools
  (`summarize-counter` / `find-spikes` / `get-series`) read **per-frame counters** —
  `describe` lists the readable ones. Thread names like **"Main Thread" are not
  counters** and will throw. For CPU/GPU frame time use Unity's readable
  **`"CPU Total Frame Time"`** / **`"CPU Main Thread Frame Time"`** / **`"GPU Frame Time"`**
  counters (ns), or `frame-breakdown {frame:-1}` for the single worst frame. An
  unknown or no-data counter name throws (with the readable list) rather than
  silently returning empty.
- **No frames are hidden.** "Worst frame" (`profiler.frame-breakdown {frame:-1}`)
  is the true max CPU frame-time frame, and spike detection scans every frame —
  nothing is silently excluded. A breakdown's `IsLoadFrame` flags when the chosen
  frame is a real scene load (`Application.LoadLevelAsync`) rather than gameplay;
  first-frame JIT spikes and domain-reload frames are obvious from their markers
  (`Mono.JIT`, `Profiler.Callstack`). To focus on steady state, window past
  warmup with the `from`/`to` arguments.

## See also

- [`recorder`](recorder.md) — the temporal counterpart. The recorder tracks how
  an instrumented gameplay value evolved across frames; the profiler tracks where
  engine/CPU time and allocation went. Both are read-only, but the recorder owns
  per-session files while the profiler shares Unity's single buffer.
- [`console`](console.md) — point-in-time `Debug.Log*` triage; pair an error's
  frame with a `profiler.frame-breakdown` of that frame.
- [`screenshot`](screenshot.md) — visual evidence at a moment, paired with the
  profiler's numeric breakdown of the same frame.
- [`editor`](editor.md) — confirm a settled domain (`IsCompiling:false`) after a
  recompile before trusting a read; a reload clears the live capture.

### profiler.describe

Call this first. It is the only read tool that does **not** throw on an empty
buffer — it reports the empty state instead. Returns a compact plain-text summary
(frame range, threads, and the readable counters grouped by category). Counter
availability reflects the live session, so a loaded capture may expose counters
with empty `Category`/`Unit` metadata. The list is filtered to counters actually
readable per-frame; for CPU/GPU frame time use Unity's `"CPU Total Frame Time"` /
`"CPU Main Thread Frame Time"` / `"GPU Frame Time"` counters (ns).

### profiler.frame-breakdown

`frame < 0` (the default) selects the **worst** frame — the true max CPU
frame-time frame (the result's `IsLoadFrame` flags whether that frame is a scene
load rather than gameplay). Markers come from an inverted, same-name-merged hierarchy, so each row is a leaf function
aggregated across the frame rather than a single call-stack node; cross-check a
row against the Profiler window's inverted hierarchy for that frame. `sortBy` is
one of `"self"`, `"total"`, or `"gc"` — any other value fails loud.

### profiler.find-spikes

Pass an absolute `threshold` (counter values above it are spikes) **or** an
n-`sigma` multiplier (values above `mean + sigma·stddev`). With both left at 0 it
defaults to 3σ. Spikes come back ordered by descending value and capped — the
envelope's `Truncated`/`TotalCount` report any elision. Operates on a per-frame
counter — an unknown name or one with no per-frame data (e.g. a thread name)
throws. Use `"CPU Total Frame Time"` to find CPU frame-time spikes.

### profiler.summarize-counter

Operates on a per-frame counter; an unknown name or one with no per-frame data
(e.g. a thread name like "Main Thread") throws with guidance. Use
`"CPU Total Frame Time"` (ns) to summarize CPU frame time — `ArgMaxFrame` is the
drill handle for `profiler.frame-breakdown`.

### profiler.get-series

Operates on a per-frame counter; an unknown name or one with no per-frame data
throws. Use `"CPU Total Frame Time"` (ns) for the CPU frame-time shape over a
window. Points are peak-preserving downsampled, so spikes survive.

### profiler.query

The escape hatch for questions the structured tools can't express. You write a
C# method body ending in `return <value>;` against `q`, a **read-only**
`ProfilerQueryContext` over the current buffer (`q.FirstFrame`, `q.LastFrame`,
`q.CounterValue(frame, name)`, `q.CounterSeries(...)`, `q.Breakdown(...)`,
`q.WorstFrame()`, and `q.IsLoadFrame(...)`). It exposes no mutators, so
the snippet cannot load a capture or alter the buffer.

Inline `body` calls require a `fileName` and a `description`. The wrapped source
is persisted to `{projectRoot}/Library/McpQueries/Profiler/<fileName>.cs` (a `.cs`
extension is appended if omitted, and a same-named file is overwritten), with the
`description` written as a `//` comment header at the top. `Library/` survives
Editor restarts, so named queries accumulate as a reusable, self-documenting
library. The response always returns `ArtifactPath` pointing at that file, so a
compile-failed or to-be-tweaked query can be edited in place and re-run via
`filePath` — which is mutually exclusive with `body`, `fileName`, and
`description` (a saved file already carries its own header and is compiled
verbatim).

The response is a `ToolRunEnvelope`: `Status` (`"executed"` / `"compile_failed"` /
`"runtime_error"`), `ArtifactPath`, `Result` (the capped, pre-serialized value),
`Meta`, plus `Diagnostics` on a compile failure or `RuntimeError` on a runtime
throw. Posture matches [`script.execute`](script.md): the snippet runs on the
Editor main thread with the full reference set, the result is capped at `maxRows`
(default 200) — unlike [`recorder.query`](recorder.md)'s 2000-row hard cap,
`profiler.query` has no hard row cap, so a larger `maxRows` is honored verbatim;
`Meta.Truncated` / `Meta.TotalCount` report truncation. A compile error returns file-relative `Diagnostics`,
a runtime throw returns structured in `RuntimeError` (it does not throw), and an
unbounded loop **freezes the Editor** — keep it bounded.
