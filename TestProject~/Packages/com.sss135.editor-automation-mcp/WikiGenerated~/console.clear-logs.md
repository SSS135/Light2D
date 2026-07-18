<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/console.clear-logs.md. -->

---
name: console-clear-logs
description: Clears the MCP log cache (used by console-get-logs) and the Unity Editor Console window. Useful for isolating errors related to a specific action by clearing logs before performing the action.
---

# Console / Clear Logs

Clears the MCP log cache (used by console-get-logs) and the Unity Editor Console window. Useful for isolating errors related to a specific action by clearing logs before performing the action.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "console.clear-logs", "args": {
  "nothing": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "console.clear-logs" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `nothing` | `string` | No |  |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "nothing": {
      "type": "string"
    }
  }
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

