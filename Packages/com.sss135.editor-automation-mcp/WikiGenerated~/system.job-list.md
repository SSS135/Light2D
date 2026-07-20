<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/system.job-list.md. -->

---
name: system-job-list
description: List every job ticket the gateway currently tracks (running plus completed-within-TTL). Returns slim entries — ticket_id, method, status, started_at. Use 'system-job-status' on a specific ticket id for the full result body.
---

# System / Job / List

List every job ticket the gateway currently tracks (running plus completed-within-TTL). Returns slim entries — ticket_id, method, status, started_at. Use 'system-job-status' on a specific ticket id for the full result body.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "system.job-list", "args": {} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "system.job-list" }
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

