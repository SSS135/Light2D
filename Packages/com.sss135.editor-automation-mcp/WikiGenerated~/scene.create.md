<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/scene.create.md. -->

---
name: scene-create
description: Create new scene in the project assets. Use 'scene-list-opened' tool to list all opened scenes after creation.
---

# Scene / Create

Create new scene in the project assets. Use 'scene-list-opened' tool to list all opened scenes after creation.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "scene.create", "args": {
  "path": "string_value",
  "newSceneSetup": "string_value",
  "newSceneMode": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "scene.create" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `path` | `string` | Yes | Path to the scene file. Should end with ".unity" extension. |
| `newSceneSetup` | `any` | No |  |
| `newSceneMode` | `any` | No |  |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "path": {
      "type": "string"
    },
    "newSceneSetup": {
      "$ref": "#/$defs/UnityEditor.SceneManagement.NewSceneSetup"
    },
    "newSceneMode": {
      "$ref": "#/$defs/UnityEditor.SceneManagement.NewSceneMode"
    }
  },
  "$defs": {
    "UnityEditor.SceneManagement.NewSceneSetup": {
      "type": "string",
      "enum": [
        "EmptyScene",
        "DefaultGameObjects"
      ]
    },
    "UnityEditor.SceneManagement.NewSceneMode": {
      "type": "string",
      "enum": [
        "Single",
        "Additive"
      ]
    }
  },
  "required": [
    "path"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

