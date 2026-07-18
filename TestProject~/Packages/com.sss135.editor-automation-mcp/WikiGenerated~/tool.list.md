<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/tool.list.md. -->

---
name: tool-list
description: List all available MCP tools. Optionally filter by regex across tool names, descriptions, and arguments.
---

# Tool / List

List all available MCP tools. Optionally filter by regex across tool names, descriptions, and arguments.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "tool.list", "args": {
  "regexSearch": "string_value",
  "includeDescription": "string_value",
  "includeInputs": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "tool.list" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `regexSearch` | `string` | No | Regex pattern to filter tools. Matches against tool name, description, and argument names and descriptions. |
| `includeDescription` | `any` | No | Include tool descriptions in the result. Default: false |
| `includeInputs` | `any` | No | Include input arguments in the result. Default: None |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "regexSearch": {
      "type": "string"
    },
    "includeDescription": {
      "$ref": "#/$defs/System.Boolean"
    },
    "includeInputs": {
      "$ref": "#/$defs/com.SSS135.EditorAutomation.Editor.API.Tool_Tool\u002BInputRequest"
    }
  },
  "$defs": {
    "System.Boolean": {
      "type": "boolean"
    },
    "com.SSS135.EditorAutomation.Editor.API.Tool_Tool\u002BInputRequest": {
      "type": "string",
      "enum": [
        "None",
        "Inputs",
        "InputsWithDescription"
      ],
      "description": "Specifies what to include for tool input arguments."
    }
  }
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

## Notes

`includeDescription` and `includeInputs` both default to off — the bare call
returns just tool names. Turn them on only when you actually need the wider
payload; for most agent flows the name list plus a targeted `call("<method>")`
to fetch the page is cheaper than dumping every description inline.

`regexSearch` matches against the tool name, the tool description, *and* each
argument's name and description, so a search like `regexSearch: "prefab"` will
also surface methods that merely accept a `prefabPath` argument. Anchor the
pattern (`^assets\.prefab-`) when you want a strict namespace filter.

