<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/recorder.query.md. -->

---
name: recorder-query
description: |-
  Run a small C# LINQ snippet over a recorded or live session and get back a compact result — the flexible escape hatch for ad-hoc questions the summary tools (get-state, summarize-change, get-series, find-events) can't express, e.g. event-anchored windows that join the variable and event streams. CALL 'recorder-describe-session' FIRST to learn the object keys and tags.
  Your 'query' is a C# method BODY that must `return` a value. In scope: `q` (the read-only session), plus System.Linq. The `q` surface:
    q.Objects (key->ObjectRecord), q.Variables (tag->VariableManifest), q.Events (list of EventRecord),
    q.SeriesKeys ((key,tag) pairs), q.Series(key,tag) -> IReadOnlyList<ChangePoint>,
    q.ValueAt(key,tag,frame) -> ChangePoint? (as-of), q.Magnitude(in RecordedValue) -> double,
    q.MinFrame, q.MaxFrame, q.SessionId, q.FrameBase.
  A ChangePoint has { int Frame; RecordedValue V }; RecordedValue has { ValueKind Kind; double F0,F1,F2,F3; string S }.
  RETURN a primitive or a List<Dictionary<string,object>> (one dict per…
---

# Recorder / Query (C#)

Run a small C# LINQ snippet over a recorded or live session and get back a compact result — the flexible escape hatch for ad-hoc questions the summary tools (get-state, summarize-change, get-series, find-events) can't express, e.g. event-anchored windows that join the variable and event streams. CALL 'recorder-describe-session' FIRST to learn the object keys and tags.
Your 'query' is a C# method BODY that must `return` a value. In scope: `q` (the read-only session), plus System.Linq. The `q` surface:
  q.Objects (key->ObjectRecord), q.Variables (tag->VariableManifest), q.Events (list of EventRecord),
  q.SeriesKeys ((key,tag) pairs), q.Series(key,tag) -> IReadOnlyList<ChangePoint>,
  q.ValueAt(key,tag,frame) -> ChangePoint? (as-of), q.Magnitude(in RecordedValue) -> double,
  q.MinFrame, q.MaxFrame, q.SessionId, q.FrameBase.
A ChangePoint has { int Frame; RecordedValue V }; RecordedValue has { ValueKind Kind; double F0,F1,F2,F3; string S }.
RETURN a primitive or a List<Dictionary<string,object>> (one dict per row) — NOT an anonymous type. Read-only (no mutators on `q`); runs on the Editor main thread, so KEEP IT BOUNDED — an unbounded loop freezes the Editor. The returned sequence is capped at 'maxRows' (Meta.Truncated/Meta.TotalCount report it).
Inline 'query' is persisted to '{projectRoot}/Library/McpQueries/Recorder/<fileName>.cs' under the caller-supplied `fileName` (survives Editor restart, overwriting any existing file). The response's `ArtifactPath` points at that file so you can edit-in-place and re-invoke via `filePath` instead of resubmitting the whole snippet on iteration. On a compile error the response carries `Status: "compile_failed"` + file-relative `Diagnostics`; a runtime throw is reported in `RuntimeError`.
Example query: return q.Series("iid:1234","health_fraction").Where(p => p.Frame>=100 && p.Frame<=200).Select(p => new Dictionary<string,object>{{"frame",p.Frame},{"h",p.V.F0}}).ToList();

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "recorder.query", "args": {
  "session": "string_value",
  "query": "string_value",
  "fileName": "string_value",
  "description": "string_value",
  "filePath": "string_value",
  "maxRows": 0
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "recorder.query" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `session` | `string` | Yes | "live" for the running session, or a recording file path / id. Required in both modes. |
| `query` | `string` | No | A C# method body that returns a value, with `q` (the read-only session) and System.Linq in scope. Mutually exclusive with `filePath` — supply exactly one. |
| `fileName` | `string` | No | File name for the persisted snippet under 'Library/McpQueries/Recorder/'. A '.cs' extension is appended if omitted ('Foo', 'Foo.cs' both save as 'Foo.cs'). If a file with this name already exists it is overwritten. Must be a bare file name — directory separators and other invalid characters are rejected. REQUIRED when 'query' is provided; must be omitted when 'filePath' is provided. |
| `description` | `string` | No | Short summary of what the snippet does, written as a '//' comment header at the top of the saved file. REQUIRED when 'query' is provided; must be omitted when 'filePath' is provided. |
| `filePath` | `string` | No | Absolute path to a previously persisted snippet file (typically the `ArtifactPath` returned by an earlier call). Mutually exclusive with `query`, `fileName`, and `description`. The file is compiled verbatim. Use this to iterate on a query that compile-failed: read `ArtifactPath` from the failure response, edit the file in place, then re-invoke with the same path (and the same `session`). |
| `maxRows` | `integer` | No | Maximum rows returned for an enumerable result (capped at the engine hard limit). Default 200. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "session": {
      "type": "string"
    },
    "query": {
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
  },
  "required": [
    "session"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

## Notes

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

