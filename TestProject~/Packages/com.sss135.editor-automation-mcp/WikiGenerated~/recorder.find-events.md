<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/recorder.find-events.md. -->

---
name: recorder-find-events
description: "Find recorded events matching a filter — the \"locate the bad moment\" tool. Filter by event name, minimum severity, object key, and a frame window; results are ordered by frame and carry the anchor frame plus the event's props. Feed an anchor frame back into 'recorder-get-state' or 'recorder-summarize-change' to inspect what the state was around it."
---

# Recorder / Find Events

Find recorded events matching a filter — the "locate the bad moment" tool. Filter by event name, minimum severity, object key, and a frame window; results are ordered by frame and carry the anchor frame plus the event's props. Feed an anchor frame back into 'recorder-get-state' or 'recorder-summarize-change' to inspect what the state was around it.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "recorder.find-events", "args": {
  "session": "string_value",
  "name": "string_value",
  "severityMin": "string_value",
  "key": "string_value",
  "from": 0,
  "to": 0,
  "limit": 0
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "recorder.find-events" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `session` | `string` | Yes | "live" for the running session, or a recording file path / id. |
| `name` | `string` | No | Exact event name to match (e.g. "hit", "death"). Omit (null) to match any name. |
| `severityMin` | `string` | No | Minimum severity to include: Trace | Debug | Info | Warning | Error | Fatal. Omit (null) for any severity. |
| `key` | `string` | No | Restrict to events on one object key. Omit (null) to match every object plus global events. |
| `from` | `integer` | No | Window start frame (inclusive). Pass -1 (default) for no lower bound. |
| `to` | `integer` | No | Window end frame (inclusive). Pass -1 (default) for no upper bound. |
| `limit` | `integer` | No | Maximum number of events to return. Capped at the engine's hard row limit regardless. 0 uses the default limit. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "session": {
      "type": "string"
    },
    "name": {
      "type": "string"
    },
    "severityMin": {
      "type": "string"
    },
    "key": {
      "type": "string"
    },
    "from": {
      "type": "integer"
    },
    "to": {
      "type": "integer"
    },
    "limit": {
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

