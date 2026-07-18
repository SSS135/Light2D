<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/system.md. -->

# `system`

Small plumbing surface — liveness, registry introspection, registry toggles.
These are the calls you reach for *before* doing real work, not the work
itself. None of them edit assets, scenes, or play state.

## Cheat sheet

| Goal | Call | Notes |
|---|---|---|
| Is the gateway reachable? | `call("ping", {})` | Cheapest readiness probe. Returns `pong` (or the echoed `message`). |
| What tools exist right now? | `call("tool.list", {})` | Live registry snapshot; honours `regexSearch`. |
| Mute / restore a tool by name | `call("tool.set-enabled-state", {...})` | Targeted enable/disable; see [`tool`](tool.md). |

## Liveness probing

`ping` is the watchdog primitive. The gateway runs inside the Editor's main
loop, and Unity throttles `EditorApplication.update` when its window isn't
foregrounded — so the port is the canonical signal that the Editor is alive
and the message pump is draining. A `ping` round-trip under a few hundred
milliseconds means the gateway is responsive; a missed `ping` means the
Editor either isn't focused, is mid-compile / domain reload, or has crashed.

The shipped [`wake-unity.ps1`](../../Tools/wake-unity.ps1) one-shot helper
(at `Packages/com.sss135.editor-automation-mcp/Tools/wake-unity.ps1`) is built
around exactly this: it foregrounds the Editor process so a stalled
reload can complete. Treat any longer call (`tests.run`, `assets.refresh`
after a `.cs` write, `package.add`) as a recompile risk and run
[`wake-unity.ps1`](../../Tools/wake-unity.ps1) immediately before issuing
it, not after the gateway has already gone silent.

## See also

- [`tool`](tool.md) — registry-management entry points (`tool.list`,
  `tool.set-enabled-state`).
- [`wiki`](wiki.md) — routing rules for the `call()` shape itself.

To read a method's wiki, call `tools/call` with `arguments = { method: "<dotted>" }` (omit `args`). Include `args` (even `{}`) to execute.

## Methods

- `ping` — Lightweight readiness probe. Returns the input message or 'pong' if omitted.
- `system.editor-modal-respond` — Programmatically clicks a button on a native modal dialog previously surfaced by system-editor-modal-status. Refuses to click obviously-destructive buttons unless acknowledgeDestructive=true.
- `system.editor-modal-status` — Returns metadata for any native Win32 modal dialog currently blocking Unity's main thread. Use this to detect why a main-thread tool is hanging. Hwnd values are stringified Int64; pass them back verba…
- `system.job-list` — List every job ticket the gateway currently tracks (running plus completed-within-TTL). Returns slim entries — ticket_id, method, status, started_at. Use 'system-job-status' on a specific ticket id fo…
- `system.job-status` — Fetch the current status and (when finished) the final result of a long-running job ticket. Use this after a tool returns a Processing response carrying a ticket id. Returns NOT_FOUND-shaped error dat…
- `system.job-wait` — Block server-side until a job ticket reaches a terminal state or the timeout expires. Returns the same payload shape as system-job-status, plus a 'timed_out' flag and an optional 'status_filter_unmatc…

