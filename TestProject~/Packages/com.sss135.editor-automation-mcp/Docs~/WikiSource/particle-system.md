# particle-system

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

### particle-system.get

Defaults to returning only the `MainModule` plus the live state snapshot
(`isPlaying`, `isPaused`, `particleCount`, `time`). Every other module is
opt-in via its `include<Module>` flag — set `includeAll = true` for an
unfiltered dump. `deepSerialization = true` recurses into nested objects
(curves, gradients, sub-emitter references); the default top-level
serialization is cheaper and usually enough for diffing.

When the GameObject has multiple `ParticleSystem` components, pass
`componentRef` to disambiguate — otherwise the tool picks the first match
order-of-`GetComponents` returns.

### particle-system.modify

Module parameters expect the `SerializedMember` shape returned by
`particle-system.get`; omit any property you don't want changed. The tool
re-fetches each module via `ps.<module>`, applies the patch through
`ReflectorNet.TryModify`, and writes the boxed struct back — bypassing the
Unity gotcha where `ps.main.startSpeed = …` is a no-op because `ps.main`
returns a copy.

The system is **not** stopped or restarted around the edit, so live particles
keep their existing values until the next emission tick. The Editor is
marked dirty and all editor windows repaint, but the scene is not saved —
pair with `editor.save-scene` (or equivalent) when the change must persist.
