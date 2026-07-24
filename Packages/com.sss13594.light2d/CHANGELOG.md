# Changelog

All notable package changes are documented here. This file at the package root is the canonical changelog location.

## [Unreleased]

Light-source authoring, an obstacle preview, and shader modernization on the 2.x
line; the package version is unchanged.

### Breaking changes

- `LightSprite.LightOrigin` changed from `Vector3` to `Vector2` — it is now a pure
  XY correction offset (center/apex, hidden for Line lights); the light's height
  above the plane moved to the new `NormalMapHeight` field.
- Removed the `LightSprite.Shape` enum. A light's footprint now comes from
  `LightType` (Point/Spot/Line) and its serialized fields, not a shape selector.
- `CustomSprite` is now a public **abstract** base and can no longer be added as a
  component on its own.
- Removed the `CustomSprite` renderer-facade members `Renderer`, `RendererEnabled`,
  and `IsPartOfStaticBatch`; role components own no scene renderer to expose.
- Role components (lights, obstacles, volumes) no longer own a `MeshRenderer` or
  `MeshFilter`. Each builds an internal mesh plus a generated material and draws
  through the registry; the ambient emitter and `LightObstacleMesh` keep a
  `MeshFilter` only as an optional/required self-mesh input.
- `LightingSystem` is now values-only: the per-feature override toggles are removed,
  and its material slots are hidden and auto-populated from `Light2DDefaultResources`
  (including the new `ObstacleMaterial`).
- Renamed the `LightingSystem` HDR setting label "64 Bit Color" to
  "16-bit HDR Color"; the serialized `HDR` field name is unchanged.
- Removed the dead shaders `Light2D/Internal/Alpha Replace`,
  `Light2D/Internal/Light Blender`, and `Light2D/Light Overlay Single Camera`; the
  orphaned per-count light materials `Light1Point.mat`, `Light30Points.mat`,
  `Light50Points.mat`, `Light60Points.mat`, and `Light70Points.mat`; and the
  duplicate sample `AlphaBlended.shader`.

### Added

- First-class `LightSprite` authoring fields: `LightType` (Point/Spot/Line),
  `Radius`, `Intensity`, an HDR light color, `Falloff` (edge hardness over `0..1`,
  or a progressively longer/dimmer inverse-square-style tail over `-1..0`),
  `SpotAngle`, and `ShadowQuality`.
- Procedurally generated light falloff textures — no light bitmaps are shipped —
  shared, cached, and LRU-bounded across lights with matching parameters. An
  assigned `Sprite` overrides the generated falloff.
- `LightSpriteEditor` custom inspector: a Type-led layout with draggable scene
  handles for radius and the spot cone, and HelpBox warnings with Fix buttons.
- **Obstacle Preview** editor window (**Window > Light2D > Obstacle Preview**) that
  renders the live obstacle transmission buffer a `LightingSystem` produces
  (Built-in in play mode, URP in edit and play mode).
- Authorable ambient knobs on `LightingSystem` (obstacle multiplier and add,
  emission multiplier, sampling distance, and iteration count) and an optional
  colored ambient bounce that lets an obstacle's color tint the indirect fill; off
  by default.
- `LightSprite.NormalMapHeight` (default 1): the light's height above the plane used
  by normal mapping, shown only when a sprite override is set.
- Line-light shaping on `LightSprite`: `Length`, `Throw`, and `Sides`
  (`Both`/`Front`/`Back`). `Radius` is unused for a Line light.
- `LightingSystem.ObstacleColor`, a global obstacle multiplier applied through the
  `_Light2DObstacleGlobalMul` shader property.
- `Light2DDefaultResources.ObstacleMaterial`, auto-populating the hidden obstacle
  material slot on `LightingSystem`.
- Thruster lights in the Rocket sample (`ThrusterLight.cs`) with a 0.05 s rise and
  0.3 s fall.
- A values-only `LightingSystem` inspector with non-collapsible headers, live
  play-mode value tuning (structural fields stay locked), a "16-bit HDR Color"
  toggle, and a read-only "Estimated Light Textures" block.
- A Light Volume visual smoke test that captures a PNG regression artifact.

### Fixed

- URP SubShader selection: the object shaders drawn with `CommandBuffer.DrawMesh`
  now list their URP SubShader first, and the fullscreen Blit shaders tag their
  Built-in SubShader `"RenderPipeline"="BuiltInPipeline"`, so URP no longer selects
  the Built-in pass and renders Light2D black.
- Spot lights now emit from their apex at the transform position.
- The `LightSprite` scene gizmo disc now matches the visible reach (`Radius * 0.5`).
- `Light2DVolume` scene handles now scale correctly under a scaled transform.
- Self-mesh `LightObstacleMesh` now receives a default obstacle material instead of
  being silently undrawn.
- `Light2D/Fast Blur` under URP now matches the Built-in blur tap pattern.

### Changed

- Light size now comes from the new serialized `LightSprite.Radius` field, baked
  into the light mesh, instead of `transform.localScale`.
- Unified the per-sample-count ray-march light shaders `Light2D/Light 20 Points`
  through `Light2D/Light 90 Points` into a single `Light2D/Light Points` shader
  driven by a `_PathTrackingSamples` property.
- Migrated the Small Examples sample walls to `SpriteRenderer`-sourced obstacles;
  the obstacle `Material` slots are hidden.
- Re-baselined the Light Volume world-gate visual reference from 0.45 to 0.42, an
  intentional scene edit.

### Removed

- The legacy immediate-mode methods `LightSprite.DrawLightingNow` and
  `LightSprite.DrawLightNormalsNow`.

## [2.0.0] - 2026-07-21

Light2D 2.0.0 is the first published release. It converts the legacy
`Assets/Light2D` project into the root UPM package `com.sss13594.light2d` and ships a
breaking rewrite of the render architecture built on register-and-draw. Existing
(legacy, layer-based) Light2D scenes must re-add their components after upgrading;
there is no automatic upgrade. See the "Upgrading to 2.0.0" section in
[MIGRATION.md](Documentation~/MIGRATION.md#upgrading-to-200). The minimum supported
Unity editor line is 6000.3 (validated on 6000.3.20f1 / URP 17.3); the development
host is 6000.5.4f1.

### Breaking changes

- Removed the three-layer model. The serialized `LightingSystem` fields
  `LightSourcesLayer`, `AmbientLightLayer`, and `LightObstaclesLayer` are gone, and
  Light2D no longer creates layers or edits the source-camera culling mask. Scenes
  that relied on those serialized layer indices must re-add components.
- Removed the Built-in helper camera. `LightingSystem.LightCamera`, the
  `Lighting Camera.prefab`, and the `LightingSystemPrefabConfig` public type are
  retired. Built-in composition materials now come from an internal
  `Light2DDefaultResources` asset assigned by the creation window.
- Removed the legacy public utility API: the `Util` extension class, the `Point2`
  struct (replaced internally by `Vector2Int`), the unused `ReadOnlyAttribute`, and
  the remaining `LIGHT2D_2DTK` / 2D Toolkit compile branches and the obsolete 2D
  Toolkit enable workflow. These types were unreferenced by package serialized
  assets; code that consumed them must migrate to `UnityEngine` equivalents.

### Added

- Converted the repository from a legacy Unity project with `Assets/Light2D` into
  the root UPM package `com.sss13594.light2d`, split into the `Light2D.Runtime` and
  `Light2D.Editor` assemblies.
- Register-and-draw render core: light sources, ambient emitters, and obstacles
  self-register when enabled and are drawn into the lighting textures via
  `CommandBuffer.DrawMesh` on both backends — no layers, no helper camera, no
  child cameras. Component `MeshRenderer`s are kept disabled so lighting-input
  geometry never leaks into the game image.
- `LightAmbientEmitter` component, replacing the old "any renderer on the ambient
  layer" convention. Add it with **GameObject > Light2D > Ambient Emitter**.
- `Light2DVolume` component and coverage volumes: a world-space region (`Box`,
  `Circle`, `Sprite`, or `Global` shape) that scopes how much Light2D applies via
  `final = lerp(rawGame, composite, coverage)`. `Coverage` ranges from 1 (lighting
  applies) to 0 (raw game image passes through); `Priority` resolves overlaps and
  `BlendDistance` softens edges. The new `LightingSystem.DefaultCoverage` sets the
  world-wide baseline — 1 is lighting everywhere (the legacy default, allocation-free
  when no volumes exist), and 0 gives a bright world in which volumes carve lit
  "caves". With no volumes and a full-coverage baseline the render is byte-identical
  to before. Add one with **GameObject > Light2D > Light Volume**.
- `LightAmbientEmitter` `Fill` mode (`None`/`Box`/`Circle`): emits a flat,
  primitive-shaped ambient region through the existing ambient path, for a
  lit-but-stylized area or a soft ambient floor without a coverage bypass.
- `LightObstacleTilemap` component (#8): a texture-aware Tilemap obstacle that maps
  each cell to its tile sprite so transparent tile pixels pass light, rebuilt on
  tile change. Requires `com.unity.2d.tilemap`; compiled under `LIGHT2D_TILEMAP`.
- **Project Settings > Light2D** panel and a texture-conditional default obstacle
  color (#1): textured obstacles default to white (the texture shapes the shadow);
  solid, untextured obstacles default to opaque black. The obstacle menu and
  component defaults read these values.
- Importable **Examples** UPM sample under `Samples~/Examples`, including the
  **Small Examples**, **Rocket**, and a baked, deterministic **Rocket Tilemap**
  cave scene with a matching visual-regression guard.
- Preserved legacy asset GUIDs where assets moved from `Assets/Light2D`, allowing
  serialized references to resolve after a correctly ordered migration.
- Built-in Render Pipeline backend with owned render resources and per-camera
  material instances.
- URP 17.3 RenderGraph support through `Light2DUniversalRendererFeature` for
  Universal Renderer Data and 2D Renderer Data, plus editor commands to install,
  repair, and remove the renderer feature selected by a camera.
- Multi-instance `LightingSystem` tracking and per-camera URP contexts.
- XY/XZ shader variants; retained legacy shader names and serialized property names
  for material compatibility.
- `LightingSystem.ActivePipeline` and the `Light2DPipeline` classification
  distinguishing Built-in, supported URP, and unsupported Scriptable Render
  Pipelines; `IsRenderingReady` reports unsupported pipelines as not ready and an
  unsupported pipeline logs one actionable warning.
- `Light2DProfiling` markers and `Light2DDiagnostics`/`Light2DFrameMetrics` as the
  supported diagnostics API.
- Made implementation helpers internal. The supported public runtime API is the
  components (`LightingSystem`, `LightSprite`, `CustomSprite`, `LightObstacleSprite`,
  `LightObstacleMesh`, `LightObstacleGenerator`, `LightAmbientEmitter`, and — when
  `com.unity.2d.tilemap` is installed — `LightObstacleTilemap`), the URP renderer
  feature, the diagnostics types, and `Light2DPipeline`.
- Replaced unqualified editor `Resources.Load` calls with package-qualified
  `AssetDatabase` paths.
- The package development host (`TestProject`) and an editor-automation MCP endpoint
  on port 17777. Host automation is self-contained and has no external
  sibling-project fallback.
- EditMode suites covering contracts, lifecycle and resource release, components,
  diagnostics, editor workflows, package integrity, shader import, and URP
  feature/camera policy, plus Built-in behavioral PlayMode coverage and URP PlayMode
  tests. Validated a Built-in Windows x64 sample-player build with a successful
  `BuildReport`, zero reported errors, and zero actionable step errors.

### Fixed

- Built-in ambient light computation, light blur, and single-camera overlay
  rendering: `Graphics.Blit` uses a material's first SubShader and ignores the
  `RenderPipeline` tag, so shaders listing their URP SubShader first executed the
  wrong pass under Built-in. The ambient texture converged to a flat constant and
  obstacle rock never darkened. `AmbientLightComputer`, `FastBlur`, and
  `LightOverlaySingleCamera` now list the Built-in SubShader first, matching
  `LightOverlay`; all shader names, properties, tags, and keywords are unchanged.
- Rocket sample terrain rendering on Unity 6: generated chunk meshes now use
  per-source-texture submeshes with per-slot texture binding (the legacy Sprite
  Packer atlas assumption no longer holds) and explicit white vertex colors,
  restoring visible tiles.
- Built-in XZ-plane ambient reprojection used the Y delta instead of Z, smearing
  ambient history when panning on the XZ plane.
- URP HDR keyword now keys off the resolved light-texture format, not the requested
  `HDR` bool, correcting composition on platforms without FP16 render targets.
- Built-in ambient history now resets on camera rotation/aspect change (mirroring
  URP), reducing smear on resolution and aspect changes.
- Build-safe default obstacle material: obstacle components now resolve the obstacle
  shader in player builds, not only in the editor — `LightObstacleTilemap`
  previously produced no obstacle geometry in a build.
- Sample scripts hardened against hangs and null-reference errors (`Bat`,
  `MapGenerator`, `Flare`, `Wisp`).
- Per-frame allocation and redundant-work guards in the Built-in and URP render
  loops (material-clone gating, border-mesh rebuild guard, cached obstacle-mesh
  parent lookup, de-duplicated resource reconciliation).

### Changed

- Development host migrated to Unity 6000.5.4f1. The external
  Unity 6000.4.7f1 / ShaderGraph 17.4.0 URP importer defect is resolved on the new
  host; the only remaining limitation is that a runtime-created URP pipeline does
  not activate under batchmode PlayMode, so the URP PlayMode tests `Assert.Ignore`
  via a pipeline-activation guard.
- Runtime components now carry tooltips and `[AddComponentMenu]` entries.

### Migration notes

- Remove the legacy `Assets/Light2D` tree before installing 2.0.0 so Unity never
  imports duplicate GUIDs from both locations.
- There is no automatic upgrade. Re-add Light2D components on existing scenes rather
  than expecting serialized layer indices to carry over — a `LightSprite` for
  lights, an obstacle component for occluders, and a `LightAmbientEmitter` for
  ambient emission. There are no Light2D layers or helper camera to recreate.
- Import the Examples sample through Package Manager instead of copying package
  sample assets by hand.
- Project code that used `Light2D.Util` or `Light2D.Point2` must switch to
  `UnityEngine` equivalents such as `Vector2Int`, `Object.Destroy`, and `Vector2`
  math; the package no longer ships these helpers.
- See [MIGRATION.md](Documentation~/MIGRATION.md#upgrading-to-200) for the complete
  sequence and rollback guidance.

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
