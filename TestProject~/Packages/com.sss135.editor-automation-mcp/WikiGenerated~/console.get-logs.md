<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/console.get-logs.md. -->

---
name: console-get-logs
description: Retrieves Unity Editor logs. Useful for debugging and monitoring Unity Editor activity.
---

# Console / Get Logs

Retrieves Unity Editor logs. Useful for debugging and monitoring Unity Editor activity.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "console.get-logs", "args": {
  "maxEntries": 0,
  "logTypeFilter": "string_value",
  "includeStackTrace": false,
  "lastMinutes": 0
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "console.get-logs" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `maxEntries` | `integer` | No | Maximum number of log entries to return. Minimum: 1. Default: 100 |
| `logTypeFilter` | `any` | No | Filter by log type. 'null' means All. |
| `includeStackTrace` | `boolean` | No | Include stack traces in the output. Default: false |
| `lastMinutes` | `integer` | No | Return logs from the last N minutes. If 0, returns all available logs. Default: 0 |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "maxEntries": {
      "type": "integer"
    },
    "logTypeFilter": {
      "$ref": "#/$defs/UnityEngine.LogType"
    },
    "includeStackTrace": {
      "type": "boolean"
    },
    "lastMinutes": {
      "type": "integer"
    }
  },
  "$defs": {
    "UnityEngine.LogType": {
      "type": "string",
      "enum": [
        "Error",
        "Assert",
        "Warning",
        "Log",
        "Exception"
      ]
    }
  }
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

