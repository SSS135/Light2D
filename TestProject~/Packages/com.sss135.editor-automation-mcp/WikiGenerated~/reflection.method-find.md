<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/reflection.method-find.md. -->

---
name: reflection-method-find
description: Find method in the project using C# Reflection. It looks for all assemblies in the project and finds method by its name, class name and parameters. Even private methods are available. Use 'reflection-method-call' to call the method after finding it.
---

# Method C# / Find

Find method in the project using C# Reflection. It looks for all assemblies in the project and finds method by its name, class name and parameters. Even private methods are available. Use 'reflection-method-call' to call the method after finding it.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "reflection.method-find", "args": {
  "filter": "string_value",
  "knownNamespace": false,
  "typeNameMatchLevel": 0,
  "methodNameMatchLevel": 0,
  "parametersMatchLevel": 0
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "reflection.method-find" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `filter` | `any` | Yes | Method reference. Used to find method in codebase of the project. |
| `knownNamespace` | `boolean` | No | Set to true if 'Namespace' is known and full namespace name is specified in the 'filter.Namespace' property. Otherwise, set to false. |
| `typeNameMatchLevel` | `integer` | No | Minimal match level for 'typeName'. 0 - ignore 'filter.typeName', 1 - contains ignoring case (default value), 2 - contains case sensitive, 3 - starts with ignoring case, 4 - starts with case sensitive, 5 - equals ignoring case, 6 - equals case sensitive. |
| `methodNameMatchLevel` | `integer` | No | Minimal match level for 'MethodName'. 0 - ignore 'filter.MethodName', 1 - contains ignoring case (default value), 2 - contains case sensitive, 3 - starts with ignoring case, 4 - starts with case sensitive, 5 - equals ignoring case, 6 - equals case sensitive. |
| `parametersMatchLevel` | `integer` | No | Minimal match level for 'Parameters'. 0 - ignore 'filter.Parameters' (default value), 1 - parameters count is the same, 2 - equals. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "filter": {
      "$ref": "#/$defs/com.IvanMurzak.ReflectorNet.Model.MethodRef"
    },
    "knownNamespace": {
      "type": "boolean"
    },
    "typeNameMatchLevel": {
      "type": "integer"
    },
    "methodNameMatchLevel": {
      "type": "integer"
    },
    "parametersMatchLevel": {
      "type": "integer"
    }
  },
  "$defs": {
    "System.Collections.Generic.List\u003Ccom.IvanMurzak.ReflectorNet.Model.MethodRef\u002BParameter\u003E": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/com.IvanMurzak.ReflectorNet.Model.MethodRef\u002BParameter",
        "description": "Parameter of a method. Contains type and name of the parameter."
      }
    },
    "com.IvanMurzak.ReflectorNet.Model.MethodRef\u002BParameter": {
      "type": "object",
      "properties": {
        "typeName": {
          "type": "string",
          "description": "Type of the parameter including namespace. Sample: \u0027System.String\u0027, \u0027System.Int32\u0027, \u0027UnityEngine.GameObject\u0027, etc."
        },
        "name": {
          "type": "string",
          "description": "Name of the parameter. It may be empty if the name is unknown."
        }
      },
      "description": "Parameter of a method. Contains type and name of the parameter."
    },
    "com.IvanMurzak.ReflectorNet.Model.MethodRef": {
      "type": "object",
      "properties": {
        "namespace": {
          "type": "string",
          "description": "Namespace of the class. It may be empty if the class is in the global namespace or the namespace is unknown."
        },
        "typeName": {
          "type": "string",
          "description": "Class name, or substring a class name. It may be empty if the class is unknown."
        },
        "methodName": {
          "type": "string",
          "description": "Method name, or substring of the method name. It may be empty if the method is unknown."
        },
        "inputParameters": {
          "$ref": "#/$defs/System.Collections.Generic.List\u003Ccom.IvanMurzak.ReflectorNet.Model.MethodRef\u002BParameter\u003E",
          "description": "List of input parameters. Can be null if the method has no parameters or the parameters are unknown."
        }
      },
      "description": "Method reference. Used to find method in codebase of the project."
    }
  },
  "required": [
    "filter"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

