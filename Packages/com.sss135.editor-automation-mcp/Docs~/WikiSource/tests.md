# tests

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

### tests.run

Default mode is `EditMode`, which skips the play-mode enter/exit cycle and is
the right choice for iterative red-green loops. Reach for `PlayMode` only
when the assertion depends on `MonoBehaviour` lifecycle events, coroutines,
or physics stepping — the save-precondition above is what bites first when
switching to `PlayMode`.

Filters compose left-to-right and each one narrows the previous: pass
`testAssembly` to scope to one `.asmdef`, then `testNamespace` to one
namespace inside it, then `testClass` for one fixture, then `testMethod`
(fully qualified, e.g. `MyNs.MyFixture.MyTest`) for a single test.
`testMethod` is the strongest filter and overrides anything broader.

Output knobs default to a slim shape: only failing tests are returned and
no logs or stack traces are attached. Flip them in this order when a
failure needs investigation:

1. `includeMessages: true` (default already on) — message of the failing
   assertion.
2. `includeStacktrace: true` — full failing call stack.
3. `includeLogs: true` plus `logType` (default `Warning`) — captures
   `Debug.LogError` / `Debug.LogException` emitted during the run.
4. `includeLogsStacktrace: true` — last resort; expensive payload.

`includePassingTests: true` only when you need a baseline of what ran —
the default false keeps the payload bounded to failures.
