# Light2D

Light2D is a GPU-based dynamic 2D lighting package for Unity. It renders light obstacles, direct lights, and optional ambient propagation into low-resolution textures, then composites the result over a camera. Lights and obstacles can move at runtime and do not require physics colliders.

Version 2.0.0 targets **Unity 6000.4** and is developed with **Unity 6000.4.7f1**. The package supports the Built-in Render Pipeline and URP 17.4. HDRP and other Scriptable Render Pipelines are not supported.

![Light2D result](Documentation~/Images/overall.png)

## Documentation

- [Installation and setup](Documentation~/INSTALLATION.md)
- [Components, settings, and render architecture](Documentation~/ARCHITECTURE.md)
- [Legacy `Assets/Light2D` migration](Documentation~/MIGRATION.md)
- [Troubleshooting](Documentation~/TROUBLESHOOTING.md)
- [Development and testing](Documentation~/TESTING.md)
- [Changelog](Documentation~/CHANGELOG.md)
- [License](LICENSE.md)

## Install

The repository root is the UPM package. Do not select a subdirectory.

**Git URL:** In **Window > Package Management > Package Manager**, select **Install package from git URL...** and enter:

```text
https://github.com/SSS135/Light2D.git
```

Pin a tag, branch, or commit by appending a revision, for example `#v2.0.0` when that tag is available.

**Local disk:** Select **Install package from disk...** and choose the repository-root `package.json`. A manifest entry for this checkout is:

```json
"com.sss13594.light2d": "file:X:/src/unity/Light2D"
```

Unity installs the package dependencies declared in `package.json`, including URP 17.4 even when the consuming project uses the Built-in Render Pipeline.

## First Setup

1. Create three distinct layers for light sources, ambient emitters, and light obstacles in **Edit > Project Settings > Tags and Layers**.
2. Ensure the source camera is tagged `MainCamera`.
3. Select **GameObject > Light2D > Lighting System** and assign the three layers.
4. Exclude all three Light2D layers from the source camera culling mask. The creation window does this automatically.
5. Add a light with **GameObject > Light2D > Light Source** and create obstacles from selected rendered objects with **GameObject > Light2D > Light Obstacle**.

The pipeline-specific steps differ:

- **Built-in:** the creation window instantiates and assigns the package’s hidden lighting camera. Keep that camera disabled for normal camera rendering; `LightingSystem` renders it explicitly.
- **URP:** install `Light2DUniversalRendererFeature` on the renderer selected by the source camera. The feature derives culling and matrices from that camera; do not create or assign a helper `LightCamera`. The feature supports both Universal Renderer Data and 2D Renderer Data. See the [URP setup](Documentation~/INSTALLATION.md#urp).

A newly configured scene can appear black until it contains a light source or an ambient emitter.

## Samples

Open Package Manager, select **Light2D**, expand **Samples**, and import **Examples**. Unity copies the sample to `Assets/Samples/Light2D/2.0.0/Examples`. It contains the **Small Examples** and **Rocket** scenes and their supporting assets.

The sample scenes are legacy demonstrations. Confirm their layers, camera culling masks, material assignments, and URP renderer feature after import before using them as production templates.

## Core Concepts

- `LightingSystem` belongs on the camera receiving the final composite. It owns quality settings, layer indices, material references, and per-camera rendering state.
- `LightSprite` renders a sprite-shaped point or line light. Ray-marching materials trade GPU cost for more obstacle samples; `Light 1 Point` and `Light Ignoring Obstacles` are cheaper alternatives.
- `LightObstacleSprite` mirrors a parent sprite or `CustomSprite` into the obstacle layer. `LightObstacleMesh` mirrors mesh data. `LightObstacleGenerator` creates the appropriate child obstacle at runtime.
- Any renderer on the ambient-light layer contributes ambient emission when ambient lighting is enabled.
- Obstacle RGB controls transmitted light by channel. Dark values block more light; colored obstacles filter channels. Alpha participates in obstacle rendering according to the selected material.
- The overlay and ambient materials expose the artistic controls. Runtime code clones serialized materials, so per-camera updates do not mutate project assets.

Light texture resolution is derived from camera coverage and `Light Pixel Size`. A smaller value creates more pixels per world unit and costs more GPU time and memory. Start with a displayed texture height of 50-200 on mobile or 200-1000 on desktop, then profile the target device.

## Pipeline Support

| Pipeline | Status | Backend |
| --- | --- | --- |
| Built-in | Supported | `OnRenderImage` plus an explicitly rendered helper camera |
| URP 17.4 | Implemented; behavioral validation blocked on the pinned host | RenderGraph renderer feature on Universal or 2D Renderer Data |
| HDRP or another SRP | Not supported | `LightingSystem` rejects non-URP SRPs |

Current limitations include:

- URP uses the source camera plus its selected renderer's `Light2DUniversalRendererFeature`; a helper `LightCamera` is a Built-in-only requirement.
- Normal mapping is orthographic XY-only in both backends, and the inspector warns against mobile use.
- Perspective cameras are supported for basic lighting, but depend on one `Camera To Light Obstacles Distance` plane and do not support normal mapping.
- `Affect Only This Camera` is a Built-in-only composition mode and is incompatible with a source camera target texture. In URP, the feature already runs per camera.
- URP skips preview cameras, reflection cameras, and frames rendering directly to the active back buffer. The feature requests an intermediate color texture.
- Very small obstacles can disappear at low light-texture resolutions. Off-screen lights can clip if the lighting camera margin is too small.
- The package has 17 EditMode tests, two Built-in behavioral PlayMode tests, and a URP PlayMode test. The EditMode and Built-in PlayMode coverage has been exercised on the development host. URP behavior remains unverified because a Unity 6000.4.7f1/ShaderGraph 17.4 global-settings import defect prevents a clean URP validation environment.

## Package Layout

| Path | Purpose |
| --- | --- |
| `Runtime/` | Components, materials, resources, shaders, and the URP renderer feature |
| `Editor/` | Creation tools, inspectors, renderer-feature installer, and migration utility |
| `Samples~/Examples/` | Importable demonstration scenes and assets |
| `Documentation~/` | Package documentation, changelog, and illustrations |
| `Tests/` | EditMode and PlayMode test assemblies |
| `TestProject~/` | Development host, test automation, and URP sentinel; excluded from the UPM payload by its trailing `~` |

The package name is `com.sss13594.light2d`. Runtime types are in the `Light2D` namespace and compile into `Light2D.Runtime`; editor tooling compiles into `Light2D.Editor`.

## License

Light2D is available under the [MIT License](LICENSE.md).
