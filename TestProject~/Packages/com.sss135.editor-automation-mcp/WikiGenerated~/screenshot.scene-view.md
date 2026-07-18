<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/screenshot.scene-view.md. -->

---
name: screenshot-scene-view
description: Captures a screenshot from the Unity Editor Scene View and returns it as an image. Returns the image directly for visual inspection by the LLM.
---

# Screenshot / Scene View

Captures a screenshot from the Unity Editor Scene View and returns it as an image. Returns the image directly for visual inspection by the LLM.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "screenshot.scene-view", "args": {
  "width": 0,
  "height": 0
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "screenshot.scene-view" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `width` | `integer` | No | Width of the screenshot in pixels. |
| `height` | `integer` | No | Height of the screenshot in pixels. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "width": {
      "type": "integer"
    },
    "height": {
      "type": "integer"
    }
  }
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

