<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/script.execute.md. -->

---
name: script-execute
description: "Compiles and executes C# code dynamically using Roslyn. Inline-code calls are persisted to '{projectRoot}/Library/McpScripts/<fileName>.cs' (outside Unity's compiled paths; survives Editor restart) under the caller-supplied `fileName`, overwriting any existing file with that name. The response is a structured `ToolRunEnvelope` whose `ArtifactPath` points at that file so callers can edit-in-place and re-invoke via the `filePath` parameter instead of resubmitting the full source on iteration. Supports two input modes: inline code (`csharpCode`, full or body-only) OR a previously saved file (`filePath`). Exactly one of those must be provided. `fileName` and `description` are required with `csharpCode` and prohibited with `filePath`. Both compile failures and runtime errors are returned structured rather than thrown: `Status` is \"executed\" | \"compile_failed\" | \"runtime_error\", with `Diagnostics` set on compile failure and `RuntimeError` (\"{Type}: {Message}\n{StackTrace}\") set on a runtime error; `ArtifactPath`…"
---

# Script / Execute

Compiles and executes C# code dynamically using Roslyn. Inline-code calls are persisted to '{projectRoot}/Library/McpScripts/<fileName>.cs' (outside Unity's compiled paths; survives Editor restart) under the caller-supplied `fileName`, overwriting any existing file with that name. The response is a structured `ToolRunEnvelope` whose `ArtifactPath` points at that file so callers can edit-in-place and re-invoke via the `filePath` parameter instead of resubmitting the full source on iteration. Supports two input modes: inline code (`csharpCode`, full or body-only) OR a previously saved file (`filePath`). Exactly one of those must be provided. `fileName` and `description` are required with `csharpCode` and prohibited with `filePath`. Both compile failures and runtime errors are returned structured rather than thrown: `Status` is "executed" | "compile_failed" | "runtime_error", with `Diagnostics` set on compile failure and `RuntimeError` ("{Type}: {Message}\n{StackTrace}") set on a runtime error; `ArtifactPath` is always populated so the file can be edited and re-run. Unity objects (GameObject, Component, etc.) can be passed as parameters using their Ref types (GameObjectRef, ComponentRef, etc.) or directly by type.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "script.execute", "args": {
  "csharpCode": "string_value",
  "fileName": "string_value",
  "description": "string_value",
  "className": "string_value",
  "methodName": "string_value",
  "parameters": "string_value",
  "isMethodBody": false,
  "filePath": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "script.execute" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `csharpCode` | `string` | No | C# code to compile and execute. Mutually exclusive with `filePath` — supply exactly one. In full code mode (default, isMethodBody=false): must define a complete class with a static method. Example: 'using UnityEngine; public class Script { public static void Main() { Debug.Log("Hello"); } }'. Do NOT use top-level statements. In body-only mode (isMethodBody=true): provide only the method body statements. The tool auto-generates usings, class, and method header. Example body: 'go.SetActive(false);'. Leading `using ...;` directives at the top of the body are lifted automatically to file scope, so body-only callers can declare namespace aliases or extra imports without falling back to full code mode. Custom helper classes can still be defined inline in the body-only string after the main logic, but for complex additional class definitions use full code mode instead. |
| `fileName` | `string` | No | File name for the persisted script under 'Library/McpScripts/'. A '.cs' extension is appended if omitted ('Foo', 'Foo.cs' both save as 'Foo.cs'). If a file with this name already exists it is overwritten. Must be a bare file name — directory separators and other invalid characters are rejected. REQUIRED when 'csharpCode' is provided; must be omitted when 'filePath' is provided. |
| `description` | `string` | No | Short summary of what the script does, written as a '//' comment header at the top of the saved file. REQUIRED when 'csharpCode' is provided; must be omitted when 'filePath' is provided. |
| `className` | `string` | No | The name of the class containing the method to execute. In body-only mode this becomes the generated class name. |
| `methodName` | `string` | No | The name of the method to execute. Must be a static method. In body-only mode this becomes the generated method name. |
| `parameters` | `any` | No | Serialized parameters to pass to the method. Each entry must specify 'name' and 'typeName'. Supported parameter types include primitives, strings, and Unity object references: - 'UnityEngine.GameObject': resolves an actual GameObject from value '{"instanceID": N}', '{"name": "..."}', or '{"path": "..."}'. - 'UnityEngine.Component' (or any component subtype): resolves from '{"instanceID": N}'. - 'EA.GameObjectRef': passes a GameObjectRef POCO directly;   the method body calls goRef.FindGameObject() to resolve it. - 'EA.ComponentRef': passes a ComponentRef POCO. - 'EA.ObjectRef': passes a base ObjectRef POCO. If the method does not require parameters, leave this empty. |
| `isMethodBody` | `boolean` | No | When true, 'csharpCode' is treated as just the method body. The tool auto-generates standard using directives (System, UnityEngine, EA, com.SSS135.EditorAutomation.Runtime.Extensions, UnityEditor), the class definition, and the method signature (void return type). Parameters from the 'parameters' list are automatically added to the method signature using their typeName and name. When false (default), 'csharpCode' must be a complete C# compilation unit with class and method definitions. Ignored when `filePath` is provided (the file is already in final compiled form) — passing isMethodBody=true with filePath is rejected. |
| `filePath` | `string` | No | Absolute path to a previously persisted script file (typically the `ArtifactPath` returned by an earlier call). Mutually exclusive with `csharpCode`, `fileName`, and `description`. When supplied, the file content is compiled verbatim — no wrapping, no `using` lifting, no description header added (the file already carries its own). Use this to iterate on a script that previously compile-failed: read `ArtifactPath` from the failure response, edit the file in place to fix the diagnostic, then re-invoke with the same path. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "csharpCode": {
      "type": "string"
    },
    "fileName": {
      "type": "string"
    },
    "description": {
      "type": "string"
    },
    "className": {
      "type": "string"
    },
    "methodName": {
      "type": "string"
    },
    "parameters": {
      "$ref": "#/$defs/com.IvanMurzak.ReflectorNet.Model.SerializedMemberList"
    },
    "isMethodBody": {
      "type": "boolean"
    },
    "filePath": {
      "type": "string"
    }
  },
  "$defs": {
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
  }
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

## Notes

Compiles and runs arbitrary C# inside the Editor process via Roslyn. Two
modes: **full code** (`isMethodBody: false`, default) requires a complete
compilation unit with `using` directives and a static method named by
`methodName` — no top-level statements. **Body-only** (`isMethodBody: true`)
takes just the method body; the tool wraps it in standard usings (`System`,
`UnityEngine`, `EA`, `UnityEditor`, the runtime extensions namespace),
generates a class shell, and emits the method header from `methodName` and
declared `parameters`. Helper classes can be declared inline in body-only
mode; for non-trivial helpers prefer full code mode.

Inline-code calls require a `fileName` and a `description`. The compiled source
is persisted to `{projectRoot}/Library/McpScripts/<fileName>.cs` (a `.cs`
extension is appended if omitted, and a same-named file is overwritten), with
the `description` written as a `//` comment header at the top. `Library/`
survives Editor restarts, so named scripts accumulate as a reusable,
self-documenting library. The response always returns `ArtifactPath` pointing at
that file, so a compile-failed or to-be-tweaked script can be edited in place
and re-run via `filePath` — which is mutually exclusive with `csharpCode`,
`fileName`, and `description` (a saved file already carries its own header and
is compiled verbatim).

The response is a `ToolRunEnvelope`: `Status` (`"executed"` / `"compile_failed"` /
`"runtime_error"`), `ArtifactPath`, `DurationMs`, `LogCount`, `Result` (the
method's pre-serialized return value, when non-void), plus `Diagnostics` on a
compile failure or `RuntimeError` on a runtime throw. A compile error returns
`Status: "compile_failed"` with file-relative `Diagnostics` (and the
`ArtifactPath` to edit). Runtime errors now return **structured** — the invoked
method throwing comes back as `Status: "runtime_error"` with `RuntimeError`
(`"{Type}: {Message}\n{StackTrace}"`) rather than propagating as a tool error, so
a throwing script can be diagnosed and re-run via `filePath` like a compile
failure.

Unity object parameters are resolved by reference type.
`UnityEngine.GameObject` and `UnityEngine.Component` (or any subtype) accept
the standard `{ "instanceID": N }` / `{ "path": "…" }` / `{ "name": "…" }`
shapes and are resolved server-side before invocation. The POCO variants
(`EA.GameObjectRef`, `EA.ComponentRef`, `EA.ObjectRef`) pass through
unresolved; the method body calls `goRef.FindGameObject()` (or equivalent)
when it needs the live object. Use the POCO form when the method wants to
inspect the ref's metadata, or when resolution should fail inside your code
rather than at parameter binding.

**Safety note.** `script.execute` is arbitrary C# in the Editor with full
Editor and runtime API access — `AssetDatabase`, `EditorApplication`,
filesystem, network. Appropriate for a **local-only** gateway driven by a
developer in their own Editor; not appropriate for any deployment exposing
the gateway beyond `localhost`. Treat the port as a debug REPL.

