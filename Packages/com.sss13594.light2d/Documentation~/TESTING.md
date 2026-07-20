# Development and Testing

## Host Project

Open the repository root `X:\src\unity\Light2D` as the Unity project when developing the package. The package is embedded under `Packages/com.sss13594.light2d/` and is auto-discovered, so the host manifest no longer needs a `file:` dependency on it. The host manifest lists the package in its `testables` array so the package tests run:

```json
"testables": [
  "com.sss13594.light2d"
]
```

Generated host directories and user state are ignored by Git; the project settings and package manifest are source inputs.

The exact editor used by the host is:

```text
C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe
```

Open the repository root directly. It is a Unity 6000.4.7f1 project whose `Assets/` and `ProjectSettings/` are the development host, and it embeds the package under `Packages/`.

## Validation Order

1. Confirm JSON and Markdown paths without Unity.
2. Compile/import the host and inspect the editor log for C# and shader errors.
3. Run Edit Mode tests.
4. Run PlayMode tests under an external process watchdog.
5. Inspect the Unity Console after tests; logged exceptions are not always promoted to test failures.
6. Manually verify a Built-in scene.
7. Manually verify URP with both Universal Renderer Data and 2D Renderer Data when renderer integration changes.
8. Check `git diff --check` and ensure generated host files are not staged.

The EditMode suites cover contract behavior (public API surface, pipeline classification, and legacy-API removal), lifecycle and resource release, component resources, diagnostics, editor workflows, package integrity, shader import, and deterministic URP feature setup. The package PlayMode suite contains four Built-in tests covering rendered output, obstacle attenuation, and owned-resource cleanup, plus four URP rendering tests: aligned non-square target output, partial viewport-rect alignment, reduced-render-scale composition, and two-base-camera lighting isolation. Every URP PlayMode test begins with the positive three-condition host-defect detection described under the blocker section below: on the pinned defective host all four report as skipped with the defect message, and on a healthy host all four run fully.

The host project additionally provides a sample PlayMode suite in assembly `Light2D.TestProject.SampleTests` under `Assets/Light2DTestAutomation/Tests/`: two frame-advance sample smoke tests plus one visual smoke test that writes its PNG artifact to `TestResults/Light2DSampleVisual/rocket-visual-smoke.png` before asserting. The EditMode suites and the Built-in PlayMode tests have been exercised on the pinned host; the URP PlayMode tests self-skip there under the current blocker below.

## Batch Commands

Do not run these while the same project is open in another Unity process. Quote every Windows path.

Compile/import and write a log:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" -batchmode -quit -projectPath "X:\src\unity\Light2D" -logFile "X:\src\unity\Light2D\unity-import.log"
```

Run Edit Mode tests:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" -batchmode -projectPath "X:\src\unity\Light2D" -runTests -testPlatform EditMode -testResults "X:\src\unity\Light2D\editmode-results.xml" -logFile "X:\src\unity\Light2D\unity-editmode-tests.log"
```

Run Play Mode tests:

```powershell
$arguments = @(
    '-batchmode',
    '-projectPath', '"X:\src\unity\Light2D"',
    '-runTests',
    '-testPlatform', 'PlayMode',
    '-testResults', '"X:\src\unity\Light2D\playmode-results.xml"',
    '-logFile', '"X:\src\unity\Light2D\unity-playmode-tests.log"'
)
$unity = Start-Process -FilePath 'C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe' -ArgumentList $arguments -PassThru
if (-not $unity.WaitForExit(900000)) {
    Stop-Process -Id $unity.Id -Force
    throw 'PlayMode test runner exceeded the 15-minute watchdog timeout.'
}
if ($unity.ExitCode -ne 0) { throw "PlayMode test runner exited with code $($unity.ExitCode)." }
```

To run a single test as a fast path, add a `-testFilter` entry to the same `$arguments` list with the fully qualified test name (a regex; separate multiple names with semicolons):

```powershell
    '-testFilter', 'Light2D.TestProject.Tests.RocketVisualSmokeTest.RocketSceneRendersDarkCaveWithColoredLights',
```

On this host (Unity 6000.4.7f1, Test Framework 1.6) `-testFilter` reliably starts the matched tests, whereas `-assemblyNames Light2D.TestProject.SampleTests` registers the filter in the run settings but never starts a test, so the batch job hangs until the watchdog kills it. Use `-testFilter` for filtered fast paths, or run unfiltered.

Do not add `-quit` to either `-runTests` command; Unity Test Framework owns shutdown for a test run. On this host, a PlayMode run can produce its result and still leave the editor process alive, so the external watchdog prevents an indefinite batch job. The sample suite advances several minutes of captured game frames, which is why the watchdog allows 15 minutes. Logs and result XML files written to the repository root are generated artifacts and must not be committed.

## Built-in Windows Build Evidence

The Built-in sample player was built for `StandaloneWindows64` with `Light2D.TestProject.Editor.WindowsBuildAutomation.BuildWindows64Builtin`. The recorded `BuildReport` result was `Succeeded`, with zero reported errors and zero actionable step errors, and produced `Builds/Windows64/Builtin/Light2D.exe`. This is build evidence, not a substitute for rendering tests or manual visual review.

## Host Sentinel and Workaround

The host deliberately keeps `Assets/UniversalRenderPipelineGlobalSettings.asset` and its `.meta` registered in `ProjectSettings/GraphicsSettings.asset`. Built-in automation treats this as a sentinel: it validates the script GUID and registration, keeps the active render pipeline null, and removes numbered global-settings copies generated by URP. Preserve the sentinel and its GUID. This workaround allows Built-in tests and the Windows build to complete despite URP package postprocessor errors; it does not create a valid URP validation environment.

Host setup is fully reproducible from this repository alone. The automation in `Assets/Light2DTestAutomation` generates URP global settings locally (`UrpProjectSetup.RegenerateGlobalSettings`) and has no fallback to any external project or copied foreign asset. No external project or copied external global-settings asset is a user requirement or supported validation prerequisite.

## Current URP Validation Blocker

URP RenderGraph support for `UniversalRendererData` and `Renderer2DData` is implemented, and the URP PlayMode tests use the source camera with no helper `LightCamera`. Neither renderer path is currently behaviorally verified in the pinned host.

Unity 6000.4.7f1 with ShaderGraph/URP 17.4 imports required URP ShaderGraphs such as `AutodeskInteractive.shadergraph` as the wrong host asset type. URP global-settings creation then fails through `UniversalRenderPipelineGlobalSettingsPostprocessor`/`RenderPipelineGlobalSettingsUtils`, often creating numbered global-settings assets and logging `NullReferenceException`. The checked-in sentinel contains the fallout for Built-in validation but cannot make URP initialization trustworthy. Treat URP behavioral tests and builds as blocked until the editor/package defect is fixed or a clean, reproducible host setup proves both Universal Renderer Data and 2D Renderer Data paths.

**Root cause (confirmed 2026-07-20).** This is an external Unity defect, not a Light2D issue. ShaderGraph's importer fails on the first import of URP's package ShaderGraphs (observed as importer `NullReferenceException`s inside `GraphData` in one environment, and as API-Updater corruption of the immutable package source `UnityEditor.GUID` to `UnityEngine.GUID` in another). The affected `.shadergraph`/`.urtshader` assets are then permanently cached as `DefaultAsset`, so URP global-settings resource resolution fails (`RenderPipelineResourcesEditorUtils.TryReloadContainedNullFields` reports "Host type is not matching any asset type"), and a runtime-created `UniversalRenderPipelineAsset` never activates (`RenderPipelineManager.currentPipeline` stays null). The failure reproduces clean-room on a minimal project with only URP added, confirming it is environmental rather than repository-specific. Rebuilding the host `Library` does not help: on a fresh `Library`, `MonoScript.FromScriptableObject(UniversalRendererData)` resolves (non-null) while the defect persists, which is why the former fourth detection condition was dropped. A rebuilt-`Library` defective host would otherwise hard-fail every URP test at pipeline initialization instead of skipping. The only plausible supported remediation is a newer 6000.4.x editor patch that fixes the ShaderGraph importer (unverified).

The test suites detect this defect positively instead of blanket-disabling URP coverage. A test skips only when all three robust conditions hold at once: the editor version is exactly `6000.4.7f1`, the URP package version is exactly `17.4.0`, and `AutodeskInteractive.shadergraph` loads as a non-`Shader` main asset. A former fourth condition (`MonoScript.FromScriptableObject` failing to resolve a probe `UniversalRendererData`) was removed because it is Library-state-dependent; see the root cause above. The canonical implementation is the private helper in `Tests/EditMode/EditorWorkflowTests.cs`; `Tests/PlayMode/UrpHostDefectDetection.cs` duplicates it for PlayMode, and the two must stay in sync. On the pinned host the expected outcome is green-with-skips: two EditMode skips and four URP PlayMode skips, each carrying the defect message in the result XML. On a healthy host the three conditions cannot all hold, so the full URP suite runs and any failure is a real regression.

## Editor Automation MCP

The host embeds `com.sss135.editor-automation-mcp`. While the editor is open and responsive, it serves:

```text
http://127.0.0.1:17777/mcp
```

The root `.mcp.json` configures that endpoint. There is no separate server process. The active port is also written to `Library/EditorAutomationHttp/port.txt`.

Code edits require an asset refresh and completed Unity compilation before tests exercise the new assembly. An unfocused Windows editor can stall refresh or domain reload. Before a recompile-triggering MCP call, run the one-shot helper:

```powershell
& "X:\src\unity\Light2D\Packages\com.sss135.editor-automation-mcp\Tools\wake-unity.ps1" -ProjectFilter "Light2D"
```

Then request `assets.refresh`, poll `editor.application-get-state` until compilation and updating are both settled, run `tests.run`, poll its job ticket, and inspect `console.get-logs`. The gateway disappears during domain reload; reconnect only after its health endpoint responds again. If port 17777 is already occupied, close the other Unity editor using it.

## Manual Matrix

- Built-in orthographic: direct light, colored obstacle, ambient light, blur, point filtering, and camera movement.
- Built-in perspective: shared obstacle depth and edge coverage; normal mapping must remain disabled.
- Built-in single-camera mode: with no source target texture.
- URP Universal Renderer: feature installed on the camera-selected renderer, intermediate color, HDR on/off.
- URP 2D Renderer: same checks with 2D Renderer Data.
- Multiple source cameras: independent `LightingSystem` instances and ambient histories.
- Normal mapping in both backends: orthographic XY only, several visible lights, base/normal texture alignment.
- Resource lifetime: disable, re-enable, destroy, change resolution, and switch quality settings without leaked generated resources.
