<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/editor.selection-get.md. -->

---
name: editor-selection-get
description: Get information about the current Selection in the Unity Editor. Use 'editor-selection-set' tool to set the selection.
---

# Editor / Selection / Get

Get information about the current Selection in the Unity Editor. Use 'editor-selection-set' tool to set the selection.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "editor.selection-get", "args": {
  "includeGameObjects": false,
  "includeTransforms": false,
  "includeInstanceIDs": false,
  "includeAssetGUIDs": false,
  "includeActiveObject": false,
  "includeActiveTransform": false
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "editor.selection-get" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `includeGameObjects` | `boolean` | No |  |
| `includeTransforms` | `boolean` | No |  |
| `includeInstanceIDs` | `boolean` | No |  |
| `includeAssetGUIDs` | `boolean` | No |  |
| `includeActiveObject` | `boolean` | No |  |
| `includeActiveTransform` | `boolean` | No |  |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "includeGameObjects": {
      "type": "boolean"
    },
    "includeTransforms": {
      "type": "boolean"
    },
    "includeInstanceIDs": {
      "type": "boolean"
    },
    "includeAssetGUIDs": {
      "type": "boolean"
    },
    "includeActiveObject": {
      "type": "boolean"
    },
    "includeActiveTransform": {
      "type": "boolean"
    }
  }
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

