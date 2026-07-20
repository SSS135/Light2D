<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/particle-system.md. -->

# `particle-system`

Inspect and edit `UnityEngine.ParticleSystem` components on scene GameObjects. The
namespace is a focused wrapper around the per-module struct surface
(`MainModule`, `EmissionModule`, `ShapeModule`, …) that Unity exposes as
value-typed accessors — direct `gameobject.component-modify` calls cannot
reach those modules because writes to a returned struct are silently
discarded by the runtime. Reach for `particle-system.*` whenever the change
touches a module; fall back to `gameobject.component-*` only for top-level
component fields that live outside the module surface.

## Workflow

The two tools are a matched read / write pair built around a ReflectorNet
`SerializedMember` payload shape:

1. Call `particle-system.get` with the boolean `include*` flag for each
   module you intend to touch (or `includeAll = true` for an initial dump).
   The response includes the module's serialized members.
2. Mutate the returned `SerializedMember` for each module — keep only the
   properties you actually want to change.
3. Pass the trimmed payload back through `particle-system.modify` under the
   matching module parameter.

`modify` calls succeed silently when no module parameters are supplied; the
response `logs` array records which modules actually changed and why anything
was skipped.

## Cross-cluster overlap

- **`gameobject.component-*` for non-module fields** — top-level
  `ParticleSystem` state (`playOnAwake` lives in `MainModule`; `transform`
  lives on the GameObject) uses the standard `gameobject` namespace.
- **Renderer is a sibling component** — `ParticleSystemRenderer` lives on the
  same GameObject as a separate component. Both tools surface it under the
  `renderer` field as a convenience; reaching it through
  `gameobject.component-get` / `gameobject.component-modify` also works.

