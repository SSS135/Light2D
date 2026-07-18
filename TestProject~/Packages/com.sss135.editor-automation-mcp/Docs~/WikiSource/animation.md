# animation

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

### animation.modify

`modifications` is an ordered array of `AnimationModification` ops dispatched
by the `ModificationType` enum: `SetCurve`, `RemoveCurve`, `ClearCurves`,
`SetFrameRate`, `SetWrapMode`, `SetLegacy`, `AddEvent`, `ClearEvents`. Per-op
errors are collected into `response.errors` keyed by index — the remaining
ops still run and the clip is saved once at the end. `RemoveCurve` clears
and re-adds every other binding internally, so batch removals into a single
call when possible.
