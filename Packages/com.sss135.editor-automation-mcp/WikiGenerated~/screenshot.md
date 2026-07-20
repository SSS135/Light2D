<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/screenshot.md. -->

# `screenshot`

Capture visual evidence from the Editor. Four surfaces map onto four different
"what would a human see right now?" questions — pick the one that matches the
intent, not the one whose parameters look most familiar:

- `screenshot.camera` — render from a specific scene `Camera` (Main Camera by
  default). No editor chrome, no HUD; whatever that camera sees.
- `screenshot.game-view` — exactly what the **Game** window is showing,
  including post-processing and play-mode UI. Reads the Game view's render
  texture directly, so the output size is the current Game-view resolution
  rather than a passed-in `width`/`height`.
- `screenshot.scene-view` — what the **Scene** tab is showing, gizmos and all.
  Use when the user is debugging editor-time placement or the gizmo overlay
  is the point of the capture.
- `screenshot.isolated` — render one GameObject in a synthetic scene with a
  computed camera frame and a controlled light rig. Best for thumbnail-style
  asset previews.

## Picking a surface

The four are not interchangeable. "What does my game look like right now"
almost always means `screenshot.game-view` — `screenshot.camera` will miss
post-processing or screen-space UI. Placement, lighting setup, or gizmo
questions mean `screenshot.scene-view`. A clean preview of a single asset
means `screenshot.isolated`. `screenshot.camera` is the right tool when a
specific in-scene camera (cinematic, security-prop, debug viewpoint) is the
subject.

## Cross-cluster overlap

- **Locating the target first** — `screenshot.camera` and `screenshot.isolated`
  both take a `GameObjectRef`. Resolve it through [`gameobject`](workflows.md)
  before calling; an unresolved ref is a much more common failure than the
  render itself.
- **Scene readiness** — none of the surfaces wait for a domain reload. After
  C# or asset writes, settle the Editor through
  [`assets.refresh`](assets.md) +
  [`editor`](editor.md) before capturing, or the
  image will reflect the pre-reload scene.
- **Play mode vs Edit mode** — only `screenshot.game-view` reflects play-mode
  HUD/UI; the others render from cameras under editor control.

## See also

- [`workflows`](workflows.md) — "Capture a screenshot for review".
- [`scene`](scene.md) — load or activate the right scene before capturing.
- [`recorder`](recorder.md) — pair `screenshot.game-view` for visual evidence
  at a moment with the recorder's numeric state at that same frame.

To read a method's wiki, call `tools/call` with `arguments = { method: "<dotted>" }` (omit `args`). Include `args` (even `{}`) to execute.

## Methods

- `screenshot.camera` — Captures a screenshot from a camera and returns it as an image. If no camera is specified, uses the Main Camera. Returns the image directly for visual inspection by the LLM.
- `screenshot.game-view` — Captures a screenshot from the Unity Editor Game View and returns it as an image. Reads the Game View's own render texture directly via the Unity Editor API. The image size matches the current Game Vi…
- `screenshot.isolated` — Renders a screenshot of a target GameObject with configurable isolation, background, camera angle, and lighting. When isolated=true (default), only the target object is visible via layer-based culling…
- `screenshot.scene-view` — Captures a screenshot from the Unity Editor Scene View and returns it as an image. Returns the image directly for visual inspection by the LLM.

