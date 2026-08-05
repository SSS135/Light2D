# Light2D Examples

The sample contains four scenes:

- **Small Examples** cycles through direct lights, sprite and mesh obstacles, ambient emitters, blur and quality controls, and XY normal mapping. Primary click or one-finger tap changes the example. Secondary click or a two-finger tap changes light color.
- **Rocket** combines direct and ambient light with generated obstacle meshes. Use the arrow keys to fly, `Space` for landing gear, `C` for flares, `R` to restart, and `F1` to pause. On mobile, use the on-screen controls.
- **Rocket Tilemap** renders the same style of cave through a baked `Tilemap` occluded by a texture-aware `LightObstacleTilemap` (requires the `com.unity.2d.tilemap` package), so transparent tile pixels pass light.
- **Light Volume** demonstrates `Light2DVolume` coverage. The block world renders raw and full-brightness by default (`DefaultCoverage = 0`); a single Box volume re-enables Light2D over the cave, so the interior reads dark and is lit only by a mouse-controlled lantern while the surface stays untouched. Also requires `com.unity.2d.tilemap`.

## Render Pipeline Setup

For the Built-in Render Pipeline, open either scene and use its existing `LightingSystem` component.

For URP, add the Light2D renderer feature to the active Universal Renderer Data or 2D Renderer Data asset, then open a fresh copy of a sample scene. Each camera must be an independent Base camera. A second independent Base camera with its own viewport can be used to demonstrate split screen.

Light2D has no layers to configure: each light source, ambient emitter, and obstacle is a self-registering component whose own renderer stays disabled, so lighting inputs never draw into the game image. Use the `LightingSystem` quality fields to compare texture scale, antialiasing, blur, HDR, and ambient cycle settings.

## Support

- Desktop and mobile input need no project-side setup. The sample scripts read through the Input System package when it is present and fall back to the legacy `Input` class otherwise; every read site is double-guarded on `LIGHT2D_SAMPLES_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM` with a legacy `#else` branch, and `LIGHT2D_SAMPLES_INPUT_SYSTEM` comes from the `Light2D.Samples.Runtime` assembly's `versionDefines`. `com.unity.inputsystem` is a declared package dependency. The on-screen overlay is UI Toolkit (`UI/SampleUI.uxml` driven by `SampleUiController.cs`), not uGUI, but the scenes' `EventSystem` input modules are mixed: **Small Examples** uses `InputSystemUIInputModule`, while **Rocket**, **Rocket Tilemap**, and **Light Volume** use `StandaloneInputModule`. Set **Active Input Handling** (Player Settings) to **Both** — it is the only value that satisfies all four scenes.
- Orthographic XY normal mapping is supported.
- XR is not supported.
- URP Base/Overlay camera stacks are not supported. Use independent Base cameras instead.
