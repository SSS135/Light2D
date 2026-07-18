<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/recorder.md. -->

# `recorder`

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

To read a method's wiki, call `tools/call` with `arguments = { method: "<dotted>" }` (omit `args`). Include `args` (even `{}`) to execute.

## Methods

- `recorder.describe-session` — MANDATORY FIRST CALL for any recorder query. Returns the session manifest: the object catalog (key / label / path / type / frame range), the per-tag variable manifest (tag / kind / unit / epsilon), th…
- `recorder.find-events` — Find recorded events matching a filter — the "locate the bad moment" tool. Filter by event name, minimum severity, object key, and a frame window; results are ordered by frame and carry the anchor fra…
- `recorder.get-series` — Get the change-point series of one tag of one object over a frame window, reduced to a bounded set of points. Reductions: "summary"/"downsample" return a peak-preserving budget of points (the first, l…
- `recorder.get-state` — Get the as-of value of one recorded object's tags at a specific frame. For each requested tag the last value set at or before 'frame' is returned (carry-forward); a tag with no value at or before the…
- `recorder.list-sessions` — List discoverable gameplay-recorder sessions: every recording file on disk plus the live in-memory session when one is recording. Each entry gives the id, path (or "live"), label, and frame range — us…
- `recorder.query` — Run a small C# LINQ snippet over a recorded or live session and get back a compact result — the flexible escape hatch for ad-hoc questions the summary tools (get-state, summarize-change, get-series, f…
- `recorder.summarize-change` — Summarize how one tag of one object changed over a frame window: start/end value, net delta, min/max/mean, the argmax/argmin frames (drill-down handles), and the number of change points. The compact w…

