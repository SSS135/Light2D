<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/gameobject.component-modify.md. -->

---
name: gameobject-component-modify
description: |-
  Modify a specific Component on a GameObject in opened Prefab or in a Scene. Allows direct modification of component fields and properties without wrapping in GameObject structure. Use 'gameobject-component-get' first to inspect the component structure before modifying.
  
  Three modification surfaces (use whichever fits the task):
    1. 'componentDiff' — full SerializedMember diff (legacy, backwards compatible).
    2. 'pathPatches' — list of {path, value} pairs routed through Reflector.TryModifyAt; atomic per-path modification, multiple entries can target different depths.
    3. 'jsonPatch' — a JSON Merge Patch (RFC 7396, extended with [i]/[key] notation) routed through Reflector.TryPatch; multiple fields at any depth in a single call.
  When more than one is supplied they run in this order: jsonPatch → pathPatches → componentDiff. At least one is required.
  Path syntax: 'fieldName', 'nested/field', 'arrayField/[i]', 'dictField/[key]'. Leading '#/' is stripped.
---

# GameObject / Component / Modify

Modify a specific Component on a GameObject in opened Prefab or in a Scene. Allows direct modification of component fields and properties without wrapping in GameObject structure. Use 'gameobject-component-get' first to inspect the component structure before modifying.

Three modification surfaces (use whichever fits the task):
  1. 'componentDiff' — full SerializedMember diff (legacy, backwards compatible).
  2. 'pathPatches' — list of {path, value} pairs routed through Reflector.TryModifyAt; atomic per-path modification, multiple entries can target different depths.
  3. 'jsonPatch' — a JSON Merge Patch (RFC 7396, extended with [i]/[key] notation) routed through Reflector.TryPatch; multiple fields at any depth in a single call.
When more than one is supplied they run in this order: jsonPatch → pathPatches → componentDiff. At least one is required.
Path syntax: 'fieldName', 'nested/field', 'arrayField/[i]', 'dictField/[key]'. Leading '#/' is stripped.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "gameobject.component-modify", "args": {
  "gameObjectRef": "string_value",
  "componentRef": "string_value",
  "componentDiff": "string_value",
  "pathPatches": "string_value",
  "jsonPatch": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "gameobject.component-modify" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `gameObjectRef` | `any` | Yes | Find GameObject in opened Prefab or in the active Scene. |
| `componentRef` | `any` | Yes | Component reference. Used to find a Component at GameObject. |
| `componentDiff` | `any` | No | Optional. The full component data to apply (legacy path). Should contain 'fields' and/or 'props' with the values to modify.
Only include the fields/properties you want to change.
Any unknown or invalid fields and properties will be reported in the response. |
| `pathPatches` | `any` | No | Optional. List of path-scoped patches routed through Reflector.TryModifyAt. Each entry targets one field/element/entry by path. Path syntax: 'fieldName', 'nested/field', 'arrayField/[i]', 'dictField/[key]'. |
| `jsonPatch` | `string` | No | Optional. JSON Merge Patch (RFC 7396, extended with [i]/[key] keys) routed through Reflector.TryPatch. Allows multiple fields at any depth to be updated in a single call. Use '$type' for compatible-subtype replacement. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "gameObjectRef": {
      "$ref": "#/$defs/EA.GameObjectRef"
    },
    "componentRef": {
      "$ref": "#/$defs/EA.ComponentRef"
    },
    "componentDiff": {
      "$ref": "#/$defs/com.IvanMurzak.ReflectorNet.Model.SerializedMember"
    },
    "pathPatches": {
      "$ref": "#/$defs/System.Collections.Generic.List\u003CEA.PathPatch\u003E"
    },
    "jsonPatch": {
      "type": "string"
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
    "EA.ComponentRef": {
      "type": "object",
      "properties": {
        "index": {
          "type": "integer",
          "description": "Component \u0027index\u0027 attached to a gameObject. The first index is \u00270\u0027 and that is usually Transform or RectTransform. Priority: 2. Default value is -1."
        },
        "typeName": {
          "type": "string",
          "description": "Component type full name. Sample \u0027UnityEngine.Transform\u0027. If the gameObject has two components of the same type, the output component is unpredictable. Priority: 3. Default value is null."
        },
        "instanceID": {
          "$ref": "#/$defs/UnityEngine.EntityId",
          "description": "instanceID of the UnityEngine.Object. If this is \u00270\u0027, then it will be used as \u0027null\u0027."
        }
      },
      "required": [
        "index",
        "instanceID"
      ],
      "description": "Component reference. Used to find a Component at GameObject."
    },
    "com.IvanMurzak.ReflectorNet.Model.SerializedMemberList": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/com.IvanMurzak.ReflectorNet.Model.SerializedMember"
      }
    },
    "com.IvanMurzak.ReflectorNet.Model.SerializedMember": {
      "type": "object",
      "properties": {
        "typeName": {
          "type": "string",
          "description": "Full type name. Eg: \u0027System.String\u0027, \u0027System.Int32\u0027, \u0027UnityEngine.Vector3\u0027, etc."
        },
        "name": {
          "type": "string",
          "description": "Object name."
        },
        "value": {
          "description": "Value of the object, serialized as a non stringified JSON element. Can be null if the value is not set. Can be default value if the value is an empty object or array json."
        },
        "fields": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/com.IvanMurzak.ReflectorNet.Model.SerializedMember",
            "description": "Nested field value."
          },
          "description": "Fields of the object, serialized as a list of \u0027SerializedMember\u0027."
        },
        "props": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/com.IvanMurzak.ReflectorNet.Model.SerializedMember",
            "description": "Nested property value."
          },
          "description": "Properties of the object, serialized as a list of \u0027SerializedMember\u0027."
        }
      },
      "required": [
        "typeName"
      ],
      "additionalProperties": false
    },
    "EA.PathPatch": {
      "type": "object",
      "properties": {
        "Path": {
          "type": "string",
          "description": "Slash-delimited path to the target field/element/entry. Plain segment navigates a field or property (e.g. \u0027admin\u0027 or \u0027admin/name\u0027). Use \u0027[i]\u0027 for array/list index (e.g. \u0027planets/[0]/orbitRadius\u0027). Use \u0027[key]\u0027 for dictionary entry (e.g. \u0027config/[timeout]\u0027). A leading \u0027#/\u0027 is stripped automatically. Required."
        },
        "Value": {
          "$ref": "#/$defs/com.IvanMurzak.ReflectorNet.Model.SerializedMember",
          "description": "The new value to write at the path. Use the standard SerializedMember envelope: \u0027typeName\u0027 \u002B \u0027value\u0027 for primitives, or nested \u0027fields\u0027/\u0027props\u0027 for complex types. Required \u2014 omitting it overwrites the target with a default empty SerializedMember."
        }
      }
    },
    "System.Collections.Generic.List\u003CEA.PathPatch\u003E": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/EA.PathPatch"
      }
    }
  },
  "required": [
    "gameObjectRef",
    "componentRef"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

## Notes

Three modification surfaces — when more than one is supplied they run in this
order: `jsonPatch` → `pathPatches` → `componentDiff`. At least one is
required.

- `componentDiff` is the full SerializedMember envelope (legacy, backwards
  compatible). Include only the `fields` / `props` you want to change; unknown
  or wrongly-located members are surfaced in the response `Logs`, not
  rejected.
- `pathPatches` is a list of `{Path, Value}` entries routed through
  `Reflector.TryModifyAt`. Each entry is atomic and can target a different
  depth; multiple entries on the same call walk to their own targets
  independently.
- `jsonPatch` is a JSON Merge Patch (RFC 7396) string, extended with the
  `[i]` / `[key]` keys for array indices and dictionary entries. Use `$type`
  for compatible-subtype replacement at a polymorphic slot.

Path syntax is uniform across `pathPatches` and the JSON Merge Patch
extension: `fieldName`, `nested/field`, `arrayField/[i]`, `dictField/[key]`;
a leading `#/` is stripped. Run `gameobject.component-get` with `paths` or
`viewQuery` to confirm the live field names before authoring the patch —
`Logs` will tell you what was ignored, but only after the round trip.

