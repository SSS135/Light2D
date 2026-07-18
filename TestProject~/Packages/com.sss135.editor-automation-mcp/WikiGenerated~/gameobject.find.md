<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/gameobject.find.md. -->

---
name: gameobject-find
description: |-
  Finds specific GameObject by provided information in opened Prefab or in a Scene. First it looks for the opened Prefab, if any Prefab is opened it looks only there ignoring a scene. If no opened Prefab it looks into current active scene. Returns GameObject information and its children. Also, it returns Components preview just for the target GameObject.
  
  Path-scoped reads (token-saving): supply 'paths' (a list of paths) to read only the listed fields/elements via Reflector.TryReadAt, or 'viewQuery' (a ViewQuery) to navigate to a subtree and/or filter by name regex / max depth / type via Reflector.View. When either is supplied, the result populates 'Data' on the returned GameObjectData and overrides 'includeData' (which would otherwise produce a full recursive serialization). These two parameters are mutually exclusive — supply at most one.
  Path syntax: 'fieldName', 'nested/field', 'arrayField/[i]', 'dictField/[key]'. Leading '#/' is stripped.
---

# GameObject / Find

Finds specific GameObject by provided information in opened Prefab or in a Scene. First it looks for the opened Prefab, if any Prefab is opened it looks only there ignoring a scene. If no opened Prefab it looks into current active scene. Returns GameObject information and its children. Also, it returns Components preview just for the target GameObject.

Path-scoped reads (token-saving): supply 'paths' (a list of paths) to read only the listed fields/elements via Reflector.TryReadAt, or 'viewQuery' (a ViewQuery) to navigate to a subtree and/or filter by name regex / max depth / type via Reflector.View. When either is supplied, the result populates 'Data' on the returned GameObjectData and overrides 'includeData' (which would otherwise produce a full recursive serialization). These two parameters are mutually exclusive — supply at most one.
Path syntax: 'fieldName', 'nested/field', 'arrayField/[i]', 'dictField/[key]'. Leading '#/' is stripped.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "gameobject.find", "args": {
  "gameObjectRef": "string_value",
  "includeData": false,
  "includeComponents": false,
  "includeBounds": false,
  "includeHierarchy": false,
  "hierarchyDepth": 0,
  "paths": "string_value",
  "viewQuery": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "gameobject.find" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `gameObjectRef` | `any` | Yes | Find GameObject in opened Prefab or in the active Scene. |
| `includeData` | `boolean` | No | Include editable GameObject data (tag, layer, etc). |
| `includeComponents` | `boolean` | No | Include attached components references. |
| `includeBounds` | `boolean` | No | Include 3D bounds of the GameObject. |
| `includeHierarchy` | `boolean` | No | Include hierarchy metadata. |
| `hierarchyDepth` | `integer` | No | Determines the depth of the hierarchy to include. 0 - means only the target GameObject. 1 - means to include one layer below. |
| `paths` | `any` | No | Optional. List of paths to read individually via Reflector.TryReadAt. When supplied, replaces 'includeData'-style full serialization with a path-scoped aggregate. Path syntax: 'fieldName', 'nested/field', 'arrayField/[i]', 'dictField/[key]'. Mutually exclusive with 'viewQuery'. |
| `viewQuery` | `any` | No | Optional. View-query filter routed through Reflector.View. When supplied, replaces 'includeData'-style full serialization with the filtered subtree. Mutually exclusive with 'paths'. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "gameObjectRef": {
      "$ref": "#/$defs/EA.GameObjectRef"
    },
    "includeData": {
      "type": "boolean"
    },
    "includeComponents": {
      "type": "boolean"
    },
    "includeBounds": {
      "type": "boolean"
    },
    "includeHierarchy": {
      "type": "boolean"
    },
    "hierarchyDepth": {
      "type": "integer"
    },
    "paths": {
      "$ref": "#/$defs/System.Collections.Generic.List\u003CSystem.String\u003E"
    },
    "viewQuery": {
      "$ref": "#/$defs/com.IvanMurzak.ReflectorNet.Model.ViewQuery"
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
    "System.Collections.Generic.List\u003CSystem.String\u003E": {
      "type": "array",
      "items": {
        "type": "string"
      }
    },
    "com.IvanMurzak.ReflectorNet.Model.ViewQuery": {
      "type": "object",
      "properties": {
        "Path": {
          "type": "string",
          "description": "Navigate to this path first, then serialize only that subtree. Path segments are separated by \u0027/\u0027. Use \u0027[i]\u0027 for array/list index (e.g. \u0027users/[2]/name\u0027) and \u0027[key]\u0027 for dictionary entry (e.g. \u0027config/[timeout]\u0027). A leading \u0027#/\u0027 is stripped automatically. Examples: \u0027admin/name\u0027, \u0027users/[0]/email\u0027, \u0027config/[timeout]\u0027. Leave null to start from the root object."
        },
        "NamePattern": {
          "type": "string",
          "description": "Case-insensitive .NET regex pattern matched against field and property names. Only branches containing at least one match are kept in the result tree. Examples: \u0027orbitRadius\u0027 (exact name), \u0027orbit.*\u0027 (prefix match), \u0027radius|speed\u0027 (either name). When nothing matches, the root envelope is returned with empty fields/props. Leave null to return all fields and properties without filtering."
        },
        "MaxDepth": {
          "type": "integer",
          "description": "Maximum nesting depth of the returned serialized tree. 0 = root type name and value only \u2014 no nested fields or properties. 1 = one level of fields/props visible, their children stripped. 2 = two levels visible, and so on. Leave null (default) for unlimited depth."
        },
        "TypeFilter": {
          "$ref": "#/$defs/System.Type",
          "description": "When set, prunes the result tree to members whose runtime type is assignable to this type. Non-matching branches are removed; the root envelope is always preserved. Examples: typeof(float) keeps only float fields, typeof(IEnumerable) keeps only collections. Leave null to include members of any type."
        }
      }
    }
  },
  "required": [
    "gameObjectRef"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

