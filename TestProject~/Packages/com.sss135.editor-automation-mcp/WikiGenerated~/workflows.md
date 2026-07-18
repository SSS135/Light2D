<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/workflows.md. -->

# `workflows`

Task-oriented entry points for the Editor Automation wiki. Use this page when you
know the job you want done but not which namespace owns the calls. These
workflows describe current RPC and wiki surfaces only; drill into the linked
namespace pages for exact parameters.

## Current Workflow Pages

| Task | Start with | Use when | Main supporting namespaces |
|---|---|---|---|
| Edit an asset on disk | [`assets`](assets.md) | You need to copy, move, refresh, or modify a Prefab / Material / ScriptableObject under `Assets/` or `Packages/`. | `assets`, `gameobject`, `object` |
| Edit a live scene object | [`gameobject`](gameobject.md) | You need to spawn, transform, query, tag, or attach components to a GameObject in the active scene. | `gameobject`, `scene` |
| Run / inspect Edit-Mode tests | [`tests`](tests.md) | You need to run tests or read the last test result without leaving the chat surface. | `tests`, `console`, `scene` |
| Reload after writing C# | [`compile`](compile.md) | A tool wrote `.cs` files and you need a reload-safe compile/no-compile result with warnings and errors. | `compile`, `system`, `script` |
| Patch a Component or asset value | [`gameobject`](gameobject.md) | You are changing serialized fields on a placed component (live scene), or on a ScriptableObject asset (use `object` instead). | `object`, `assets` |
| Capture a screenshot for review | [`screenshot`](screenshot.md) | You need visual evidence from a camera, the Game view, the Scene view, or a single isolated GameObject. | `gameobject`, `scene`, `editor` |
| Debug a running game's state over time | [`recorder`](recorder.md) | You need to know what a value / object was at frame N, how it changed across a window, or what happened around an event — against the live session or a recording. | `recorder`, `console`, `editor` |
| Install a UPM package | [`package`](package.md) | You are adding or removing packages; the add path triggers a domain reload, so the gateway is unreachable briefly. | `wiki` (wake-unity workaround) |
| Discover a C# type or method | [`type`](type.md) | You need a JSON schema for an unknown type before authoring a patch payload or a reflection call. | `reflection`, `object` |

## How To Use A Workflow Page

1. Open the workflow page with `call("<slug>")`.
2. Follow the workflow until it names the namespace or method that owns the
   next step.
3. Open that namespace page (for example `call("assets")`,
   `call("gameobject")`, or `call("component")`) before executing.
4. Execute only after you have the exact method and parameter shape.
5. If a call returns a `ticket_id`, poll `call("system.job-status", {"ticket_id": "..."})`
   before consuming output.

## When No Workflow Fits

Start at `call()` for the root namespace index, then drill into the nearest
namespace. Use `call("wiki")` for wiki navigation rules if available, or read
the `Docs~/WikiSource/README.md` overlay-authoring rules directly.

