# editor

Editor-wide state — playmode, focused selection, compilation status, and the
asset-database readiness flags that gate every other RPC. This namespace is the
control surface for `UnityEditor.EditorApplication` and `UnityEditor.Selection`,
so it pairs naturally with any workflow that mutates project content: read the
flags first, perform the mutation, then restore whatever the Inspector was
focused on before the call landed. Treat `editor` as a pre-flight branch rather
than a destination — most calls here exist to make subsequent calls safe.

The application half covers playmode entry/exit, pause toggling, and the
compile / asset-database readiness flags exposed by Unity's editor loop.
`editor.application-get-state` is the readiness probe before any RPC that
depends on a stable domain (test runs, prefab mutations, scene loads); a `true`
on `IsCompiling` or `IsUpdating` means the next call may execute against a
half-reloaded type system, after which managed object references and instance
IDs captured from the pre-reload world are stale. The same flags also flip on
non-MCP triggers — a script save in the Editor, an external asset import, a
`call("assets-refresh")` — so the readback contract is "wait, then re-probe"
regardless of what set them.

**Editing C# is not testing C#.** A `.cs` change on disk — written by a file
tool, an external editor, or by hand — is inert until Unity recompiles, and
Unity only recompiles when the Editor window regains focus, when it is woken
(see the [root index](wiki.md) wake note), or when an explicit
`call("assets-refresh")` runs; it throttles compilation entirely while
unfocused. So **after any code change, trigger a recompile and let the domain
settle before testing the new behaviour.** MCP tools execute inside the Editor
against whichever assembly is *currently loaded*, so a tool call issued right
after an edit runs the **old** code and yields a misleading result. The contract
is: edit → recompile (focus / wake / `assets-refresh`) → poll
`editor.application-get-state` until `IsCompiling` and `IsUpdating` are both
`false` → only then call the tool under test. `LastCompilationFinishedUtc` and
`SecondsSinceLastCompilation` make that confirmation positive rather than
inferred: a fresh timestamp (or a small seconds-since value) proves a recompile
actually landed *after* your edit, whereas `IsCompiling == false` alone only
proves nothing is compiling right now — including the case where the edit never
triggered a compile at all. Both are session-scoped (cleared on editor restart)
and reset on every compile, MCP-triggered or not.

The selection half bridges `UnityEditor.Selection.objects` — read what the
human just clicked in the Hierarchy / Project window, or programmatically focus
the Inspector on an object before a screenshot, a property edit, or a
context-sensitive menu action. Hierarchy selections come back through the
`GameObjects` / `Transforms` arrays, Project selections through `AssetGUIDs`,
and `Active*` mirrors Unity's distinction between the full selection set and
the single object the Inspector is currently bound to. The two halves pair
because a domain reload (triggered by playmode entry or recompile) clears the
Inspector focus — snapshot via `editor.selection-get` before the toggle, then
restore via `editor.selection-set` after `editor.application-get-state`
confirms the new domain has settled.

## Cross-cluster overlap

- `editor.application-get-state.isCompiling` and `.isUpdating` are
  preconditions for `tests-run`, `script-update-or-create`, and any
  `assets-*` mutation. Treat a `true` on either flag as a wait signal — the
  managed type system is mid-rebuild and any reflection-bound RPC may bind
  against either the old or new assembly.
- `editor.selection` is the bridge that supplies live object references to
  `call("gameobject-find")` and `call("assets-find")` workflows: rather than
  guessing paths or GUIDs, ask the human to click the target and then read it
  back with `editor.selection-get`. The returned instance IDs and asset GUIDs
  feed those find/modify RPCs directly.
- Snapshot/restore across a playmode toggle is the canonical combined pattern:
  `editor.selection-get` → `editor.application-set-state` (with the poll loop
  below) → `editor.selection-set` against the captured IDs once
  `IsPlayingOrWillChangePlaymode == IsPlaying` and both compile flags are
  `false`.

## See also

- [`assets`](assets.md) — `call("assets-refresh")` is the usual trigger that
  flips `IsCompiling` and `IsUpdating`; budget for the readback loop after any
  refresh. `assets-find` consumes the GUIDs that `editor.selection-get`
  returns for Project-window selections.
- [`gameobject`](gameobject.md) — `gameobject-find` consumes the instance IDs
  that `editor.selection-get` returns for Hierarchy selections.
- [`tests`](tests.md) — never dispatch `tests-run` while `IsCompiling` or
  `IsUpdating` is true.
- [`scene`](scene.md) — scene loads clear selection and may trigger a domain
  reload; pair with the snapshot/restore idiom.
- [`recorder`](recorder.md) — confirm play mode and a settled domain via
  `editor.application-get-state` before querying a `"live"` recorder session;
  live tools take a consistent main-thread snapshot.

### editor.application-set-state

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

### editor.selection-set

`select: [{ "instanceID": 0 }]` clears the selection — the `0` instance ID
resolves to `null`, which Unity treats as "deselect everything" rather than
"select the null object." Use this idiom to drop focus before a destructive
mutation that would otherwise leave the Inspector pointing at a stale managed
reference.

Pass `includeAssetGUIDs: true` on the paired `editor.selection-get` when the
human's selection is in the Project window rather than the scene — Hierarchy
selections come back through `GameObjects` / `Transforms`, Project selections
through `AssetGUIDs`. The two arrays are independent: a mixed selection
populates both, so consumers should iterate whichever matches the workflow
(scene-graph edits vs. asset-database edits) rather than assuming one is
empty.
