<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/compile.request.md. -->

---
name: compile-request
description: Starts a normal Unity refresh/import compile check. The call returns a job ticket before AssetDatabase.Refresh runs so the result survives compilation and domain reload. Poll system.job-status with the returned ticket id for compile/no-compile status and compiler diagnostics.
---

# Compile / Request

Starts a normal Unity refresh/import compile check. The call returns a job ticket before AssetDatabase.Refresh runs so the result survives compilation and domain reload. Poll system.job-status with the returned ticket id for compile/no-compile status and compiler diagnostics.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "compile.request", "args": {} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "compile.request" }
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

Starts a normal refresh/import pass and returns a `ticket_id`. Poll
`system.job-status` until the ticket is terminal.

Result fields:

- `compiled` / `compileObserved` - `true` when this request observed Unity's
  compilation pipeline.
- `success` - `true` only when the project is compile-clean after the refresh.
- `preExistingErrors` - `true` when no new compile was observed but Unity still
  reports script compilation errors after refresh.
- `statusReason` - machine-readable completion reason, such as
  `compilation_finished`, `no_compile_needed`, or
  `pre_existing_compile_errors_after_refresh`.
- `errorCount`, `warningCount`, `diagnostics` - compiler diagnostics captured
  from Unity's compilation pipeline where available.

Example:

```json
{"method":"compile.request","args":{}}
```

Then:

```json
{"method":"system.job-status","args":{"ticketId":"j_..."}}
```

