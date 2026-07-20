<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/animator.md. -->

# `animator`

Operate on Unity `AnimatorController` assets (`.controller` files under
`Assets/`) — the state-machine graphs that drive `Animator` components. For
`AnimationClip` assets and the legacy `Animation` component, use the sibling
[`animation`](animation.md) namespace instead.

Typical workflow is `animator-create` to author empty controllers, then
`animator-get-data` to inspect the current graph (parameters, layers, states,
transitions, conditions, default state, any-state transitions), then
`animator-modify` to mutate it in place. Inspect-then-modify is the supported
loop: `animator-modify` takes a sequence of typed operations keyed by the
`AnimatorModificationType` enum, and each operation references existing layers
and states by name — round-trip the names through `animator-get-data` rather
than guessing.

The returned data model is nested: a controller has parameters and layers;
each layer owns a state machine with states, a default state, and any-state
transitions; each state owns transitions; each transition owns conditions.
`animator-modify` operations target one level of that tree at a time —
there's no bulk-replace verb.

## See also

- [`animation`](animation.md) — `AnimationClip` and `Animation` (legacy)
  component operations.
- [`assets`](assets.md) — generic asset discovery, copy/move/delete, and
  refresh; use it to locate `.controller` paths before calling
  `animator-get-data`.

To read a method's wiki, call `tools/call` with `arguments = { method: "<dotted>" }` (omit `args`). Include `args` (even `{}`) to execute.

## Methods

- `animator.create` — Create Unity's AnimatorController asset files. Creates folders recursively if they do not exist. Each path should start with 'Assets/' and end with '.controller'.
- `animator.get-data` — Get data about a Unity AnimatorController asset file. Returns information such as name, layers, parameters, and states.
- `animator.modify` — Modify Unity's AnimatorController asset. Apply an array of modifications including adding/removing parameters, layers, states, and transitions. Use 'animator-get-data' tool to get valid names and para…

