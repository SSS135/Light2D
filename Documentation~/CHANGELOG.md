# Changelog

All notable package changes are documented here.

## [2.0.0]

- Converted the repository from a legacy Unity project with `Assets/Light2D` into the root UPM package `com.sss13594.light2d`.
- Set the supported Unity editor line to 6000.4; development and host-project validation use Unity 6000.4.7f1.
- Split runtime and editor code into `Light2D.Runtime` and `Light2D.Editor` assemblies.
- Moved examples to the importable `Samples~/Examples` UPM sample.
- Preserved legacy asset GUIDs where assets moved from `Assets/Light2D`, allowing serialized references to resolve after a correctly ordered migration.
- Added a Built-in Render Pipeline backend with owned render resources and per-camera material instances.
- Added URP 17.4 RenderGraph support through `Light2DUniversalRendererFeature` for Universal Renderer Data and 2D Renderer Data.
- Added editor commands to install, repair, and remove the URP renderer feature selected by a camera.
- Added multi-instance `LightingSystem` tracking and per-camera URP contexts.
- Added XY/XZ shader variants and retained legacy shader names and serialized property names for material compatibility.
- Removed the obsolete 2D Toolkit enable workflow; the remaining menu command removes the legacy `LIGHT2D_2DTK` scripting define.
- Added `TestProject~` as the package development host and an editor-automation MCP endpoint on port 17777.
- Added 17 EditMode tests, Built-in behavioral PlayMode coverage, and a URP PlayMode test. URP behavioral execution remains blocked on the Unity 6000.4.7f1/ShaderGraph 17.4 global-settings import defect.
- Validated a Built-in Windows x64 sample-player build with a successful `BuildReport`, zero reported errors, and zero actionable step errors.

### Migration notes

- Remove the legacy `Assets/Light2D` tree before installing 2.0.0 so Unity never imports duplicate GUIDs from both locations.
- Import the Examples sample through Package Manager instead of copying package sample assets by hand.
- Recreate or verify the three Light2D layers because Unity layer indices belong to the consuming project and are not supplied by a UPM package.
- Recheck source-camera culling masks and all serialized layer indices after migration.
- Built-in scenes require the helper `Lighting Camera`; URP scenes use the source camera and require the renderer feature selected by that camera, with no helper `LightCamera` assignment.
- See [MIGRATION.md](MIGRATION.md) for the complete sequence and rollback guidance.

## [1.4]

- Added single-camera mode through `LightingSystem.AffectOnlyThisCamera`.
- Added XZ-plane support.

## [1.3]

- Added normal mapping.
- Added the ability to turn bloom off by setting the overlay material’s additive-light power to zero.
- Added point filtering for pixelated lighting textures.
- Fixed lighting after orthographic camera-size changes and camera Z rotation.

## [1.2.1]

- Added `LightObstacleGenerator.LightObstacleScale`.
- Improved perspective-camera handling.
- Added fallback behavior when 64-bit color is unavailable.
- Updated lighting when camera field of view changes at runtime.

## [1.2]

- Improved legacy 2D Toolkit and perspective-camera support.
- Fixed mobile shader compilation issues.

## [1.1]

- Added perspective-camera support using `LightCameraFovAdd`.

## [1.0.1]

- Added Unity 5 support.

## [1.0]

- Initial release.
