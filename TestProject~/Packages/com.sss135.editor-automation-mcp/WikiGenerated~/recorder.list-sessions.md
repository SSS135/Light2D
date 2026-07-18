<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/recorder.list-sessions.md. -->

---
name: recorder-list-sessions
description: "List discoverable gameplay-recorder sessions: every recording file on disk plus the live in-memory session when one is recording. Each entry gives the id, path (or \"live\"), label, and frame range — use one as the 'session' argument of the other recorder tools. Pure file IO, so it stays callable while the Editor is busy recompiling."
---

# Recorder / List Sessions

List discoverable gameplay-recorder sessions: every recording file on disk plus the live in-memory session when one is recording. Each entry gives the id, path (or "live"), label, and frame range — use one as the 'session' argument of the other recorder tools. Pure file IO, so it stays callable while the Editor is busy recompiling.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "recorder.list-sessions", "args": {} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "recorder.list-sessions" }
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

