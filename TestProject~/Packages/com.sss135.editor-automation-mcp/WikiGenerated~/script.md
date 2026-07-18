<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/script.md. -->

# `script`

C# file CRUD on disk plus Roslyn-compiled in-Editor execution: read a `.cs`
file (`script.read`), write or overwrite one (`script.update-or-create`),
delete one (`script.delete`), and compile + invoke arbitrary C# inside the
live Editor (`script.execute`).

Reading and writing files is the easy half. The hard half is the **recompile
chain**: changes on disk are not visible to subsequent RPCs until Unity has
re-imported the file and rebuilt the affected assembly.

## The recompile chain

1. Write every `.cs` file that belongs to the logical change.
2. Call [`compile.request`](compile.md). It returns a job ticket before the
   refresh starts, so the result survives script compilation and domain reload.
3. Poll `system.job-status` until the ticket is terminal. Only then is the new
   assembly live; only then will `script.execute`, component construction, or
   anything reflecting on the new types see them.

Anything depending on the new code, issued before that probe goes `false`,
races the reload and returns stale-type errors.

## Cross-cluster overlap

- **Disk vs scene** — `script.update-or-create` writes the file but does not
  attach the resulting component to anything. Use `gameobject.component-add`
  (via `call("gameobject")`) once compilation settles.
- **`.cs.meta` files** — Unity owns those; the delete tool removes both the
  `.cs` and its sibling `.meta` as a unit. Do not edit `.meta` through this
  namespace.

## See also

- [`compile`](compile.md) — reload-safe refresh plus compiler diagnostics.
- [`assets.refresh`](assets.md) — general asset refresh when compile
  diagnostics are not needed.
- [`editor`](editor.md) — `IsCompiling` / `IsUpdating`
  readback half.
- [`workflows`](workflows.md) — "Reload after writing C#" walks it end-to-end.

To read a method's wiki, call `tools/call` with `arguments = { method: "<dotted>" }` (omit `args`). Include `args` (even `{}`) to execute.

## Methods

- `script.delete` — Delete the script file(s). Does AssetDatabase.Refresh() and waits for Unity compilation to complete before reporting results. Use 'script-read' tool to read existing script files first.
- `script.execute` — Compiles and executes C# code dynamically using Roslyn. Inline-code calls are persisted to '{projectRoot}/Library/McpScripts/<fileName>.cs' (outside Unity's compiled paths; survives Editor restart) un…
- `script.read` — Reads the content of a script file and returns it as a string. Use 'script-update-or-create' tool to update or create script files.
- `script.update-or-create` — Updates or creates script file with the provided C# code. Does AssetDatabase.Refresh() at the end. Provides compilation error details if the code has syntax errors. Use 'script-read' tool to read exis…

