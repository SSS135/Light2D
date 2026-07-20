<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/assets.md. -->

# `assets`

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

To read a method's wiki, call `tools/call` with `arguments = { method: "<dotted>" }` (omit `args`). Include `args` (even `{}`) to execute.

## Methods

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

