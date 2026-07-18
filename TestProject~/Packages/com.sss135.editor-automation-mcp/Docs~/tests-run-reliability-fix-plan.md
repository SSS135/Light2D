# MCP tests.run Reliability Fix Plan

## Scope

This plan targets the vendored Editor Automation package in this workspace:
`Packages/com.sss135.editor-automation-mcp`. No authoritative external Editor Automation source
checkout was configured during investigation, so line references below point at
the vendored package and the Unity Test Framework package cache.

The observed failure was a `tests.run` PlayMode job that stayed `running` while
Unity was idle. This is an MCP test-run lifecycle bug, not a ColorWars gameplay
bug.

## Verified Root Cause

`tests.run` stores a job ticket in `TestResultCollector.TestCallRequestID`,
starts `TestRunnerApi.Execute`, and relies on `TestResultCollector.RunFinished`
to call `JobRegistry.Complete`:

- `Editor/Scripts/API/Tool/Tests.Run.cs:124`
- `Editor/Scripts/API/Tool/Tests.Run.cs:183`
- `Editor/Scripts/API/Tool/Tests/TestResultCollector.cs:116`
- `Editor/Scripts/API/Tool/Tests/TestResultCollector.cs:171`

The Unity Test Framework abort path used in this failure does not call
`ICallbacks.RunFinished`. It calls `CallbacksDelegator.RunFailed`, which only
notifies callbacks implementing `IErrorCallbacks`:

- `Library/PackageCache/com.unity.test-framework@76560ee600cb/UnityEditor.TestRunner/TestRun/Tasks/Scene/SaveModifiedSceneTask.cs:13`
- `Library/PackageCache/com.unity.test-framework@76560ee600cb/UnityEditor.TestRunner/TestRun/TestJobRunner.cs:213`
- `Library/PackageCache/com.unity.test-framework@76560ee600cb/UnityEditor.TestRunner/Api/CallbacksDelegator.cs:65`
- `Library/PackageCache/com.unity.test-framework@76560ee600cb/UnityEditor.TestRunner/Api/IErrorCallbacks.cs:8`

MCP currently registers `TestResultCollector : ICallbacks`, not
`IErrorCallbacks`, so Unity reports the test-run failure but MCP never
terminalizes the job:

- `Editor/Scripts/API/Tool/Tests/TestResultCollector.cs:29`

The specific Unity abort was triggered while the PlayMode runner tried to save
scenes during play mode:

- `InvalidOperationException: This cannot be used during play mode`
- `An unexpected error happened while running tests`
- `Too many instant steps in test execution mode: Error. Current task ExitPlayModeTask`

Afterward, Unity was idle and responsive, but MCP still had only a `started`
job-log entry until the stale ticket was manually completed through
`Tool_Tests.Cancel(...)`.

## Secondary Issue: tests.cancel RPC Path

`tests.cancel` is marked `MainThreadRequired = false`:

- `Editor/Scripts/API/Tool/Tests.Cancel.cs:25`

The RPC dispatcher can therefore invoke it off the Unity main thread, but the
method reads PlayerPrefs-backed state before its explicit main-thread hop:

- `Editor/Scripts/API/Tool/Tests.Cancel.cs:57`

Direct `Tool_Tests.Cancel(ticket)` from `script.execute` ran on the main thread
and succeeded. RPC `tests.cancel` returned a generic
`TargetInvocationException`, consistent with an exception thrown inside the
reflected worker-thread invocation and then flattened by `RunTool`:

- `Editor/Scripts/Http/RpcDispatcher.cs:441`
- `Editor/Scripts/Framework/Runtime/RunTool.cs:163`

## Implementation Plan

### 1. Complete jobs on Unity Test Framework run failures

Update `TestResultCollector` to implement `IErrorCallbacks`.

Add `OnError(string message)` that:

1. Reads the current `TestCallRequestID`.
2. Unsubscribes from `Application.logMessageReceivedThreaded`.
3. Clears `_runTicketId`, `TestCallRequestID`, and `RunGuidRegistry.RunGuid`.
4. Completes the ticket with `McpResponse.Error(...)`.

Extract shared cleanup from `RunFinished` so success, failure, and cancel paths
cannot diverge.

Expected result: Unity `RunFailed(...)` produces an MCP `errored` ticket rather
than a forever-`running` ticket.

### 2. Add PlayMode handling before running tests

Extend the existing preflight in `Tests.Run.cs` before `JobHelpers.StartJob`.
It already checks dirty scenes; add explicit editor/test-run state handling:

1. Add a public parameter such as `exitPlayModeFirst`, defaulting to `true` for
   PlayMode requests.
2. When `testMode == PlayMode` and `EditorApplication.isPlaying` is true, exit
   Play Mode before starting the test run if `exitPlayModeFirst` is true.
3. Wait for `EditorApplication.isPlayingOrWillChangePlaymode` to settle before
   refreshing assets or calling `TestRunnerApi.Execute`.
4. If `exitPlayModeFirst` is false, return a clear blocked response.
5. Reject or clearly defer when `EditorApplication.isCompiling` is already true
   before MCP triggers its own refresh.
6. Reject if a Unity Test Framework run is already active.

`TestRunnerApi.IsRunActive()` is not public in this installed API surface, so
wrap active-run detection behind a helper that uses reflection when available
and degrades to conservative local state checks when not.

Expected result: MCP either exits stale Play Mode and starts from a clean Test
Runner state, or reports an explicit blocked response when the caller opts out.

### 3. Normalize stale test-run state

Add a small helper used by both `tests.run` preflight and `tests.cancel`:

`NormalizeStaleTestRunState()`

It should inspect:

- `TestResultCollector.TestCallRequestID.Value`
- `RunGuidRegistry.RunGuid.Value`
- `JobRegistry.Find(ticketId)`
- active-run detection from step 2
- editor play/compile transition state

If a ticket/guid exists but Unity has no active run and is not entering/exiting
play mode, complete the old ticket with a structured stale-run error or
cancelled response, then clear both persisted values.

Default terminal status recommendation:

- Use `Errored` for Unity test-run aborts reported through `OnError`.
- Use `Cancelled` only when the caller explicitly invokes `tests.cancel`.
- Use `Errored` for automatic stale cleanup before a new run, with
  `reason: "stale_test_run_state"` in structured data.

Expected result: stale PlayerPrefs state cannot poison a later run or cancel.

### 4. Make tests.cancel main-thread safe

Change the tool declaration so `tests.cancel` runs on the Unity main thread, or
move all Unity/PlayerPrefs access into a single main-thread lambda.

Preferred implementation:

1. Remove `MainThreadRequired = false` or set `MainThreadRequired = true`.
2. Read `RunGuidRegistry.RunGuid.Value` on the main thread.
3. Call `TestRunnerApi.CancelTestRun(guid)` on the main thread.
4. Clear stale guid/ticket state if cancel returns false.
5. Return structured success for `no_active_run` / `cancel_failed_no_active_run`
   rather than letting reflection surface `TargetInvocationException`.

Expected result: RPC `tests.cancel` and direct `Tool_Tests.Cancel(...)` behave
the same.

### 5. Tighten diagnostics

Improve returned errors enough that agents do not need to inspect Editor.log for
basic state mistakes.

Add messages for:

- "Exiting Play Mode before starting PlayMode tests."
- "Cannot run PlayMode tests while the Editor is in Play Mode and
  exitPlayModeFirst is false."
- "Cannot run tests while the Editor is entering or exiting Play Mode."
- "Cannot run tests while another Unity test run is active."
- "Previous test run aborted without RunFinished; stale ticket was completed."

Also update `Docs~/WikiSource/tests.md` to document these preconditions and the fact
that PlayMode runs may auto-exit an already-playing Editor unless the caller
opts out.

### 6. Make PlayMode runs async-only by default

For `testMode == PlayMode`, return the job ticket immediately even when
`waitSeconds > 0`.

Include structured fields when a positive wait was requested:

- `async_forced: true`
- `requested_wait_seconds`
- hint: PlayMode tests can trigger domain reload / gateway restart; poll
  `system.job-status` or `system.job-wait` after receiving the ticket.

Expected result: callers always receive a ticket before PlayMode test execution
crosses a domain reload boundary.

### 7. Add package regression tests

The package currently has an empty `Tests` folder. Seed editor tests for the
test-run lifecycle code.

Suggested layout:

- `Tests/Editor/com.SSS135.EditorAutomation.Editor.Tests.asmdef`
- `Tests/Editor/TestResultCollectorTests.cs`
- `Tests/Editor/TestsCancelTests.cs`
- `Tests/Editor/TestsRunPreflightTests.cs`
- `Tests/Editor/JobStatusJsonlFallbackTests.cs`

Regression coverage:

1. `TestResultCollector.OnError` completes the active ticket as `errored` and
   clears `TestCallRequestID` / `RunGuidRegistry.RunGuid`.
2. `RunFinished` still completes success/failure result payloads and clears the
   same state.
3. `tests.run` PlayMode handling exits Play Mode when `exitPlayModeFirst` is
   true, and blocks without allocating a ticket when it is false.
4. `tests.run` preflight rejects an already-active Unity test run.
5. `tests.cancel` with a stale guid returns structured success and completes the
   ticket instead of throwing.
6. RPC invocation of `tests.cancel` through the `RunTool` path does not flatten
   a stale-run outcome into `TargetInvocationException`.
7. `system.job-status` reconstructs `error` and `cancelled` terminal events
   from `jobs.jsonl` after an in-memory ticket miss.
8. `system.job-status` returns `status: "unknown_ticket"` with
   `state_detail: "lost_after_reload"` when JSONL contains `started` but no
   terminal event for a missing in-memory ticket.

For preflight tests, extract editor state reads behind a small internal helper
or delegate set so tests do not have to actually put the Editor into Play Mode.

## Verification Plan

1. Run the new MCP editor tests.
2. Start Unity in a normal Edit Mode state and run a focused `tests.run`
   EditMode test through the HTTP gateway.
3. Put Unity into Play Mode, call PlayMode `tests.run`, and verify it exits
   Play Mode, returns a ticket immediately, and starts the test run.
4. Repeat with `exitPlayModeFirst = false` and verify it returns an immediate
   blocked response with no new running ticket.
5. Simulate or trigger a Unity Test Framework `RunFailed` path and verify
   `system.job-status` reports `errored`.
6. Seed stale `TestCallRequestID` / `RunGuidRegistry.RunGuid`, call RPC
   `tests.cancel`, and verify it returns structured success and terminalizes
   the ticket.
7. Re-run the original failed workflow and confirm no ticket remains
   `running` after Unity is idle.

## Non-Goals

- Do not change ColorWars `PlayFromAppLoader` as part of the MCP fix. It made
  the session's abort noisier, but MCP should still terminalize Unity Test
  Framework aborts from any project code.
- Do not use PlayMode for pure ECS regression tests unless MonoBehaviour,
  scene, coroutine, or physics-play-loop behavior is required. The targeting
  regression that exposed this should move to EditMode or use a direct ECS
  harness.

## Unresolved Questions

1. What is the authoritative Editor Automation source checkout for this machine?
2. Should the auto-exit parameter be named `exitPlayModeFirst`,
   `autoExitPlayMode`, or something else?
3. Should automatic stale-run cleanup report `Errored` or `Cancelled`? This
   plan recommends `Errored` unless the user explicitly requested cancel.
4. Should `tests.run` ever defer behind an already-active compile, or should it
   always fail fast and ask the caller to retry after compilation clears?
5. Is reflection-based active-run detection acceptable, or should the plugin
   avoid Unity Test Framework internals and rely only on its own stored guid and
   editor state?
