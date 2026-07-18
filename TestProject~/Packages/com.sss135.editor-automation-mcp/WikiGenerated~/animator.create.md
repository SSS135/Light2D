<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/animator.create.md. -->

---
name: animator-create
description: Create Unity's AnimatorController asset files. Creates folders recursively if they do not exist. Each path should start with 'Assets/' and end with '.controller'.
---

# Animator / Create

Create Unity's AnimatorController asset files. Creates folders recursively if they do not exist. Each path should start with 'Assets/' and end with '.controller'.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "animator.create", "args": {
  "sourcePaths": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "animator.create" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `sourcePaths` | `any` | Yes | The paths of the animator controller assets to create. Each path should start with 'Assets/' and end with '.controller'. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "sourcePaths": {
      "$ref": "#/$defs/System.String[]"
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
    "sourcePaths"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

