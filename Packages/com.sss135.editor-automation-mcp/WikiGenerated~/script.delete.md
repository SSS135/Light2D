<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/script.delete.md. -->

---
name: script-delete
description: Delete the script file(s). Does AssetDatabase.Refresh() and waits for Unity compilation to complete before reporting results. Use 'script-read' tool to read existing script files first.
---

# Script / Delete

Delete the script file(s). Does AssetDatabase.Refresh() and waits for Unity compilation to complete before reporting results. Use 'script-read' tool to read existing script files first.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "script.delete", "args": {
  "files": "string_value",
  "requestId": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "script.delete" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `files` | `any` | Yes | File paths to the files. Sample: "Assets/Scripts/MyScript.cs". |
| `requestId` | `string` | No |  |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "files": {
      "$ref": "#/$defs/System.String[]"
    },
    "requestId": {
      "type": "string"
    }
  },
  "$defs": {
    "System.String[]": {
      "type": "array",
      "items": {
        "type": "string"
      }
    }
  },
  "required": [
    "files"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

