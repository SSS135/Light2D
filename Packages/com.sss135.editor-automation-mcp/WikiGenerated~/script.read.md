<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/script.read.md. -->

---
name: script-read
description: Reads the content of a script file and returns it as a string. Use 'script-update-or-create' tool to update or create script files.
---

# Script / Read

Reads the content of a script file and returns it as a string. Use 'script-update-or-create' tool to update or create script files.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "script.read", "args": {
  "filePath": "string_value",
  "lineFrom": 0,
  "lineTo": 0
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "script.read" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `filePath` | `string` | Yes | The path to the file. Sample: "Assets/Scripts/MyScript.cs". |
| `lineFrom` | `integer` | No | The line number to start reading from (1-based). |
| `lineTo` | `integer` | No | The line number to stop reading at (1-based, -1 for all lines). |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "filePath": {
      "type": "string"
    },
    "lineFrom": {
      "type": "integer"
    },
    "lineTo": {
      "type": "integer"
    }
  },
  "required": [
    "filePath"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

