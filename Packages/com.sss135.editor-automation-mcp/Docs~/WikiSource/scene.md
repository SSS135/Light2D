# scene

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

### scene.open

`loadSceneMode` selects between three load shapes: `Single` closes every
currently open scene before loading the target (unsaved edits in those
scenes are lost — save first); `Additive` keeps existing scenes open and
loads the target alongside them; `AdditiveWithoutLoading` registers the
scene in the Editor's scene list without actually loading its
GameObjects, which is the right choice when you only need the entry to
exist for build settings or later activation. The response is the full
post-call scene list, not just the newly opened scene.
