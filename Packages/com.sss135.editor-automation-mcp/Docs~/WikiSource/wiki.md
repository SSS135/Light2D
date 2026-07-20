# Editor Automation Wiki Root Prelude

This page is the authored prelude rendered at the top of the live wiki root index. Update `Docs~/WikiSource/wiki.md` in the plugin to change it.

Live tool surface for Editor Automation. Reach a namespace directly with `call("assets")`, `call("gameobject")`, `call("scene")`, `call("editor")`, `call("screenshot")`, `call("script")`, `call("package")`, `call("reflection")`, `call("console")`, `call("recorder")`, `call("profiler")`, `call("object")`, `call("tool")`, `call("tests")`, `call("system")`, or `call("type")`, or start at [`workflows`](workflows.md) for task-oriented navigation. New plugins and setup live on [`initial-setup`](initial-setup.md); transport internals are on [`http-gateway`](http-gateway.md).

## Recompile-needs-focus workaround — [`wake-unity.ps1`](../../Tools/wake-unity.ps1)

**Editing code is not testing code.** A `.cs` change on disk does nothing until Unity recompiles, and Unity only recompiles when the Editor is focused (or woken / explicitly refreshed). So **after any code change, recompile and let the domain settle before testing** — MCP tools run against the currently-loaded assembly, so a call right after an edit exercises the *old* code and "passes" misleadingly. See [`editor`](editor.md) for the `editor.application-get-state` readiness-flag poll loop.

The HTTP gateway becomes unreachable for the duration of an Editor domain reload. In the unfocused-editor case this can stretch indefinitely: Unity throttles `EditorApplication.update` while the editor window is not the foreground process, which means the new AppDomain's compile + assembly load can stall, which means the gateway autostart on the new domain never fires.

The in-plugin wake watchdog in `RpcDispatcher.StartWakeWatchdog` can only call `EditorWin32.TryWakeEditor` while a request is in flight on a live gateway. It cannot fire during the reload window — there is no plugin code running in the editor process between `beforeAssemblyReload` and the next domain's autostart.

The fix is external. The plugin ships [`wake-unity.ps1`](../../Tools/wake-unity.ps1) at `Packages/com.sss135.editor-automation-mcp/Tools/wake-unity.ps1`, which calls `SetForegroundWindow` on the Unity process on demand. The script is Windows-only and runs as a one-shot wake — invoke it before each recompile-triggering MCP call.

Recommended usage from the project root:

```powershell
# One-shot wake (run before each recompile-triggering call):
.\Packages\com.sss135.editor-automation-mcp\Tools\wake-unity.ps1
# Target a specific Editor when multiple are running:
.\Packages\com.sss135.editor-automation-mcp\Tools\wake-unity.ps1 -ProjectFilter "MyGame"
```

This is the only known reliable mitigation for the unfocused-recompile case. Plugin-side fixes are not feasible — once `beforeAssemblyReload` fires, no plugin code runs in the editor process until the new AppDomain is live, by which time the wake is no longer needed.
