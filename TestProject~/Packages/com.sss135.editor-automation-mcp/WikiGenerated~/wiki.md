<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/wiki.md. -->

# Editor Automation — Method Reference

Auto-generated from the live tool registry.

Call any method without `args` to read its wiki page; include `args` (even `{}`) to execute.

This page is the authored prelude rendered at the top of the live wiki root index. Update `Docs~/WikiSource/wiki.md` in the plugin to change it.

Live tool surface for Editor Automation. Reach a namespace directly with `call("assets")`, `call("gameobject")`, `call("scene")`, `call("editor")`, `call("screenshot")`, `call("script")`, `call("package")`, `call("reflection")`, `call("console")`, `call("recorder")`, `call("profiler")`, `call("object")`, `call("tool")`, `call("tests")`, `call("system")`, or `call("type")`, or start at [`workflows`](workflows.md) for task-oriented navigation. New plugins and setup live on [`initial-setup`](initial-setup.md); transport internals are on [`http-gateway`](http-gateway.md).

## Recompile-needs-focus workaround — [`wake-unity.ps1`](../../Tools/wake-unity.ps1)

**Editing code is not testing code.** A `.cs` change on disk does nothing until Unity recompiles, and Unity only recompiles when the Editor is focused (or woken / explicitly refreshed). So **after any code change, recompile and let the domain settle before testing** — MCP tools run against the currently-loaded assembly, so a call right after an edit exercises the *old* code and "passes" misleadingly. See [`editor`](editor.md) for the `editor.application-get-state` readiness-flag poll loop.

The HTTP gateway becomes unreachable for the duration of an Editor domain reload. In the unfocused-editor case this can stretch indefinitely: Unity throttles `EditorApplication.update` while the editor window is not the foreground process, which means the new AppDomain's compile + assembly load can stall, which means the gateway autostart on the new domain never fires.

The in-plugin wake watchdog in `RpcDispatcher.StartWakeWatchdog` can only call `EditorWin32.TryWakeEditor` while a request is in flight on a live gateway. It cannot fire during the reload window — there is no plugin code running in the editor process between `beforeAssemblyReload` and the next domain's autostart.

The fix is external. The plugin ships [`wake-unity.ps1`](../../Tools/wake-unity.ps1) at `Packages/com.sss135.editor-automation-mcp/Tools/wake-unity.ps1`, which calls `SetForegroundWindow` on the Unity process on demand. The script is Windows-only and runs as a one-shot wake — invoke it before each recompile-triggering MCP call.

Recommended usage from the project root:

```powershell
# One-shot wake (run before each recompile-triggering call):
.\Packages\com.sss135.editor-automation-mcp\Tools\wake-unity.ps1
# Target a specific Editor when multiple are running:
.\Packages\com.sss135.editor-automation-mcp\Tools\wake-unity.ps1 -ProjectFilter "MyGame"
```

This is the only known reliable mitigation for the unfocused-recompile case. Plugin-side fixes are not feasible — once `beforeAssemblyReload` fires, no plugin code runs in the editor process until the new AppDomain is live, by which time the wake is no longer needed.

## Namespaces

- `animation` (3 methods)
- `animator` (3 methods)
- `assets` (17 methods)
- `compile` (1 method)
- `console` (2 methods)
- `editor` (4 methods)
- `gameobject` (11 methods)
- `object` (2 methods)
- `package` (4 methods)
- `particle` (2 methods)
- `profiler` (7 methods)
- `recorder` (7 methods)
- `reflection` (2 methods)
- `scene` (7 methods)
- `screenshot` (4 methods)
- `script` (4 methods)
- `system` (6 methods)
- `tests` (2 methods)
- `tool` (1 method)
- `type` (1 method)

## `animation`

Author and edit Unity `AnimationClip` assets (`.anim` files under `Assets/`).
The namespace is asset-scoped — for runtime `Animator` state-machine wiring on
an `AnimatorController` asset, reach for the sibling `animator.*` namespace
instead.

Typical workflow is `call("animation-create")` to mint empty clips,
`call("animation-get-data")` to inspect existing curve bindings / events /
frame rate / wrap mode, then `call("animation-modify")` to mutate the clip in
place. `animation-modify` consumes a sequence of typed operations (the
`ModificationType` enum) rather than a full clip overwrite, so callers can
add one curve or flip the frame rate without rebuilding the rest of the clip.
Property names and component types fed to `SetCurve` / `RemoveCurve` should
be sourced from a prior `animation-get-data` response so they match the
exact strings Unity expects.

- `animation.create` — Create Unity's Animation asset files (AnimationClip). Creates folders recursively if they do not exist. Each path should start with 'Assets/' and end with '.anim'.
- `animation.get-data` — Get data about a Unity AnimationClip asset file. Returns information such as name, length, frame rate, wrap mode, animation curves, and events.
- `animation.modify` — Modify Unity's AnimationClip asset. Apply an array of modifications including setting curves, clearing curves, setting properties, and managing animation events. Use 'animation-get-data' tool to get v…

## `animator`

Operate on Unity `AnimatorController` assets (`.controller` files under
`Assets/`) — the state-machine graphs that drive `Animator` components. For
`AnimationClip` assets and the legacy `Animation` component, use the sibling
[`animation`](animation.md) namespace instead.

Typical workflow is `animator-create` to author empty controllers, then
`animator-get-data` to inspect the current graph (parameters, layers, states,
transitions, conditions, default state, any-state transitions), then
`animator-modify` to mutate it in place. Inspect-then-modify is the supported
loop: `animator-modify` takes a sequence of typed operations keyed by the
`AnimatorModificationType` enum, and each operation references existing layers
and states by name — round-trip the names through `animator-get-data` rather
than guessing.

The returned data model is nested: a controller has parameters and layers;
each layer owns a state machine with states, a default state, and any-state
transitions; each state owns transitions; each transition owns conditions.
`animator-modify` operations target one level of that tree at a time —
there's no bulk-replace verb.

## See also

- [`animation`](animation.md) — `AnimationClip` and `Animation` (legacy)
  component operations.
- [`assets`](assets.md) — generic asset discovery, copy/move/delete, and
  refresh; use it to locate `.controller` paths before calling
  `animator-get-data`.

- `animator.create` — Create Unity's AnimatorController asset files. Creates folders recursively if they do not exist. Each path should start with 'Assets/' and end with '.controller'.
- `animator.get-data` — Get data about a Unity AnimatorController asset file. Returns information such as name, layers, parameters, and states.
- `animator.modify` — Modify Unity's AnimatorController asset. Apply an array of modifications including adding/removing parameters, layers, states, and transitions. Use 'animator-get-data' tool to get valid names and para…

## `assets`

Operate on Unity content under the `Assets/` and `Packages/` roots — discover,
copy, move, delete, refresh, and read shader / prefab / material data. Use
`assets.*` as the catch-all for AssetDatabase-level operations; per-asset
edits live on the relevant component or GameObject namespace and should be
preferred for runtime-instance state.

Three themes recur across this namespace. **Prefabs** are the lifecycle of
`.prefab` files on disk and their relationship to scene instances — create,
open a Prefab Stage, save, instantiate, close. The Stage is modal and changes
what `gameobject.*` / `component.*` calls target, so it carries the bulk of
the gotchas. **Materials** are the minting of `.mat` assets against a chosen
shader; per-property edits do not happen here. **Shaders** are read-only
discovery — list names, inspect property schemas — feeding the material
authoring path and any downstream property-bag edits.

The single most important boundary in this namespace is **asset on disk vs.
live instance in a scene**. `assets.prefab-create` / `assets.prefab-save`
write the asset file, while `assets.prefab-instantiate` and any subsequent
`gameobject.*` / `component.*` edits operate on a scene instance — unless a
Prefab Stage is open, in which case those same scene-mode calls quietly
redirect to the asset. Mixing the two surfaces is the most common source of
"my edit didn't stick" or "my edit propagated to every instance" surprises.

## Cross-cluster overlap

- **GameObject vs. asset editing** — `assets.modify` mutates a serialized
  asset file on disk (a Prefab, a Material, a ScriptableObject). Use
  `gameobject.*` and `component.*` for runtime scene-instance state instead.
  The two surfaces are not interchangeable: editing a Prefab asset through
  `assets.modify` propagates to every instance on next reload, while
  `gameobject.*` only affects the live instance.
- **Refresh semantics** — `assets.refresh` triggers `AssetDatabase.Refresh`.
  Use it for general asset import. For C# compile verification, prefer
  `compile.request` because it returns a job ticket before refresh can trigger
  script compilation and domain reload.
- **Prefab Stage is modal.** `assets.prefab-open` switches the Editor into
  Prefab Stage; the active scene is no longer the edit target. Pair every
  open with a matching `call("assets.prefab-close")` before issuing any
  scene-mode call (`gameobject.*`, `scene.*`, `assets.prefab-instantiate`),
  otherwise those calls run against the Stage hierarchy and not the scene.
- **Edits inside the Stage target the asset.** While the Stage is open,
  `gameobject.*` / `component.*` mutations write to the prefab asset and
  propagate to every instance on next reload. Outside the Stage they only
  touch the live scene instance.
- **Variants** — passing `sourcePrefabAssetPath` to `assets.prefab-create`
  always creates a Variant; passing `gameObjectRef` of an existing prefab
  instance with `connectGameObjectToPrefab: true` also creates a Variant.
- **Material authoring needs an existing shader.** `assets.material-create`
  takes a shader name that must already exist — discover names via
  `assets.shader-list-all` and inspect property schemas via
  `assets.shader-get-data` before authoring. Missing intermediate folders on
  the `.mat` path are created automatically; the path must end in `.mat` and
  start under `Assets/`.
- **Material property edits live elsewhere.** Once a `.mat` exists, route
  texture / color / float / keyword changes through `call("object.modify")`
  for the asset file or `call("gameobject.component-modify")` for a renderer
  instance — not back through `assets.material-create`.
- **Shader property names feed material edits.** The `Properties[].Name`
  field returned by `assets.shader-get-data` (e.g. `_MainTex`, `_Color`) is
  what `gameobject.component-modify` and material-property RPCs expect —
  sourcing names from this call avoids guesswork and silent no-op writes.
- **Shader payloads can balloon.** `assets.shader-get-data` defaults to a
  compact summary; `includeSubshaders` and especially `includeSourceCode`
  produce very large responses. Prefer `paths` or `viewQuery` to scope reads
  to the field you actually need.
- **Shader compilation errors surface here.** `HasErrors` plus the
  `Messages` list on `assets.shader-get-data` expose shader compile failures
  per platform — useful when a material created via `assets.material-create`
  renders pink.
- **Shader and material references travel with the prefab** — author the
  material first via `assets.material-create` so the prefab authoring step
  can reference an existing asset, otherwise the reference resolves to
  `None` on next reload.

## See also

- [`workflows`](workflows.md) — task-oriented walkthroughs that combine
  `assets.*` with `gameobject.*` / `component.*` / `editor.*`.
- [`animator`](animator.md) — `AnimatorController` assets are also reached
  via `assets.*` for discovery / copy / move / delete, then mutated through
  the `animator-*` namespace.

- `assets.copy` — Copy assets at given paths and store them at new paths. Does AssetDatabase.Refresh() at the end. Use 'assets-find' tool to find assets before copying.
- `assets.create-folder` — Creates a new folder in the specified parent folder. The parent folder string must start with the 'Assets' folder, and all folders within the parent folder string must already exist. For example, when…
- `assets.delete` — Delete the assets at paths from the project. Does AssetDatabase.Refresh() at the end. Use 'assets-find' tool to find assets before deleting.
- `assets.find` — Search the asset database using the search filter string. Allows you to search for Assets. The string argument can provide names, labels or types (classnames).
- `assets.find-built-in` — Search the built-in assets of the Unity Editor located in the built-in resources: Resources/unity_builtin_extra. Doesn't support GUIDs since built-in assets do not have them.
- `assets.get-data` — Get asset data from the asset file in the Unity project. It includes all serializable fields and properties of the asset. Use 'assets-find' tool to find asset before using this tool.

Path-scoped read…
- `assets.material-create` — Create new material asset with default parameters. Creates folders recursively if they do not exist. Provide proper 'shaderName' - use 'assets-shader-list-all' tool to find available shaders.
- `assets.modify` — Modify asset file in the project. Use 'assets-get-data' tool first to inspect the asset structure before modifying. Not allowed to modify asset file in 'Packages/' folder. Please modify it in 'Assets/…
- `assets.move` — Move the assets at paths in the project. Should be used for asset rename. Does AssetDatabase.Refresh() at the end. Use 'assets-find' tool to find assets before moving.
- `assets.prefab-close` — Close currently opened prefab. Use it when you are in prefab editing mode in Unity Editor. Use 'assets-prefab-open' tool to open a prefab first.
- `assets.prefab-create` — Create a prefab from a GameObject in the current active scene. The prefab will be saved in the project assets at the specified path. Creates folders recursively if they do not exist. If the source Gam…
- `assets.prefab-instantiate` — Instantiates prefab in the current active scene. Use 'assets-find' tool to find prefab assets in the project.
- `assets.prefab-open` — Open prefab edit mode for a specific GameObject. In the Edit mode you can modify the prefab. The modification will be applied to all instances of the prefab across the project. Note: Please use 'asset…
- `assets.prefab-save` — Save a prefab. Use it when you are in prefab editing mode in Unity Editor. Use 'assets-prefab-open' tool to open a prefab first.
- `assets.refresh` — Refreshes the AssetDatabase. Use it if any file was added or updated in the project outside of Unity API. Use it if need to force scripts recompilation when '.cs' file changed.
- `assets.shader-get-data` — Get detailed data about a shader asset in the Unity project. Returns shader properties, subshaders, passes, compilation errors, and supported status. Use 'assets-find' tool with filter 't:Shader' to f…
- `assets.shader-list-all` — List all available shaders in the project assets and packages. Returns their names. Use this to find a shader name for 'assets-material-create' tool.

## `compile`

Request a normal Unity compile check after files have changed on disk. This
namespace is for the "focus Unity and let it import/recompile" workflow: it
wakes the Editor, runs a normal `AssetDatabase.Refresh()`, and reports whether
that refresh observed compilation plus any compiler warnings or errors.

Use `compile.request` when an agent edited C# files outside Unity and you need
a single durable result instead of manually chaining `assets.refresh`,
`editor.application-get-state`, and `console.get-logs`.

## Reload-safe contract

`compile.request` always returns a job ticket before the refresh starts. That
ordering is intentional: a refresh can trigger script compilation and domain
reload, and during that window the in-process MCP gateway may stop responding.
The ticket is persisted before refresh so the final result can be reconstructed
through `system.job-status` even if the in-memory registry was cleared.

The public call has no knobs. It does not force a rebuild, clear the build
cache, or expose wake/focus as an option. Waking the Editor is an internal
implementation detail so the operation behaves like a developer focusing the
Unity window.

- `compile.request` — Starts a normal Unity refresh/import compile check. The call returns a job ticket before AssetDatabase.Refresh runs so the result survives compilation and domain reload. Poll system.job-status with th…

## `console`

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

- `console.clear-logs` — Clears the MCP log cache (used by console-get-logs) and the Unity Editor Console window. Useful for isolating errors related to a specific action by clearing logs before performing the action.
- `console.get-logs` — Retrieves Unity Editor logs. Useful for debugging and monitoring Unity Editor activity.

## `editor`

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

- `editor.application-get-state` — Returns available information about 'UnityEditor.EditorApplication'. Use it to get information about the current state of the Unity Editor application. Such as: playmode, paused state, compilation sta…
- `editor.application-set-state` — Control the Unity Editor application state. You can start, stop, or pause the 'playmode'. Use 'editor-application-get-state' tool to get the current state first.
- `editor.selection-get` — Get information about the current Selection in the Unity Editor. Use 'editor-selection-set' tool to set the selection.
- `editor.selection-set` — Set the current Selection in the Unity Editor to the provided objects. Use 'editor-selection-get' tool to get the current selection first.

## `gameobject`

Live-scene-instance operations plus full component lifecycle on the resolved
instance — create, find, modify, duplicate, reparent, destroy GameObjects, and
add / list / inspect / patch / remove their Components. Everything in this
namespace edits the live runtime: the active Scene's hierarchy by default, or
the working copy inside the currently open Prefab Stage when one is open.
On-disk prefab and asset state lives on `call("assets")` /
`call("assets.prefab")`.

GameObject ops and Component ops share the same `GameObjectRef` shape
(`instanceID` > `path` > `name`, plus `assetPath` / `assetGuid` for prefab
assets). Resolve once with `gameobject.find`, then reuse the returned ref for
every subsequent edit — including the component lifecycle calls described in
the [Component lifecycle](#component-lifecycle) subsection below. Component
calls additionally take a `ComponentRef` (priority: `instanceID` > `index` >
`typeName`); `gameobject.component-get` returns the live `instanceID` per
component so a follow-up patch can dispatch by the cheapest key.

The two clusters are deliberately split at the method level so the GameObject
patch surface (`gameobject.modify`) never silently swallows component-internal
field edits — those are routed through `gameobject.component-modify` instead.
The patch shape (`jsonPatch` → `pathPatches` → `*Diff` precedence, the
`fieldName` / `nested/field` / `arrayField/[i]` / `dictField/[key]` path
syntax, the optional `#/` prefix, and the silent-ignore-then-log behavior on
unknown members) is identical between the two — only the target differs.

For sibling patch entry points beyond this namespace, see `call("object.modify")`
for any `UnityEngine.Object` that is not a GameObject or Component, and
`call("assets.modify")` for a serialized asset file on disk rather than a live
instance.

## Cross-cluster overlap

- **Instance vs. asset.** `gameobject.*` only edits the live runtime instance
  (or, while a Prefab Stage is open, the Stage's working copy). To mutate the
  on-disk prefab asset directly, route through
  [`assets`](assets.md) (`open` → edit via `gameobject.*` /
  `gameobject.component-*` → `save` / `close`). To rewrite a serialized asset
  field on any other `UnityEngine.Object` use `call("assets.modify")`.
- **`gameobject.find` is Stage-aware.** When a Prefab Stage is open, find
  resolves inside the Stage hierarchy and ignores the active Scene entirely;
  with no Stage open it falls back to the active Scene. The same `GameObjectRef`
  shape is consumed by every other method in this namespace — resolve once,
  reuse the returned ref.
- **Read before write.** `gameobject.find` returns shallow component refs and
  optional hierarchy/bounds/data slices. Use `paths` or `viewQuery` to scope
  large reads instead of paying for the full recursive serialization that
  `includeData` triggers. For component internals, hand off to
  `gameobject.component-get`.
- **`gameobject.set-parent` reparents in place.** `worldPositionStays` mirrors
  Unity's `Transform.SetParent(_, worldPositionStays)`. The parent ref can
  itself be inside the open Stage; cross-Stage / cross-Scene reparents are not
  supported.
- **Always read components before modifying them.** `gameobject.component-get`
  (or `gameobject.component-list-all` when you only need type names) returns
  the live field/property layout. Patch payloads keyed by stale field names
  are dropped silently and only surface in the response's log/`Logs` array —
  they do not fail the call. Reading first also gives you the `instanceID` to
  feed back into `componentRef` for the cheapest subsequent dispatch.
- **`component-list-all` is a type catalog, not a per-GameObject query.** It
  enumerates every C# class derived from `UnityEngine.Component` in the
  loaded assemblies with paginated search — feed the result into
  `gameobject.component-add`'s `componentNames`. For the components actually
  attached to a given GameObject, use `gameobject.find` with
  `includeComponents: true`.
- **`component-add` takes full type names.** `componentNames` requires the
  fully qualified namespaced class name (e.g. `UnityEngine.Rigidbody`,
  `App.Gameplay.UnitProvider`), not the short class name visible in the
  Inspector's Add Component menu.
- **`component-destroy` will not remove missing-script slots.** The handler
  refuses to destroy components it cannot resolve via `ComponentRef`; clean
  up missing scripts through Unity's own UI or a `script.execute` pass.

## Component lifecycle

The `gameobject.component-*` methods — `component-add`, `component-get`,
`component-list-all`, `component-modify`, `component-destroy` — operate on
components attached to a GameObject resolved through `gameobject.find`. They
follow the same instance-vs-asset rule as the rest of this namespace: edits
hit the live instance, or the Prefab Stage working copy when one is open via
[`assets`](assets.md). Patch semantics for
`gameobject.component-modify` are described in detail in its method section
below.

## See also

- [`assets`](assets.md) — open the Prefab Stage so subsequent
  `gameobject.*` edits propagate to the on-disk asset.
- [`scene`](scene.md) — switch the active Scene before resolving refs that
  rely on Scene-side lookup.
- `call("object.modify")` — same patch surface (`jsonPatch` / `pathPatches` /
  `objectDiff`) for any `UnityEngine.Object` that is not a GameObject or
  Component; useful for ScriptableObjects and other assets reached by raw
  object reference rather than by GameObject path.
- `call("assets.modify")` — sibling patch entry point that targets a
  serialized asset file on disk instead of a live instance.

- `gameobject.component-add` — Add Component to GameObject in opened Prefab or in a Scene. Use 'gameobject-find' tool to find the target GameObject first. Use 'gameobject-component-list-all' tool to find the component type names to…
- `gameobject.component-destroy` — Destroy one or many components from target GameObject. Can't destroy missed components. Use 'gameobject-find' tool to find the target GameObject and 'gameobject-component-get' to get component details…
- `gameobject.component-get` — Get detailed information about a specific Component on a GameObject. Returns component type, enabled state, and optionally serialized fields and properties. Use this to inspect component data before m…
- `gameobject.component-list-all` — List C# class names extended from UnityEngine.Component. Use this to find component type names for 'gameobject-component-add' tool. Results are paginated to avoid overwhelming responses.
- `gameobject.component-modify` — Modify a specific Component on a GameObject in opened Prefab or in a Scene. Allows direct modification of component fields and properties without wrapping in GameObject structure. Use 'gameobject-comp…
- `gameobject.create` — Create a new GameObject in opened Prefab or in a Scene. If needed - provide proper 'position', 'rotation' and 'scale' to reduce amount of operations.
- `gameobject.destroy` — Destroy GameObject and all nested GameObjects recursively in opened Prefab or in a Scene. Use 'gameobject-find' tool to find the target GameObject first.
- `gameobject.duplicate` — Duplicate GameObjects in opened Prefab or in a Scene. Use 'gameobject-find' tool to find the target GameObjects first.
- `gameobject.find` — Finds specific GameObject by provided information in opened Prefab or in a Scene. First it looks for the opened Prefab, if any Prefab is opened it looks only there ignoring a scene. If no opened Prefa…
- `gameobject.modify` — Modify GameObject fields and properties in opened Prefab or in a Scene. You can modify multiple GameObjects at once. Just provide the same number of GameObject references and SerializedMember objects.…
- `gameobject.set-parent` — Set parent GameObject to list of GameObjects in opened Prefab or in a Scene. Use 'gameobject-find' tool to find the target GameObjects first.

## `object`

Generic read/write surface for any `UnityEngine.Object` — the union of asset
references, components, GameObjects, ScriptableObjects, EditorWindows, and
anything else extending the engine's base object type. Operates by
`instanceID` rather than by asset path or hierarchy lookup, so the typical
chain is to resolve the ref through a typed namespace first
(`call("assets.find")`, `call("gameobject.find")`,
[`editor`](editor.md)) and forward the returned ref.

Prefer the typed namespace whenever one exists: `call("gameobject")` for
scene-instance state (including its `gameobject.component-*` methods),
[`assets`](assets.md) for serialized asset files. Reach for `object.*` only
when no typed surface fits — orphan ScriptableObjects, EditorWindow state,
ProjectSettings entries, render pipeline assets, and other singletons that
do not live under either umbrella. If even `object.*` cannot describe the
target (e.g. a static helper, a non-`UnityEngine.Object` CLR type), drop to
[`reflection.method-call`](reflection.md) instead.

## Cross-cluster overlap

- **Three sibling write paths.** [`object.modify`](object.md#objectmodify),
  `call("gameobject.component-modify")`, and
  [`assets.modify`](assets.md) share the same `SerializedMember` /
  `pathPatches` / `jsonPatch` patch surface and the same ordering rule.
  Pick by the target's kind, not by the patch shape: components and
  GameObjects through `gameobject.*`, on-disk assets through `assets.*`,
  everything else through `object.*`.
- **Path-scoped reads save tokens.** `object.get-data` defaults to a full
  recursive serialization, which is expensive on deep
  ScriptableObjects. Supply `paths` to read a fixed list of fields, or
  `viewQuery` to navigate to a subtree and prune by name regex / max depth
  / runtime type. The two parameters are mutually exclusive.
- **Path syntax is shared with `pathPatches`.** `fieldName` for a plain
  field, `nested/field` for a nested member, `arrayField/[i]` for an
  array/list index, `dictField/[key]` for a dictionary entry. A leading
  `#/` is stripped. The same grammar applies to `jsonPatch` keys, so a
  payload authored against a `get-data` read can be turned into a write
  with minimal reshaping.

## See also

- `call("gameobject.component-modify")` — canonical write path for scene
  component state; the patch-ordering rules and example payloads live
  there.
- [`assets`](assets.md) — `assets.modify` is the write path for serialized
  asset files (`.asset`, `.mat`, `.prefab` overrides).
- [`reflection`](reflection.md) — drop here when the target is not a
  `UnityEngine.Object` at all, or when you need to invoke a method rather
  than write a field.

- `object.get-data` — Get data of the specified Unity Object. Returns serialized data of the object including its properties and fields. If need to modify the data use 'object-modify' tool.

Path-scoped reads (token-saving…
- `object.modify` — Modify the specified Unity Object. Allows direct modification of object fields and properties. Use 'object-get-data' first to inspect the object structure before modifying.

Three modification surface…

## `package`

Drive the Unity Package Manager — add, remove, list, and search. Calls
mutate `Packages/manifest.json` and go through `UnityEditor.PackageManager`,
so add/remove participate in Unity's package resolution and domain reloads.

## The domain-reload gap

`package.add` and `package.remove` both trigger UPM resolution, which on a
real change ends in a **domain reload**. While the reload runs, the
plugin's HTTP gateway is unreachable — no managed code runs in the Editor
between `beforeAssemblyReload` and the next domain's autostart, so the
response only lands after the reload completes.

Foreground Editor settles in seconds. **Unfocused**, Unity throttles
`EditorApplication.update`, autostart fires late, and the gateway looks
hung. Mitigation is the external wake-unity helper described in
[`wiki`](wiki.md) — run [`wake-unity.ps1`](../../Tools/wake-unity.ps1)
(at `Packages/com.sss135.editor-automation-mcp/Tools/wake-unity.ps1`) immediately
before each `package.add` / `package.remove` from an unfocused Editor to
bring the window forward so the reload can complete.

`package.list` and `package.search` are read-only — safe to call any time
the gateway is up.

## Cross-cluster overlap

- **`package.add` then code that uses the new types** — the new assemblies
  aren't in the type system until the reload settles. Probe
  [`editor.application-get-state`](editor.md) and wait for
  `IsCompiling` / `IsUpdating` to flip to `false` before invoking
  [`script.execute`](script.md) or constructing components from the new
  package.
- **`package.search` vs `package.list`** — `search` covers registry plus
  installed packages with ranked matches; `list` only enumerates the
  current project. "Is X installed?" → `list` with `nameFilter`. "What
  versions of X exist?" → `search` with `offlineMode: false`.

## See also

- [`wiki`](wiki.md) — wake-unity workaround for the unfocused-reload case.
- [`workflows`](workflows.md) — "Install a UPM package" entry.

- `package.add` — Install a package from the Unity Package Manager registry, Git URL, or local path. This operation modifies the project's manifest.json and triggers package resolution. Note: Package installation may t…
- `package.list` — List all packages installed in the Unity project (UPM packages). Returns information about each installed package including name, version, source, and description. Use this to check which packages are…
- `package.remove` — Remove (uninstall) a package from the Unity project. This removes the package from the project's manifest.json and triggers package resolution. Note: Built-in packages and packages that are dependenci…
- `package.search` — Search for packages in both Unity Package Manager registry and installed packages. Use this to find packages by name before installing them. Returns available versions and installation status. Searche…

## `particle`

- `particle.system-get` — Get detailed information about a ParticleSystem component on a GameObject. Returns particle system state and optionally serialized data for each module. Use the boolean flags to request specific modul…
- `particle.system-modify` — Modify a ParticleSystem component on a GameObject. Provide the data model with only the modules you want to change. Use 'particle-system-get' first to inspect the ParticleSystem structure before modif…

## `profiler`

Read-only analysis of Unity Profiler data. These tools answer "where did the
frame time go", "how does this counter behave over the run", and "which frames
spiked" against whatever is currently in the Profiler's in-memory buffer — a
live recording **or** the last-loaded capture file. Everything here reads back
`UnityEditorInternal.ProfilerDriver` and the `UnityEditor.Profiling`
`*FrameDataView` family on the Editor main thread; nothing is recomputed, no
runtime instrumentation is required.

This namespace never controls recording. Starting and stopping a capture stays
entirely in your hands — use the Profiler window's Record button (or load a
saved file with `profiler.load-capture`). The agent only analyzes data Unity
already captured.

## Recording is the user's job

There is no `profiler.start` / `profiler.stop`, and deliberately so. Unity
captures everything natively the moment you arm the Profiler window's Record
button, so the only useful surface is analysis of an already-captured session.
Before calling any read tool, make sure a recording exists: either it is live
in the Profiler window now, or you have loaded a file with
`profiler.load-capture`. An empty buffer makes the read tools throw
`No profiler frames available` (except `profiler.describe`, which reports the
empty state instead).

Be careful about domain reloads. Recompiling the plugin clears the current live
capture, so re-record (or load a `.raw`/`.data` file) after any recompile before
trusting a read. See the recompile note on the [root wiki page](wiki.md).

## One global buffer

`ProfilerDriver` is a single editor-wide singleton, so there is **no per-call
`source` argument** — every read operates on the one buffer that is loaded right
now. `profiler.load-capture` is the only stateful tool: it calls
`ProfilerDriver.LoadProfile` and **replaces the global buffer**, which also
changes what the Profiler window shows and what every other `profiler.*` read
returns, until you re-record or load a different file. This is the deliberate
divergence from [`recorder`](recorder.md), which takes a `session` argument on
every call — the recorder owns its own session files, the profiler shares
Unity's single window-backed buffer.

## describe, then summarize, then drill

The intended flow funnels from an orientation call to a per-counter summary to a
single frame — it never dumps every frame's markers:

1. **`profiler.describe`** — the orientation call. Frame range, thread names, and
   the available counter list (name / category / unit). Counter names from here
   are the inputs to the other tools.
2. **Find the moment.** `profiler.summarize-counter` for one counter's
   min/max/mean/p95/p99 plus the **argmax frame** — that frame index is the
   drill handle. `profiler.find-spikes` lists the frames that break out of the
   counter's normal range. `profiler.get-series` returns a bounded,
   peak-preserving slice when you need the shape over a window.
3. **Drill into a frame.** Feed an argmax / spike frame into
   `profiler.frame-breakdown` for that frame's top markers (inverted + merged
   hierarchy: leaf functions aggregated, sorted by self time, total time, or GC
   bytes).

`profiler.query` is the C# escape hatch for ad-hoc questions the structured
tools can't express; it runs a read-only snippet against a `ProfilerQueryContext`
view of the buffer.

## Reading the numbers

- **Editor numbers are relative only.** The Profiler running inside the Editor
  measures the Editor process, not a clean player build — there is editor
  overhead in every frame and no IL2CPP. Use the figures to compare frames,
  counters, and code paths against *each other*; do not read them as the
  shipping-build cost. For absolute timings, profile a development player (out of
  scope for this surface).
- **Time counters are nanoseconds; GC is bytes.** Each counter's `unit` string
  says what its values mean — `"ns"` for time, `"bytes"` for memory, `"count"`,
  `"percent"`, `"hz"`, or `""` when unknown (common for a loaded capture whose
  counter metadata isn't live). `GC Allocated In Frame` is a *byte* count of
  managed allocation, not a time — high values mean GC pressure, not slow code.
  `frameTimeMs` on a breakdown and the `*Ms` marker columns are milliseconds.
- **Counters vs threads; finding CPU spikes.** The counter tools
  (`summarize-counter` / `find-spikes` / `get-series`) read **per-frame counters** —
  `describe` lists the readable ones. Thread names like **"Main Thread" are not
  counters** and will throw. For CPU/GPU frame time use Unity's readable
  **`"CPU Total Frame Time"`** / **`"CPU Main Thread Frame Time"`** / **`"GPU Frame Time"`**
  counters (ns), or `frame-breakdown {frame:-1}` for the single worst frame. An
  unknown or no-data counter name throws (with the readable list) rather than
  silently returning empty.
- **No frames are hidden.** "Worst frame" (`profiler.frame-breakdown {frame:-1}`)
  is the true max CPU frame-time frame, and spike detection scans every frame —
  nothing is silently excluded. A breakdown's `IsLoadFrame` flags when the chosen
  frame is a real scene load (`Application.LoadLevelAsync`) rather than gameplay;
  first-frame JIT spikes and domain-reload frames are obvious from their markers
  (`Mono.JIT`, `Profiler.Callstack`). To focus on steady state, window past
  warmup with the `from`/`to` arguments.

## See also

- [`recorder`](recorder.md) — the temporal counterpart. The recorder tracks how
  an instrumented gameplay value evolved across frames; the profiler tracks where
  engine/CPU time and allocation went. Both are read-only, but the recorder owns
  per-session files while the profiler shares Unity's single buffer.
- [`console`](console.md) — point-in-time `Debug.Log*` triage; pair an error's
  frame with a `profiler.frame-breakdown` of that frame.
- [`screenshot`](screenshot.md) — visual evidence at a moment, paired with the
  profiler's numeric breakdown of the same frame.
- [`editor`](editor.md) — confirm a settled domain (`IsCompiling:false`) after a
  recompile before trusting a read; a reload clears the live capture.

- `profiler.describe` — Orientation call before any other profiler.* tool. Reports whether the buffer is from a live recording or a loaded capture, the available frame range, the threads present, the counters you can summari…
- `profiler.find-spikes` — Find the frames where one profiler counter spikes. Pass an absolute 'threshold' (a frame counts as a spike when its value exceeds it) OR an n-'sigma' multiplier (spike when value > mean + sigma*stddev…
- `profiler.frame-breakdown` — Top markers for one frame, the inverted+merged hierarchy (leaf functions aggregated by name), sorted by self time / total time / GC bytes. This is the drill-down step: get the worst / spike frame from…
- `profiler.get-series` — Chart one counter's per-frame values over a window, peak-preserving downsampled to a bounded point budget (the first, last, and per-bucket extrema survive, so spikes are never smoothed away). Run 'pro…
- `profiler.load-capture` — Load a saved .raw/.data Profiler capture into the global Profiler buffer. WARNING: this REPLACES the current in-memory data — the Profiler window and every other profiler.* read now reflect the loaded…
- `profiler.query` — Run a small C# snippet over the current Profiler buffer and get back a compact result — the flexible escape hatch for ad-hoc questions the structured tools (describe, summarize-counter, get-series, fr…
- `profiler.summarize-counter` — Summary stats (min/max/mean/p95/p99 + argmax/argmin frames) for one profiler counter over a frame window. Report p95/p99, not just the mean, so a few spike frames don't mask the steady state. The retu…

## `recorder`

Temporal debugging surface for a running game. Devs instrument code with
`Recorder.LogVariable(obj, "tag", value)` / `Recorder.LogEvent(...)`; during
play the plugin records *significant changes* — keyed by object and frame —
into a per-session NDJSON file and an in-memory buffer. The `recorder.*` tools
then answer "what was the state at frame N", "how did `hp` change A→B", and
"what happened around this death" against a recorded file **or** the **live**
running session.

This namespace is read-only. It never enters play mode, mutates the scene, or
emits data — it only queries recordings produced by the runtime instrumentation
(see below). The recorder is engine-agnostic and editor-only: a pure-managed
runtime assembly produces the data, the editor assembly answers the questions.

Every tool takes a `session` argument — `"live"` for the running in-memory
session, or a recording file path / id from `recorder.list-sessions`. Every
result carries a self-describing `Meta` envelope (`Units`, `TimeDomain`,
`FrameRangeCovered`, `Reduction`, `ThresholdApplied`, `Elided`, `Truncated`,
`TotalCount`, `NextCursor`, `SchemaVersion`) so a token-bounded consumer can
tell what it got — and what was clipped — without dumping raw frames.

## Producing data — runtime instrumentation

The query tools are useless without data; data comes from the runtime
`Recorder` API in the `com.SSS135.EditorAutomation.Recorder` assembly
(`Runtime/Recorder/`), which auto-references into any project. Three ways to
emit it, cheapest first:

- **Drag-on a `RecordedTransform` component** (Add Component → Editor Automation →
  Recorded Transform). Logs `pos` / `rot` / optional `scale` every
  `LateUpdate` with no code. An optional tag prefix namespaces multiple
  recorders on one object (`"body." → "body.pos"`).
- **Annotate fields with `[Record]` and add a `RecordedFields` component**
  (Editor Automation → Recorded Fields). It reflects each target's `[Record]`-marked
  fields/properties once in `Awake` and logs them every frame. `[Record("hp")]`
  overrides the tag; `Unit` annotates the manifest. Editor-Mono only — IL2CPP
  needs `[Preserve]`/link.xml, which is out of scope.
- **Call the API directly** for full control:
  - `Recorder.LogVariable(obj, "tag", value)` — typed overloads cover
    `float`/`double`/`int`/`long`/`bool`, `Vector2/3/4`, `Quaternion`,
    `Color`, `Enum`, `string`, plus a boxed `object` fallback. `obj` can be a
    `UnityEngine.Object` (identity = its `GetInstanceID()`, label/path/parent
    captured on first sight) or a plain `string` key.
  - `Recorder.LogEvent(obj|name, props, severity)` — instantaneous, free-form
    events with a `Dictionary<string,object>` of props and a `Severity`
    (`Trace`…`Fatal`, default `Info`). Object-less global events are allowed.
  - `Recorder.DeclareVariable(tag, unit, epsilon)` — optional sugar to pre-seed
    a tag's unit / change-threshold before its first value.
  - `Recorder.SetFrame(int)` — override the frame stamp (default
    `Time.frameCount`); sticky for the session, so feed it each tick to drive a
    custom simulation clock.

Every call is a cheap no-op while no session is active (recording disabled or
outside play mode), so instrumentation can be left in shipped debug builds.

## Querying — describe, then narrow, then drill

The intended flow never dumps raw frames. It funnels from a manifest to a
summary to a single point:

1. **`recorder.describe-session`** — the **mandatory first call**. Returns the
   object catalog (key / label / path / type / frame range) and the variable
   manifest (tag / kind / unit / epsilon). Every later call needs a `key` and
   `tag` from here.
2. **Locate the moment.** `recorder.summarize-change` (net delta, min/max/mean,
   and the **argmax/argmin frames** as drill-down handles) for one tag, or
   `recorder.find-events` (anchor frames for "the bad moment") for events.
3. **Drill into a frame.** Feed an anchor / argmax frame into
   `recorder.get-state` (as-of values of one object's tags at that frame) or
   `recorder.get-series` (a bounded, peak-preserving slice of one tag).

`recorder.list-sessions` discovers what is queryable (files on disk plus the
live session); `recorder.query` is the C# escape hatch for ad-hoc questions the
summary tools can't express.

## Change-only encoding and as-of reads

The recorder stores a value only when it **significantly changes** — `|Δ|` past
the tag's epsilon (Euclidean magnitude for vector/quat kinds). Per-kind default
epsilon is `0.01` for float/vector/quat/color; int/bool/enum/string compare
exactly. The **first value of each (key, tag) is always stored** so reads have a
baseline. This kills float noise and keeps a session small.

Reads are therefore **as-of**: `recorder.get-state` and the summary endpoints
return the last value set *at or before* the requested frame (carry-forward).
A value logged once at frame 0 reconstructs at any later frame; a tag with no
value at or before the frame comes back marked `NoData`. Identity uses
`GetInstanceID()`, so two same-named objects stay distinct `key`s; a tag whose
kind drifts mid-session is tolerated per-row and flagged `Mixed` in the
manifest.

## Sessions and storage

A session is one play-mode enter→exit. On play it auto-begins via
`RuntimeInitializeOnLoadMethod`; on exit (`Application.quitting`) it flushes and
closes. Files land in `Temp/EditorAutomationRecordings/session-<utc>-<guid>.ndjson`
(NDJSON: one `header` / `obj` / `var` / `val` / `evt` line each), survive
play-mode cycles within an Editor run, and are pruned to the newest ~20. The
live session is queryable with `session: "live"` while play is running; closed
sessions are queried by file path or id.

## Enabling / disabling

Recording is gated by **Project Settings → Project / Editor Automation → "Enable
Gameplay Recorder"**, backed by the `EditorAutomation.Recorder.Enabled` PlayerPrefs key
(default ON) and mirrored by the runtime `Recorder.Enabled` property. When off,
`Recorder.BeginSession` no-ops, the active session stays null, and every
instrumentation call costs nothing. The `recorder.*` query tools stay
registered regardless of the toggle — disabling only stops *new* data; existing
session files remain queryable.

## See also

- [`workflows`](workflows.md) — "Debug a running game's state over time".
- [`console`](console.md) — point-in-time `Debug.Log*` triage; the recorder is
  the temporal counterpart for tracking how a value evolved across frames.
- [`editor`](editor.md) — confirm play mode and a settled domain before
  querying `"live"`; live tools take a consistent main-thread snapshot.
- [`screenshot.game-view`](screenshot.md) — visual evidence at a moment, paired
  with the recorder's numeric state at that frame.

- `recorder.describe-session` — MANDATORY FIRST CALL for any recorder query. Returns the session manifest: the object catalog (key / label / path / type / frame range), the per-tag variable manifest (tag / kind / unit / epsilon), th…
- `recorder.find-events` — Find recorded events matching a filter — the "locate the bad moment" tool. Filter by event name, minimum severity, object key, and a frame window; results are ordered by frame and carry the anchor fra…
- `recorder.get-series` — Get the change-point series of one tag of one object over a frame window, reduced to a bounded set of points. Reductions: "summary"/"downsample" return a peak-preserving budget of points (the first, l…
- `recorder.get-state` — Get the as-of value of one recorded object's tags at a specific frame. For each requested tag the last value set at or before 'frame' is returned (carry-forward); a tag with no value at or before the…
- `recorder.list-sessions` — List discoverable gameplay-recorder sessions: every recording file on disk plus the live in-memory session when one is recording. Each entry gives the id, path (or "live"), label, and frame range — us…
- `recorder.query` — Run a small C# LINQ snippet over a recorded or live session and get back a compact result — the flexible escape hatch for ad-hoc questions the summary tools (get-state, summarize-change, get-series, f…
- `recorder.summarize-change` — Summarize how one tag of one object changed over a frame window: start/end value, net delta, min/max/mean, the argmax/argmin frames (drill-down handles), and the number of change points. The compact w…

## `reflection`

Generic escape hatch into any C# method when no first-class tool exists.
Walks every loaded assembly via `System.Reflection`, so `internal`, `private`,
and `static` members are all reachable. Reach for `reflection.*` whenever you
need to invoke a Unity Editor API, a third-party package method, or a project
script that no `assets.*` / `gameobject.*` / `editor.*` tool wraps.

The canonical chain is two calls: `call("reflection.method-find")` discovers
the method signature against the live assembly graph, then
`call("reflection.method-call")` invokes it with values shaped to that
signature. Skipping `method-find` and guessing parameter types is the most
common failure mode — the resolver matches on full type names and silently
rejects mismatched parameter lists.

`type.get-json-schema` lives under a sibling `type.*` namespace but belongs to
the same conceptual umbrella: use it to discover the JSON shape of any C# type
before authoring a `SerializedMember` payload (a `targetObject`, an
`inputParameters` entry, or a `pathPatches` value for [`object.modify`](object.md)).

## Cross-cluster overlap

- **Prefer typed namespaces when one exists.** Reaching for reflection on a
  GameObject component, an asset, or the console is a smell — `gameobject.*`,
  [`assets`](assets.md), [`console`](console.md), and [`editor`](editor.md)
  carry stable contracts and richer responses. Use `reflection.*` for orphan
  static helpers, EditorWindow methods, or any API the surface does not yet
  cover.
- **Narrow `method-find` before calling `method-call`.** The `*MatchLevel`
  knobs widen or tighten matching, but the resolver returns every candidate
  that passes the threshold. When more than one hit comes back, tighten the
  filter (`knownNamespace: true`, exact `typeName`, full parameter list)
  before invoking — otherwise `method-call` binds to the first match.

## See also

- [`object`](object.md) — typed read/write of any `UnityEngine.Object`; the
  preferred surface when you already have an `instanceID`.
- [`assets`](assets.md), [`scene`](scene.md), [`editor`](editor.md) —
  typed namespaces that should be tried before falling back to reflection.
- `call("type.get-json-schema")` — discover the JSON shape of a C# type
  before authoring `targetObject`, `inputParameters`, or `pathPatches`.

- `reflection.method-call` — Call C# method. Any method could be called, even private methods. It requires to receive proper method schema. Use 'reflection-method-find' to find available method before using it. Receives input par…
- `reflection.method-find` — Find method in the project using C# Reflection. It looks for all assemblies in the project and finds method by its name, class name and parameters. Even private methods are available. Use 'reflection-…

## `scene`

Manage the set of Editor-loaded scenes — create, open, save, unload, set
active, and read structure. Open scenes form a list (single active scene
plus any additive ones); operations key off either the loaded scene name
or an `AssetObjectRef` resolved from disk, not raw asset paths.

## Cross-cluster overlap

- **`assets.find` produces the `sceneRef`.** `scene.open` and
  `scene.set-active` take an `AssetObjectRef`, not a path string —
  resolve the `.unity` asset with `call("assets.find")` (e.g. filter
  `t:Scene`) first and forward the returned ref.
- **Save before `tests.run` and before scene swaps.** Dirty scenes lose
  unsaved edits when `scene.open` runs in `Single` mode or when Play
  Mode kicks off via `tests.run`. Check `IsDirty` on
  `call("scene.list-opened")` results and call `scene.save` for any
  scene you mutated through `gameobject.*`.
- **`scene.unload` keeps the entry in `scene.list-opened`.** Unload
  leaves the scene as a closed entry in the Editor's scene list (still
  visible in the Hierarchy header) — it is not fully removed. The
  returned `UnloadSceneResult` carries the asset ref you can reopen
  with.
- **Active-scene defaulting.** `scene.save` and `scene.get-data` treat
  an empty `openedSceneName` as "use the active scene". Use
  `scene.set-active` to steer which scene that resolves to when several
  are loaded additively.

## See also

- [`assets`](assets.md) — `call("assets.find")` to obtain the
  `AssetObjectRef` consumed by `scene.open` / `scene.set-active`.
- [`workflows`](workflows.md) — task-oriented walkthroughs combining
  scene swaps with `gameobject.*` edits.
- `call("tests.run")` — save dirty scenes first; Play Mode launches off
  the on-disk asset, not in-memory edits.

- `scene.create` — Create new scene in the project assets. Use 'scene-list-opened' tool to list all opened scenes after creation.
- `scene.get-data` — This tool retrieves the list of root GameObjects in the specified scene. Use 'scene-list-opened' tool to get the list of all opened scenes.

Path-scoped reads (token-saving): supply 'paths' (a list of…
- `scene.list-opened` — Returns the list of currently opened scenes in Unity Editor. Use 'scene-get-data' tool to get detailed information about a specific scene.
- `scene.open` — Open scene from the project asset file. Use 'assets-find' tool to find the scene asset first.
- `scene.save` — Save Opened scene to the asset file. Use 'scene-list-opened' tool to get the list of all opened scenes.
- `scene.set-active` — Set the specified opened scene as the active scene. Use 'scene-list-opened' tool to get the list of all opened scenes.
- `scene.unload` — Unload scene from the Opened scenes in Unity Editor. Use 'scene-list-opened' tool to get the list of all opened scenes.

## `screenshot`

Capture visual evidence from the Editor. Four surfaces map onto four different
"what would a human see right now?" questions — pick the one that matches the
intent, not the one whose parameters look most familiar:

- `screenshot.camera` — render from a specific scene `Camera` (Main Camera by
  default). No editor chrome, no HUD; whatever that camera sees.
- `screenshot.game-view` — exactly what the **Game** window is showing,
  including post-processing and play-mode UI. Reads the Game view's render
  texture directly, so the output size is the current Game-view resolution
  rather than a passed-in `width`/`height`.
- `screenshot.scene-view` — what the **Scene** tab is showing, gizmos and all.
  Use when the user is debugging editor-time placement or the gizmo overlay
  is the point of the capture.
- `screenshot.isolated` — render one GameObject in a synthetic scene with a
  computed camera frame and a controlled light rig. Best for thumbnail-style
  asset previews.

## Picking a surface

The four are not interchangeable. "What does my game look like right now"
almost always means `screenshot.game-view` — `screenshot.camera` will miss
post-processing or screen-space UI. Placement, lighting setup, or gizmo
questions mean `screenshot.scene-view`. A clean preview of a single asset
means `screenshot.isolated`. `screenshot.camera` is the right tool when a
specific in-scene camera (cinematic, security-prop, debug viewpoint) is the
subject.

## Cross-cluster overlap

- **Locating the target first** — `screenshot.camera` and `screenshot.isolated`
  both take a `GameObjectRef`. Resolve it through [`gameobject`](workflows.md)
  before calling; an unresolved ref is a much more common failure than the
  render itself.
- **Scene readiness** — none of the surfaces wait for a domain reload. After
  C# or asset writes, settle the Editor through
  [`assets.refresh`](assets.md) +
  [`editor`](editor.md) before capturing, or the
  image will reflect the pre-reload scene.
- **Play mode vs Edit mode** — only `screenshot.game-view` reflects play-mode
  HUD/UI; the others render from cameras under editor control.

## See also

- [`workflows`](workflows.md) — "Capture a screenshot for review".
- [`scene`](scene.md) — load or activate the right scene before capturing.
- [`recorder`](recorder.md) — pair `screenshot.game-view` for visual evidence
  at a moment with the recorder's numeric state at that same frame.

- `screenshot.camera` — Captures a screenshot from a camera and returns it as an image. If no camera is specified, uses the Main Camera. Returns the image directly for visual inspection by the LLM.
- `screenshot.game-view` — Captures a screenshot from the Unity Editor Game View and returns it as an image. Reads the Game View's own render texture directly via the Unity Editor API. The image size matches the current Game Vi…
- `screenshot.isolated` — Renders a screenshot of a target GameObject with configurable isolation, background, camera angle, and lighting. When isolated=true (default), only the target object is visible via layer-based culling…
- `screenshot.scene-view` — Captures a screenshot from the Unity Editor Scene View and returns it as an image. Returns the image directly for visual inspection by the LLM.

## `script`

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

- `script.delete` — Delete the script file(s). Does AssetDatabase.Refresh() and waits for Unity compilation to complete before reporting results. Use 'script-read' tool to read existing script files first.
- `script.execute` — Compiles and executes C# code dynamically using Roslyn. Inline-code calls are persisted to '{projectRoot}/Library/McpScripts/<fileName>.cs' (outside Unity's compiled paths; survives Editor restart) un…
- `script.read` — Reads the content of a script file and returns it as a string. Use 'script-update-or-create' tool to update or create script files.
- `script.update-or-create` — Updates or creates script file with the provided C# code. Does AssetDatabase.Refresh() at the end. Provides compilation error details if the code has syntax errors. Use 'script-read' tool to read exis…

## `system`

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

- `ping` — Lightweight readiness probe. Returns the input message or 'pong' if omitted.
- `system.editor-modal-respond` — Programmatically clicks a button on a native modal dialog previously surfaced by system-editor-modal-status. Refuses to click obviously-destructive buttons unless acknowledgeDestructive=true.
- `system.editor-modal-status` — Returns metadata for any native Win32 modal dialog currently blocking Unity's main thread. Use this to detect why a main-thread tool is hanging. Hwnd values are stringified Int64; pass them back verba…
- `system.job-list` — List every job ticket the gateway currently tracks (running plus completed-within-TTL). Returns slim entries — ticket_id, method, status, started_at. Use 'system-job-status' on a specific ticket id fo…
- `system.job-status` — Fetch the current status and (when finished) the final result of a long-running job ticket. Use this after a tool returns a Processing response carrying a ticket id. Returns NOT_FOUND-shaped error dat…
- `system.job-wait` — Block server-side until a job ticket reaches a terminal state or the timeout expires. Returns the same payload shape as system-job-status, plus a 'timed_out' flag and an optional 'status_filter_unmatc…

## `tests`

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

- `tests.cancel` — Cancel an in-flight tests-run job. Reads the captured TestRunner GUID from RunGuidRegistry and calls TestRunnerApi.CancelTestRun. On a successful cancel the ticket is completed by TestResultCollector.…
- `tests.run` — Execute Unity tests and return detailed results. Supports filtering by test mode, assembly, namespace, class, and method. Recommended to use 'EditMode' for faster iteration during development. Precond…

## `tool`

Live tool-registry surface. Use `tool.*` to enumerate the methods that are
currently registered with the gateway and to toggle individual tools on or off
at runtime. Pair this namespace with [`system`](system.md) for the basic
liveness / introspection plumbing, and with [`wiki`](wiki.md) for routing
rules.

## When to reach for `tool.*`

- **Discovery before scripting.** Call `tool.list` with a `regexSearch` to
  confirm a method exists under the name you expect before chaining several
  calls into a workflow. This is cheaper than reading the full root index and
  faster than trial-and-error against `call("<method>")`.
- **Mute a noisy or destructive tool.** Disable a single tool by name with
  `tool.set-enabled-state` when you want it kept out of agent reach for the
  rest of the session.
- **Restore the surface.** Re-enable with the same call, flipping `Enabled`
  back to `true`.

A disabled tool stops appearing in the gateway's advertised tool list and
returns an error if called by id. Enable state is persisted across the current
Editor session by the registry — there is no per-call override.

- `tool.list` — List all available MCP tools. Optionally filter by regex across tool names, descriptions, and arguments.

## `type`

Reflection-driven JSON Schema discovery for any C# type loaded in the Editor's
AppDomain. The single method `call("type.get-json-schema")` resolves a type by
full name, walks its public fields and properties, and emits a JSON Schema
describing the shape an `args` payload would need to hydrate an instance of
that type. Use it as the discovery step before authoring a structured payload
against an API surface you do not already have memorised.

The typical trigger is one of three patterns: (1) you are about to call
[`reflection.method-call`](reflection.md) on a method whose parameter is a
custom POCO or a Unity struct, and you need the field/property names; (2) you
are about to call [`object.modify`](object.md) or
[`gameobject.component-modify`](gameobject.md) with a `pathPatches` entry, and
you need the JSON path layout of a `[Serializable]` data model on the target
component; (3) you are inspecting an unfamiliar `[Serializable]` field bag (a
ScriptableObject schema, a custom asset, an MCP tool's input model) and want
the shape without grep-walking the source.

The type lookup runs through `System.Reflection`, so the type must be present
in a loaded assembly. Fully qualified names (`UnityEngine.Vector3`,
`UnityEditor.Animations.AnimatorControllerLayer`) are the safe bet; bare names
like `Vector3` resolve when there is exactly one match across loaded
assemblies. Generic types use the C# syntax — pass
`System.Collections.Generic.List<System.Int32>` rather than the CLR
back-tick form.

Discovery is read-only. Nothing is registered, no asset is touched, and the
call does not need to run on the main thread. The returned schema is a
serialized JSON string in the `result` field; parse it client-side to drive
payload construction.

## Cross-cluster overlap

- **Pair with `reflection.*`.** The canonical discovery chain is
  `call("type.get-json-schema")` → [`call("reflection.method-find")`](reflection.md)
  → `call("reflection.method-call")`. Use type-schema to learn what the
  parameter object looks like, use `method-find` to confirm the method exists
  and resolve its full signature, then use `method-call` to invoke it with a
  payload that matches both.
- **Pair with patch surfaces.** [`object.modify`](object.md),
  [`gameobject.component-modify`](gameobject.md), and `assets.modify` all take
  `pathPatches` whose `value` is a JSON document shaped like the field tree of
  the target. When that tree includes a nested `[Serializable]` struct, run
  `type.get-json-schema` against the struct's type name first to get the path
  segments and value shapes right on the first attempt.
- **Skip the schema for primitives.** Numbers, strings, booleans, and plain
  enums don't need a round-trip — feed them directly. Reach for
  `type.get-json-schema` when the payload is a nested object, a list of
  composites, or a Unity-specific struct like `Bounds`, `Quaternion`,
  `LayerMask`, or `AnimationCurve` whose JSON form is not obvious.

## See also

- [`reflection`](reflection.md) — the typical follow-up: `method-find` then
  `method-call` against the schema you just discovered.
- [`object`](object.md) — typed read/write of any `UnityEngine.Object` by
  `instanceID`; pair with type-schema when authoring `pathPatches` for
  unfamiliar component data.
- [`gameobject`](gameobject.md) — `gameobject.component-modify` accepts the
  same `pathPatches` shape and benefits from the same schema-first workflow.

- `type.get-json-schema` — Generates a JSON Schema for a given C# type name using reflection. Supports primitives, enums, arrays, generic collections, dictionaries, and complex objects. The type must be present in any loaded as…

