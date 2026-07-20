<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/recorder.get-series.md. -->

---
name: recorder-get-series
description: "Get the change-point series of one tag of one object over a frame window, reduced to a bounded set of points. Reductions: \"summary\"/\"downsample\" return a peak-preserving budget of points (the first, last, and per-bucket extrema survive); \"diff\" returns the raw change list paginated by 'cursor'. The result is hard-capped regardless of request — there is no raw dump over a wide range; an over-broad window comes back truncated with the elided count in the envelope. Use 'recorder-summarize-change' first to find the interesting window, then page through it here."
---

# Recorder / Get Series

Get the change-point series of one tag of one object over a frame window, reduced to a bounded set of points. Reductions: "summary"/"downsample" return a peak-preserving budget of points (the first, last, and per-bucket extrema survive); "diff" returns the raw change list paginated by 'cursor'. The result is hard-capped regardless of request — there is no raw dump over a wide range; an over-broad window comes back truncated with the elided count in the envelope. Use 'recorder-summarize-change' first to find the interesting window, then page through it here.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "recorder.get-series", "args": {
  "session": "string_value",
  "key": "string_value",
  "tag": "string_value",
  "from": 0,
  "to": 0,
  "reduction": "string_value",
  "maxPoints": 0,
  "cursor": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "recorder.get-series" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `session` | `string` | Yes | "live" for the running session, or a recording file path / id. |
| `key` | `string` | Yes | Object key, e.g. "iid:123". From the describe-session object catalog. |
| `tag` | `string` | Yes | Tag to fetch, e.g. "pos". From the describe-session variable manifest. |
| `from` | `integer` | No | Window start frame (inclusive). |
| `to` | `integer` | No | Window end frame (inclusive). |
| `reduction` | `string` | No | Reduction: "summary" | "downsample" (peak-preserving point budget) or "diff" (raw change list, paginated). Defaults to "summary". |
| `maxPoints` | `integer` | No | Maximum number of points to return. Capped at the engine's hard row limit regardless. 0 uses the default budget. |
| `cursor` | `string` | No | Opaque pagination cursor for "diff"; pass the 'nextCursor' from a prior page. Null for the first page. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "session": {
      "type": "string"
    },
    "key": {
      "type": "string"
    },
    "tag": {
      "type": "string"
    },
    "from": {
      "type": "integer"
    },
    "to": {
      "type": "integer"
    },
    "reduction": {
      "type": "string"
    },
    "maxPoints": {
      "type": "integer"
    },
    "cursor": {
      "type": "string"
    }
  },
  "required": [
    "session",
    "key",
    "tag"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

## Notes

Hard-capped by construction — there is **no raw dump over a wide range**. The
`summary` / `downsample` reductions return a peak-preserving point budget
(first, last, and per-bucket extrema survive); `diff` returns the raw change
list paginated by `cursor`. An over-broad window comes back truncated with the
elided count in the envelope, not a wall of points. Find the interesting window
with `recorder.summarize-change` first, then page here.

