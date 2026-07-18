<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/editor.application-get-state.md. -->

---
name: editor-application-get-state
description: "Returns available information about 'UnityEditor.EditorApplication'. Use it to get information about the current state of the Unity Editor application. Such as: playmode, paused state, compilation state, etc."
---

# Editor / Application / Get State

Returns available information about 'UnityEditor.EditorApplication'. Use it to get information about the current state of the Unity Editor application. Such as: playmode, paused state, compilation state, etc.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "editor.application-get-state", "args": {
  "nothing": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "editor.application-get-state" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `nothing` | `string` | No |  |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "nothing": {
      "type": "string"
    }
  }
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

