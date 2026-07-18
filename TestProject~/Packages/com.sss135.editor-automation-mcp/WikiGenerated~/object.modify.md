<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/object.modify.md. -->

---
name: object-modify
description: |-
  Modify the specified Unity Object. Allows direct modification of object fields and properties. Use 'object-get-data' first to inspect the object structure before modifying.
  
  Three modification surfaces (use whichever fits the task):
    1. 'objectDiff' — full SerializedMember diff (legacy, backwards compatible).
    2. 'pathPatches' — list of {path, value} pairs routed through Reflector.TryModifyAt; atomic per-path modification, multiple entries can target different depths.
    3. 'jsonPatch' — a JSON Merge Patch (RFC 7396, extended with [i]/[key] notation) routed through Reflector.TryPatch; multiple fields at any depth in a single call.
  When more than one is supplied they run in this order: jsonPatch → pathPatches → objectDiff. At least one is required.
  Path syntax: 'fieldName', 'nested/field', 'arrayField/[i]', 'dictField/[key]'. Leading '#/' is stripped.
---

# Object / Modify

Modify the specified Unity Object. Allows direct modification of object fields and properties. Use 'object-get-data' first to inspect the object structure before modifying.

Three modification surfaces (use whichever fits the task):
  1. 'objectDiff' — full SerializedMember diff (legacy, backwards compatible).
  2. 'pathPatches' — list of {path, value} pairs routed through Reflector.TryModifyAt; atomic per-path modification, multiple entries can target different depths.
  3. 'jsonPatch' — a JSON Merge Patch (RFC 7396, extended with [i]/[key] notation) routed through Reflector.TryPatch; multiple fields at any depth in a single call.
When more than one is supplied they run in this order: jsonPatch → pathPatches → objectDiff. At least one is required.
Path syntax: 'fieldName', 'nested/field', 'arrayField/[i]', 'dictField/[key]'. Leading '#/' is stripped.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "object.modify", "args": {
  "objectRef": "string_value",
  "objectDiff": "string_value",
  "pathPatches": "string_value",
  "jsonPatch": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "object.modify" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `objectRef` | `any` | Yes | Reference to UnityEngine.Object instance. It could be GameObject, Component, Asset, etc. Anything extended from UnityEngine.Object. |
| `objectDiff` | `any` | No | Optional. The full object data to apply (legacy path). Should contain 'fields' and/or 'props' with the values to modify.
Only include the fields/properties you want to change.
Any unknown or invalid fields and properties will be reported in the response. |
| `pathPatches` | `any` | No | Optional. List of path-scoped patches routed through Reflector.TryModifyAt. |
| `jsonPatch` | `string` | No | Optional. JSON Merge Patch (RFC 7396, extended with [i]/[key] keys) routed through Reflector.TryPatch. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "objectRef": {
      "$ref": "#/$defs/EA.ObjectRef"
    },
    "objectDiff": {
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
    "EA.ObjectRef": {
      "type": "object",
      "properties": {
        "instanceID": {
          "$ref": "#/$defs/UnityEngine.EntityId",
          "description": "instanceID of the UnityEngine.Object. If this is \u00270\u0027, then it will be used as \u0027null\u0027."
        }
      },
      "required": [
        "instanceID"
      ],
      "description": "Reference to UnityEngine.Object instance. It could be GameObject, Component, Asset, etc. Anything extended from UnityEngine.Object."
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
    "objectRef"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

## Notes

Same patch surface as `gameobject.component-modify`: a `SerializedMember`
`objectDiff` (legacy), a `pathPatches` array, or a `jsonPatch` string, with
execution order `jsonPatch → pathPatches → objectDiff` when more than one
is supplied. The canonical write-up for patch shape, ordering semantics,
and `SerializedMember` envelope conventions lives on
`call("gameobject.component-modify")` — consult that page when authoring a
non-trivial patch, the rules apply unchanged here.

