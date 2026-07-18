<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/scene.list-opened.md. -->

---
name: scene-list-opened
description: Returns the list of currently opened scenes in Unity Editor. Use 'scene-get-data' tool to get detailed information about a specific scene.
---

# Scene / List Opened

Returns the list of currently opened scenes in Unity Editor. Use 'scene-get-data' tool to get detailed information about a specific scene.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "scene.list-opened", "args": {
  "nothing": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "scene.list-opened" }
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

