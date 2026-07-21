# Light2D Examples

The sample contains three scenes:

- **Small Examples** cycles through direct lights, sprite and mesh obstacles, ambient emitters, blur and quality controls, and XY normal mapping. Primary click or one-finger tap changes the example. Secondary click or a two-finger tap changes light color.
- **Rocket** combines direct and ambient light with generated obstacle meshes. Use the arrow keys to fly, `Space` for landing gear, `C` for flares, `R` to restart, and `F1` to pause. On mobile, use the on-screen controls.
- **Rocket Tilemap** renders the same style of cave through a baked `Tilemap` occluded by a texture-aware `LightObstacleTilemap` (requires the `com.unity.2d.tilemap` package), so transparent tile pixels pass light.

## Render Pipeline Setup

For the Built-in Render Pipeline, open either scene and use its existing `LightingSystem` component.

For URP, add the Light2D renderer feature to the active Universal Renderer Data or 2D Renderer Data asset, then open a fresh copy of a sample scene. Each camera must be an independent Base camera. A second independent Base camera with its own viewport can be used to demonstrate split screen.

Light2D has no layers to configure: each light source, ambient emitter, and obstacle is a self-registering component whose own renderer stays disabled, so lighting inputs never draw into the game image. Use the `LightingSystem` quality fields to compare texture scale, antialiasing, blur, HDR, and ambient cycle settings.

## Support

- Desktop and mobile input work without forcing an Input System package dependency. The sample scripts read through the Input System package automatically when it is present in the project (the `Light2D.Samples.Runtime` assembly gates this on the package), and fall back to the legacy `Input` class otherwise. Because the sample scenes drive their on-screen touch UI through uGUI's legacy input modules, keep **Active Input Handling** (Player Settings) set to **Input Manager (Old)** or **Both**; the scenes' uGUI does not run under **Input System Package (New)** only.
- Orthographic XY normal mapping is supported.
- XR is not supported.
- URP Base/Overlay camera stacks are not supported. Use independent Base cameras instead.
