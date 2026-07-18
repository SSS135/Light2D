<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/system.editor-modal-respond.md. -->

---
name: system-editor-modal-respond
description: Programmatically clicks a button on a native modal dialog previously surfaced by system-editor-modal-status. Refuses to click obviously-destructive buttons unless acknowledgeDestructive=true.
---

# System / Editor / Modal Respond

Programmatically clicks a button on a native modal dialog previously surfaced by system-editor-modal-status. Refuses to click obviously-destructive buttons unless acknowledgeDestructive=true.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "system.editor-modal-respond", "args": {
  "hwnd": "string_value",
  "button": "string_value",
  "acknowledgeDestructive": false
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "system.editor-modal-respond" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `hwnd` | `string` | Yes | hwnd of the dialog as returned by system-editor-modal-status (stringified Int64). |
| `button` | `string` | Yes | Either a button text (case-insensitive) or stringified Int64 button hwnd. Text is matched against the dialog's currently-enumerated buttons. |
| `acknowledgeDestructive` | `boolean` | No | Set true to confirm clicking buttons whose text matches a destructive keyword (delete, don't save, discard, reset, remove, overwrite, abandon). Default false. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "hwnd": {
      "type": "string"
    },
    "button": {
      "type": "string"
    },
    "acknowledgeDestructive": {
      "type": "boolean"
    }
  },
  "required": [
    "hwnd",
    "button"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

