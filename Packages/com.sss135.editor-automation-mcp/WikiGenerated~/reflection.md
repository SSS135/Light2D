<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/reflection.md. -->

# `reflection`

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

To read a method's wiki, call `tools/call` with `arguments = { method: "<dotted>" }` (omit `args`). Include `args` (even `{}`) to execute.

## Methods

- `reflection.method-call` — Call C# method. Any method could be called, even private methods. It requires to receive proper method schema. Use 'reflection-method-find' to find available method before using it. Receives input par…
- `reflection.method-find` — Find method in the project using C# Reflection. It looks for all assemblies in the project and finds method by its name, class name and parameters. Even private methods are available. Use 'reflection-…

