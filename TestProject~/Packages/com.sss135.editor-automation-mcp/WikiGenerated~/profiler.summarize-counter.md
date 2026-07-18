<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/profiler.summarize-counter.md. -->

---
name: profiler-summarize-counter
description: "Summary stats (min/max/mean/p95/p99 + argmax/argmin frames) for one profiler counter over a frame window. Report p95/p99, not just the mean, so a few spike frames don't mask the steady state. The returned ArgMaxFrame is the drill handle: feed it to profiler.frame-breakdown to see which markers dominated the worst frame. Call profiler.describe first for the counter list and frame range. Operates on per-frame counters; an unknown name or one with no per-frame data (e.g. a thread name like \"Main Thread\") throws. Use \"CPU Total Frame Time\" (ns) for CPU frame time."
---

# Profiler / Summarize Counter

Summary stats (min/max/mean/p95/p99 + argmax/argmin frames) for one profiler counter over a frame window. Report p95/p99, not just the mean, so a few spike frames don't mask the steady state. The returned ArgMaxFrame is the drill handle: feed it to profiler.frame-breakdown to see which markers dominated the worst frame. Call profiler.describe first for the counter list and frame range. Operates on per-frame counters; an unknown name or one with no per-frame data (e.g. a thread name like "Main Thread") throws. Use "CPU Total Frame Time" (ns) for CPU frame time.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "profiler.summarize-counter", "args": {
  "counter": "string_value",
  "from": 0,
  "to": 0
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "profiler.summarize-counter" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `counter` | `string` | Yes | Counter name from profiler.describe, e.g. "CPU Total Frame Time" or "GC Allocated In Frame". |
| `from` | `integer` | No | Window start frame (inclusive). 0 / negative selects the first available frame. |
| `to` | `integer` | No | Window end frame (inclusive). 0 / negative selects the last available frame. |

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
(e.g. a thread name like "Main Thread") throws with guidance. Use
`"CPU Total Frame Time"` (ns) to summarize CPU frame time — `ArgMaxFrame` is the
drill handle for `profiler.frame-breakdown`.

