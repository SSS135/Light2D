<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/object.md. -->

# `object`

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

To read a method's wiki, call `tools/call` with `arguments = { method: "<dotted>" }` (omit `args`). Include `args` (even `{}`) to execute.

## Methods

- `object.get-data` — Get data of the specified Unity Object. Returns serialized data of the object including its properties and fields. If need to modify the data use 'object-modify' tool.

Path-scoped reads (token-saving…
- `object.modify` — Modify the specified Unity Object. Allows direct modification of object fields and properties. Use 'object-get-data' first to inspect the object structure before modifying.

Three modification surface…

