# Troubleshooting

## Black or Unlit Output

- Add at least one `LightSprite` or `LightAmbientEmitter`; a scene with no lighting input can composite to black.
- Confirm the receiving camera has an enabled `LightingSystem` and its material references are assigned.
- Confirm each obstacle, light, and ambient emitter carries its Light2D component and the component is enabled — an input participates in lighting only if it does.
- In URP, confirm `Light2DUniversalRendererFeature` is installed on the renderer actually selected by the camera (see below).

## URP Feature Missing or on the Wrong Renderer

If a camera has an enabled `LightingSystem` under URP but no `Light2DUniversalRendererFeature` is installed on its renderer, lighting is a silent no-op; `LightingSystem` logs a warning pointing here. The `LightingSystem` inspector reports missing, duplicate, wrong-renderer, and unsupported-renderer states. Select the source camera and use **Install / Repair Renderer Feature**, or run **Tools > Light2D > Universal Render Pipeline > Install Renderer Feature**.

The renderer data must be a persistent writable asset. A camera can override the URP asset’s default renderer through Universal Additional Camera Data; the feature must be installed on that selected renderer. Only Universal Renderer Data and 2D Renderer Data are supported.

## Light Inputs Are Visible as Ordinary Sprites

This does not happen in Light2D 2.0. Each input component keeps its own `MeshRenderer` disabled and is drawn only into Light2D's textures, so lighting-input geometry never leaks into the game image — there are no layers to exclude. If an obstacle or light source is visibly rendering as an ordinary sprite, the visible renderer is a separate, still-enabled component on the object (for example the original `SpriteRenderer` you want the obstacle to mirror). Disable or remove that renderer if it should not appear.

## Small Obstacles or Edge Lights Disappear

Decrease `Light Pixel Size` to raise lighting resolution. Increase `Light Camera Size Add` for orthographic cameras or `Light Camera Fov Add` for perspective cameras when contributors clip near the screen edge. Both changes increase rendering cost; profile after adjustment.

## Poor Performance or Excess Memory

- Increase `Light Pixel Size`.
- Disable ambient lighting when unused.
- Disable source or ambient blur.
- Disable obstacle antialiasing.
- Disable 64-bit color when HDR precision is unnecessary.
- Use a lower-point direct-light shader.
- Avoid normal mapping unless its full-resolution normal buffer and per-light passes are acceptable.
- Reduce the number and screen coverage of lights, especially normal-mapped lights.

## Perspective Lighting Is Unstable

Set `Camera To Light Obstacles Distance` to the effective distance between the camera and obstacle plane. Keep obstacles at a consistent depth. Perspective mode uses that single depth to derive texture coverage and does not support normal mapping.

## Normal Mapping Does Not Render

- Use an orthographic camera.
- Use XY-plane rendering; normal mapping is not supported on the XZ plane in either backend.
- Assign `_MainTex` and `_NormalTex` on a compatible material.
- Ensure the shader SubShader has the `LightObstacle=True` tag.
- Avoid mismatched base and normal atlas UVs.
- Do not use source blur when crisp normal detail is required.

## HDR Looks Clamped or Different in URP

Enable HDR on the camera and URP asset. When **64 Bit Color** is enabled, set the URP asset **HDR Color Buffer Precision** to **64 Bits**. Light2D falls back when the platform cannot render to the required half-float format.

## `Affect Only This Camera` Fails

In Built-in, this mode cannot be used when the source camera already has a target texture. In URP it adds no behavior because the feature is already scoped to the camera containing `LightingSystem`.

## Migrated Components or Materials Are Missing

Stop before deleting missing components. Confirm the package imported without compile errors and that the legacy folder was removed with Unity closed. Restore `.meta` files from backup if the old folder was copied or regenerated without them. Follow [MIGRATION.md](MIGRATION.md) rather than importing both copies together.

## Sample Scene Is Misconfigured

Samples are copied into the project and register their lights, obstacles, and ambient emitters automatically — there are no layers or helper camera to reassign. The active pipeline asset and renderer data are project settings, so for URP install the renderer feature on the renderer selected by the sample camera after import.

## More Questions

See the [FAQ](FAQ.md) for common questions about using lights on moving objects, runtime-instantiated lights and obstacles, Tilemap occlusion, fog-of-war/visibility, and project status.
