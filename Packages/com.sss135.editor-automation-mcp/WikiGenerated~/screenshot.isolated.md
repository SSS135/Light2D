<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/screenshot.isolated.md. -->

---
name: screenshot-isolated
description: Renders a screenshot of a target GameObject with configurable isolation, background, camera angle, and lighting. When isolated=true (default), only the target object is visible via layer-based culling and inactive children of the target are temporarily activated for the render (their OnEnable callbacks may fire — restored in finally, but side effects like audio/network/animation events are not undoable). When isolated=false, the existing scene state is rendered as-is without activating inactive objects. Supports custom multi-light setups via JSON. Returns a base64-encoded PNG.
---

# Screenshot / Isolated GameObject

Renders a screenshot of a target GameObject with configurable isolation, background, camera angle, and lighting. When isolated=true (default), only the target object is visible via layer-based culling and inactive children of the target are temporarily activated for the render (their OnEnable callbacks may fire — restored in finally, but side effects like audio/network/animation events are not undoable). When isolated=false, the existing scene state is rendered as-is without activating inactive objects. Supports custom multi-light setups via JSON. Returns a base64-encoded PNG.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "screenshot.isolated", "args": {
  "gameObjectRef": "string_value",
  "includeChildren": "string_value",
  "isolated": "string_value",
  "backgroundMode": "string_value",
  "backgroundColor": "string_value",
  "cameraView": "string_value",
  "fieldOfView": "string_value",
  "nearClipPlane": "string_value",
  "farClipPlane": "string_value",
  "padding": "string_value",
  "lights": "string_value",
  "resolution": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "screenshot.isolated" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `gameObjectRef` | `any` | Yes | Reference to the target GameObject (by instanceId, path, or name). |
| `includeChildren` | `any` | No | Include child GameObjects in the render. Default: true. |
| `isolated` | `any` | No | When true, renders only the target object using layer-based culling. When false, renders the full scene from the computed camera position. Default: true. |
| `backgroundMode` | `any` | No | Background mode. Default: SolidColor. |
| `backgroundColor` | `string` | No | Hex background color (e.g. '#404040'). Only used when backgroundMode is SolidColor. |
| `cameraView` | `any` | No | Camera angle relative to the target object's bounding box. Default: Front. |
| `fieldOfView` | `any` | No | Camera vertical field of view in degrees. Default: 60. |
| `nearClipPlane` | `any` | No | Camera near clip plane distance. Default: 0.01. |
| `farClipPlane` | `any` | No | Camera far clip plane distance. Default: 1000. |
| `padding` | `any` | No | Framing multiplier around the object. 1.0 = tight fit, 1.5 = 50% extra space. Default: 1.2. |
| `lights` | `string` | No | JSON array of light configurations. Each object defines type, color, intensity, rotation, position, range, spotAngle, shadows, etc. When null, a default white directional light at rotation (50,-30,0) is used. Example: [{"type":"Directional","color":"#FFF4E5","intensity":1.2,"rotation":[45,-45,0]}] |
| `resolution` | `any` | No | Output image resolution in pixels (width = height). Default: 512. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "gameObjectRef": {
      "$ref": "#/$defs/EA.GameObjectRef"
    },
    "includeChildren": {
      "$ref": "#/$defs/System.Boolean"
    },
    "isolated": {
      "$ref": "#/$defs/System.Boolean"
    },
    "backgroundMode": {
      "$ref": "#/$defs/com.SSS135.EditorAutomation.Editor.API.Tool_Screenshot\u002BBackgroundMode"
    },
    "backgroundColor": {
      "type": "string"
    },
    "cameraView": {
      "$ref": "#/$defs/com.SSS135.EditorAutomation.Editor.API.Tool_Screenshot\u002BCameraView"
    },
    "fieldOfView": {
      "$ref": "#/$defs/System.Single"
    },
    "nearClipPlane": {
      "$ref": "#/$defs/System.Single"
    },
    "farClipPlane": {
      "$ref": "#/$defs/System.Single"
    },
    "padding": {
      "$ref": "#/$defs/System.Single"
    },
    "lights": {
      "type": "string"
    },
    "resolution": {
      "$ref": "#/$defs/System.Int32"
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
    },
    "System.Boolean": {
      "type": "boolean"
    },
    "com.SSS135.EditorAutomation.Editor.API.Tool_Screenshot\u002BBackgroundMode": {
      "type": "string",
      "enum": [
        "SolidColor",
        "Skybox",
        "Transparent"
      ]
    },
    "com.SSS135.EditorAutomation.Editor.API.Tool_Screenshot\u002BCameraView": {
      "type": "string",
      "enum": [
        "Front",
        "Back",
        "Left",
        "Right",
        "Top",
        "Bottom",
        "Composite"
      ]
    },
    "System.Single": {
      "type": "number"
    },
    "System.Int32": {
      "type": "integer"
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

Renders into a transient, plugin-owned scene. With `isolated: true` (default)
layer-based culling hides everything except the target, and any **inactive
children of the target are temporarily activated** so the render shows the
full assembled object. That activation pass fires children's `OnEnable`
callbacks — most of the time harmless, but side effects like audio sources
auto-playing, animation events firing, or networked behaviours dispatching
are **not undone** when the prior active state is restored. Treat the call
as read-mostly, not read-only.

With `isolated: false` the existing scene state is rendered as-is from the
computed camera position; no children are activated and no culling happens.
Use it when surrounding scene context is part of the desired image.

The `lights` parameter takes a JSON array — the per-entry shape (`type`,
`color`, `intensity`, `rotation`, `position`, `range`, `spotAngle`, `shadows`,
…) lives in the generated parameter schema and is not duplicated here. Omit
`lights` to get the default white directional light at rotation
`(50, -30, 0)`; supply an array for matcap-style or three-point compositions.

