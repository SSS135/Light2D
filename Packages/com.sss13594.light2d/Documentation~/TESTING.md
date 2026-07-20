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
C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe
```

Open the repository root directly. It is a Unity 6000.5.4f1 project whose `Assets/` and `ProjectSettings/` are the development host, and it embeds the package under `Packages/`.

## Validation Order

1. Confirm JSON and Markdown paths without Unity.
2. Compile/import the host and inspect the editor log for C# and shader errors.
3. Run Edit Mode tests.
4. Run PlayMode tests under an external process watchdog.
5. Inspect the Unity Console after tests; logged exceptions are not always promoted to test failures.
6. Manually verify a Built-in scene.
7. Manually verify URP with both Universal Renderer Data and 2D Renderer Data when renderer integration changes.
8. Check `git diff --check` and ensure generated host files are not staged.

The EditMode suites cover contract behavior (public API surface, pipeline classification, and legacy-API removal), lifecycle and resource release, component resources, diagnostics, editor workflows, package integrity, shader import, and deterministic URP feature setup. They run green with no skips on the current host. The package PlayMode suite contains four Built-in tests covering rendered output, obstacle attenuation, and owned-resource cleanup, plus four URP rendering tests: aligned non-square target output, partial viewport-rect alignment, reduced-render-scale composition, and two-base-camera lighting isolation. Each URP PlayMode test `Assert.Ignore`s only when the runtime-created URP pipeline fails to activate in batchmode (the `IgnoreIfPipelineInactive` guard described under URP Verification Status below); on a host where the pipeline activates they run fully.

The host project additionally provides a sample PlayMode suite in assembly `Light2D.TestProject.SampleTests` under `Assets/Light2DTestAutomation/Tests/`: two frame-advance sample smoke tests plus two visual smoke tests that write PNG artifacts to `TestResults/Light2DSampleVisual/` (`rocket-visual-smoke.png` and `rocket-tilemap-visual-smoke.png`) before asserting. The EditMode suites and the Built-in PlayMode tests have been exercised on the current host; the URP PlayMode tests self-skip there when the pipeline does not activate in batchmode (see URP Verification Status below).

## Batch Commands

Do not run these while the same project is open in another Unity process. Quote every Windows path.

Compile/import and write a log:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe" -batchmode -quit -projectPath "X:\src\unity\Light2D" -logFile "X:\src\unity\Light2D\unity-import.log"
```

Run Edit Mode tests:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe" -batchmode -projectPath "X:\src\unity\Light2D" -runTests -testPlatform EditMode -testResults "X:\src\unity\Light2D\editmode-results.xml" -logFile "X:\src\unity\Light2D\unity-editmode-tests.log"
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
$unity = Start-Process -FilePath 'C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe' -ArgumentList $arguments -PassThru
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

On this host (Unity 6000.5.4f1, Test Framework 1.7) `-testFilter` reliably starts the matched tests, whereas `-assemblyNames Light2D.TestProject.SampleTests` registers the filter in the run settings but never starts a test, so the batch job hangs until the watchdog kills it. Use `-testFilter` for filtered fast paths, or run unfiltered.

Do not add `-quit` to either `-runTests` command; Unity Test Framework owns shutdown for a test run. On this host, a PlayMode run can produce its result and still leave the editor process alive, so the external watchdog prevents an indefinite batch job. The sample suite advances several minutes of captured game frames, which is why the watchdog allows 15 minutes. Logs and result XML files written to the repository root are generated artifacts and must not be committed.

## Built-in Windows Build Evidence

The Built-in sample player was built for `StandaloneWindows64` with `Light2D.TestProject.Editor.WindowsBuildAutomation.BuildWindows64Builtin`. The recorded `BuildReport` result was `Succeeded`, with zero reported errors and zero actionable step errors, and produced `Builds/Windows64/Builtin/Light2D.exe`. This is build evidence, not a substitute for rendering tests or manual visual review.

## Host Sentinel and Workaround

The host deliberately keeps `Assets/UniversalRenderPipelineGlobalSettings.asset` and its `.meta` registered in `ProjectSettings/GraphicsSettings.asset`. Built-in automation treats this as a sentinel: it validates the script GUID and registration, keeps the active render pipeline null for Built-in runs, and removes numbered global-settings copies that URP generates. Preserve the sentinel and its GUID. Do not delete, replace, or unregister it, and do not depend on a global-settings asset copied from another project.

Host setup is fully reproducible from this repository alone. The automation in `Assets/Light2DTestAutomation` generates URP global settings locally (`UrpProjectSetup.RegenerateGlobalSettings`) and has no fallback to any external project or copied foreign asset. No external project or copied external global-settings asset is a user requirement or supported validation prerequisite.

## URP Verification Status

URP RenderGraph support for `UniversalRendererData` and `Renderer2DData` is implemented, and the URP passes derive culling and matrices from the source camera (there is no helper camera on either backend).

The external Unity 6000.4.7f1 / ShaderGraph 17.4.0 importer defect that previously blocked URP validation is resolved on the Unity 6000.5.4f1 host. On the old host the URP `.shadergraph`/`.urtshader` assets cached permanently as `DefaultAsset`, URP global settings could not populate their resource fields ("Host type is not matching any asset type"), and a runtime-created `UniversalRenderPipelineAsset` never activated. On 6000.5.4f1 those ShaderGraphs import correctly and the global-settings sentinel populates its resource fields.

The remaining limitation is narrower and is a batchmode/test-harness artifact rather than a rendering defect: a runtime-created URP pipeline does not activate under batchmode PlayMode (`RenderPipelineManager.currentPipeline` stays null after many frames, with no error and a real GPU). The four URP PlayMode tests therefore `Assert.Ignore` through the `IgnoreIfPipelineInactive` guard, which is scoped strictly to non-activation so it cannot mask a genuine URP failure. On a host where the runtime pipeline activates, the full URP suite runs and any failure is a real regression. URP pixel correctness is verified through an interactive/edit-mode path rather than batchmode PlayMode; do not claim URP pixel correctness from a batch run alone.

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
