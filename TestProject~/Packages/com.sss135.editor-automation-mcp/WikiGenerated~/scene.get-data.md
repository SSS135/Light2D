<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/scene.get-data.md. -->

---
name: scene-get-data
description: |-
  This tool retrieves the list of root GameObjects in the specified scene. Use 'scene-list-opened' tool to get the list of all opened scenes.
  
  Path-scoped reads (token-saving): supply 'paths' (a list of paths) to read only the listed fields/elements from the scene's root-GameObjects array via Reflector.TryReadAt, or 'viewQuery' (a ViewQuery) to navigate/filter the same array via Reflector.View. The result populates 'Data' on the returned SceneData. These two parameters are mutually exclusive.
  Path syntax: 'fieldName', 'nested/field', 'arrayField/[i]', 'dictField/[key]'. Leading '#/' is stripped. Example: paths=['[0]/name'] reads the name of the first root GameObject.
---

# Scene / Get Data

This tool retrieves the list of root GameObjects in the specified scene. Use 'scene-list-opened' tool to get the list of all opened scenes.

Path-scoped reads (token-saving): supply 'paths' (a list of paths) to read only the listed fields/elements from the scene's root-GameObjects array via Reflector.TryReadAt, or 'viewQuery' (a ViewQuery) to navigate/filter the same array via Reflector.View. The result populates 'Data' on the returned SceneData. These two parameters are mutually exclusive.
Path syntax: 'fieldName', 'nested/field', 'arrayField/[i]', 'dictField/[key]'. Leading '#/' is stripped. Example: paths=['[0]/name'] reads the name of the first root GameObject.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "scene.get-data", "args": {
  "openedSceneName": "string_value",
  "includeRootGameObjects": false,
  "includeChildrenDepth": 0,
  "includeBounds": false,
  "includeData": false,
  "paths": "string_value",
  "viewQuery": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "scene.get-data" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `openedSceneName` | `string` | No | Name of the opened scene. If empty or null, the active scene will be used. |
| `includeRootGameObjects` | `boolean` | No | If true, includes root GameObjects in the scene data. |
| `includeChildrenDepth` | `integer` | No | Determines the depth of the hierarchy to include. |
| `includeBounds` | `boolean` | No | If true, includes bounding box information for GameObjects. |
| `includeData` | `boolean` | No | If true, includes component data for GameObjects. |
| `paths` | `any` | No | Optional. List of paths to read individually via Reflector.TryReadAt against the scene's root-GameObjects array. Path syntax: 'fieldName', '[i]/field', '[i]/component/[j]/property'. Mutually exclusive with 'viewQuery'. |
| `viewQuery` | `any` | No | Optional. View-query filter routed through Reflector.View on the scene's root-GameObjects array. Mutually exclusive with 'paths'. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "openedSceneName": {
      "type": "string"
    },
    "includeRootGameObjects": {
      "type": "boolean"
    },
    "includeChildrenDepth": {
      "type": "integer"
    },
    "includeBounds": {
      "type": "boolean"
    },
    "includeData": {
      "type": "boolean"
    },
    "paths": {
      "$ref": "#/$defs/System.Collections.Generic.List\u003CSystem.String\u003E"
    },
    "viewQuery": {
      "$ref": "#/$defs/com.IvanMurzak.ReflectorNet.Model.ViewQuery"
    }
  },
  "$defs": {
    "System.Collections.Generic.List\u003CSystem.String\u003E": {
      "type": "array",
      "items": {
        "type": "string"
      }
    },
    "System.Type": {
      "type": "string"
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
  }
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

