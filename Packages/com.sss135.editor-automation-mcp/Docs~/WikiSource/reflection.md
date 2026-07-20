# reflection

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

### reflection.method-call

The `typeNameMatchLevel` and `methodNameMatchLevel` integers share one
ladder: `0` ignores the filter field, `1` is case-insensitive `contains`
(default), `2` is case-sensitive `contains`, `3`/`4` are
case-insensitive/sensitive `startsWith`, and `5`/`6` are
case-insensitive/sensitive equality. `parametersMatchLevel` is a separate
ladder: `0` ignores `filter.inputParameters`, `1` requires matching
parameter count, `2` requires full equality. Default is `2` here but `0`
on `method-find`, so an unconstrained `method-find` typically returns more
overloads than the same filter would invoke through `method-call`.

Set `executeInMainThread: true` when the call touches a Unity API (anything
under `UnityEngine.*` / `UnityEditor.*`, scene mutation, `Application.*`).
Without it the invocation runs on the MCP worker thread and most Unity APIs
throw or return stale results. Pure CLR helpers (`System.*` IO, string,
math) can stay on the worker thread for lower latency.

`targetObject` distinguishes instance from static dispatch: leave it `null`
for static methods. For instance methods, supply a `SerializedMember`
envelope whose `typeName` + nested `fields`/`props` reconstruct the
receiver, or leave it `null` and the resolver constructs a fresh instance
via the declaring type's parameterless constructor — which silently fails
for types without a public default constructor. When in doubt, hydrate
`targetObject` explicitly.
