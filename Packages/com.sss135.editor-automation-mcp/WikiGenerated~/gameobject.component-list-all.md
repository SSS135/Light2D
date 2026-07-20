<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/gameobject.component-list-all.md. -->

---
name: gameobject-component-list-all
description: List C# class names extended from UnityEngine.Component. Use this to find component type names for 'gameobject-component-add' tool. Results are paginated to avoid overwhelming responses.
---

# GameObject / Component / List All

List C# class names extended from UnityEngine.Component. Use this to find component type names for 'gameobject-component-add' tool. Results are paginated to avoid overwhelming responses.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "gameobject.component-list-all", "args": {
  "search": "string_value",
  "page": 0,
  "pageSize": 0
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "gameobject.component-list-all" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `search` | `string` | No | Substring for searching components. Could be empty. |
| `page` | `integer` | No | Page number (0-based). Default is 0. |
| `pageSize` | `integer` | No | Number of items per page. Default is 5. Max is 500. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "search": {
      "type": "string"
    },
    "page": {
      "type": "integer"
    },
    "pageSize": {
      "type": "integer"
    }
  }
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

