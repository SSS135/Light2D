<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/animation.md. -->

# `animation`

Author and edit Unity `AnimationClip` assets (`.anim` files under `Assets/`).
The namespace is asset-scoped — for runtime `Animator` state-machine wiring on
an `AnimatorController` asset, reach for the sibling `animator.*` namespace
instead.

Typical workflow is `call("animation-create")` to mint empty clips,
`call("animation-get-data")` to inspect existing curve bindings / events /
frame rate / wrap mode, then `call("animation-modify")` to mutate the clip in
place. `animation-modify` consumes a sequence of typed operations (the
`ModificationType` enum) rather than a full clip overwrite, so callers can
add one curve or flip the frame rate without rebuilding the rest of the clip.
Property names and component types fed to `SetCurve` / `RemoveCurve` should
be sourced from a prior `animation-get-data` response so they match the
exact strings Unity expects.

To read a method's wiki, call `tools/call` with `arguments = { method: "<dotted>" }` (omit `args`). Include `args` (even `{}`) to execute.

## Methods

- `animation.create` — Create Unity's Animation asset files (AnimationClip). Creates folders recursively if they do not exist. Each path should start with 'Assets/' and end with '.anim'.
- `animation.get-data` — Get data about a Unity AnimationClip asset file. Returns information such as name, length, frame rate, wrap mode, animation curves, and events.
- `animation.modify` — Modify Unity's AnimationClip asset. Apply an array of modifications including setting curves, clearing curves, setting properties, and managing animation events. Use 'animation-get-data' tool to get v…

