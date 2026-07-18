<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/assets.modify.md. -->

---
name: assets-modify
description: |-
  Modify asset file in the project. Use 'assets-get-data' tool first to inspect the asset structure before modifying. Not allowed to modify asset file in 'Packages/' folder. Please modify it in 'Assets/' folder.
  
  Three modification surfaces (use whichever fits the task):
    1. 'content' — full SerializedMember override (legacy, backwards compatible).
    2. 'pathPatches' — list of {path, value} pairs routed through Reflector.TryModifyAt.
    3. 'jsonPatch' — JSON Merge Patch routed through Reflector.TryPatch.
  When more than one is supplied they run in this order: jsonPatch → pathPatches → content. At least one is required.
  Path syntax: 'fieldName', 'nested/field', 'arrayField/[i]', 'dictField/[key]'. Leading '#/' is stripped.
---

# Assets / Modify

Modify asset file in the project. Use 'assets-get-data' tool first to inspect the asset structure before modifying. Not allowed to modify asset file in 'Packages/' folder. Please modify it in 'Assets/' folder.

Three modification surfaces (use whichever fits the task):
  1. 'content' — full SerializedMember override (legacy, backwards compatible).
  2. 'pathPatches' — list of {path, value} pairs routed through Reflector.TryModifyAt.
  3. 'jsonPatch' — JSON Merge Patch routed through Reflector.TryPatch.
When more than one is supplied they run in this order: jsonPatch → pathPatches → content. At least one is required.
Path syntax: 'fieldName', 'nested/field', 'arrayField/[i]', 'dictField/[key]'. Leading '#/' is stripped.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "assets.modify", "args": {
  "assetRef": "string_value",
  "content": "string_value",
  "pathPatches": "string_value",
  "jsonPatch": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "assets.modify" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `assetRef` | `any` | Yes | Reference to UnityEngine.Object asset instance. It could be Material, ScriptableObject, Prefab, and any other Asset. Anything located in the Assets and Packages folders. |
| `content` | `any` | No | Optional. The asset content. It overrides the existing asset content (legacy path). |
| `pathPatches` | `any` | No | Optional. List of path-scoped patches routed through Reflector.TryModifyAt. |
| `jsonPatch` | `string` | No | Optional. JSON Merge Patch (RFC 7396, extended with [i]/[key] keys) routed through Reflector.TryPatch. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "assetRef": {
      "$ref": "#/$defs/EA.AssetObjectRef"
    },
    "content": {
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
    "assetRef"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

