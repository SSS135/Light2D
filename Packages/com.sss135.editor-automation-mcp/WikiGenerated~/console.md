<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/console.md. -->

# `console`

Read or clear the Unity Editor Console. Two methods: `console.get-logs`
pulls structured entries (severity, message, timestamp, optional stack trace)
from the MCP log cache that mirrors `UnityEngine.Debug.Log*` output, and
`console.clear-logs` wipes both the cache and the Editor's Console window.

`console.get-logs` returns an object `{ Entries, Count }` — the `Entries` array
holds the log rows and `Count` is its length — not a bare top-level array, so the
payload is a single MCP `structuredContent` object.

Typical use cases:

- **Post-test inspection.** After `call("tests.run")`, read the console to
  surface any `Debug.LogError` / exception lines the test framework did not
  promote into a failure — Unity tests pass when no assertion fires even if
  user code logged an error. Pair with `logTypeFilter: "Error"` and a
  `lastMinutes` window covering the run.
- **Recompile verification.** After `call("script.update-or-create")` (or
  any other write that triggers a domain reload via
  [`assets.refresh`](assets.md#assetsrefresh)), wait for `IsCompiling` to
  settle on [`editor`](editor.md), then read with
  `logTypeFilter: "Error"` to confirm the new assembly compiled cleanly. A
  silent recompile that produced compile errors will not surface anywhere
  else through the MCP surface.
- **Isolating a single action.** Clear immediately before the action under
  investigation, run the action, then read with a short `lastMinutes` to
  get only what it produced.

## Cross-cluster overlap

- **`logTypeFilter` is a single severity, not a mask.** One of `Error`,
  `Assert`, `Warning`, `Log`, `Exception`, or `null` for all. There is no
  combined "errors plus exceptions" value — read twice, or omit the filter
  and partition client-side.
- **`includeStackTrace` defaults to `false`.** Stack traces dominate the
  payload; leave off for triage scans, switch on only when drilling into a
  specific entry.

## See also

- `call("tests.run")` — run after this completes, then read the console
  for error / exception entries the test harness did not surface itself.
- `call("script.update-or-create")` — pair with a post-recompile
  `console.get-logs` filtered to `Error` to catch silent compile failures.
- [`editor`](editor.md) — probe `IsCompiling`
  before reading logs after a recompile-triggering call, otherwise the
  cache may still be in flux.
- [`recorder`](recorder.md) — the temporal counterpart: where the console
  shows point-in-time `Debug.Log*` output, the recorder tracks how an
  instrumented value evolved across frames in the live or a recorded session.

To read a method's wiki, call `tools/call` with `arguments = { method: "<dotted>" }` (omit `args`). Include `args` (even `{}`) to execute.

## Methods

- `console.clear-logs` — Clears the MCP log cache (used by console-get-logs) and the Unity Editor Console window. Useful for isolating errors related to a specific action by clearing logs before performing the action.
- `console.get-logs` — Retrieves Unity Editor logs. Useful for debugging and monitoring Unity Editor activity.

