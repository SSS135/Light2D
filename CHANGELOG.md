# Changelog

All notable package changes are documented here. This file at the package root is the canonical changelog location.

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
- Added `LightingSystem.ActivePipeline` and the `Light2DPipeline` classification distinguishing Built-in, supported URP, and unsupported Scriptable Render Pipelines; `IsRenderingReady` reports unsupported pipelines as not ready and an unsupported pipeline logs one actionable warning.
- Added `Light2DProfiling` markers and `Light2DDiagnostics`/`Light2DFrameMetrics` as the supported diagnostics API.
- Removed the legacy public utility API: the `Util` extension class, the `Point2` struct (replaced internally by `Vector2Int`), the unused `ReadOnlyAttribute`, and the remaining `LIGHT2D_2DTK`/2D Toolkit compile branches. These types were unreferenced by package serialized assets; code that consumed them must migrate to `UnityEngine` equivalents.
- Made implementation helpers internal. The supported public runtime API is the components (`LightingSystem`, `LightSprite`, `CustomSprite`, `LightObstacleSprite`, `LightObstacleMesh`, `LightObstacleGenerator`, `LightingSystemPrefabConfig`), the URP renderer feature, the diagnostics types, and `Light2DPipeline`.
- Replaced unqualified editor `Resources.Load` calls with package-qualified `AssetDatabase` paths.
- Removed the obsolete 2D Toolkit enable workflow and the legacy `LIGHT2D_2DTK` scripting define.
- Added `TestProject~` as the package development host and an editor-automation MCP endpoint on port 17777. Host automation is self-contained and has no external sibling-project fallback.
- Added EditMode suites covering contracts, lifecycle and resource release, components, diagnostics, editor workflows, package integrity, shader import, and URP feature setup, plus Built-in behavioral PlayMode coverage and a URP PlayMode test. URP behavioral execution remains blocked on the Unity 6000.4.7f1/ShaderGraph 17.4 global-settings import defect.
- Validated a Built-in Windows x64 sample-player build with a successful `BuildReport`, zero reported errors, and zero actionable step errors.
- Fixed Built-in ambient light computation, light blur, and single-camera overlay rendering: `Graphics.Blit` uses a material's first SubShader and ignores the `RenderPipeline` tag, so shaders listing their URP SubShader first executed the wrong pass under Built-in. The ambient texture converged to a flat constant and obstacle rock never darkened. `AmbientLightComputer`, `FastBlur`, and `LightOverlaySingleCamera` now list the Built-in SubShader first, matching `LightOverlay`; all shader names, properties, tags, and keywords are unchanged.
- Fixed Rocket sample terrain rendering on Unity 6: generated chunk meshes now use per-source-texture submeshes with per-slot texture binding (the legacy Sprite Packer atlas assumption no longer holds) and explicit white vertex colors, restoring visible tiles.

### Migration notes

- Remove the legacy `Assets/Light2D` tree before installing 2.0.0 so Unity never imports duplicate GUIDs from both locations.
- Import the Examples sample through Package Manager instead of copying package sample assets by hand.
- Recreate or verify the three Light2D layers because Unity layer indices belong to the consuming project and are not supplied by a UPM package.
- Recheck source-camera culling masks and all serialized layer indices after migration.
- Built-in scenes require the helper `Lighting Camera`; URP scenes use the source camera and require the renderer feature selected by that camera, with no helper `LightCamera` assignment.
- Project code that used `Light2D.Util` or `Light2D.Point2` must switch to `UnityEngine` equivalents such as `Vector2Int`, `Object.Destroy`, and `Vector2` math; the package no longer ships these helpers.
- See [MIGRATION.md](Documentation~/MIGRATION.md) for the complete sequence and rollback guidance.

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
