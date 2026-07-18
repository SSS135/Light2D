<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/profiler.query.md. -->

---
name: profiler-query
description: |-
  Run a small C# snippet over the current Profiler buffer and get back a compact result — the flexible escape hatch for ad-hoc questions the structured tools (describe, summarize-counter, get-series, frame-breakdown, find-spikes) can't express, e.g. joining a counter series to per-frame marker breakdowns or custom percentiles. CALL 'profiler-describe' FIRST to learn the frame range and counter names.
  Your 'body' is a C# method BODY that must `return` a value. In scope: `q` (the read-only ProfilerQueryContext), plus System.Linq and the UnityEditor.Profiling / UnityEditorInternal namespaces. The `q` surface:
    q.FirstFrame, q.LastFrame, q.FrameCount, q.IsRecording,
    q.AvailableCounters() -> IReadOnlyList<CounterInfo>,
    q.CounterValue(frame, counter) -> double? (null when absent),
    q.CounterSeries(counter, from, to) -> IReadOnlyList<ProfilerSample> { int Frame; double Value },
    q.Breakdown(frame, topN, threadIndex, sortBy) -> MarkerRow[],
    q.WorstFrame() -> worst frame by CPU time, q.IsLoadFrame(frame) ->…
---

# Profiler / Query (C#)

Run a small C# snippet over the current Profiler buffer and get back a compact result — the flexible escape hatch for ad-hoc questions the structured tools (describe, summarize-counter, get-series, frame-breakdown, find-spikes) can't express, e.g. joining a counter series to per-frame marker breakdowns or custom percentiles. CALL 'profiler-describe' FIRST to learn the frame range and counter names.
Your 'body' is a C# method BODY that must `return` a value. In scope: `q` (the read-only ProfilerQueryContext), plus System.Linq and the UnityEditor.Profiling / UnityEditorInternal namespaces. The `q` surface:
  q.FirstFrame, q.LastFrame, q.FrameCount, q.IsRecording,
  q.AvailableCounters() -> IReadOnlyList<CounterInfo>,
  q.CounterValue(frame, counter) -> double? (null when absent),
  q.CounterSeries(counter, from, to) -> IReadOnlyList<ProfilerSample> { int Frame; double Value },
  q.Breakdown(frame, topN, threadIndex, sortBy) -> MarkerRow[],
  q.WorstFrame() -> worst frame by CPU time, q.IsLoadFrame(frame) -> bool.
RETURN a primitive or a List<Dictionary<string,object>> (one dict per row) — NOT an anonymous type. Read-only (no buffer-replacing mutators on `q`); runs on the Editor main thread, so KEEP IT BOUNDED — an unbounded loop freezes the Editor. The returned sequence is capped at 'maxRows' (Meta reports truncation).
Two input modes: inline 'body' OR a previously saved 'filePath' — supply exactly one. Inline calls are persisted to '{projectRoot}/Library/McpQueries/Profiler/<fileName>.cs' (outside Unity's compiled paths; survives Editor restart) under 'fileName', overwriting any existing file. The response 'ArtifactPath' points at that file, so you can edit-in-place and re-invoke via 'filePath' instead of resubmitting the snippet on each iteration. 'fileName' and 'description' are REQUIRED with 'body' and prohibited with 'filePath'. The response is a 'ToolRunEnvelope': 'Status' ("executed" | "compile_failed" | "runtime_error"), 'ArtifactPath', 'Result' (the capped value), 'Meta', plus 'Diagnostics' (file-relative, on compile failure) or 'RuntimeError' (on a runtime throw). Runtime errors return structured — they do not throw.
Example body: return Enumerable.Range(q.FirstFrame, q.FrameCount).Select(f => q.CounterValue(f, "Main Thread")).Where(v => v.HasValue).Max();

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "profiler.query", "args": {
  "body": "string_value",
  "fileName": "string_value",
  "description": "string_value",
  "filePath": "string_value",
  "maxRows": 0
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "profiler.query" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `body` | `string` | No | C# method body that returns a value, with `q` (the read-only ProfilerQueryContext) and System.Linq in scope. Mutually exclusive with `filePath` — supply exactly one. |
| `fileName` | `string` | No | File name for the persisted query under 'Library/McpQueries/Profiler/'. A '.cs' extension is appended if omitted ('Foo', 'Foo.cs' both save as 'Foo.cs'). Must be a bare file name — directory separators and other invalid characters are rejected. REQUIRED when 'body' is provided; must be omitted when 'filePath' is provided. |
| `description` | `string` | No | Short summary of what the query does, written as a '//' comment header at the top of the saved file. REQUIRED when 'body' is provided; must be omitted when 'filePath' is provided. |
| `filePath` | `string` | No | Absolute path to a previously persisted query file (typically the 'ArtifactPath' returned by an earlier call). Mutually exclusive with 'body', 'fileName', and 'description'. Compiled verbatim — edit it in place to fix a diagnostic, then re-invoke with the same path. |
| `maxRows` | `integer` | No | Maximum rows returned for an enumerable result. 0 uses the default budget of 200. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "body": {
      "type": "string"
    },
    "fileName": {
      "type": "string"
    },
    "description": {
      "type": "string"
    },
    "filePath": {
      "type": "string"
    },
    "maxRows": {
      "type": "integer"
    }
  }
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

## Notes

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

