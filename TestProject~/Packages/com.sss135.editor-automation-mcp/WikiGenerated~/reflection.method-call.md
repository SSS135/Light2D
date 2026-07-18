<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/reflection.method-call.md. -->

---
name: reflection-method-call
description: Call C# method. Any method could be called, even private methods. It requires to receive proper method schema. Use 'reflection-method-find' to find available method before using it. Receives input parameters and returns result.
---

# Method C# / Call

Call C# method. Any method could be called, even private methods. It requires to receive proper method schema. Use 'reflection-method-find' to find available method before using it. Receives input parameters and returns result.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "reflection.method-call", "args": {
  "filter": "string_value",
  "knownNamespace": false,
  "typeNameMatchLevel": 0,
  "methodNameMatchLevel": 0,
  "parametersMatchLevel": 0,
  "targetObject": "string_value",
  "inputParameters": "string_value",
  "executeInMainThread": false
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "reflection.method-call" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `filter` | `any` | Yes | Method reference. Used to find method in codebase of the project. |
| `knownNamespace` | `boolean` | No | Set to true if 'Namespace' is known and full namespace name is specified in the 'filter.Namespace' property. Otherwise, set to false. |
| `typeNameMatchLevel` | `integer` | No | Minimal match level for 'typeName'. 0 - ignore 'filter.typeName', 1 - contains ignoring case (default value), 2 - contains case sensitive, 3 - starts with ignoring case, 4 - starts with case sensitive, 5 - equals ignoring case, 6 - equals case sensitive. |
| `methodNameMatchLevel` | `integer` | No | Minimal match level for 'MethodName'. 0 - ignore 'filter.MethodName', 1 - contains ignoring case (default value), 2 - contains case sensitive, 3 - starts with ignoring case, 4 - starts with case sensitive, 5 - equals ignoring case, 6 - equals case sensitive. |
| `parametersMatchLevel` | `integer` | No | Minimal match level for 'Parameters'. 0 - ignore 'filter.Parameters', 1 - parameters count is the same, 2 - equals (default value). |
| `targetObject` | `any` | No | Specify target object to call method on. Should be null if the method is static or if there is no specific target instance. New instance of the specified class will be created if the method is instance method and the targetObject is null. Required: type - full type name of the object to call method on, value - serialized object value (it will be deserialized to the specified type). |
| `inputParameters` | `any` | No | Method input parameters. Per each parameter specify: type - full type name of the object to call method on, name - parameter name, value - serialized object value (it will be deserialized to the specified type). |
| `executeInMainThread` | `boolean` | No | Set to true if the method should be executed in the main thread. Otherwise, set to false. |

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
    },
    "targetObject": {
      "$ref": "#/$defs/com.IvanMurzak.ReflectorNet.Model.SerializedMember"
    },
    "inputParameters": {
      "$ref": "#/$defs/com.IvanMurzak.ReflectorNet.Model.SerializedMemberList"
    },
    "executeInMainThread": {
      "type": "boolean"
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
    }
  },
  "required": [
    "filter"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

## Notes

The `typeNameMatchLevel` and `methodNameMatchLevel` integers share one
ladder: `0` ignores the filter field, `1` is case-insensitive `contains`
(default), `2` is case-sensitive `contains`, `3`/`4` are
case-insensitive/sensitive `startsWith`, and `5`/`6` are
case-insensitive/sensitive equality. `parametersMatchLevel` is a separate
ladder: `0` ignores `filter.inputParameters`, `1` requires matching
parameter count, `2` requires full equality. Default is `2` here but `0`
on `method-find`, so an unconstrained `method-find` typically returns more
overloads than the same filter would invoke through `method-call`.

Set `executeInMainThread: true` when the call touches a Unity API (anything
under `UnityEngine.*` / `UnityEditor.*`, scene mutation, `Application.*`).
Without it the invocation runs on the MCP worker thread and most Unity APIs
throw or return stale results. Pure CLR helpers (`System.*` IO, string,
math) can stay on the worker thread for lower latency.

`targetObject` distinguishes instance from static dispatch: leave it `null`
for static methods. For instance methods, supply a `SerializedMember`
envelope whose `typeName` + nested `fields`/`props` reconstruct the
receiver, or leave it `null` and the resolver constructs a fresh instance
via the declaring type's parameterless constructor — which silently fails
for types without a public default constructor. When in doubt, hydrate
`targetObject` explicitly.

