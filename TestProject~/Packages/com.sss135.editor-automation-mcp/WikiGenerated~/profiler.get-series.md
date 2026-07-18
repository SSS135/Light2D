<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/profiler.get-series.md. -->

---
name: profiler-get-series
description: "Chart one counter's per-frame values over a window, peak-preserving downsampled to a bounded point budget (the first, last, and per-bucket extrema survive, so spikes are never smoothed away). Run 'profiler-summarize-counter' first to find the interesting window and the argmax frame, then drill into a single frame with 'profiler-frame-breakdown'. Values are in the counter's native unit (time counters in nanoseconds, e.g. \"CPU Total Frame Time\"); see the 'unit' field. An unknown counter or one with no per-frame data throws. Reads whatever is currently loaded — a live recording or the last 'profiler-load-capture'."
---

# Profiler / Get Series

Chart one counter's per-frame values over a window, peak-preserving downsampled to a bounded point budget (the first, last, and per-bucket extrema survive, so spikes are never smoothed away). Run 'profiler-summarize-counter' first to find the interesting window and the argmax frame, then drill into a single frame with 'profiler-frame-breakdown'. Values are in the counter's native unit (time counters in nanoseconds, e.g. "CPU Total Frame Time"); see the 'unit' field. An unknown counter or one with no per-frame data throws. Reads whatever is currently loaded — a live recording or the last 'profiler-load-capture'.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "profiler.get-series", "args": {
  "counter": "string_value",
  "from": 0,
  "to": 0,
  "maxPoints": 0
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "profiler.get-series" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `counter` | `string` | Yes | Counter name from the profiler-describe list, e.g. "CPU Total Frame Time". |
| `from` | `integer` | No | Window start frame (inclusive). 0 / negative selects the first available frame. |
| `to` | `integer` | No | Window end frame (inclusive). 0 / negative selects the last available frame. |
| `maxPoints` | `integer` | No | Maximum number of points to return. 0 uses the default budget of 200. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "counter": {
      "type": "string"
    },
    "from": {
      "type": "integer"
    },
    "to": {
      "type": "integer"
    },
    "maxPoints": {
      "type": "integer"
    }
  },
  "required": [
    "counter"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

## Notes

Operates on a per-frame counter; an unknown name or one with no per-frame data
throws. Use `"CPU Total Frame Time"` (ns) for the CPU frame-time shape over a
window. Points are peak-preserving downsampled, so spikes survive.

