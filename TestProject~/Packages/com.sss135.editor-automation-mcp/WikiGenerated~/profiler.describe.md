<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/profiler.describe.md. -->

---
name: profiler-describe
description: "Orientation call before any other profiler.* tool. Reports whether the buffer is from a live recording or a loaded capture, the available frame range, the threads present, the counters you can summarize / chart / scan for spikes. Counter availability reflects the LIVE session — a capture loaded via profiler.load-capture may carry counters not in this list. Reports the empty state rather than throwing when nothing is recorded. The counter list is filtered to those readable per-frame (so the other tools never get handed a name that yields nothing). For CPU/GPU frame time use Unity's readable \"CPU Total Frame Time\" / \"CPU Main Thread Frame Time\" / \"GPU Frame Time\" counters (ns). Use the returned counter names and frame indices to drive profiler.summarize-counter, profiler.get-series, profiler.frame-breakdown, and profiler.find-spikes."
---

# Profiler / Describe

Orientation call before any other profiler.* tool. Reports whether the buffer is from a live recording or a loaded capture, the available frame range, the threads present, the counters you can summarize / chart / scan for spikes. Counter availability reflects the LIVE session — a capture loaded via profiler.load-capture may carry counters not in this list. Reports the empty state rather than throwing when nothing is recorded. The counter list is filtered to those readable per-frame (so the other tools never get handed a name that yields nothing). For CPU/GPU frame time use Unity's readable "CPU Total Frame Time" / "CPU Main Thread Frame Time" / "GPU Frame Time" counters (ns). Use the returned counter names and frame indices to drive profiler.summarize-counter, profiler.get-series, profiler.frame-breakdown, and profiler.find-spikes.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "profiler.describe", "args": {} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "profiler.describe" }
```

## Input

This tool takes no input parameters.

### Input JSON Schema

```json
{
  "type": "object"
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

## Notes

Call this first. It is the only read tool that does **not** throw on an empty
buffer — it reports the empty state instead. Returns a compact plain-text summary
(frame range, threads, and the readable counters grouped by category). Counter
availability reflects the live session, so a loaded capture may expose counters
with empty `Category`/`Unit` metadata. The list is filtered to counters actually
readable per-frame; for CPU/GPU frame time use Unity's `"CPU Total Frame Time"` /
`"CPU Main Thread Frame Time"` / `"GPU Frame Time"` counters (ns).

