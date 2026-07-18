# compile

Request a normal Unity compile check after files have changed on disk. This
namespace is for the "focus Unity and let it import/recompile" workflow: it
wakes the Editor, runs a normal `AssetDatabase.Refresh()`, and reports whether
that refresh observed compilation plus any compiler warnings or errors.

Use `compile.request` when an agent edited C# files outside Unity and you need
a single durable result instead of manually chaining `assets.refresh`,
`editor.application-get-state`, and `console.get-logs`.

## Reload-safe contract

`compile.request` always returns a job ticket before the refresh starts. That
ordering is intentional: a refresh can trigger script compilation and domain
reload, and during that window the in-process MCP gateway may stop responding.
The ticket is persisted before refresh so the final result can be reconstructed
through `system.job-status` even if the in-memory registry was cleared.

The public call has no knobs. It does not force a rebuild, clear the build
cache, or expose wake/focus as an option. Waking the Editor is an internal
implementation detail so the operation behaves like a developer focusing the
Unity window.

### compile.request

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
