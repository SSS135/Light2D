<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/scene.unload.md. -->

---
name: scene-unload
description: Unload scene from the Opened scenes in Unity Editor. Use 'scene-list-opened' tool to get the list of all opened scenes.
---

# Scene / Unload

Unload scene from the Opened scenes in Unity Editor. Use 'scene-list-opened' tool to get the list of all opened scenes.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "scene.unload", "args": {
  "name": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "scene.unload" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `name` | `string` | Yes | Name of the loaded scene. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "name": {
      "type": "string"
    }
  },
  "required": [
    "name"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

