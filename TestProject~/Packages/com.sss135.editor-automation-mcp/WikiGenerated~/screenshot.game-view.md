<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/screenshot.game-view.md. -->

---
name: screenshot-game-view
description: Captures a screenshot from the Unity Editor Game View and returns it as an image. Reads the Game View's own render texture directly via the Unity Editor API. The image size matches the current Game View resolution. Returns the image directly for visual inspection by the LLM.
---

# Screenshot / Game View

Captures a screenshot from the Unity Editor Game View and returns it as an image. Reads the Game View's own render texture directly via the Unity Editor API. The image size matches the current Game View resolution. Returns the image directly for visual inspection by the LLM.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "screenshot.game-view", "args": {
  "nothing": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "screenshot.game-view" }
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

