<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/scene.md. -->

# `scene`

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

To read a method's wiki, call `tools/call` with `arguments = { method: "<dotted>" }` (omit `args`). Include `args` (even `{}`) to execute.

## Methods

- `scene.create` — Create new scene in the project assets. Use 'scene-list-opened' tool to list all opened scenes after creation.
- `scene.get-data` — This tool retrieves the list of root GameObjects in the specified scene. Use 'scene-list-opened' tool to get the list of all opened scenes.

Path-scoped reads (token-saving): supply 'paths' (a list of…
- `scene.list-opened` — Returns the list of currently opened scenes in Unity Editor. Use 'scene-get-data' tool to get detailed information about a specific scene.
- `scene.open` — Open scene from the project asset file. Use 'assets-find' tool to find the scene asset first.
- `scene.save` — Save Opened scene to the asset file. Use 'scene-list-opened' tool to get the list of all opened scenes.
- `scene.set-active` — Set the specified opened scene as the active scene. Use 'scene-list-opened' tool to get the list of all opened scenes.
- `scene.unload` — Unload scene from the Opened scenes in Unity Editor. Use 'scene-list-opened' tool to get the list of all opened scenes.

