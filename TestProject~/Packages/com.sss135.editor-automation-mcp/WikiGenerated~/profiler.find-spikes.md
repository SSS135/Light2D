<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/profiler.find-spikes.md. -->

---
name: profiler-find-spikes
description: "Find the frames where one profiler counter spikes. Pass an absolute 'threshold' (a frame counts as a spike when its value exceeds it) OR an n-'sigma' multiplier (spike when value > mean + sigma*stddev over the window). 'threshold' wins when both are set; both 0 defaults to 3-sigma. Results are ordered by descending value and capped — the worst frames survive, the count of any dropped ones is in the envelope. Feed a spike frame to profiler.frame-breakdown to see the cause. Operates on per-frame counters; an unknown name or one with no per-frame data throws. Use \"CPU Total Frame Time\" to find CPU frame-time spikes."
---

# Profiler / Find Spikes

Find the frames where one profiler counter spikes. Pass an absolute 'threshold' (a frame counts as a spike when its value exceeds it) OR an n-'sigma' multiplier (spike when value > mean + sigma*stddev over the window). 'threshold' wins when both are set; both 0 defaults to 3-sigma. Results are ordered by descending value and capped — the worst frames survive, the count of any dropped ones is in the envelope. Feed a spike frame to profiler.frame-breakdown to see the cause. Operates on per-frame counters; an unknown name or one with no per-frame data throws. Use "CPU Total Frame Time" to find CPU frame-time spikes.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "profiler.find-spikes", "args": {
  "counter": "string_value",
  "threshold": 0,
  "sigma": 0,
  "from": 0,
  "to": 0
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "profiler.find-spikes" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `counter` | `string` | Yes | Counter name from profiler.describe, e.g. "CPU Total Frame Time". |
| `threshold` | `number` | No | Absolute value threshold; a frame is a spike when its value exceeds it. 0 falls back to sigma. |
| `sigma` | `number` | No | Spike when value > mean + sigma*stddev. Used only when 'threshold' is 0; 0 here defaults to 3. |
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
    "threshold": {
      "type": "number"
    },
    "sigma": {
      "type": "number"
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

Pass an absolute `threshold` (counter values above it are spikes) **or** an
n-`sigma` multiplier (values above `mean + sigma·stddev`). With both left at 0 it
defaults to 3σ. Spikes come back ordered by descending value and capped — the
envelope's `Truncated`/`TotalCount` report any elision. Operates on a per-frame
counter — an unknown name or one with no per-frame data (e.g. a thread name)
throws. Use `"CPU Total Frame Time"` to find CPU frame-time spikes.

