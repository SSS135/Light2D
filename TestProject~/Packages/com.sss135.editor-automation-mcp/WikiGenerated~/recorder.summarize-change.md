<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/recorder.summarize-change.md. -->

---
name: recorder-summarize-change
description: "Summarize how one tag of one object changed over a frame window: start/end value, net delta, min/max/mean, the argmax/argmin frames (drill-down handles), and the number of change points. The compact workhorse for \"how did X change from A to B\" — feed the argmax frame back into 'recorder-get-state' or 'recorder-get-series' to inspect the peak."
---

# Recorder / Summarize Change

Summarize how one tag of one object changed over a frame window: start/end value, net delta, min/max/mean, the argmax/argmin frames (drill-down handles), and the number of change points. The compact workhorse for "how did X change from A to B" — feed the argmax frame back into 'recorder-get-state' or 'recorder-get-series' to inspect the peak.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "recorder.summarize-change", "args": {
  "session": "string_value",
  "key": "string_value",
  "tag": "string_value",
  "from": 0,
  "to": 0
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "recorder.summarize-change" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `session` | `string` | Yes | "live" for the running session, or a recording file path / id. |
| `key` | `string` | Yes | Object key, e.g. "iid:123". From the describe-session object catalog. |
| `tag` | `string` | Yes | Tag to summarize, e.g. "hp". From the describe-session variable manifest. |
| `from` | `integer` | No | Window start frame (inclusive). |
| `to` | `integer` | No | Window end frame (inclusive). |

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

The compact workhorse for "how did X change from A to B". Keep it as the first
drill after `describe` — its `argMaxFrame` / `argMinFrame` are the handles you
feed into `recorder.get-state` / `recorder.get-series` to inspect the peak,
rather than paging the whole window. Vector tags summarize by magnitude.

