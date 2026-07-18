<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/recorder.describe-session.md. -->

---
name: recorder-describe-session
description: "MANDATORY FIRST CALL for any recorder query. Returns the session manifest: the object catalog (key / label / path / type / frame range), the per-tag variable manifest (tag / kind / unit / epsilon), the session's frame and time domain, and the total event count. Use the returned keys and tags to drive 'recorder-get-state', 'recorder-summarize-change', 'recorder-get-series', and 'recorder-find-events'. Pass \"live\" to describe the running session or a recording file path / id."
---

# Recorder / Describe Session

MANDATORY FIRST CALL for any recorder query. Returns the session manifest: the object catalog (key / label / path / type / frame range), the per-tag variable manifest (tag / kind / unit / epsilon), the session's frame and time domain, and the total event count. Use the returned keys and tags to drive 'recorder-get-state', 'recorder-summarize-change', 'recorder-get-series', and 'recorder-find-events'. Pass "live" to describe the running session or a recording file path / id.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "recorder.describe-session", "args": {
  "session": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "recorder.describe-session" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `session` | `string` | Yes | "live" for the running session, or a recording file path / id. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "session": {
      "type": "string"
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

Call this first for every session you have not described this turn — the other
recorder tools need a `key` and `tag`, and those come only from this manifest.
Pass `"live"` to describe the running session or a file path / id for a closed
one. The result is bounded (catalog + manifest + ranges + event count), never
raw rows, so it is safe to call even on a large session.

