<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/profiler.frame-breakdown.md. -->

---
name: profiler-frame-breakdown
description: "Top markers for one frame, the inverted+merged hierarchy (leaf functions aggregated by name), sorted by self time / total time / GC bytes. This is the drill-down step: get the worst / spike frame from profiler.summarize-counter (ArgMaxFrame) or profiler.find-spikes, then break it down here to see which markers dominated it. Pass frame < 0 to auto-pick the worst frame by CPU frame time — the result's IsLoadFrame flags whether that frame is a scene load rather than gameplay. Mirrors the Profiler window's CPU Usage timeline with Invert Call Hierarchy on."
---

# Profiler / Frame Breakdown

Top markers for one frame, the inverted+merged hierarchy (leaf functions aggregated by name), sorted by self time / total time / GC bytes. This is the drill-down step: get the worst / spike frame from profiler.summarize-counter (ArgMaxFrame) or profiler.find-spikes, then break it down here to see which markers dominated it. Pass frame < 0 to auto-pick the worst frame by CPU frame time — the result's IsLoadFrame flags whether that frame is a scene load rather than gameplay. Mirrors the Profiler window's CPU Usage timeline with Invert Call Hierarchy on.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "profiler.frame-breakdown", "args": {
  "frame": 0,
  "topN": 0,
  "threadIndex": 0,
  "sortBy": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "profiler.frame-breakdown" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `frame` | `integer` | No | Frame to break down. Negative picks the worst frame by CPU frame time. |
| `topN` | `integer` | No | Number of top markers to return. |
| `threadIndex` | `integer` | No | Thread index to read (0 = main thread). From profiler.describe's thread list. |
| `sortBy` | `string` | No | Sort column: "self" (self time) | "total" (total time) | "gc" (GC.Alloc bytes). |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "frame": {
      "type": "integer"
    },
    "topN": {
      "type": "integer"
    },
    "threadIndex": {
      "type": "integer"
    },
    "sortBy": {
      "type": "string"
    }
  }
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

## Notes

`frame < 0` (the default) selects the **worst** frame — the true max CPU
frame-time frame (the result's `IsLoadFrame` flags whether that frame is a scene
load rather than gameplay). Markers come from an inverted, same-name-merged hierarchy, so each row is a leaf function
aggregated across the frame rather than a single call-stack node; cross-check a
row against the Profiler window's inverted hierarchy for that frame. `sortBy` is
one of `"self"`, `"total"`, or `"gc"` — any other value fails loud.

