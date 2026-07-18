<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/assets.prefab-open.md. -->

---
name: assets-prefab-open
description: "Open prefab edit mode for a specific GameObject. In the Edit mode you can modify the prefab. The modification will be applied to all instances of the prefab across the project. Note: Please use 'assets-prefab-close' tool later to exit prefab editing mode."
---

# Assets / Prefab / Open

Open prefab edit mode for a specific GameObject. In the Edit mode you can modify the prefab. The modification will be applied to all instances of the prefab across the project. Note: Please use 'assets-prefab-close' tool later to exit prefab editing mode.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "assets.prefab-open", "args": {
  "gameObjectRef": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "assets.prefab-open" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `gameObjectRef` | `any` | Yes | GameObject that represents prefab instance of an original prefab GameObject. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "gameObjectRef": {
      "$ref": "#/$defs/EA.GameObjectRef"
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
    "gameObjectRef"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

## Notes

Enters Prefab Stage mode for the prefab backing the given scene instance.
Every `open` must be paired with a `call("assets.prefab-close")` — leaving
the Stage open silently redirects subsequent scene-mode calls
(`gameobject.*`, `scene.*`, `assets.prefab-instantiate`) to the Stage
hierarchy rather than the live scene. That redirection is invisible in the
response payload, so a missing `close` typically surfaces as a later
"object disappeared" or "edit went to the wrong place" symptom.

`assets.prefab-close` takes a `save` flag; pass `true` to persist edits
made inside the Stage, `false` to discard. Prefer `close(save: true)` over
a trailing `save` + `close(save: false)` pair when you're done editing —
fewer round trips and the same on-disk result.

