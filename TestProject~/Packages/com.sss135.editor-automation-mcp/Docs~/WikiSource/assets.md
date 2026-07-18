# assets

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

### assets.refresh

Forces an `AssetDatabase.Refresh()` from C# instead of waiting for the
Editor to notice file changes on next focus. Use this after a tool wrote
`.prefab`, `.asset`, or other non-script files on disk that Unity must
re-import before subsequent calls observe them. For `.cs` compile checks,
prefer `compile.request`.

When the Editor's `kAutoRefreshMode` preference is disabled (Preferences →
Asset Pipeline → Auto Refresh = Disabled), the response includes a
`warnings` array noting that background script reload will not run until
the user manually focuses the Editor or invokes a recompile. The tool
itself still runs synchronously; the warning is purely advisory.

### assets.prefab-open

Enters Prefab Stage mode for the prefab backing the given scene instance.
Every `open` must be paired with a `call("assets.prefab-close")` — leaving
the Stage open silently redirects subsequent scene-mode calls
(`gameobject.*`, `scene.*`, `assets.prefab-instantiate`) to the Stage
hierarchy rather than the live scene. That redirection is invisible in the
response payload, so a missing `close` typically surfaces as a later
"object disappeared" or "edit went to the wrong place" symptom.

`assets.prefab-close` takes a `save` flag; pass `true` to persist edits
made inside the Stage, `false` to discard. Prefer `close(save: true)` over
a trailing `save` + `close(save: false)` pair when you're done editing —
fewer round trips and the same on-disk result.

### assets.prefab-save

Writes the currently open Prefab Stage to disk. Only meaningful while a
Stage is open via `assets.prefab-open`; outside the Stage there is nothing
to save and the call has no effect. Prefer
`assets.prefab-close(save: true)` when you're done editing — `save` is for
mid-edit checkpoints where you want to flush state without leaving the
Stage (for example, between batched `gameobject.*` mutations that you
might want to verify before continuing).

Save does not exit the Stage, so any follow-up `gameobject.*` or
`component.*` call still targets the asset, not the scene. If you wanted
to verify the on-disk result against a fresh instance, save, close, then
`assets.prefab-instantiate` — running the verify pass while the Stage is
still open will inspect the in-memory edit tree rather than the file
Unity wrote.

### assets.prefab-instantiate

Spawns the prefab into the **currently active Scene**, not into the Prefab
Stage. If a Stage is open, close it first or the resulting GameObject will
land inside the Stage hierarchy rather than the scene the caller expects —
this is the most common failure mode for "I instantiated the prefab but I
can't find it in the scene". The response returns a `GameObjectRef`
suitable for follow-up `gameobject.*` and `component.*` calls on the live
instance.

`connectGameObjectToPrefab: true` plus a `gameObjectRef` of an existing
prefab instance produces a Variant rather than a plain instance; the same
flag with a non-prefab source GameObject produces a regular prefab
connection. Mind the asymmetry when scripting bulk instantiate flows.
