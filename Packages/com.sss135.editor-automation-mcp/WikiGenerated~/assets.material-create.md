<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/assets.material-create.md. -->

---
name: assets-material-create
description: Create new material asset with default parameters. Creates folders recursively if they do not exist. Provide proper 'shaderName' - use 'assets-shader-list-all' tool to find available shaders.
---

# Assets / Create Material

Create new material asset with default parameters. Creates folders recursively if they do not exist. Provide proper 'shaderName' - use 'assets-shader-list-all' tool to find available shaders.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "assets.material-create", "args": {
  "assetPath": "string_value",
  "shaderName": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "assets.material-create" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `assetPath` | `string` | Yes | Asset path. Starts with 'Assets/'. Ends with '.mat'. |
| `shaderName` | `string` | Yes | Name of the shader that need to be used to create the material. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "assetPath": {
      "type": "string"
    },
    "shaderName": {
      "type": "string"
    }
  },
  "required": [
    "assetPath",
    "shaderName"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

