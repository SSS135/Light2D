<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/assets.find-built-in.md. -->

---
name: assets-find-built-in
description: "Search the built-in assets of the Unity Editor located in the built-in resources: Resources/unity_builtin_extra. Doesn't support GUIDs since built-in assets do not have them."
---

# Assets / Find (Built-in)

Search the built-in assets of the Unity Editor located in the built-in resources: Resources/unity_builtin_extra. Doesn't support GUIDs since built-in assets do not have them.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "assets.find-built-in", "args": {
  "name": "string_value",
  "type": "string_value",
  "maxResults": 0
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "assets.find-built-in" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `name` | `string` | No | The name of the asset to filter by. |
| `type` | `any` | No | The type of the asset to filter by. |
| `maxResults` | `integer` | No | Maximum number of assets to return. If the number of found assets exceeds this limit, the result will be truncated. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "name": {
      "type": "string"
    },
    "type": {
      "$ref": "#/$defs/System.Type"
    },
    "maxResults": {
      "type": "integer"
    }
  },
  "$defs": {
    "System.Type": {
      "type": "string"
    }
  }
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

