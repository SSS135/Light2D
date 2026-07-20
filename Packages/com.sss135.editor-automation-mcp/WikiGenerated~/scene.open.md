<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/scene.open.md. -->

---
name: scene-open
description: Open scene from the project asset file. Use 'assets-find' tool to find the scene asset first.
---

# Scene / Open

Open scene from the project asset file. Use 'assets-find' tool to find the scene asset first.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "scene.open", "args": {
  "sceneRef": "string_value",
  "loadSceneMode": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "scene.open" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `sceneRef` | `any` | Yes | Reference to UnityEngine.Object asset instance. It could be Material, ScriptableObject, Prefab, and any other Asset. Anything located in the Assets and Packages folders. |
| `loadSceneMode` | `string` | No | Open scene mode. Single: closes the current scenes and opens a new one. Additive: keeps the current scene and opens additional one. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "sceneRef": {
      "$ref": "#/$defs/EA.AssetObjectRef"
    },
    "loadSceneMode": {
      "type": "string",
      "enum": [
        "Single",
        "Additive",
        "AdditiveWithoutLoading"
      ]
    }
  },
  "$defs": {
    "UnityEngine.EntityId": {
      "type": "integer"
    },
    "System.Type": {
      "type": "string"
    },
    "EA.AssetObjectRef": {
      "type": "object",
      "properties": {
        "instanceID": {
          "$ref": "#/$defs/UnityEngine.EntityId",
          "description": "instanceID of the UnityEngine.Object. If this is \u00270\u0027 and \u0027assetPath\u0027 and \u0027assetGuid\u0027 is not provided, empty or null, then it will be used as \u0027null\u0027."
        },
        "assetType": {
          "$ref": "#/$defs/System.Type",
          "description": "Type of the asset."
        },
        "assetPath": {
          "type": "string",
          "description": "Path to the asset within the project. Starts with \u0027Assets/\u0027"
        },
        "assetGuid": {
          "type": "string",
          "description": "Unique identifier for the asset."
        }
      },
      "required": [
        "instanceID"
      ],
      "description": "Reference to UnityEngine.Object asset instance. It could be Material, ScriptableObject, Prefab, and any other Asset. Anything located in the Assets and Packages folders."
    }
  },
  "required": [
    "sceneRef"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

## Notes

`loadSceneMode` selects between three load shapes: `Single` closes every
currently open scene before loading the target (unsaved edits in those
scenes are lost — save first); `Additive` keeps existing scenes open and
loads the target alongside them; `AdditiveWithoutLoading` registers the
scene in the Editor's scene list without actually loading its
GameObjects, which is the right choice when you only need the entry to
exist for build settings or later activation. The response is the full
post-call scene list, not just the newly opened scene.

