# Troubleshooting

## Black or Unlit Output

- Add at least one direct light or ambient emitter; a scene with no lighting input can composite to black.
- Confirm the source camera has `LightingSystem` and the required material references.
- Confirm all three layer fields are distinct and objects use the matching layers.
- Exclude the three input layers from the source camera culling mask.
- In Built-in, assign a disabled helper `Lighting Camera`.
- In URP, install the feature on the renderer actually selected by the source camera. Leave `LightCamera` unassigned.

## URP Feature Missing or on the Wrong Renderer

The `LightingSystem` inspector reports missing, duplicate, wrong-renderer, and unsupported-renderer states. Select the source camera and use **Install / Repair Renderer Feature**, or run **Tools > Light2D > Universal Render Pipeline > Install Renderer Feature**.

The renderer data must be a persistent writable asset. A camera can override the URP asset’s default renderer through Universal Additional Camera Data; the feature must be installed on that selected renderer. Only Universal Renderer Data and 2D Renderer Data are supported.

## URP Validation Fails During Import

Unity 6000.4.7f1 with ShaderGraph/URP 17.4 can import required package ShaderGraphs as the wrong asset type while URP creates or populates global settings. Errors such as `Host type is not matching any asset type`, followed by global-settings postprocessor exceptions, are the known development-host defect described in [TESTING.md](TESTING.md#current-urp-validation-blocker), not evidence that URP needs a helper camera.

## Light Inputs Are Visible as Ordinary Sprites

Remove the light-source, ambient, and obstacle layers from the source camera culling mask. Light2D performs separate culling and rendering for those layers.

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

Samples are copied into the project but cannot carry project layer definitions, camera renderer assignments, or the active pipeline asset. Reassign the three layers and camera culling mask. For Built-in, assign the helper camera; for URP, install the renderer feature selected by the source camera and leave `LightCamera` unassigned.
