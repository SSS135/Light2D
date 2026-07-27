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

The EditMode suites cover contract behavior (public API surface, pipeline classification, and legacy-API removal), lifecycle and resource release, component resources, diagnostics, editor workflows, package integrity, shader import, and deterministic URP feature setup. The package PlayMode suite includes four Built-in tests covering rendered output, obstacle attenuation, and owned-resource cleanup; target-alignment and coverage-volume tests; and four URP rendering tests (aligned non-square target output, partial viewport-rect alignment, reduced-render-scale composition, and two-base-camera lighting isolation). The URP tests drive rendering explicitly with `RenderPipeline.SubmitRenderRequest` and `UniversalRenderPipeline.SingleCameraRequest`; they have no skip path.

The host project additionally provides a sample PlayMode suite in assembly `Light2D.TestProject.SampleTests` under `Assets/Light2DTestAutomation/Tests/`: two frame-advance sample smoke tests plus three visual smoke tests that write PNG artifacts to `TestResults/Light2DSampleVisual/` (Rocket, Rocket Tilemap, and Light Volume captures) before asserting. Current Unity 6000.5.4f1 evidence is: full EditMode 358 total (347 pass, 11 intentional skips); Light2D EditMode 120/120; and selected PlayMode 11/11, comprising Built-in 4/4, URP 4/4, sample smokes 2/2, and Rocket visual 1/1.

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

Passive frames remain insufficient to advance a runtime-created URP pipeline in batchmode: `RenderPipelineManager.currentPipeline` stays null after frame yields. The four URP PlayMode tests do not depend on passive activation. Each issues `RenderPipeline.SubmitRenderRequest` with a `UniversalRenderPipeline.SingleCameraRequest` against the source camera, warming ambient history and then capturing the settled frame. This composes URP on demand, leaves no skip path, and runs every pixel assertion; all four tests pass in batchmode on Unity 6000.5.4f1.

## Editor Automation MCP

The host embeds `com.sss135.unity-mcp-streamlined`. Do not also install official Unity-MCP: the streamlined derivative intentionally shares assembly and code identities with it, so the packages cannot coexist in one Unity project. While the editor is open and responsive, the gateway serves:

```text
http://127.0.0.1:<port>/mcp
```

The root `.mcp.json` configures the stdio proxy. The in-editor gateway writes its active project-derived port to `Library/UnityMcpStreamlined/gateway-port` and its token beside it; do not assume a fixed port. On first import, wait for package import, script compilation, and any resulting domain reload to finish before using MCP. The setup menu and gateway are unavailable until Unity is ready.

Code edits require an asset refresh and completed Unity compilation before tests exercise the new assembly. An unfocused Windows editor can stall refresh or domain reload. Before a recompile-triggering MCP call, run the one-shot helper:

```powershell
& "X:\src\unity\Light2D\Packages\com.sss135.unity-mcp-streamlined\Tools\wake-unity.ps1" -ProjectFilter "Light2D"
```

Then request `assets.refresh`, poll `editor.application-get-state` until compilation and updating are both settled, run `tests.run`, poll its job ticket, and inspect `console.get-logs`. The gateway disappears during domain reload; the stdio proxy reconnects after the gateway republishes its active port and token. For direct diagnostics, read `gateway-port` rather than using a hard-coded endpoint. If its derived port conflicts with another process, stop that process rather than changing Light2D configuration.

## Manual Matrix

- Built-in orthographic: direct light, colored obstacle, ambient light, blur, point filtering, and camera movement.
- Built-in perspective: shared obstacle depth and edge coverage; normal mapping must remain disabled.
- Built-in single-camera mode: with no source target texture.
- URP Universal Renderer: feature installed on the camera-selected renderer, intermediate color, HDR on/off.
- URP 2D Renderer: same checks with 2D Renderer Data.
- Multiple source cameras: independent `LightingSystem` instances and ambient histories.
- Normal mapping in both backends: orthographic XY only, several visible lights, base/normal texture alignment.
- Resource lifetime: disable, re-enable, destroy, change resolution, and switch quality settings without leaked generated resources.
