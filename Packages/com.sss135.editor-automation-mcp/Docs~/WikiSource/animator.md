# animator

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

### animator.modify

Operations dispatch on `modifications[i].type`. Supported
`AnimatorModificationType` values: `AddParameter`, `RemoveParameter`,
`AddLayer`, `RemoveLayer`, `AddState`, `RemoveState`, `SetDefaultState`,
`AddTransition`, `RemoveTransition`, `AddAnyStateTransition`,
`SetStateMotion`, `SetStateSpeed`. Each op only reads the `AnimatorModification`
fields relevant to its type (`parameterName`, `layerName`, `stateName`,
`sourceStateName`, `destinationStateName`, `motionAssetPath`, `speed`,
`conditions`, transition timing); missing required fields fail that op only
and the error is reported in `errors[]` with the op's index. Other ops in the
same call still apply. Parameter types must be one of `Float`, `Int`, `Bool`,
`Trigger`; condition modes must be one of `If`, `IfNot`, `Greater`, `Less`,
`Equals`, `NotEqual`.
