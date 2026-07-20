<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/editor.selection-set.md. -->

---
name: editor-selection-set
description: Set the current Selection in the Unity Editor to the provided objects. Use 'editor-selection-get' tool to get the current selection first.
---

# Editor / Selection / Set

Set the current Selection in the Unity Editor to the provided objects. Use 'editor-selection-get' tool to get the current selection first.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "editor.selection-set", "args": {
  "select": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "editor.selection-set" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `select` | `any` | Yes |  |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "select": {
      "$ref": "#/$defs/EA.ObjectRef[]"
    }
  },
  "$defs": {
    "EA.ObjectRef": {
      "type": "object",
      "properties": {
        "instanceID": {
          "$ref": "#/$defs/UnityEngine.EntityId",
          "description": "instanceID of the UnityEngine.Object. If this is \u00270\u0027, then it will be used as \u0027null\u0027."
        }
      },
      "required": [
        "instanceID"
      ],
      "description": "Reference to UnityEngine.Object instance. It could be GameObject, Component, Asset, etc. Anything extended from UnityEngine.Object."
    },
    "UnityEngine.EntityId": {
      "type": "integer"
    },
    "EA.ObjectRef[]": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/EA.ObjectRef",
        "description": "Reference to UnityEngine.Object instance. It could be GameObject, Component, Asset, etc. Anything extended from UnityEngine.Object."
      }
    }
  },
  "required": [
    "select"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

## Notes

`select: [{ "instanceID": 0 }]` clears the selection — the `0` instance ID
resolves to `null`, which Unity treats as "deselect everything" rather than
"select the null object." Use this idiom to drop focus before a destructive
mutation that would otherwise leave the Inspector pointing at a stale managed
reference.

Pass `includeAssetGUIDs: true` on the paired `editor.selection-get` when the
human's selection is in the Project window rather than the scene — Hierarchy
selections come back through `GameObjects` / `Transforms`, Project selections
through `AssetGUIDs`. The two arrays are independent: a mixed selection
populates both, so consumers should iterate whichever matches the workflow
(scene-graph edits vs. asset-database edits) rather than assuming one is
empty.

