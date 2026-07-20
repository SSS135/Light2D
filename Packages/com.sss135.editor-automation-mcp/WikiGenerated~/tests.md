<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/tests.md. -->

# `tests`

Run Unity tests (`EditMode` or `PlayMode`) through the gateway and read the
structured result without leaving the chat surface. Use this namespace as the
single entry point for "did my change break anything" verification; pair it
with [`console`](editor.md) for runtime log inspection and with
[`assets`](assets.md) / `script` for the write-then-recompile half of the
cycle.

## Preconditions

Two preconditions matter and both fail loudly when violated:

- **Every open scene must be saved.** `tests.run` throws an
  `InvalidOperationException` listing the dirty scene paths if any open
  scene has unsaved changes. PlayMode tests would otherwise discard the
  active scene's unsaved edits when entering play mode. Save with
  `scene.save` (per-scene path) before the call.
- **The Editor must be done compiling.** Poll
  `editor.application-get-state` and gate on `isCompiling: false` before
  invoking the test run. Tests started during a compile silently queue
  behind the domain reload and the gateway will look unresponsive in the
  meantime.

When a compile is pending because a tool just wrote `.cs`, run `assets.refresh`
first, then poll `editor.application-get-state` until `isCompiling` clears, then
call `tests.run`.

## See also

- [`workflows`](workflows.md) — the "Run / inspect Edit-Mode tests" row links
  this page from the task-oriented index.
- `script.update-or-create` — the usual upstream of a test run.
- `console.get-logs` — for log evidence outside the test result payload.

To read a method's wiki, call `tools/call` with `arguments = { method: "<dotted>" }` (omit `args`). Include `args` (even `{}`) to execute.

## Methods

- `tests.cancel` — Cancel an in-flight tests-run job. Reads the captured TestRunner GUID from RunGuidRegistry and calls TestRunnerApi.CancelTestRun. On a successful cancel the ticket is completed by TestResultCollector.…
- `tests.run` — Execute Unity tests and return detailed results. Supports filtering by test mode, assembly, namespace, class, and method. Recommended to use 'EditMode' for faster iteration during development. Precond…

