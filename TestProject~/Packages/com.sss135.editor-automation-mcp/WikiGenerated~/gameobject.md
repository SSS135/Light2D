<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/gameobject.md. -->

# `gameobject`

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

To read a method's wiki, call `tools/call` with `arguments = { method: "<dotted>" }` (omit `args`). Include `args` (even `{}`) to execute.

## Methods

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

