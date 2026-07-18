# script

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

### script.update-or-create

Writes the file and runs `AssetDatabase.Refresh()` before returning. For a
durable compile result, especially after several file writes, follow with
[`compile.request`](compile.md) and poll the returned ticket.

When a single logical change spans several files, issue every
`script.update-or-create` first, then a single
[`compile.request`](compile.md), then the ticket poll. Refreshing between
each write multiplies reload count and gateway-busy time.

### script.execute

Compiles and runs arbitrary C# inside the Editor process via Roslyn. Two
modes: **full code** (`isMethodBody: false`, default) requires a complete
compilation unit with `using` directives and a static method named by
`methodName` — no top-level statements. **Body-only** (`isMethodBody: true`)
takes just the method body; the tool wraps it in standard usings (`System`,
`UnityEngine`, `EA`, `UnityEditor`, the runtime extensions namespace),
generates a class shell, and emits the method header from `methodName` and
declared `parameters`. Helper classes can be declared inline in body-only
mode; for non-trivial helpers prefer full code mode.

Inline-code calls require a `fileName` and a `description`. The compiled source
is persisted to `{projectRoot}/Library/McpScripts/<fileName>.cs` (a `.cs`
extension is appended if omitted, and a same-named file is overwritten), with
the `description` written as a `//` comment header at the top. `Library/`
survives Editor restarts, so named scripts accumulate as a reusable,
self-documenting library. The response always returns `ArtifactPath` pointing at
that file, so a compile-failed or to-be-tweaked script can be edited in place
and re-run via `filePath` — which is mutually exclusive with `csharpCode`,
`fileName`, and `description` (a saved file already carries its own header and
is compiled verbatim).

The response is a `ToolRunEnvelope`: `Status` (`"executed"` / `"compile_failed"` /
`"runtime_error"`), `ArtifactPath`, `DurationMs`, `LogCount`, `Result` (the
method's pre-serialized return value, when non-void), plus `Diagnostics` on a
compile failure or `RuntimeError` on a runtime throw. A compile error returns
`Status: "compile_failed"` with file-relative `Diagnostics` (and the
`ArtifactPath` to edit). Runtime errors now return **structured** — the invoked
method throwing comes back as `Status: "runtime_error"` with `RuntimeError`
(`"{Type}: {Message}\n{StackTrace}"`) rather than propagating as a tool error, so
a throwing script can be diagnosed and re-run via `filePath` like a compile
failure.

Unity object parameters are resolved by reference type.
`UnityEngine.GameObject` and `UnityEngine.Component` (or any subtype) accept
the standard `{ "instanceID": N }` / `{ "path": "…" }` / `{ "name": "…" }`
shapes and are resolved server-side before invocation. The POCO variants
(`EA.GameObjectRef`, `EA.ComponentRef`, `EA.ObjectRef`) pass through
unresolved; the method body calls `goRef.FindGameObject()` (or equivalent)
when it needs the live object. Use the POCO form when the method wants to
inspect the ref's metadata, or when resolution should fail inside your code
rather than at parameter binding.

**Safety note.** `script.execute` is arbitrary C# in the Editor with full
Editor and runtime API access — `AssetDatabase`, `EditorApplication`,
filesystem, network. Appropriate for a **local-only** gateway driven by a
developer in their own Editor; not appropriate for any deployment exposing
the gateway beyond `localhost`. Treat the port as a debug REPL.
