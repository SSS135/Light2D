<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/assets.move.md. -->

---
name: assets-move
description: Move the assets at paths in the project. Should be used for asset rename. Does AssetDatabase.Refresh() at the end. Use 'assets-find' tool to find assets before moving.
---

# Assets / Move

Move the assets at paths in the project. Should be used for asset rename. Does AssetDatabase.Refresh() at the end. Use 'assets-find' tool to find assets before moving.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "assets.move", "args": {
  "sourcePaths": "string_value",
  "destinationPaths": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "assets.move" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `sourcePaths` | `any` | Yes | The paths of the assets to move. |
| `destinationPaths` | `any` | Yes | The paths of moved assets. |

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

