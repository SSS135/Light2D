<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/system.job-status.md. -->

---
name: system-job-status
description: Fetch the current status and (when finished) the final result of a long-running job ticket. Use this after a tool returns a Processing response carrying a ticket id. Returns NOT_FOUND-shaped error data when the ticket has aged out beyond the configured TTL.
---

# System / Job / Status

Fetch the current status and (when finished) the final result of a long-running job ticket. Use this after a tool returns a Processing response carrying a ticket id. Returns NOT_FOUND-shaped error data when the ticket has aged out beyond the configured TTL.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "system.job-status", "args": {
  "ticketId": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "system.job-status" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `ticketId` | `string` | Yes | Ticket id returned by the originating tool (format: 'j_<utc>_<8hex>'). |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "ticketId": {
      "type": "string"
    }
  },
  "required": [
    "ticketId"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

