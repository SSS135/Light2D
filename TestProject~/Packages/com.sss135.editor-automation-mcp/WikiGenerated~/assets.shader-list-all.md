<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/assets.shader-list-all.md. -->

---
name: assets-shader-list-all
description: List all available shaders in the project assets and packages. Returns their names. Use this to find a shader name for 'assets-material-create' tool.
---

# Assets / List Shaders

List all available shaders in the project assets and packages. Returns their names. Use this to find a shader name for 'assets-material-create' tool.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "assets.shader-list-all", "args": {
  "nothing": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "assets.shader-list-all" }
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

