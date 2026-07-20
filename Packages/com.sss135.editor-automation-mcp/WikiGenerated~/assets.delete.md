<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/assets.delete.md. -->

---
name: assets-delete
description: Delete the assets at paths from the project. Does AssetDatabase.Refresh() at the end. Use 'assets-find' tool to find assets before deleting.
---

# Assets / Delete

Delete the assets at paths from the project. Does AssetDatabase.Refresh() at the end. Use 'assets-find' tool to find assets before deleting.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "assets.delete", "args": {
  "paths": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "assets.delete" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `paths` | `any` | Yes | The paths of the assets |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "paths": {
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
    "paths"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

