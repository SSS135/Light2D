<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/gameobject.modify.md. -->

---
name: gameobject-modify
description: |-
  Modify GameObject fields and properties in opened Prefab or in a Scene. You can modify multiple GameObjects at once. Just provide the same number of GameObject references and SerializedMember objects.
  
  Three modification surfaces (per GameObject — parallel arrays must have the same length as gameObjectRefs):
    1. 'gameObjectDiffs' — full SerializedMember diff per GameObject (legacy, backwards compatible).
    2. 'pathPatchesPerGameObject' — list of {path, value} patches per GameObject routed through Reflector.TryModifyAt; atomic per-path modification.
    3. 'jsonPatchesPerGameObject' — JSON Merge Patch per GameObject routed through Reflector.TryPatch.
  When more than one is supplied for the same GameObject they run in this order: jsonPatch → pathPatches → diff. At least one of the three is required.
  Path syntax: 'fieldName', 'nested/field', 'arrayField/[i]', 'dictField/[key]'.
---

# GameObject / Modify

Modify GameObject fields and properties in opened Prefab or in a Scene. You can modify multiple GameObjects at once. Just provide the same number of GameObject references and SerializedMember objects.

Three modification surfaces (per GameObject — parallel arrays must have the same length as gameObjectRefs):
  1. 'gameObjectDiffs' — full SerializedMember diff per GameObject (legacy, backwards compatible).
  2. 'pathPatchesPerGameObject' — list of {path, value} patches per GameObject routed through Reflector.TryModifyAt; atomic per-path modification.
  3. 'jsonPatchesPerGameObject' — JSON Merge Patch per GameObject routed through Reflector.TryPatch.
When more than one is supplied for the same GameObject they run in this order: jsonPatch → pathPatches → diff. At least one of the three is required.
Path syntax: 'fieldName', 'nested/field', 'arrayField/[i]', 'dictField/[key]'.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "gameobject.modify", "args": {
  "gameObjectRefs": "string_value",
  "gameObjectDiffs": "string_value",
  "pathPatchesPerGameObject": "string_value",
  "jsonPatchesPerGameObject": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "gameobject.modify" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `gameObjectRefs` | `any` | Yes | Array of GameObjects in opened Prefab or in the active Scene. |
| `gameObjectDiffs` | `any` | No | Optional. Each item in the array represents a GameObject modification of the 'gameObjectRefs' at the same index. Usually a GameObject is a container for components. Each component may have fields and properties for modification. If you need to modify components of a GameObject, please use 'gameobject-component-modify' tool. Ignore values that should not be modified. Any unknown or wrong located fields and properties will be ignored. Check the result of this command to see what was changed. The ignored fields and properties will be listed. |
| `pathPatchesPerGameObject` | `any` | No | Optional. Per-GameObject list of path-scoped patches routed through Reflector.TryModifyAt. Outer index aligns with 'gameObjectRefs'; inner list contains {path, value} entries. Pass null or omit for GameObjects that should not receive path patches. |
| `jsonPatchesPerGameObject` | `any` | No | Optional. Per-GameObject JSON Merge Patch (RFC 7396, extended with [i]/[key] keys) routed through Reflector.TryPatch. Outer index aligns with 'gameObjectRefs'. Pass null or omit for GameObjects that should not receive a JSON patch. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "gameObjectRefs": {
      "$ref": "#/$defs/EA.GameObjectRefList"
    },
    "gameObjectDiffs": {
      "$ref": "#/$defs/com.IvanMurzak.ReflectorNet.Model.SerializedMemberList"
    },
    "pathPatchesPerGameObject": {
      "$ref": "#/$defs/System.Collections.Generic.List\u003CSystem.Collections.Generic.List\u003CEA.PathPatch\u003E\u003E"
    },
    "jsonPatchesPerGameObject": {
      "$ref": "#/$defs/System.Collections.Generic.List\u003CSystem.String\u003E"
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
    "System.Collections.Generic.List\u003CEA.PathPatch\u003E": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/EA.PathPatch"
      }
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
    "System.Collections.Generic.List\u003CSystem.Collections.Generic.List\u003CEA.PathPatch\u003E\u003E": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/System.Collections.Generic.List\u003CEA.PathPatch\u003E"
      }
    },
    "System.Collections.Generic.List\u003CSystem.String\u003E": {
      "type": "array",
      "items": {
        "type": "string"
      }
    }
  },
  "required": [
    "gameObjectRefs"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

## Notes

Three modification surfaces stack per GameObject — when more than one is
supplied for the same ref the handler applies them in a fixed order:
`jsonPatchesPerGameObject` → `pathPatchesPerGameObject` → `gameObjectDiffs`.
At least one of the three is required. All three arrays are parallel to
`gameObjectRefs`; pass `null` at an index to skip that surface for that
GameObject.

Path syntax across `pathPatches` and the `[i]`/`[key]` JSON Merge Patch
extension is uniform: plain segment for fields/properties, `[i]` for
list/array index, `[key]` for dictionary entry, leading `#/` stripped. To edit
component-owned fields, prefer
[`gameobject.component-modify`](#gameobjectcomponent-modify) — the GameObject
surface ignores component-internal fields silently and reports them in the
returned log entries.

