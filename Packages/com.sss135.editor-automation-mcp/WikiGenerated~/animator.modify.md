<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/animator.modify.md. -->

---
name: animator-modify
description: Modify Unity's AnimatorController asset. Apply an array of modifications including adding/removing parameters, layers, states, and transitions. Use 'animator-get-data' tool to get valid names and parameters for modifications.
---

# Animator / Modify

Modify Unity's AnimatorController asset. Apply an array of modifications including adding/removing parameters, layers, states, and transitions. Use 'animator-get-data' tool to get valid names and parameters for modifications.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "animator.modify", "args": {
  "animatorRef": "string_value",
  "modifications": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "animator.modify" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `animatorRef` | `any` | Yes | Reference to the AnimatorController asset to modify. |
| `modifications` | `any` | Yes | Array of modifications to apply to the controller. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "animatorRef": {
      "$ref": "#/$defs/EA.AssetObjectRef"
    },
    "modifications": {
      "$ref": "#/$defs/EA.AnimatorModification[]"
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
    "EA.AnimatorModification": {
      "type": "object",
      "properties": {
        "type": {
          "type": "string",
          "enum": [
            "AddParameter",
            "RemoveParameter",
            "AddLayer",
            "RemoveLayer",
            "AddState",
            "RemoveState",
            "SetDefaultState",
            "AddTransition",
            "RemoveTransition",
            "AddAnyStateTransition",
            "SetStateMotion",
            "SetStateSpeed"
          ],
          "description": "Modification type. Properties below are used conditionally based on this value."
        },
        "parameterName": {
          "type": "string",
          "description": "Parameter name. Required for: AddParameter, RemoveParameter."
        },
        "parameterType": {
          "type": "string",
          "description": "Parameter type: Float, Int, Bool, Trigger. Required for: AddParameter."
        },
        "defaultFloat": {
          "type": "number",
          "description": "Default float value. Optional for: AddParameter (Float type)."
        },
        "defaultInt": {
          "type": "integer",
          "description": "Default int value. Optional for: AddParameter (Int type)."
        },
        "defaultBool": {
          "type": "boolean",
          "description": "Default bool value. Optional for: AddParameter (Bool type)."
        },
        "layerName": {
          "type": "string",
          "description": "Layer name. Required for: AddLayer, RemoveLayer, AddState, RemoveState, SetDefaultState, AddTransition, RemoveTransition, AddAnyStateTransition, SetStateMotion, SetStateSpeed."
        },
        "stateName": {
          "type": "string",
          "description": "State name. Required for: AddState, RemoveState, SetDefaultState, SetStateMotion, SetStateSpeed."
        },
        "motionAssetPath": {
          "type": "string",
          "description": "Asset path to AnimationClip. Optional for: AddState. Required for: SetStateMotion."
        },
        "speed": {
          "type": "number",
          "description": "Speed multiplier. Required for: SetStateSpeed."
        },
        "sourceStateName": {
          "type": "string",
          "description": "Source state name. Required for: AddTransition, RemoveTransition."
        },
        "destinationStateName": {
          "type": "string",
          "description": "Destination state name. Required for: AddTransition, RemoveTransition, AddAnyStateTransition."
        },
        "hasExitTime": {
          "type": "boolean",
          "description": "Whether transition waits for exit time. Optional for: AddTransition, AddAnyStateTransition."
        },
        "exitTime": {
          "type": "number",
          "description": "Normalized exit time (0-1). Optional for: AddTransition, AddAnyStateTransition."
        },
        "duration": {
          "type": "number",
          "description": "Transition blend duration. Optional for: AddTransition, AddAnyStateTransition."
        },
        "hasFixedDuration": {
          "type": "boolean",
          "description": "Whether duration is in seconds (true) or normalized (false). Optional for: AddTransition, AddAnyStateTransition."
        },
        "conditions": {
          "$ref": "#/$defs/EA.AnimatorConditionData[]",
          "description": "Transition conditions. Optional for: AddTransition, AddAnyStateTransition."
        }
      },
      "required": [
        "type"
      ]
    },
    "EA.AnimatorConditionData[]": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/EA.AnimatorConditionData"
      }
    },
    "EA.AnimatorConditionData": {
      "type": "object",
      "properties": {
        "parameter": {
          "type": "string",
          "description": "Parameter name for the condition."
        },
        "mode": {
          "type": "string",
          "description": "Condition mode: If, IfNot, Greater, Less, Equals, NotEqual."
        },
        "threshold": {
          "type": "number",
          "description": "Threshold value for the condition."
        }
      }
    },
    "EA.AnimatorModification[]": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/EA.AnimatorModification"
      }
    }
  },
  "required": [
    "animatorRef",
    "modifications"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

## Notes

Operations dispatch on `modifications[i].type`. Supported
`AnimatorModificationType` values: `AddParameter`, `RemoveParameter`,
`AddLayer`, `RemoveLayer`, `AddState`, `RemoveState`, `SetDefaultState`,
`AddTransition`, `RemoveTransition`, `AddAnyStateTransition`,
`SetStateMotion`, `SetStateSpeed`. Each op only reads the `AnimatorModification`
fields relevant to its type (`parameterName`, `layerName`, `stateName`,
`sourceStateName`, `destinationStateName`, `motionAssetPath`, `speed`,
`conditions`, transition timing); missing required fields fail that op only
and the error is reported in `errors[]` with the op's index. Other ops in the
same call still apply. Parameter types must be one of `Float`, `Int`, `Bool`,
`Trigger`; condition modes must be one of `If`, `IfNot`, `Greater`, `Less`,
`Equals`, `NotEqual`.

