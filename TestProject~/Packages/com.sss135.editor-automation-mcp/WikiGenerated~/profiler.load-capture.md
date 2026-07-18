<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/profiler.load-capture.md. -->

---
name: profiler-load-capture
description: "Load a saved .raw/.data Profiler capture into the global Profiler buffer. WARNING: this REPLACES the current in-memory data — the Profiler window and every other profiler.* read now reflect the loaded file until you re-record. This is the only stateful profiler.* tool; the read tools always operate on whatever buffer is current. Throws when the file does not exist. Returns the loaded capture's frame range."
---

# Profiler / Load Capture

Load a saved .raw/.data Profiler capture into the global Profiler buffer. WARNING: this REPLACES the current in-memory data — the Profiler window and every other profiler.* read now reflect the loaded file until you re-record. This is the only stateful profiler.* tool; the read tools always operate on whatever buffer is current. Throws when the file does not exist. Returns the loaded capture's frame range.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "profiler.load-capture", "args": {
  "path": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "profiler.load-capture" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `path` | `string` | Yes | Absolute path to a saved .raw/.data Profiler capture. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "path": {
      "type": "string"
    }
  },
  "required": [
    "path"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

