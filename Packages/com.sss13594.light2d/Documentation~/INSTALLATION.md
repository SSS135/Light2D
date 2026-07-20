# Installation and Setup

## Requirements

- Unity 6000.4; this repository’s development host is pinned to Unity 6000.5.4f1.
- Built-in Render Pipeline or Universal Render Pipeline 17.4.
- Three free, distinct Unity layers.

The package declares URP 17.4 and uGUI 2.0 dependencies. Do not remove or change those dependencies when installing from source.

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

## Required Layers

Create three distinct layers in **Edit > Project Settings > Tags and Layers**:

- Light sources
- Ambient light
- Light obstacles

Names are your choice; `LightingSystem` serializes layer indices. Do not reuse one layer for multiple roles. Exclude all three from the source camera culling mask, otherwise the inputs are drawn directly as scene content as well as into Light2D’s textures.

## Built-in Render Pipeline

1. Confirm **Project Settings > Graphics > Scriptable Render Pipeline Settings** is empty.
2. Tag the receiving camera `MainCamera`.
3. Select **GameObject > Light2D > Lighting System**.
4. Choose the three layers and select **Create**.
5. Verify `LightingSystem` is on the source camera and its `Lighting Camera` field references the generated child camera.
6. Keep the child lighting camera disabled. `LightingSystem` configures and renders it explicitly.
7. Add a source with **GameObject > Light2D > Light Source**.
8. Select a `SpriteRenderer`, `MeshRenderer`, or another rendered object and use **GameObject > Light2D > Light Obstacle**.

The setup command assigns the package overlay, ambient-compute, and blur materials from `Runtime/Resources/Lighting Camera.prefab`. It also removes the three Light2D layers from the source camera culling mask.

`Affect Only This Camera` is available only on this backend. Leave it off when the camera has a target texture.

## URP

Light2D’s URP backend is a URP 17 RenderGraph renderer feature. It supports both `UniversalRendererData` and `Renderer2DData`.

1. Confirm the active pipeline asset is a `UniversalRenderPipelineAsset`.
2. Confirm the source camera uses a Universal Renderer or 2D Renderer whose renderer data is a persistent, writable asset.
3. Tag the source camera `MainCamera` and run **GameObject > Light2D > Lighting System**.
4. Leave **Install Renderer Feature** enabled. The installer adds exactly one `Light2DUniversalRendererFeature` to the renderer selected by `MainCamera` and repairs duplicates.
5. If needed, select the camera and use **Tools > Light2D > Universal Render Pipeline > Install Renderer Feature**, or select **Install / Repair Renderer Feature** in the `LightingSystem` inspector.
6. Exclude the three Light2D layers from the source camera culling mask.
7. Leave `LightingSystem.LightCamera` unassigned. It is used only by the Built-in backend.

The feature runs after transparents by default, requests an intermediate camera texture, and only enqueues for a source camera containing an enabled `LightingSystem`. It derives expanded culling and view/projection matrices from that source camera. If a camera selects a different renderer index through Universal Additional Camera Data, install the feature on that renderer data rather than only on the URP asset’s default renderer.

Support for both Universal Renderer Data and 2D Renderer Data is implemented. Behavioral validation of both paths is currently blocked in the development host by a Unity 6000.4.7f1/ShaderGraph 17.4 global-settings import defect; see [Development and Testing](TESTING.md#current-urp-validation-blocker).

For HDR output, enable HDR on the source camera and URP asset. Set the URP asset’s **HDR Color Buffer Precision** to **64 Bits** when `LightingSystem` **64 Bit Color** is enabled; otherwise the final camera composition reduces precision.

## Add Content

### Direct Lights

Use **GameObject > Light2D > Light Source** or add `LightSprite` manually. Set its GameObject to the configured source layer. `Sprite` defines the light footprint; `Color`, transform, material, and `LightOrigin` shape the output. `Shape` supports point and line origins.

### Obstacles

Use **GameObject > Light2D > Light Obstacle** on selected objects with renderers. The command creates a child `LightObstacleSprite`; `LightObstacleGenerator` can instead generate a sprite or mesh obstacle when play starts. Ensure the resulting child is on the configured obstacle layer.

### Ambient Emitters

Place any compatible renderer on the configured ambient layer. Its rendered color becomes ambient emission. Ambient propagation is temporal, so it warms up over multiple cycles and retains per-camera history.

## Import Samples

1. Open Package Manager and select **Light2D**.
2. Open the **Samples** section.
3. Select **Import** beside **Examples**.
4. Open a scene from `Assets/Samples/Light2D/2.0.0/Examples`.

Imported samples are project-owned copies. Layer indices and pipeline assets are project settings, so verify the `LightingSystem` layers, camera masks, materials, and URP feature after import.
