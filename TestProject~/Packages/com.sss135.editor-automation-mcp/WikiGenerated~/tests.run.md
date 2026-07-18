<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/tests.run.md. -->

---
name: tests-run
description: "Execute Unity tests and return detailed results. Supports filtering by test mode, assembly, namespace, class, and method. Recommended to use 'EditMode' for faster iteration during development. Precondition: every open scene MUST be saved (no unsaved changes). If any open scene is dirty, this tool throws an InvalidOperationException listing the dirty scenes; save them and retry."
---

# Tests / Run

Execute Unity tests and return detailed results. Supports filtering by test mode, assembly, namespace, class, and method. Recommended to use 'EditMode' for faster iteration during development. Precondition: every open scene MUST be saved (no unsaved changes). If any open scene is dirty, this tool throws an InvalidOperationException listing the dirty scenes; save them and retry.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "tests.run", "args": {
  "testMode": "string_value",
  "testAssembly": "string_value",
  "testNamespace": "string_value",
  "testClass": "string_value",
  "testMethod": "string_value",
  "includePassingTests": false,
  "includeMessages": false,
  "includeStacktrace": false,
  "includeLogs": false,
  "logType": "string_value",
  "includeLogsStacktrace": false,
  "waitSeconds": 0
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "tests.run" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `testMode` | `string` | No | Test mode to run. Options: 'EditMode', 'PlayMode'. Default: 'EditMode' |
| `testAssembly` | `string` | No | Specific test assembly name to run (optional). Example: 'Assembly-CSharp-Editor-testable' |
| `testNamespace` | `string` | No | Specific test namespace to run (optional). Example: 'MyTestNamespace' |
| `testClass` | `string` | No | Specific test class name to run (optional). Example: 'MyTestClass' |
| `testMethod` | `string` | No | Specific fully qualified test method to run (optional). Example: 'MyTestNamespace.FixtureName.TestName' |
| `includePassingTests` | `boolean` | No | Include details for all tests, both passing and failing (default: false). If you just need details for failing tests, set to false. |
| `includeMessages` | `boolean` | No | Include test result messages in the test results (default: true). If you just need pass/fail status, set to false. |
| `includeStacktrace` | `boolean` | No | Include stack traces in the test results (default: false). |
| `includeLogs` | `boolean` | No | Include console logs in the test results (default: false). |
| `logType` | `string` | No | Log type filter for console logs. Options: 'Log', 'Warning', 'Assert', 'Error', 'Exception'. (default: 'Warning') |
| `includeLogsStacktrace` | `boolean` | No | Include stack traces for console logs in the test results (default: false). This is huge amount of data, use only if really needed. |
| `waitSeconds` | `integer` | No | If > 0, block server-side up to this many seconds waiting for the run to finish, returning the structured result in one call. 0 (default) preserves the existing async behaviour: return immediately with the ticket envelope. Default when sync is requested without an explicit value is 30 seconds. On timeout, the tool consults the JSONL monitor log (via JobMonitorLog.TryFindTerminalEvent) so PlayMode reload-survived results still come back synchronously when available. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "testMode": {
      "type": "string",
      "enum": [
        "EditMode",
        "PlayMode"
      ]
    },
    "testAssembly": {
      "type": "string"
    },
    "testNamespace": {
      "type": "string"
    },
    "testClass": {
      "type": "string"
    },
    "testMethod": {
      "type": "string"
    },
    "includePassingTests": {
      "type": "boolean"
    },
    "includeMessages": {
      "type": "boolean"
    },
    "includeStacktrace": {
      "type": "boolean"
    },
    "includeLogs": {
      "type": "boolean"
    },
    "logType": {
      "type": "string",
      "enum": [
        "Error",
        "Assert",
        "Warning",
        "Log",
        "Exception"
      ]
    },
    "includeLogsStacktrace": {
      "type": "boolean"
    },
    "waitSeconds": {
      "type": "integer"
    }
  }
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

## Notes

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

