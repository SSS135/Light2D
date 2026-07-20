# Components and Architecture

## Frame Model

Light2D separates lighting inputs from normal scene rendering by *role*, not by
layer. Three roles — light sources, ambient emitters, and obstacles — are expressed
as self-registering components (see [Runtime Components](#runtime-components)). Each
frame it:

1. Renders obstacle opacity and color into an extended, low-resolution texture.
2. Renders direct light sprites against the obstacle texture.
3. Renders ambient emitters and advances a persistent ambient-propagation history.
4. Optionally blurs direct and ambient textures.
5. Composites the lighting textures over the source camera color.

The extended texture covers more world space than the source view so lights and obstacles just outside the screen can contribute. The small texture covers the visible camera area. Sampling positions are snapped to the lighting pixel grid to reduce shimmer while the camera moves.

![Render stages](Images/how_it_works.png)

## Register-and-Draw Core

Light2D uses a register-and-draw model rather than layers or a helper camera. A
component that participates in lighting registers itself with an internal registry
(`Light2DRenderRegistry`) in `OnEnable` and deregisters in `OnDisable`. During the
frame, `LightingSystem` (Built-in) and `Light2DUniversalRendererFeature` (URP) take
a snapshot of the registered components for each role and draw them into the
role's `RenderTexture` with `CommandBuffer.DrawMesh` (`Light2DDraw`). No source
camera, culling mask, or child camera is involved.

Every input component owns a disabled `MeshRenderer`: it builds the geometry the
render core draws, but its own renderer is kept `enabled = false` so lighting-input
geometry never appears in the game image. This invariant replaces the old
requirement to exclude Light2D layers from the source camera culling mask — there
are no layers to exclude, and inputs cannot leak into the composited frame.

Built-in composition materials (overlay, ambient compute, blur) are supplied by an
internal `Light2DDefaultResources` ScriptableObject that the creation window assigns
to a new `LightingSystem`. There is no `Lighting Camera` prefab and no
`LightingSystemPrefabConfig`.

## Pipeline Boundaries

### Built-in

`LightingSystem.OnRenderImage` owns the frame. It allocates `RenderTexture` instances through `LightingRenderResources`, draws the registered role components into them with command buffers (register-and-draw; there is no helper camera), and performs the final `Graphics.Blit` composition. `AffectOnlyThisCamera` temporarily redirects the source camera through an additional target.

### URP

`Light2DUniversalRendererFeature` owns rendering. Its RenderGraph pass derives culling and view/projection matrices from the source camera, draws the registered role components, imports ambient history, and replaces camera color. Contexts and history are keyed by `LightingSystem`, so multiple source cameras do not share ambient state. The feature is attached to renderer data, but enqueues only when the currently rendered camera itself has an enabled `LightingSystem`.

Universal Renderer Data and 2D Renderer Data integration is implemented. See [TESTING.md](TESTING.md#urp-verification-status) for the current URP verification status.

Do not route Built-in work through the renderer feature or URP work through `OnRenderImage`. `LightingSystem.IsScriptableRenderPipelineActive` prevents the Built-in backend from running under an SRP.

`LightingSystem.ActivePipeline` classifies the active pipeline as `Light2DPipeline.BuiltIn`, `Light2DPipeline.UniversalRenderPipeline`, or `Light2DPipeline.UnsupportedScriptableRenderPipeline`. `IsRenderingReady` reports false under an unsupported SRP, and enabling a `LightingSystem` there logs one actionable warning. XR cameras, URP Overlay cameras, and URP Base cameras with an active Overlay stack are rejected by the renderer feature with one warning per camera; independent Base cameras, including split screen, are supported.

## Runtime Components

### `LightingSystem`

Attach one to each source camera that needs Light2D. Important groups are:

- Coverage and resolution: `LightPixelSize`, `LightCameraSizeAdd`, `LightCameraFovAdd`, `LightObstaclesDistance`.
- Features: `EnableAmbientLight`, `BlurLightSources`, `BlurAmbientLight`, `HDR`, `LightObstaclesAntialiasing`, `EnableNormalMapping`, `AffectOnlyThisCamera`, `XZPlane`.
- Inputs: the registered role components (drawn through the registry) and the serialized compute, overlay, and blur materials. There are no layer-index fields.

Runtime rendering clones serialized materials. Change the source material assets for defaults or adjust the clones indirectly through `LightingSystem` inputs; do not depend on runtime writes mutating the asset.

### `LightSprite`

`LightSprite` derives from `CustomSprite` and generates a four-vertex mesh. It writes point or line origin data into UV1 for the light shader. The sprite and transform define coverage, while `LightOrigin.z` is the light height used by normal mapping.

Use the ray-marching materials named `Light2D/Light 20 Points` through `Light2D/Light 90 Points` to trade GPU work for shadow sampling. `Light2D/Light Auto Points` chooses a variable count and is the most expensive option. `Light2D/Light 1 Point` does minimal obstacle sampling; `Light2D/Light Ignoring Obstacles` skips obstacles and is suitable for effects such as particles.

### Obstacles

`LightObstacleSprite` mirrors a `SpriteRenderer` or `CustomSprite`. `LightObstacleMesh` mirrors a mesh and texture into a runtime-generated mesh/material. `LightObstacleGenerator` chooses one at play start and then destroys itself. With the `com.unity.2d.tilemap` package, `LightObstacleTilemap` (gated behind the `LIGHT2D_TILEMAP` define) turns a `Tilemap` into a texture-aware obstacle: it emits one obstacle draw per source-tile texture and maps each cell to that tile sprite's UVs, so the obstacle shader samples the tile alpha and transparent tile pixels pass light. Only collidable tiles occlude, and the obstacle mesh rebuilds when tiles change, not every frame. The `LightObstacleTilemap` lives on the same GameObject as the `Tilemap`; the visible `TilemapRenderer` stays enabled.

`MultiplicativeColor` is stored in vertex color. `AdditiveColor` is packed into UV1 for dual-color obstacle shaders. Textured obstacles default to white (the texture shapes the shadow); solid, untextured mesh obstacles default to opaque black so they cast a full shadow. These defaults are configurable in **Project Settings > Light2D**. Obstacles do not require colliders.

### Ambient Emitters

`LightAmbientEmitter` feeds the temporal ambient-propagation pass when
`LightingSystem.EnableAmbientLight` is on. It owns a `MeshFilter`/`MeshRenderer`
carrying the emission geometry and supports two modes: a sprite-mirroring mode that
bakes `EmissionColor` into an owned mesh, and a self-mesh mode (both sprite sources
empty) that draws the object's externally authored mesh untouched, preserving
per-vertex emission colors. It replaces the pre-3.0 convention of placing any
renderer on an ambient layer.

### `CustomSprite`

`CustomSprite` builds a quad from a Unity `Sprite`, uses `MeshFilter` and `MeshRenderer`, and reference-counts generated material instances by source material, texture, and UV mode. Generated meshes and materials use `DontSave`; they are runtime/editor working objects, not assets to edit.

## Quality Settings

| Setting | Effect and tradeoff |
| --- | --- |
| Light Pixel Size | World units per lighting pixel. Lower values increase texture dimensions, precision, memory, and GPU cost. |
| Light Texture Height | Inspector view of the derived height. Editing it adjusts Light Pixel Size. Recommended starting ranges are 50-200 mobile and 200-1000 desktop. |
| Light Camera Size Add | Orthographic world-space margin for off-screen contributors. Increase if lights clip near corners. |
| Light Camera Fov Add | Perspective field-of-view margin for off-screen contributors. |
| Camera To Light Obstacles Distance | Perspective depth plane used to derive texture coverage. Obstacles should share that effective depth. |
| 64 Bit Color | Uses half-float lighting textures when supported; otherwise falls back to 8-bit RGBA. In URP, camera and asset HDR settings also matter. |
| Light Obstacles Antialiasing | Renders obstacles at twice each dimension, then resolves them. This is roughly four times the obstacle texture pixel count. |
| Texture Filtering | `Point` preserves pixelated lighting; bilinear smooths upscaling. |
| Blur Light Sources | Adds a source blur pass. With normal mapping it can noticeably reduce normal detail. |
| Enable Ambient Light | Enables temporal ambient propagation and its persistent history textures. Disable it when unused. |
| Blur Ambient Light | Adds a blur after each ambient propagation cycle. |
| Normal Mapping | Renders full-camera normals and processes visible lights individually. It is substantially more expensive. |

Profile representative scenes on the target GPU. Historical device timings and fixed draw-call counts from pre-2.0 releases are not reliable guidance for Unity 6000.4 or URP RenderGraph.

## Material and Shader Contract

Serialized scenes and materials depend on shader names and property names. Preserve these contracts when changing shaders:

- Direct-light shaders use `_MainTex`, `_ObstacleMul`, and `_EmissionColorMul`; auto-points also uses `_StepCountMul`.
- Overlay shaders use `_GameTex`, `_AmbientLightTex`, `_LightSourcesTex`, `_LightSourcesMul`, `_LightMul`, `_AdditiveLightPow`, and `_AdditiveLightAdd`.
- Ambient propagation uses `_MainTex`, `_ObstacleMul`, `_ObstacleAdd`, `_EmissionColorMul`, and `_SamplingDist`.
- Normal-aware obstacle shaders use `_MainTex`, `_NormalTex`, and the `LightObstacle=True` SubShader tag.
- Runtime backends set global textures and vectors including `_ObstacleTex`, `_NormalsBuffer`, `_PixelsPerBlock`, `_ExtendedToSmallTextureScale`, and `_PosOffset`.
- URP-compatible object passes use `SRPDefaultUnlit`, `UniversalForward`, or `UniversalForwardOnly`; package shaders retain Built-in CG passes after their URP subshaders.

The misspelled shader name `Light2D/Obstacle Texture Post Porcessor` is a compatibility identifier and must not be corrected without a material and lookup migration.

## Normal Mapping

Use `Light2D/Transparent Normal Mapped` or `Light2D/Transparent Dual Color Normal Mapped`, assigning base and normal textures. Custom shaders must expose `_MainTex` and `_NormalTex` and include the `LightObstacle=True` SubShader tag to participate in replacement rendering.

Normal mapping is restricted to orthographic XY rendering in both backends by the current shared shader contract. Normal textures should not be packed into an atlas if their UV layout no longer matches the base sprite.
