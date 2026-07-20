<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/recorder.get-state.md. -->

---
name: recorder-get-state
description: Get the as-of value of one recorded object's tags at a specific frame. For each requested tag the last value set at or before 'frame' is returned (carry-forward); a tag with no value at or before the frame is marked NoData. Call 'recorder-describe-session' first to discover valid keys and tags.
---

# Recorder / Get State

Get the as-of value of one recorded object's tags at a specific frame. For each requested tag the last value set at or before 'frame' is returned (carry-forward); a tag with no value at or before the frame is marked NoData. Call 'recorder-describe-session' first to discover valid keys and tags.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "recorder.get-state", "args": {
  "session": "string_value",
  "key": "string_value",
  "tags": "string_value",
  "frame": 0
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "recorder.get-state" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `session` | `string` | Yes | "live" for the running session, or a recording file path / id. |
| `key` | `string` | Yes | Object key, e.g. "iid:123". From the describe-session object catalog. |
| `tags` | `any` | No | Tags to read. Omit (null) for every tag recorded on the object. |
| `frame` | `integer` | No | Frame to evaluate the as-of state at. |

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
    "tags": {
      "$ref": "#/$defs/System.Collections.Generic.List\u003CSystem.String\u003E"
    },
    "frame": {
      "type": "integer"
    }
  },
  "$defs": {
    "System.Collections.Generic.List\u003CSystem.String\u003E": {
      "type": "array",
      "items": {
        "type": "string"
      }
    }
  },
  "required": [
    "session",
    "key"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

