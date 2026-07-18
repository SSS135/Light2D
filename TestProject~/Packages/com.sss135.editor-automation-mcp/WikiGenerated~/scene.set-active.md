<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/scene.set-active.md. -->

---
name: scene-set-active
description: Set the specified opened scene as the active scene. Use 'scene-list-opened' tool to get the list of all opened scenes.
---

# Scene / Set Active

Set the specified opened scene as the active scene. Use 'scene-list-opened' tool to get the list of all opened scenes.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "scene.set-active", "args": {
  "sceneRef": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "scene.set-active" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `sceneRef` | `any` | Yes | Reference to UnityEngine.Object asset instance. It could be Material, ScriptableObject, Prefab, and any other Asset. Anything located in the Assets and Packages folders. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "sceneRef": {
      "$ref": "#/$defs/EA.AssetObjectRef"
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

