<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/editor.application-set-state.md. -->

---
name: editor-application-set-state
description: Control the Unity Editor application state. You can start, stop, or pause the 'playmode'. Use 'editor-application-get-state' tool to get the current state first.
---

# Editor / Application / Set State

Control the Unity Editor application state. You can start, stop, or pause the 'playmode'. Use 'editor-application-get-state' tool to get the current state first.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "editor.application-set-state", "args": {
  "isPlaying": false,
  "isPaused": false
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "editor.application-set-state" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `isPlaying` | `boolean` | No | If true, the 'playmode' will be started. If false, the 'playmode' will be stopped. |
| `isPaused` | `boolean` | No | If true, the 'playmode' will be paused. If false, the 'playmode' will be resumed. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "isPlaying": {
      "type": "boolean"
    },
    "isPaused": {
      "type": "boolean"
    }
  }
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

## Notes

The playmode transition is **asynchronous**. `isPlaying: true` schedules the
mode change at end-of-frame via `EditorApplication.isPlaying = true`, and on
the way into Play Unity may also kick a domain reload — so the response of a
`set-state` call reflects the state at the moment the RPC was dispatched, not
the settled state. `IsPlayingOrWillChangePlaymode` flips first; `IsPlaying`
catches up one or more frames later.

Always confirm the transition by polling `call("editor.application-get-state")`
until `IsPlayingOrWillChangePlaymode == IsPlaying` matches the requested value
and `IsCompiling` / `IsUpdating` are both `false`. Do not chain follow-up RPCs
on the assumption that the playmode flipped synchronously — the next call may
land mid-reload, after which managed object references and instance IDs from
the pre-reload world are stale.

`isPaused` toggles cleanly within an already-running playmode and does not
trigger a domain reload, so the pause/resume case settles within one frame.
Combining `isPlaying` and `isPaused` in the same call enters Play already
paused, but verification still needs the poll loop because the playmode flip
itself is async.

`IsCompiling` is the readiness flag for "C# changed on disk and the new
assembly is not yet live." A non-MCP recompile (script save, asset import)
will also set it; treat any non-zero `IsCompiling` / `IsUpdating` reading as
"wait, then re-probe" regardless of what triggered it.

