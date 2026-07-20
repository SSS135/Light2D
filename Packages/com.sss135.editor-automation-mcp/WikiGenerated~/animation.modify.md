<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/animation.modify.md. -->

---
name: animation-modify
description: Modify Unity's AnimationClip asset. Apply an array of modifications including setting curves, clearing curves, setting properties, and managing animation events. Use 'animation-get-data' tool to get valid property names and existing curves for modifications.
---

# Animation / Modify

Modify Unity's AnimationClip asset. Apply an array of modifications including setting curves, clearing curves, setting properties, and managing animation events. Use 'animation-get-data' tool to get valid property names and existing curves for modifications.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "animation.modify", "args": {
  "animRef": "string_value",
  "modifications": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "animation.modify" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `animRef` | `any` | Yes | Reference to the AnimationClip asset to modify. |
| `modifications` | `any` | Yes | Array of modifications to apply to the clip. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "animRef": {
      "$ref": "#/$defs/EA.AssetObjectRef"
    },
    "modifications": {
      "$ref": "#/$defs/EA.AnimationModification[]"
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
    },
    "EA.AnimationModification": {
      "type": "object",
      "properties": {
        "type": {
          "type": "string",
          "enum": [
            "SetCurve",
            "RemoveCurve",
            "ClearCurves",
            "SetFrameRate",
            "SetWrapMode",
            "SetLegacy",
            "AddEvent",
            "ClearEvents"
          ],
          "description": "Modification type. Properties below are used conditionally based on this value."
        },
        "relativePath": {
          "type": "string",
          "description": "Path to target GameObject relative to the root (empty for root). Used by: SetCurve, RemoveCurve."
        },
        "componentType": {
          "type": "string",
          "description": "Component type name (e.g., \u0027Transform\u0027, \u0027SpriteRenderer\u0027). Required for: SetCurve, RemoveCurve."
        },
        "propertyName": {
          "type": "string",
          "description": "Property to animate (e.g., \u0027localPosition.x\u0027, \u0027m_LocalScale.y\u0027). Required for: SetCurve, RemoveCurve."
        },
        "keyframes": {
          "$ref": "#/$defs/EA.AnimationKeyframe[]",
          "description": "Keyframes for the curve. Required for: SetCurve."
        },
        "frameRate": {
          "type": "number",
          "description": "Frames per second. Required for: SetFrameRate."
        },
        "wrapMode": {
          "type": "string",
          "enum": [
            "Default",
            "Clamp",
            "Clamp",
            "Loop",
            "PingPong",
            "ClampForever"
          ],
          "description": "How animation behaves at boundaries. Required for: SetWrapMode."
        },
        "legacy": {
          "type": "boolean",
          "description": "Use legacy animation system. Required for: SetLegacy."
        },
        "time": {
          "type": "number",
          "description": "Event trigger time in seconds. Required for: AddEvent."
        },
        "functionName": {
          "type": "string",
          "description": "Function to invoke. Required for: AddEvent."
        },
        "stringParameter": {
          "type": "string",
          "description": "String parameter passed to the function. Optional for: AddEvent."
        },
        "floatParameter": {
          "type": "number",
          "description": "Float parameter passed to the function. Optional for: AddEvent."
        },
        "intParameter": {
          "type": "integer",
          "description": "Integer parameter passed to the function. Optional for: AddEvent."
        }
      },
      "required": [
        "type"
      ]
    },
    "EA.AnimationKeyframe[]": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/EA.AnimationKeyframe"
      }
    },
    "EA.AnimationKeyframe": {
      "type": "object",
      "properties": {
        "time": {
          "type": "number",
          "description": "Time in seconds."
        },
        "value": {
          "type": "number",
          "description": "Value at this keyframe."
        },
        "inTangent": {
          "type": "number",
          "description": "Incoming tangent (slope). Default: 0."
        },
        "outTangent": {
          "type": "number",
          "description": "Outgoing tangent (slope). Default: 0."
        },
        "weightedMode": {
          "type": "string",
          "enum": [
            "None",
            "In",
            "Out",
            "Both"
          ],
          "description": "Weighted mode: None (0), In (1), Out (2), Both (3). Default: None."
        },
        "inWeight": {
          "type": "number",
          "description": "Incoming weight. Default: 0.33."
        },
        "outWeight": {
          "type": "number",
          "description": "Outgoing weight. Default: 0.33."
        }
      },
      "required": [
        "time",
        "value"
      ]
    },
    "EA.AnimationModification[]": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/EA.AnimationModification"
      }
    }
  },
  "required": [
    "animRef",
    "modifications"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

## Notes

`modifications` is an ordered array of `AnimationModification` ops dispatched
by the `ModificationType` enum: `SetCurve`, `RemoveCurve`, `ClearCurves`,
`SetFrameRate`, `SetWrapMode`, `SetLegacy`, `AddEvent`, `ClearEvents`. Per-op
errors are collected into `response.errors` keyed by index — the remaining
ops still run and the clip is saved once at the end. `RemoveCurve` clears
and re-adds every other binding internally, so batch removals into a single
call when possible.

