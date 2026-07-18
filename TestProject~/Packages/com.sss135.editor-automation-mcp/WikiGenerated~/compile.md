<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/compile.md. -->

# `compile`

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

To read a method's wiki, call `tools/call` with `arguments = { method: "<dotted>" }` (omit `args`). Include `args` (even `{}`) to execute.

## Methods

- `compile.request` — Starts a normal Unity refresh/import compile check. The call returns a job ticket before AssetDatabase.Refresh runs so the result survives compilation and domain reload. Poll system.job-status with th…

