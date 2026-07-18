<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/gameobject.set-parent.md. -->

---
name: gameobject-set-parent
description: Set parent GameObject to list of GameObjects in opened Prefab or in a Scene. Use 'gameobject-find' tool to find the target GameObjects first.
---

# GameObject / Set Parent

Set parent GameObject to list of GameObjects in opened Prefab or in a Scene. Use 'gameobject-find' tool to find the target GameObjects first.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "gameobject.set-parent", "args": {
  "gameObjectRefs": "string_value",
  "parentGameObjectRef": "string_value",
  "worldPositionStays": false
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "gameobject.set-parent" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `gameObjectRefs` | `any` | Yes | List of references to the GameObjects to set new parent. |
| `parentGameObjectRef` | `any` | Yes | Reference to the parent GameObject. |
| `worldPositionStays` | `boolean` | No | A boolean flag indicating whether the GameObject's world position should remain unchanged when setting its parent. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "gameObjectRefs": {
      "$ref": "#/$defs/EA.GameObjectRefList"
    },
    "parentGameObjectRef": {
      "$ref": "#/$defs/EA.GameObjectRef"
    },
    "worldPositionStays": {
      "type": "boolean"
    }
  },
  "$defs": {
    "EA.GameObjectRef": {
      "type": "object",
      "properties": {
        "instanceID": {
          "$ref": "#/$defs/UnityEngine.EntityId",
          "description": "instanceID of the UnityEngine.Object. If it is \u00270\u0027 and \u0027path\u0027, \u0027name\u0027, \u0027assetPath\u0027 and \u0027assetGuid\u0027 is not provided, empty or null, then it will be used as \u0027null\u0027. Priority: 1 (Recommended)"
        },
        "path": {
          "type": "string",
          "description": "Path of a GameObject in the hierarchy Sample \u0027character/hand/finger/particle\u0027. Priority: 2."
        },
        "name": {
          "type": "string",
          "description": "Name of a GameObject in hierarchy. Priority: 3."
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
      "description": "Find GameObject in opened Prefab or in the active Scene."
    },
    "UnityEngine.EntityId": {
      "type": "integer"
    },
    "System.Type": {
      "type": "string"
    },
    "EA.GameObjectRefList": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/EA.GameObjectRef",
        "description": "Find GameObject in opened Prefab or in the active Scene."
      },
      "description": "Array of GameObjects in opened Prefab or in the active Scene."
    }
  },
  "required": [
    "gameObjectRefs",
    "parentGameObjectRef"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

