<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/script.update-or-create.md. -->

---
name: script-update-or-create
description: Updates or creates script file with the provided C# code. Does AssetDatabase.Refresh() at the end. Provides compilation error details if the code has syntax errors. Use 'script-read' tool to read existing script files first.
---

# Script / Update or Create

Updates or creates script file with the provided C# code. Does AssetDatabase.Refresh() at the end. Provides compilation error details if the code has syntax errors. Use 'script-read' tool to read existing script files first.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "script.update-or-create", "args": {
  "filePath": "string_value",
  "content": "string_value",
  "requestId": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "script.update-or-create" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `filePath` | `string` | Yes | The path to the file. Sample: "Assets/Scripts/MyScript.cs". |
| `content` | `string` | Yes | C# code - content of the file. |
| `requestId` | `string` | No |  |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "filePath": {
      "type": "string"
    },
    "content": {
      "type": "string"
    },
    "requestId": {
      "type": "string"
    }
  },
  "required": [
    "filePath",
    "content"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

## Notes

Writes the file and runs `AssetDatabase.Refresh()` before returning. For a
durable compile result, especially after several file writes, follow with
[`compile.request`](compile.md) and poll the returned ticket.

When a single logical change spans several files, issue every
`script.update-or-create` first, then a single
[`compile.request`](compile.md), then the ticket poll. Refreshing between
each write multiplies reload count and gateway-busy time.

