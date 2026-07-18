<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/assets.prefab-instantiate.md. -->

---
name: assets-prefab-instantiate
description: Instantiates prefab in the current active scene. Use 'assets-find' tool to find prefab assets in the project.
---

# Assets / Prefab / Instantiate

Instantiates prefab in the current active scene. Use 'assets-find' tool to find prefab assets in the project.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "assets.prefab-instantiate", "args": {
  "prefabAssetPath": "string_value",
  "gameObjectPath": "string_value",
  "position": "string_value",
  "rotation": "string_value",
  "scale": "string_value",
  "isLocalSpace": false
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "assets.prefab-instantiate" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `prefabAssetPath` | `string` | Yes | Prefab asset path. |
| `gameObjectPath` | `string` | Yes | GameObject path in the current active scene. |
| `position` | `any` | No | Transform position of the GameObject. |
| `rotation` | `any` | No | Transform rotation of the GameObject. Euler angles in degrees. |
| `scale` | `any` | No | Transform scale of the GameObject. |
| `isLocalSpace` | `boolean` | No | World or Local space of transform. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "prefabAssetPath": {
      "type": "string"
    },
    "gameObjectPath": {
      "type": "string"
    },
    "position": {
      "$ref": "#/$defs/UnityEngine.Vector3"
    },
    "rotation": {
      "$ref": "#/$defs/UnityEngine.Vector3"
    },
    "scale": {
      "$ref": "#/$defs/UnityEngine.Vector3"
    },
    "isLocalSpace": {
      "type": "boolean"
    }
  },
  "$defs": {
    "UnityEngine.Vector3": {
      "type": "object",
      "properties": {
        "x": {
          "type": "number"
        },
        "y": {
          "type": "number"
        },
        "z": {
          "type": "number"
        }
      },
      "required": [
        "x",
        "y",
        "z"
      ],
      "additionalProperties": false
    }
  },
  "required": [
    "prefabAssetPath",
    "gameObjectPath"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

## Notes

Spawns the prefab into the **currently active Scene**, not into the Prefab
Stage. If a Stage is open, close it first or the resulting GameObject will
land inside the Stage hierarchy rather than the scene the caller expects —
this is the most common failure mode for "I instantiated the prefab but I
can't find it in the scene". The response returns a `GameObjectRef`
suitable for follow-up `gameobject.*` and `component.*` calls on the live
instance.

`connectGameObjectToPrefab: true` plus a `gameObjectRef` of an existing
prefab instance produces a Variant rather than a plain instance; the same
flag with a non-prefab source GameObject produces a regular prefab
connection. Mind the asymmetry when scripting bulk instantiate flows.

