# Light2D Agent Guide

## Repository Shape

- This repository root **is** the Unity `6000.5.4f1` development/test project. The UPM package `com.sss13594.light2d` is embedded at `Packages/com.sss13594.light2d/` and is auto-discovered by Unity from there.
- The repo-root project's generated directories (`Library/`, `Temp/`, `Obj/`, `Logs/`, `Builds/`, `UserSettings/`, `TestResults/`) are git-ignored. Consumer-facing package payload excludes `Documentation~/` and `Samples~/` staging by trailing `~` and `.npmignore`.
- Open `X:\src\unity\Light2D` (the repo root) with Unity `6000.5.4f1` at `C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe`. Do not open a subfolder.
- `Packages/manifest.json` does NOT reference embedded packages via `file:` paths. `manifest.json` keeps `"testables": ["com.sss13594.light2d", "com.sss135.unity-mcp-streamlined"]` so both embedded packages expose their test assemblies.
- `Packages/com.sss13594.light2d/Documentation~/` holds the reference guides (`ARCHITECTURE.md`, `INSTALLATION.md`, `MIGRATION.md`, `BENCHMARKING.md`, `TROUBLESHOOTING.md`, and `TESTING.md`). `Documentation~/TESTING.md` is the authoritative long-form of the test and host protocol summarized below.
- Use subagents for independent investigation or implementation work when available. Other agents and users may edit this checkout concurrently: re-read files immediately before editing, preserve unrelated changes, and never revert or overwrite work you did not author.

## Editing Rules

- Use `apply_patch` for manual file edits. Formatting or generated bulk output may use its owning tool.
- Preserve Unity `.meta` files and GUIDs. Never regenerate, delete, rename, or duplicate metas casually; serialized scenes, prefabs, materials, and the 2.0 migration depend on them.
- Do not edit generated solution/project files.
- Do not commit, amend, push, or create a pull request unless the user explicitly requests it.
- Keep package dependencies and the package version unchanged unless the task explicitly owns them.
- The legacy shader names, pass tags, properties, keywords, and serialized C# field names are compatibility contracts. In particular, preserve `_MainTex`, `_NormalTex`, `_ObstacleTex`, `_LightSourcesTex`, `_AmbientLightTex`, `_GameTex`, `_ObstacleMul`, `_EmissionColorMul`, `LightObstacle=True`, and the misspelled shader identifier `Light2D/Obstacle Texture Post Porcessor` unless the change includes a tested migration.
- SubShader ordering is load-bearing. In every dual-pipeline shader that Built-in composites through `Graphics.Blit` (the fullscreen passes: ambient, blur, overlay, and the obstacle post-processor), the Built-in `CGPROGRAM` SubShader MUST come first and the URP (`UniversalPipeline`) SubShader second. `Graphics.Blit` selects the first SubShader and ignores the `RenderPipeline` tag, so a URP-first ordering silently breaks Built-in ambient/blur/overlay composition — the defect that broke the whole Unity 6 port until it was fixed.
- The installed sample under `Assets/Samples/Light2D/Examples/` mirrors `Packages/com.sss13594.light2d/Samples~/Examples/` and must stay byte-identical; any script change to one must be mirrored to the other. `MapGenerator` builds one submesh and material/texture binding per source texture because Unity 6 has no legacy Sprite Packer atlas to merge block sprites into — do not reintroduce single-atlas assumptions.

## Architecture Boundaries

- Built-in rendering runs through `LightingSystem.OnRenderImage` and `LightingRenderResources`. Registered role components are drawn into the light-input `RenderTexture`s via `CommandBuffer.DrawMesh` (register-and-draw); there is no helper camera.
- URP rendering runs through `Light2DUniversalRendererFeature` and URP 17 RenderGraph. It derives culling and matrices from the source camera. `UniversalRendererData` and `Renderer2DData` support is implemented and behaviorally verified in batchmode through explicit single-camera render requests (the older ShaderGraph import defect is resolved on the 6000.5.4f1 host).
- Do not make the Built-in backend depend on RenderGraph or route URP rendering through `OnRenderImage`.
- Keep per-camera resources isolated. URP contexts and ambient histories are keyed by `LightingSystem`; serialized materials are cloned before runtime mutation.
- There are no Light2D layers. Roles are expressed as self-registering components — light sources (`LightSprite`), ambient emitters (`LightAmbientEmitter`), and obstacles (`LightObstacleSprite`/`LightObstacleMesh`/`LightObstacleGenerator`/gated `LightObstacleTilemap`). Each registers in `OnEnable`, deregisters in `OnDisable`, and is drawn only through the registry via `CommandBuffer.DrawMesh`. These register-and-draw components own no `MeshRenderer` at all — lights and obstacles carry neither a `MeshRenderer` nor a `MeshFilter`, and the ambient emitter carries a `MeshFilter` only as an optional self-mesh input (its generated sprite/fill modes keep their mesh in an internal field and carry no `MeshFilter`). Each owns a bare mesh plus a generated material and registers through its draw-entry snapshot, so there is no renderer to disable and no lighting-input geometry can leak into the game image (the invariant that replaces the old source-camera layer exclusion).
- Normal mapping is orthographic XY-only in both backends until implementation and tests prove otherwise.
- Runtime splits into `Runtime/Scripts/` (the components and rendering core — `LightingSystem`, `LightingRenderResources`, `CustomSprite`/`LightSprite`/`LightObstacleSprite`/`LightObstacleMesh`/`LightObstacleGenerator`/`LightAmbientEmitter`/`Light2DVolume`, the gated `LightObstacleTilemap`, the register-and-draw core `Light2DRenderRegistry`/`Light2DDraw`/`Light2DDrawContracts`/`Light2DCameraGeometry`, plus internal helpers `LightingSettingsValidator`, `Light2DObjectUtility`, `Light2DColorEncoding`, the editor-only `Light2DObstacleSettings`, and the `Light2DDefaultResources` material carrier) and `Runtime/Diagnostics/` (`Light2DProfiling` ProfilerMarkers and `Light2DFrameMetrics`/`Light2DDiagnostics`). The public API is exactly the six addable MonoBehaviour role components (`LightSprite`, `LightObstacleSprite`, `LightObstacleMesh`, `LightObstacleGenerator`, `LightAmbientEmitter`, `Light2DVolume`) — seven when `com.unity.2d.tilemap` is present, adding `LightObstacleTilemap` — plus `LightingSystem`, `CustomSprite` (the public **abstract** base of `LightSprite`/`LightObstacleSprite` that stays exported but is never added on its own), `Light2DUniversalRendererFeature`, the diagnostics types, and the `Light2DPipeline` enum; everything else is `internal`. `LightingSystemPrefabConfig` was retired.
- The legacy `Util` and `Point2` helpers are gone. Use `GetEntityId` (used repo-wide) and the internal helpers above; no `UNITY_6000_0_OR_NEWER` guards remain, since the host is Unity 6 only.

## Unity and MCP

- Do not launch Unity unless the user requests it or the task requires runtime validation and no prohibition applies.
- The Unity MCP Streamlined gateway runs inside the open host editor on a deterministic project-derived loopback port. Root `.mcp.json` launches its stdio proxy, which discovers the active port and token under `Library/UnityMcpStreamlined/`.
- On first import, wait for package import, script compilation, and any resulting domain reload to finish before using MCP. The setup menu and gateway are unavailable until Unity is ready. Do not install official Unity-MCP in this project: the streamlined derivative intentionally shares assembly and code identities with it, so they cannot coexist.
- Unity may stop processing refresh/reload work while unfocused. Before a recompile-triggering MCP request, run:

```powershell
& "X:\src\unity\Light2D\Packages\com.sss135.unity-mcp-streamlined\Tools\wake-unity.ps1" -ProjectFilter "Light2D"
```

- After source edits, call `assets.refresh` and poll `editor.application-get-state` until `IsCompiling` and `IsUpdating` are false and compilation has completed. MCP disconnects during domain reload; the stdio proxy reconnects after the gateway republishes its active port and token under `Library/UnityMcpStreamlined/`.
- Save all scenes before `tests.run`. Poll the returned job ticket and inspect `console.get-logs` after completion. A passing test result can still leave logged exceptions.
- Do not assume a fixed gateway port. For direct diagnostics, read `Library/UnityMcpStreamlined/gateway-port`; if its derived port conflicts with another process, stop that process rather than changing Light2D configuration.

## Verification Order

1. Run static checks appropriate to the changed files, including JSON parsing and documentation-link/path checks.
2. Compile/import the repo-root project and inspect the full Unity log.
3. Run Edit Mode tests.
4. Run Play Mode tests when present or relevant.
5. Inspect the Unity Console for errors and exceptions.
6. Manually verify Built-in rendering before URP when shared runtime/shader behavior changes.
7. Verify URP with Universal Renderer Data and 2D Renderer Data when renderer code changes.
8. Run `git diff --check` and inspect `git status` without staging unrelated files.

Current Unity 6000.5.4f1 verification is: full EditMode 358 total (347 pass, 11 intentional skips); Light2D EditMode 120/120; and selected PlayMode 11/11, comprising Built-in 4/4, URP 4/4, sample smokes 2/2, and Rocket visual 1/1. The Light2D-owned suites cover contracts, lifecycle/resource release, components, diagnostics, editor workflows, package integrity, shader import, URP feature/camera policy, and behavioral rendering. The host `Light2D.TestProject.SampleTests` assembly lives under `Assets/Light2DTestAutomation/Tests/`; its Rocket visual regression captures a PNG artifact to `TestResults/Light2DSampleVisual/` and guards against bright or rockless rendering. PlayMode batch runs can leave Unity alive after result generation, so run them under an external process watchdog (900000 ms).

URP host status (updated for the 6000.5.4f1 dev host): the external Unity 6000.4.7f1 / ShaderGraph 17.4.0 importer defect that previously blocked URP — the URP `.shadergraph`/`.urtshader` assets cached permanently as `UnityEditor.DefaultAsset`, URP global settings unable to populate their resource fields ("Host type is not matching any asset type"), and the runtime pipeline never activating — is RESOLVED on the current host: ShaderGraphs import correctly and the unnumbered global-settings sentinel auto-populates its resource fields. Passive frame yields still do not advance a runtime-created URP pipeline to `currentPipeline` in batchmode. The four URP PlayMode tests instead call `RenderPipeline.SubmitRenderRequest` with `UniversalRenderPipeline.SingleCameraRequest`, which composes each camera on demand; they have no skip path, and all four pass with their pixel assertions running. The host still keeps `Assets/UniversalRenderPipelineGlobalSettings.asset` (the unnumbered sentinel) registered: Built-in automation validates it and removes numbered copies that URP generates. Do not delete, replace, or unregister the sentinel, and do not work around URP by depending on assets from another project.

PlayMode environmental protocol: before every PlayMode invocation, and only when no Unity is running, delete any numbered `Assets/UniversalRenderPipelineGlobalSettings <N>.asset` copies (never the unnumbered sentinel) and stale `Temp/__Backupscenes` and `UnityLockfile` (both under the repo root). Play-mode-entry deadlocks — the log frozen after `Loaded scene 'Temp/__Backupscenes/0.backup'` — are environmental: kill Unity, clean those paths, and retry. The reliable per-test fast path is `-testFilter "<fully-qualified-name>"`; `-assemblyNames` registers but never starts tests on this host.

Compile/import example:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe" -batchmode -quit -projectPath "X:\src\unity\Light2D" -logFile "X:\src\unity\Light2D\unity-import.log"
```

Edit Mode test example:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe" -batchmode -projectPath "X:\src\unity\Light2D" -runTests -testPlatform EditMode -testResults "X:\src\unity\Light2D\editmode-results.xml" -logFile "X:\src\unity\Light2D\unity-editmode-tests.log"
```

Play Mode per-test fast path example:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe" -batchmode -projectPath "X:\src\unity\Light2D" -runTests -testPlatform PlayMode -testFilter "Light2D.Tests.BuiltInRenderingTests.ProceduralRenderIsNonuniformAndObstacleAttenuatesLight" -testResults "X:\src\unity\Light2D\playmode-results.xml" -logFile "X:\src\unity\Light2D\unity-playmode-tests.log"
```

## Never Edit or Commit Generated State

- `Library/` and `Library.meta`
- `Temp/`, `Obj/`, `Logs/`, `Builds/`, and their metas
- `UserSettings/` and its meta
- `TestResults/` and any generated PlayMode visual/PNG artifacts
- `Assets/InitTestScene*` generated test-runner scenes
- `ProjectSettings/PackageManagerSettings.asset`
- Numbered `Assets/UniversalRenderPipelineGlobalSettings <N>.asset` copies (delete them per the PlayMode protocol; never stage them, and never touch the unnumbered sentinel)
- Host `*.log`, test-result XML, generated `*.csproj`, `*.sln`, IDE settings, and crash files
- Unity package-cache contents

Do not clean generated directories with destructive Git commands. Ignore them and stage only task-owned source files.
