<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/assets.prefab-close.md. -->

---
name: assets-prefab-close
description: Close currently opened prefab. Use it when you are in prefab editing mode in Unity Editor. Use 'assets-prefab-open' tool to open a prefab first.
---

# Assets / Prefab / Close

Close currently opened prefab. Use it when you are in prefab editing mode in Unity Editor. Use 'assets-prefab-open' tool to open a prefab first.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "assets.prefab-close", "args": {
  "save": false
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "assets.prefab-close" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `save` | `boolean` | No | True to save prefab. False to discard changes. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "save": {
      "type": "boolean"
    }
  }
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

