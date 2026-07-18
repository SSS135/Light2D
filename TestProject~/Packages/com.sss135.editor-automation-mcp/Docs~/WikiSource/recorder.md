# recorder

Temporal debugging surface for a running game. Devs instrument code with
`Recorder.LogVariable(obj, "tag", value)` / `Recorder.LogEvent(...)`; during
play the plugin records *significant changes* — keyed by object and frame —
into a per-session NDJSON file and an in-memory buffer. The `recorder.*` tools
then answer "what was the state at frame N", "how did `hp` change A→B", and
"what happened around this death" against a recorded file **or** the **live**
running session.

This namespace is read-only. It never enters play mode, mutates the scene, or
emits data — it only queries recordings produced by the runtime instrumentation
(see below). The recorder is engine-agnostic and editor-only: a pure-managed
runtime assembly produces the data, the editor assembly answers the questions.

Every tool takes a `session` argument — `"live"` for the running in-memory
session, or a recording file path / id from `recorder.list-sessions`. Every
result carries a self-describing `Meta` envelope (`Units`, `TimeDomain`,
`FrameRangeCovered`, `Reduction`, `ThresholdApplied`, `Elided`, `Truncated`,
`TotalCount`, `NextCursor`, `SchemaVersion`) so a token-bounded consumer can
tell what it got — and what was clipped — without dumping raw frames.

## Producing data — runtime instrumentation

The query tools are useless without data; data comes from the runtime
`Recorder` API in the `com.SSS135.EditorAutomation.Recorder` assembly
(`Runtime/Recorder/`), which auto-references into any project. Three ways to
emit it, cheapest first:

- **Drag-on a `RecordedTransform` component** (Add Component → Editor Automation →
  Recorded Transform). Logs `pos` / `rot` / optional `scale` every
  `LateUpdate` with no code. An optional tag prefix namespaces multiple
  recorders on one object (`"body." → "body.pos"`).
- **Annotate fields with `[Record]` and add a `RecordedFields` component**
  (Editor Automation → Recorded Fields). It reflects each target's `[Record]`-marked
  fields/properties once in `Awake` and logs them every frame. `[Record("hp")]`
  overrides the tag; `Unit` annotates the manifest. Editor-Mono only — IL2CPP
  needs `[Preserve]`/link.xml, which is out of scope.
- **Call the API directly** for full control:
  - `Recorder.LogVariable(obj, "tag", value)` — typed overloads cover
    `float`/`double`/`int`/`long`/`bool`, `Vector2/3/4`, `Quaternion`,
    `Color`, `Enum`, `string`, plus a boxed `object` fallback. `obj` can be a
    `UnityEngine.Object` (identity = its `GetInstanceID()`, label/path/parent
    captured on first sight) or a plain `string` key.
  - `Recorder.LogEvent(obj|name, props, severity)` — instantaneous, free-form
    events with a `Dictionary<string,object>` of props and a `Severity`
    (`Trace`…`Fatal`, default `Info`). Object-less global events are allowed.
  - `Recorder.DeclareVariable(tag, unit, epsilon)` — optional sugar to pre-seed
    a tag's unit / change-threshold before its first value.
  - `Recorder.SetFrame(int)` — override the frame stamp (default
    `Time.frameCount`); sticky for the session, so feed it each tick to drive a
    custom simulation clock.

Every call is a cheap no-op while no session is active (recording disabled or
outside play mode), so instrumentation can be left in shipped debug builds.

## Querying — describe, then narrow, then drill

The intended flow never dumps raw frames. It funnels from a manifest to a
summary to a single point:

1. **`recorder.describe-session`** — the **mandatory first call**. Returns the
   object catalog (key / label / path / type / frame range) and the variable
   manifest (tag / kind / unit / epsilon). Every later call needs a `key` and
   `tag` from here.
2. **Locate the moment.** `recorder.summarize-change` (net delta, min/max/mean,
   and the **argmax/argmin frames** as drill-down handles) for one tag, or
   `recorder.find-events` (anchor frames for "the bad moment") for events.
3. **Drill into a frame.** Feed an anchor / argmax frame into
   `recorder.get-state` (as-of values of one object's tags at that frame) or
   `recorder.get-series` (a bounded, peak-preserving slice of one tag).

`recorder.list-sessions` discovers what is queryable (files on disk plus the
live session); `recorder.query` is the C# escape hatch for ad-hoc questions the
summary tools can't express.

## Change-only encoding and as-of reads

The recorder stores a value only when it **significantly changes** — `|Δ|` past
the tag's epsilon (Euclidean magnitude for vector/quat kinds). Per-kind default
epsilon is `0.01` for float/vector/quat/color; int/bool/enum/string compare
exactly. The **first value of each (key, tag) is always stored** so reads have a
baseline. This kills float noise and keeps a session small.

Reads are therefore **as-of**: `recorder.get-state` and the summary endpoints
return the last value set *at or before* the requested frame (carry-forward).
A value logged once at frame 0 reconstructs at any later frame; a tag with no
value at or before the frame comes back marked `NoData`. Identity uses
`GetInstanceID()`, so two same-named objects stay distinct `key`s; a tag whose
kind drifts mid-session is tolerated per-row and flagged `Mixed` in the
manifest.

## Sessions and storage

A session is one play-mode enter→exit. On play it auto-begins via
`RuntimeInitializeOnLoadMethod`; on exit (`Application.quitting`) it flushes and
closes. Files land in `Temp/EditorAutomationRecordings/session-<utc>-<guid>.ndjson`
(NDJSON: one `header` / `obj` / `var` / `val` / `evt` line each), survive
play-mode cycles within an Editor run, and are pruned to the newest ~20. The
live session is queryable with `session: "live"` while play is running; closed
sessions are queried by file path or id.

## Enabling / disabling

Recording is gated by **Project Settings → Project / Editor Automation → "Enable
Gameplay Recorder"**, backed by the `EditorAutomation.Recorder.Enabled` PlayerPrefs key
(default ON) and mirrored by the runtime `Recorder.Enabled` property. When off,
`Recorder.BeginSession` no-ops, the active session stays null, and every
instrumentation call costs nothing. The `recorder.*` query tools stay
registered regardless of the toggle — disabling only stops *new* data; existing
session files remain queryable.

## See also

- [`workflows`](workflows.md) — "Debug a running game's state over time".
- [`console`](console.md) — point-in-time `Debug.Log*` triage; the recorder is
  the temporal counterpart for tracking how a value evolved across frames.
- [`editor`](editor.md) — confirm play mode and a settled domain before
  querying `"live"`; live tools take a consistent main-thread snapshot.
- [`screenshot.game-view`](screenshot.md) — visual evidence at a moment, paired
  with the recorder's numeric state at that frame.

### recorder.describe-session

Call this first for every session you have not described this turn — the other
recorder tools need a `key` and `tag`, and those come only from this manifest.
Pass `"live"` to describe the running session or a file path / id for a closed
one. The result is bounded (catalog + manifest + ranges + event count), never
raw rows, so it is safe to call even on a large session.

### recorder.summarize-change

The compact workhorse for "how did X change from A to B". Keep it as the first
drill after `describe` — its `argMaxFrame` / `argMinFrame` are the handles you
feed into `recorder.get-state` / `recorder.get-series` to inspect the peak,
rather than paging the whole window. Vector tags summarize by magnitude.

### recorder.get-series

Hard-capped by construction — there is **no raw dump over a wide range**. The
`summary` / `downsample` reductions return a peak-preserving point budget
(first, last, and per-bucket extrema survive); `diff` returns the raw change
list paginated by `cursor`. An over-broad window comes back truncated with the
elided count in the envelope, not a wall of points. Find the interesting window
with `recorder.summarize-change` first, then page here.

### recorder.query

The flexible escape hatch — reach for it when the summary tools can't express
the question (e.g. event-anchored windows that join the variable and event
streams). You write a small **C# LINQ snippet** (a method body that `return`s a
value) against `q`, a **read-only** view of the session:

- `q.Objects` / `q.Variables` / `q.Events` / `q.SeriesKeys`
- `q.Series(key, tag)` → the change-point list; `q.ValueAt(key, tag, frame)` →
  the as-of value (carry-forward); `q.Magnitude(in value)` → scalar magnitude
- `q.MinFrame` / `q.MaxFrame` / `q.SessionId` / `q.FrameBase`

Call `describe-session` first for the keys/tags. **Return a primitive or a
`List<Dictionary<string,object>>`** (one dict per row) — not an anonymous type.
The result is capped at `maxRows` and additionally clamped to a hard cap of 2000
rows (`RecorderQueryEngine.HardRowCap`), so a large `maxRows` never returns more
than 2000 rows; `Meta.Truncated` / `Meta.TotalCount` report whichever limit
truncated. A compile error returns the file-relative `Diagnostics`.

Inline `query` calls require a `fileName` and a `description`. The wrapped source
is persisted to `{projectRoot}/Library/McpQueries/Recorder/<fileName>.cs` (a `.cs`
extension is appended if omitted, and a same-named file is overwritten), with the
`description` written as a `//` comment header at the top. `Library/` survives
Editor restarts, so named queries accumulate as a reusable, self-documenting
library. The response always returns `ArtifactPath` pointing at that file, so a
compile-failed or to-be-tweaked query can be edited in place and re-run via
`filePath` — which is mutually exclusive with `query`, `fileName`, and
`description` (a saved file already carries its own header and is compiled
verbatim). `session` is still required in `filePath` mode: the query context is
built at invoke time, so the saved file itself is session-agnostic.

The response is a `ToolRunEnvelope`: `Status` (`"executed"` / `"compile_failed"` /
`"runtime_error"`), `ArtifactPath`, `Result` (the capped, pre-serialized value),
`Meta`, plus `Diagnostics` on a compile failure or `RuntimeError` on a runtime
throw. A runtime throw is reported in `RuntimeError`, not surfaced as a tool error.

Posture is lightweight, on par with [`script.execute`](script.md): the snippet
runs on the Editor **main thread** with the full reference set. `q` hides the
session's mutators, but `System.IO` / reflection stay reachable and an unbounded
loop will **freeze the Editor** — keep queries bounded.

Example: `return q.Series("iid:1234","health_fraction").Where(p => p.Frame >= 100
&& p.Frame <= 200).Select(p => new Dictionary<string,object>{{"frame",p.Frame},
{"h",p.V.F0}}).ToList();`
