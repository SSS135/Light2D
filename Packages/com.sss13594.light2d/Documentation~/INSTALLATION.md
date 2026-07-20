# Installation and Setup

## Requirements

- Unity 6000.3 (minimum); this repository’s development host is pinned to Unity 6000.5.4f1.
- Built-in Render Pipeline or Universal Render Pipeline 17.3.
- Optional: `com.unity.2d.tilemap` for the `LightObstacleTilemap` occluder.

The package declares URP 17.3 and uGUI 2.0 dependencies. Do not remove or change those dependencies when installing from source. Light2D 3.0 has no layer requirement — lighting roles are expressed as components (see [Add Content](#add-content)).

## Install from Git

In **Window > Package Management > Package Manager**, use **Install package from git URL...**:

```text
https://github.com/SSS135/Light2D.git?path=/Packages/com.sss13594.light2d
```

The package is embedded under `Packages/com.sss13594.light2d/` in the repository, so the `?path=` suffix is required; do not point Package Manager at the repository root. For reproducible projects, append a released tag or commit hash after the `path` query, for example:

```text
https://github.com/SSS135/Light2D.git?path=/Packages/com.sss13594.light2d#v2.0.0
```

## Install from Local Disk

Clone or extract this repository outside the consuming project’s generated directories. In Package Manager, use **Install package from disk...** and select the package `package.json` under `Packages/com.sss13594.light2d/`, not the repository root. You can also embed the folder directly under your project’s `Packages/`.

The equivalent `Packages/manifest.json` dependency uses an absolute or manifest-relative path to the embedded package folder:

```json
"com.sss13594.light2d": "file:X:/src/unity/Light2D/Packages/com.sss13594.light2d"
```

Unity’s package cache is read-only in normal use. Use a Git or local-disk package when modifying Light2D itself.

## Roles, Not Layers

Light2D 3.0 has no layers to create and no camera culling mask to edit. Each
lighting input is a component that registers itself when enabled and is drawn only
into Light2D's internal textures — never into the game image. Add the component for
the role you want (light source, ambient emitter, or obstacle); remove or disable it
to opt an object out.

## Built-in Render Pipeline

1. Confirm **Project Settings > Graphics > Scriptable Render Pipeline Settings** is empty.
2. Tag the receiving camera `MainCamera`.
3. Select **GameObject > Light2D > Lighting System** and **Create**.
4. Verify `LightingSystem` is on the source camera. Its overlay, ambient-compute, and blur material fields are assigned automatically from the internal `Light2DDefaultResources` asset.
5. Add a source with **GameObject > Light2D > Light Source**.
6. Select a `SpriteRenderer`, `MeshRenderer`, or another rendered object and use **GameObject > Light2D > Light Obstacle**.

No helper camera is created, and there is no layer or culling-mask step.

`Affect Only This Camera` is available only on this backend. Leave it off when the camera has a target texture.

## URP

Light2D’s URP backend is a URP 17 RenderGraph renderer feature. It supports both `UniversalRendererData` and `Renderer2DData`.

1. Confirm the active pipeline asset is a `UniversalRenderPipelineAsset`.
2. Confirm the source camera uses a Universal Renderer or 2D Renderer whose renderer data is a persistent, writable asset.
3. Tag the source camera `MainCamera` and run **GameObject > Light2D > Lighting System**.
4. Leave **Install Renderer Feature** enabled. The installer adds exactly one `Light2DUniversalRendererFeature` to the renderer selected by `MainCamera` and repairs duplicates.
5. If needed, select the camera and use **Tools > Light2D > Universal Render Pipeline > Install Renderer Feature**, or select **Install / Repair Renderer Feature** in the `LightingSystem` inspector.

The feature runs after transparents by default, requests an intermediate camera texture, and only enqueues for a source camera containing an enabled `LightingSystem`. It derives expanded culling and view/projection matrices from that source camera. If a camera selects a different renderer index through Universal Additional Camera Data, install the feature on that renderer data rather than only on the URP asset’s default renderer. There are no layers to exclude and no helper camera to assign.

Support for both Universal Renderer Data and 2D Renderer Data is implemented; see [Development and Testing](TESTING.md#urp-verification-status) for the current URP verification status.

For HDR output, enable HDR on the source camera and URP asset. Set the URP asset’s **HDR Color Buffer Precision** to **64 Bits** when `LightingSystem` **64 Bit Color** is enabled; otherwise the final camera composition reduces precision.

## Add Content

### Direct Lights

Use **GameObject > Light2D > Light Source** or add `LightSprite` manually. `Sprite` defines the light footprint; `Color`, transform, material, and `LightOrigin` shape the output. `Shape` supports point and line origins. The light registers itself when enabled; there is no layer to assign.

### Obstacles

Use **GameObject > Light2D > Light Obstacle** on selected objects with renderers. For a `SpriteRenderer` or `MeshRenderer` the command adds a child `LightObstacleSprite` or `LightObstacleMesh`; `LightObstacleGenerator` can instead pick the right one when play starts. With the `com.unity.2d.tilemap` package, selecting a `Tilemap` adds a `LightObstacleTilemap` in place on the same GameObject (the visible `TilemapRenderer` stays on). Obstacle default colors are configured in **Project Settings > Light2D**.

### Ambient Emitters

Add a `LightAmbientEmitter` with **GameObject > Light2D > Ambient Emitter** (or add the component manually). It contributes ambient emission when `LightingSystem.EnableAmbientLight` is on. Ambient propagation is temporal, so it warms up over multiple cycles and retains per-camera history.

## Import Samples

1. Open Package Manager and select **Light2D**.
2. Open the **Samples** section.
3. Select **Import** beside **Examples**.
4. Open a scene from `Assets/Samples/Light2D/2.0.0/Examples` (the **Small Examples**, **Rocket**, or **Rocket Tilemap** scene).

Imported samples are project-owned copies that register their lights, obstacles, and ambient emitters through the Light2D components automatically. The active pipeline asset is a project setting, so for URP verify or (re)install the renderer feature on the renderer used by the sample camera after import.
