<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/system.editor-modal-status.md. -->

---
name: system-editor-modal-status
description: Returns metadata for any native Win32 modal dialog currently blocking Unity's main thread. Use this to detect why a main-thread tool is hanging. Hwnd values are stringified Int64; pass them back verbatim to 'system-editor-modal-respond'. Does NOT detect IMGUI-rendered modal EditorWindows — those have no Win32 identity.
---

# System / Editor / Modal Status

Returns metadata for any native Win32 modal dialog currently blocking Unity's main thread. Use this to detect why a main-thread tool is hanging. Hwnd values are stringified Int64; pass them back verbatim to 'system-editor-modal-respond'. Does NOT detect IMGUI-rendered modal EditorWindows — those have no Win32 identity.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "system.editor-modal-status", "args": {} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "system.editor-modal-status" }
```

## Input

This tool takes no input parameters.

### Input JSON Schema

```json
{
  "type": "object"
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

