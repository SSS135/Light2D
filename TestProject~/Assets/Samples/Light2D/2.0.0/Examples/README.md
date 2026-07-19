# Light2D Examples

The sample contains two scenes:

- **Small Examples** cycles through direct lights, sprite and mesh obstacles, ambient emitters, blur and quality controls, and XY normal mapping. Primary click or one-finger tap changes the example. Secondary click or a two-finger tap changes light color.
- **Rocket** combines direct and ambient light with generated obstacle meshes. Use the arrow keys to fly, `Space` for landing gear, `C` for flares, `R` to restart, and `F1` to pause. On mobile, use the on-screen controls.

## Render Pipeline Setup

For the Built-in Render Pipeline, open either scene and use its existing `LightingSystem` component.

For URP, add the Light2D renderer feature to the active Universal Renderer Data or 2D Renderer Data asset, then open a fresh copy of a sample scene. Each camera must be an independent Base camera. A second independent Base camera with its own viewport can be used to demonstrate split screen.

The source camera must exclude the configured light source, ambient emitter, and obstacle layers. Use the `LightingSystem` quality fields to compare texture scale, antialiasing, blur, HDR, and ambient cycle settings.

## Support

- Desktop and mobile input are supported without an Input System package dependency. In Player Settings, set **Active Input Handling** to **Input Manager (Old)** or **Both**.
- Orthographic XY normal mapping is supported.
- XR is not supported.
- URP Base/Overlay camera stacks are not supported. Use independent Base cameras instead.
