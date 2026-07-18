# Light2D Agent Guide

## Repository Shape

- This repository root is the UPM package `com.sss13594.light2d`, not a Unity project.
- `TestProject~` is the development/test host. Its trailing `~` excludes it from the package payload, and its generated directories are ignored.
- Open `X:\src\unity\Light2D\TestProject~` with Unity `6000.4.7f1` at `C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe`.
- The host manifest references the package root with `"com.sss13594.light2d": "file:../.."`.
- Use subagents for independent investigation or implementation work when available. Other agents and users may edit this checkout concurrently: re-read files immediately before editing, preserve unrelated changes, and never revert or overwrite work you did not author.

## Editing Rules

- Use `apply_patch` for manual file edits. Formatting or generated bulk output may use its owning tool.
- Preserve Unity `.meta` files and GUIDs. Never regenerate, delete, rename, or duplicate metas casually; serialized scenes, prefabs, materials, and the 2.0 migration depend on them.
- Do not edit generated solution/project files.
- Do not commit, amend, push, or create a pull request unless the user explicitly requests it.
- Keep package dependencies and the package version unchanged unless the task explicitly owns them.
- The legacy shader names, pass tags, properties, keywords, and serialized C# field names are compatibility contracts. In particular, preserve `_MainTex`, `_NormalTex`, `_ObstacleTex`, `_LightSourcesTex`, `_AmbientLightTex`, `_GameTex`, `_ObstacleMul`, `_EmissionColorMul`, `LightObstacle=True`, and the misspelled shader identifier `Light2D/Obstacle Texture Post Porcessor` unless the change includes a tested migration.

## Architecture Boundaries

- Built-in rendering runs through `LightingSystem.OnRenderImage` and `LightingRenderResources`, using an explicitly rendered helper camera.
- URP rendering runs through `Light2DUniversalRendererFeature` and URP 17 RenderGraph. It derives culling and matrices from the source camera and does not use `LightingSystem.LightCamera`. `UniversalRendererData` and `Renderer2DData` support is implemented, but current host evidence does not behaviorally verify either path.
- Do not make the Built-in backend depend on RenderGraph or route URP rendering through `OnRenderImage`.
- Keep per-camera resources isolated. URP contexts and ambient histories are keyed by `LightingSystem`; serialized materials are cloned before runtime mutation.
- Three distinct layers are required: light sources, ambient emitters, and obstacles. Source cameras must exclude all three.
- Normal mapping is orthographic XY-only in both backends until implementation and tests prove otherwise.

## Unity and MCP

- Do not launch Unity unless the user requests it or the task requires runtime validation and no prohibition applies.
- The editor-automation MCP server runs inside the open host editor at `http://127.0.0.1:17777/mcp`; root `.mcp.json` points to it. There is no standalone server.
- Unity may stop processing refresh/reload work while unfocused. Before a recompile-triggering MCP request, run:

```powershell
& "X:\src\unity\Light2D\TestProject~\Packages\com.sss135.editor-automation-mcp\Tools\wake-unity.ps1" -ProjectFilter "TestProject~"
```

- After source edits, call `assets.refresh` and poll `editor.application-get-state` until `IsCompiling` and `IsUpdating` are false and compilation has completed. MCP disconnects during domain reload; wait for `http://127.0.0.1:17777/health` before reconnecting.
- Save all scenes before `tests.run`. Poll the returned job ticket and inspect `console.get-logs` after completion. A passing test result can still leave logged exceptions.
- Port 17777 is fixed by the embedded host package. If another editor owns it, close that editor rather than changing Light2D configuration.

## Verification Order

1. Run static checks appropriate to the changed files, including JSON parsing and documentation-link/path checks.
2. Compile/import `TestProject~` and inspect the full Unity log.
3. Run Edit Mode tests.
4. Run Play Mode tests when present or relevant.
5. Inspect the Unity Console for errors and exceptions.
6. Manually verify Built-in rendering before URP when shared runtime/shader behavior changes.
7. Verify URP with Universal Renderer Data and 2D Renderer Data when renderer code changes.
8. Run `git diff --check` and inspect `git status` without staging unrelated files.

`Tests/EditMode` contains 17 tests covering contracts, lifecycle/resource release, package integrity, shader import, and URP feature setup. `Tests/PlayMode` contains two behaviorally validated Built-in tests and one URP rendering test whose execution is currently blocked. PlayMode batch runs can leave Unity alive after result generation, so run them under an external process watchdog.

The host keeps `TestProject~/Assets/UniversalRenderPipelineGlobalSettings.asset` registered as a sentinel. Do not delete, replace, or unregister it: Built-in automation validates it and removes numbered copies that URP may generate. This keeps Built-in validation usable but does not fix the Unity 6000.4.7f1/ShaderGraph 17.4 defect that imports required URP ShaderGraphs as the wrong asset type and breaks global-settings creation. URP renderer support is implemented for Universal and 2D Renderer Data, but neither path is behaviorally verified in the current host. Do not introduce a dependency on assets from another project as a workaround.

Compile/import example:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" -batchmode -quit -projectPath "X:\src\unity\Light2D\TestProject~" -logFile "X:\src\unity\Light2D\TestProject~\unity-import.log"
```

Edit Mode test example:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" -batchmode -projectPath "X:\src\unity\Light2D\TestProject~" -runTests -testPlatform EditMode -testResults "X:\src\unity\Light2D\TestProject~\editmode-results.xml" -logFile "X:\src\unity\Light2D\TestProject~\unity-editmode-tests.log"
```

## Never Edit or Commit Generated State

- `TestProject~/Library/` and `TestProject~/Library.meta`
- `TestProject~/Temp/`, `TestProject~/Obj/`, `TestProject~/Logs/`, and their metas
- `TestProject~/UserSettings/` and its meta
- Host `*.log`, test-result XML, generated `*.csproj`, `*.sln`, IDE settings, and crash files
- Root generated `*.csproj`, `*.sln`, and their generated metas
- Unity package-cache contents

Do not clean generated directories with destructive Git commands. Ignore them and stage only task-owned source files.
