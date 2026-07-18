<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/system.job-wait.md. -->

---
name: system-job-wait
description: Block server-side until a job ticket reaches a terminal state or the timeout expires. Returns the same payload shape as system-job-status, plus a 'timed_out' flag and an optional 'status_filter_unmatched' flag when terminalStates excludes the actual outcome.
---

# System / Job / Wait

Block server-side until a job ticket reaches a terminal state or the timeout expires. Returns the same payload shape as system-job-status, plus a 'timed_out' flag and an optional 'status_filter_unmatched' flag when terminalStates excludes the actual outcome.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "system.job-wait", "args": {
  "ticketId": "string_value",
  "timeoutSeconds": 0,
  "terminalStates": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "system.job-wait" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `ticketId` | `string` | Yes | Ticket id returned by the originating tool (format: 'j_<utc>_<8hex>'). |
| `timeoutSeconds` | `integer` | No | Maximum seconds to wait. Defaults to 30. Must be > 0. |
| `terminalStates` | `any` | No | Optional list of terminal statuses to wait for, from {completed, cancelled, errored}. Default: any non-running status ends the wait. When supplied, a terminal outcome not in the list still ends the wait but sets status_filter_unmatched: true on the body. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "ticketId": {
      "type": "string"
    },
    "timeoutSeconds": {
      "type": "integer"
    },
    "terminalStates": {
      "$ref": "#/$defs/System.String[]"
    }
  },
  "$defs": {
    "System.String[]": {
      "type": "array",
      "items": {
        "type": "string"
      }
    }
  },
  "required": [
    "ticketId"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

