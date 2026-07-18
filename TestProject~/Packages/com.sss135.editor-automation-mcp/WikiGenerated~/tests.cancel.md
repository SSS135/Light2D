<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/tests.cancel.md. -->

---
name: tests-cancel
description: Cancel an in-flight tests-run job. Reads the captured TestRunner GUID from RunGuidRegistry and calls TestRunnerApi.CancelTestRun. On a successful cancel the ticket is completed by TestResultCollector.RunFinished; when CancelTestRun returns false (no active run) this tool completes the ticket directly with a Cancelled envelope so callers do not see a dangling ticket.
---

# Tests / Cancel

Cancel an in-flight tests-run job. Reads the captured TestRunner GUID from RunGuidRegistry and calls TestRunnerApi.CancelTestRun. On a successful cancel the ticket is completed by TestResultCollector.RunFinished; when CancelTestRun returns false (no active run) this tool completes the ticket directly with a Cancelled envelope so callers do not see a dangling ticket.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "tests.cancel", "args": {
  "ticketId": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "tests.cancel" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `ticketId` | `string` | Yes | Ticket id returned by tests-run. Used for response correlation; the actual cancel uses the captured TestRunner GUID. |

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

