# gameobject

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

### gameobject.modify

Three modification surfaces stack per GameObject — when more than one is
supplied for the same ref the handler applies them in a fixed order:
`jsonPatchesPerGameObject` → `pathPatchesPerGameObject` → `gameObjectDiffs`.
At least one of the three is required. All three arrays are parallel to
`gameObjectRefs`; pass `null` at an index to skip that surface for that
GameObject.

Path syntax across `pathPatches` and the `[i]`/`[key]` JSON Merge Patch
extension is uniform: plain segment for fields/properties, `[i]` for
list/array index, `[key]` for dictionary entry, leading `#/` stripped. To edit
component-owned fields, prefer
[`gameobject.component-modify`](#gameobjectcomponent-modify) — the GameObject
surface ignores component-internal fields silently and reports them in the
returned log entries.

### gameobject.component-modify

Three modification surfaces — when more than one is supplied they run in this
order: `jsonPatch` → `pathPatches` → `componentDiff`. At least one is
required.

- `componentDiff` is the full SerializedMember envelope (legacy, backwards
  compatible). Include only the `fields` / `props` you want to change; unknown
  or wrongly-located members are surfaced in the response `Logs`, not
  rejected.
- `pathPatches` is a list of `{Path, Value}` entries routed through
  `Reflector.TryModifyAt`. Each entry is atomic and can target a different
  depth; multiple entries on the same call walk to their own targets
  independently.
- `jsonPatch` is a JSON Merge Patch (RFC 7396) string, extended with the
  `[i]` / `[key]` keys for array indices and dictionary entries. Use `$type`
  for compatible-subtype replacement at a polymorphic slot.

Path syntax is uniform across `pathPatches` and the JSON Merge Patch
extension: `fieldName`, `nested/field`, `arrayField/[i]`, `dictField/[key]`;
a leading `#/` is stripped. Run `gameobject.component-get` with `paths` or
`viewQuery` to confirm the live field names before authoring the patch —
`Logs` will tell you what was ignored, but only after the round trip.
