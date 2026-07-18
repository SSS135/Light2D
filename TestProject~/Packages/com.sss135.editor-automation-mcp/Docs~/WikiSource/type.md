# type

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

### type.get-json-schema

`descriptionMode` and `propertyDescriptionMode` are independent knobs. Both
default to `Ignore`, which strips the human-readable `description` text and
returns the smallest schema. Set them to `Include` to keep descriptions on the
target type (and the target's own properties) only, or `IncludeRecursively` to
keep them inside `$defs` entries as well. Use `IncludeRecursively` when you
are reading the schema yourself for documentation purposes; leave both on
`Ignore` when feeding the schema into another tool's structured output.

`includeNestedTypes: true` switches from inline-everything to a `$defs`-with-
`$ref` layout. Prefer it for large or recursive type graphs — without it the
emitter inlines every nested object at every reference site and the output
explodes for types like `AnimatorController` or
`UnityEditor.SerializedProperty`. With it, each composite type appears once
in `$defs` and the body references it. Self-referential types
(`Transform.parent` returns `Transform`) require this flag; the inline path
hits a depth limit and truncates.

`writeIndented: true` adds whitespace for human reading; leave it `false` when
the consumer is another tool.
