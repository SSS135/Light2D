<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/assets.prefab-create.md. -->

---
name: assets-prefab-create
description: Create a prefab from a GameObject in the current active scene. The prefab will be saved in the project assets at the specified path. Creates folders recursively if they do not exist. If the source GameObject is already a prefab instance and 'connectGameObjectToPrefab' is true, a Prefab Variant is created automatically. To create a Prefab Variant from an existing prefab asset, provide 'sourcePrefabAssetPath' instead of 'gameObjectRef'. Use 'gameobject-find' tool to find the target GameObject first.
---

# Assets / Prefab / Create

Create a prefab from a GameObject in the current active scene. The prefab will be saved in the project assets at the specified path. Creates folders recursively if they do not exist. If the source GameObject is already a prefab instance and 'connectGameObjectToPrefab' is true, a Prefab Variant is created automatically. To create a Prefab Variant from an existing prefab asset, provide 'sourcePrefabAssetPath' instead of 'gameObjectRef'. Use 'gameobject-find' tool to find the target GameObject first.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "assets.prefab-create", "args": {
  "prefabAssetPath": "string_value",
  "gameObjectRef": "string_value",
  "sourcePrefabAssetPath": "string_value",
  "connectGameObjectToPrefab": false
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "assets.prefab-create" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `prefabAssetPath` | `string` | Yes | Prefab asset path. Should be in the format 'Assets/Path/To/Prefab.prefab'. |
| `gameObjectRef` | `any` | No | Reference to a scene GameObject to create the prefab from. If the GameObject is already a prefab instance, a Prefab Variant is created when 'connectGameObjectToPrefab' is true. Optional if 'sourcePrefabAssetPath' is provided. |
| `sourcePrefabAssetPath` | `string` | No | Path to an existing prefab asset to create a Prefab Variant from (e.g. 'Assets/Prefabs/Base.prefab'). When provided, a temporary instance is created, saved as a Prefab Variant, and cleaned up. Optional if 'gameObjectRef' is provided. |
| `connectGameObjectToPrefab` | `boolean` | No | If true, the scene GameObject will be connected to the new prefab (becoming a prefab instance). If the source is already a prefab instance, this creates a Prefab Variant. If false, the prefab asset is created but the scene GameObject remains unchanged. Ignored when 'sourcePrefabAssetPath' is used (always creates a variant). |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "prefabAssetPath": {
      "type": "string"
    },
    "gameObjectRef": {
      "$ref": "#/$defs/EA.GameObjectRef"
    },
    "sourcePrefabAssetPath": {
      "type": "string"
    },
    "connectGameObjectToPrefab": {
      "type": "boolean"
    }
  },
  "$defs": {
    "UnityEngine.EntityId": {
      "type": "integer"
    },
    "System.Type": {
      "type": "string"
    },
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
    }
  },
  "required": [
    "prefabAssetPath"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

