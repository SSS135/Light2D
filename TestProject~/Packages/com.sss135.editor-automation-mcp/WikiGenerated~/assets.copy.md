<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/assets.copy.md. -->

---
name: assets-copy
description: Copy assets at given paths and store them at new paths. Does AssetDatabase.Refresh() at the end. Use 'assets-find' tool to find assets before copying.
---

# Assets / Copy

Copy assets at given paths and store them at new paths. Does AssetDatabase.Refresh() at the end. Use 'assets-find' tool to find assets before copying.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "assets.copy", "args": {
  "sourcePaths": "string_value",
  "destinationPaths": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "assets.copy" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `sourcePaths` | `any` | Yes | The paths of the assets to copy. |
| `destinationPaths` | `any` | Yes | The paths to store the copied assets. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "sourcePaths": {
      "$ref": "#/$defs/System.String[]"
    },
    "destinationPaths": {
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
    "sourcePaths",
    "destinationPaths"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

