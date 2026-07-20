<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/type.get-json-schema.md. -->

---
name: type-get-json-schema
description: Generates a JSON Schema for a given C# type name using reflection. Supports primitives, enums, arrays, generic collections, dictionaries, and complex objects. The type must be present in any loaded assembly. Use the full type name (e.g. 'UnityEngine.Vector3') for best results.
---

# Type / Get Json Schema

Generates a JSON Schema for a given C# type name using reflection. Supports primitives, enums, arrays, generic collections, dictionaries, and complex objects. The type must be present in any loaded assembly. Use the full type name (e.g. 'UnityEngine.Vector3') for best results.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "type.get-json-schema", "args": {
  "typeName": "string_value",
  "descriptionMode": "string_value",
  "propertyDescriptionMode": "string_value",
  "includeNestedTypes": false,
  "writeIndented": false
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "type.get-json-schema" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `typeName` | `string` | Yes | Full C# type name to generate the schema for. Examples: 'System.String', 'UnityEngine.Vector3', 'System.Collections.Generic.List<System.Int32>'. Simple names like 'Vector3' are also accepted when unambiguous. |
| `descriptionMode` | `string` | No | Controls the type-level 'description' field. Include: keep on the target type only. IncludeRecursively: keep on the target type and inside $defs entries. Ignore: strip all type-level descriptions. Default: Ignore. |
| `propertyDescriptionMode` | `string` | No | Controls 'description' fields on properties, fields, and array items. Include: keep on the target type's own properties/items only. IncludeRecursively: keep on all properties/items including those inside $defs entries. Ignore: strip all property/item descriptions. Default: Ignore. |
| `includeNestedTypes` | `boolean` | No | When true, complex nested types are extracted into '$defs' and referenced via '$ref' instead of being inlined. Useful for large or recursive types. Default: false. |
| `writeIndented` | `boolean` | No | Whether to format the output JSON with indentation for readability. Default: false. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "typeName": {
      "type": "string"
    },
    "descriptionMode": {
      "type": "string",
      "enum": [
        "Include",
        "IncludeRecursively",
        "Ignore"
      ]
    },
    "propertyDescriptionMode": {
      "type": "string",
      "enum": [
        "Include",
        "IncludeRecursively",
        "Ignore"
      ]
    },
    "includeNestedTypes": {
      "type": "boolean"
    },
    "writeIndented": {
      "type": "boolean"
    }
  },
  "required": [
    "typeName"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

## Notes

`descriptionMode` and `propertyDescriptionMode` are independent knobs. Both
default to `Ignore`, which strips the human-readable `description` text and
returns the smallest schema. Set them to `Include` to keep descriptions on the
target type (and the target's own properties) only, or `IncludeRecursively` to
keep them inside `$defs` entries as well. Use `IncludeRecursively` when you
are reading the schema yourself for documentation purposes; leave both on
`Ignore` when feeding the schema into another tool's structured output.

`includeNestedTypes: true` switches from inline-everything to a `$defs`-with-
`$ref` layout. Prefer it for large or recursive type graphs — without it the
emitter inlines every nested object at every reference site and the output
explodes for types like `AnimatorController` or
`UnityEditor.SerializedProperty`. With it, each composite type appears once
in `$defs` and the body references it. Self-referential types
(`Transform.parent` returns `Transform`) require this flag; the inline path
hits a depth limit and truncates.

`writeIndented: true` adds whitespace for human reading; leave it `false` when
the consumer is another tool.

