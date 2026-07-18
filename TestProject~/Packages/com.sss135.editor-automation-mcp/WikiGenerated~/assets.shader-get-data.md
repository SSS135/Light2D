<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/assets.shader-get-data.md. -->

---
name: assets-shader-get-data
description: |-
  Get detailed data about a shader asset in the Unity project. Returns shader properties, subshaders, passes, compilation errors, and supported status. Use 'assets-find' tool with filter 't:Shader' to find shaders, or 'assets-shader-list-all' tool to list all shader names.
  
  Path-scoped reads (token-saving): supply 'paths' (a list of paths) to read only the listed fields/elements via Reflector.TryReadAt, or 'viewQuery' (a ViewQuery) to navigate to a subtree and/or filter by name regex / max depth / type via Reflector.View. The result populates 'View' on the returned ShaderData. These two parameters are mutually exclusive.
  Path syntax: 'fieldName', 'nested/field', 'arrayField/[i]', 'dictField/[key]'. Leading '#/' is stripped.
---

# Assets / Shader / Get Data

Get detailed data about a shader asset in the Unity project. Returns shader properties, subshaders, passes, compilation errors, and supported status. Use 'assets-find' tool with filter 't:Shader' to find shaders, or 'assets-shader-list-all' tool to list all shader names.

Path-scoped reads (token-saving): supply 'paths' (a list of paths) to read only the listed fields/elements via Reflector.TryReadAt, or 'viewQuery' (a ViewQuery) to navigate to a subtree and/or filter by name regex / max depth / type via Reflector.View. The result populates 'View' on the returned ShaderData. These two parameters are mutually exclusive.
Path syntax: 'fieldName', 'nested/field', 'arrayField/[i]', 'dictField/[key]'. Leading '#/' is stripped.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "assets.shader-get-data", "args": {
  "assetRef": "string_value",
  "includeMessages": "string_value",
  "includeProperties": "string_value",
  "includeSubshaders": "string_value",
  "includeSourceCode": "string_value",
  "paths": "string_value",
  "viewQuery": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "assets.shader-get-data" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `assetRef` | `any` | Yes | Reference to UnityEngine.Object asset instance. It could be Material, ScriptableObject, Prefab, and any other Asset. Anything located in the Assets and Packages folders. |
| `includeMessages` | `any` | No | Include compilation error and warning messages. Default: true |
| `includeProperties` | `any` | No | Include shader properties (uniforms) list. Default: false |
| `includeSubshaders` | `any` | No | Include subshader and pass structure. Default: false |
| `includeSourceCode` | `any` | No | Include pass source code in subshader data. Requires 'includeSubshaders' to be true. Can produce very large responses. Default: false |
| `paths` | `any` | No | Optional. List of paths to read individually via Reflector.TryReadAt against the underlying Shader asset. Path syntax: 'fieldName', 'nested/field', 'arrayField/[i]', 'dictField/[key]'. Mutually exclusive with 'viewQuery'. |
| `viewQuery` | `any` | No | Optional. View-query filter routed through Reflector.View against the underlying Shader asset. Mutually exclusive with 'paths'. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "assetRef": {
      "$ref": "#/$defs/EA.AssetObjectRef"
    },
    "includeMessages": {
      "$ref": "#/$defs/System.Boolean"
    },
    "includeProperties": {
      "$ref": "#/$defs/System.Boolean"
    },
    "includeSubshaders": {
      "$ref": "#/$defs/System.Boolean"
    },
    "includeSourceCode": {
      "$ref": "#/$defs/System.Boolean"
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
    "System.Boolean": {
      "type": "boolean"
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
    "assetRef"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

