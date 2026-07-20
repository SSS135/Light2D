<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/profiler.md. -->

# `profiler`

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

To read a method's wiki, call `tools/call` with `arguments = { method: "<dotted>" }` (omit `args`). Include `args` (even `{}`) to execute.

## Methods

- `profiler.describe` — Orientation call before any other profiler.* tool. Reports whether the buffer is from a live recording or a loaded capture, the available frame range, the threads present, the counters you can summari…
- `profiler.find-spikes` — Find the frames where one profiler counter spikes. Pass an absolute 'threshold' (a frame counts as a spike when its value exceeds it) OR an n-'sigma' multiplier (spike when value > mean + sigma*stddev…
- `profiler.frame-breakdown` — Top markers for one frame, the inverted+merged hierarchy (leaf functions aggregated by name), sorted by self time / total time / GC bytes. This is the drill-down step: get the worst / spike frame from…
- `profiler.get-series` — Chart one counter's per-frame values over a window, peak-preserving downsampled to a bounded point budget (the first, last, and per-bucket extrema survive, so spikes are never smoothed away). Run 'pro…
- `profiler.load-capture` — Load a saved .raw/.data Profiler capture into the global Profiler buffer. WARNING: this REPLACES the current in-memory data — the Profiler window and every other profiler.* read now reflect the loaded…
- `profiler.query` — Run a small C# snippet over the current Profiler buffer and get back a compact result — the flexible escape hatch for ad-hoc questions the structured tools (describe, summarize-counter, get-series, fr…
- `profiler.summarize-counter` — Summary stats (min/max/mean/p95/p99 + argmax/argmin frames) for one profiler counter over a frame window. Report p95/p99, not just the mean, so a few spike frames don't mask the steady state. The retu…

