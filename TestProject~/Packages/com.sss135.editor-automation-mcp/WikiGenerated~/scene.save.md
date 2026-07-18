<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/scene.save.md. -->

---
name: scene-save
description: Save Opened scene to the asset file. Use 'scene-list-opened' tool to get the list of all opened scenes.
---

# Scene / Save

Save Opened scene to the asset file. Use 'scene-list-opened' tool to get the list of all opened scenes.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "scene.save", "args": {
  "openedSceneName": "string_value",
  "path": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "scene.save" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `openedSceneName` | `string` | No | Name of the opened scene that should be saved. Could be empty if need to save the current active scene. |
| `path` | `string` | No | Path to the scene file. Should end with ".unity". If null or empty save to the existed scene asset file. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "openedSceneName": {
      "type": "string"
    },
    "path": {
      "type": "string"
    }
  }
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

