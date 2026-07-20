<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/assets.prefab-save.md. -->

---
name: assets-prefab-save
description: Save a prefab. Use it when you are in prefab editing mode in Unity Editor. Use 'assets-prefab-open' tool to open a prefab first.
---

# Assets / Prefab / Save

Save a prefab. Use it when you are in prefab editing mode in Unity Editor. Use 'assets-prefab-open' tool to open a prefab first.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "assets.prefab-save", "args": {
  "nothing": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "assets.prefab-save" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `nothing` | `string` | No |  |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "nothing": {
      "type": "string"
    }
  }
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

## Notes

Writes the currently open Prefab Stage to disk. Only meaningful while a
Stage is open via `assets.prefab-open`; outside the Stage there is nothing
to save and the call has no effect. Prefer
`assets.prefab-close(save: true)` when you're done editing — `save` is for
mid-edit checkpoints where you want to flush state without leaving the
Stage (for example, between batched `gameobject.*` mutations that you
might want to verify before continuing).

Save does not exit the Stage, so any follow-up `gameobject.*` or
`component.*` call still targets the asset, not the scene. If you wanted
to verify the on-disk result against a fresh instance, save, close, then
`assets.prefab-instantiate` — running the verify pass while the Stage is
still open will inspect the in-memory edit tree rather than the file
Unity wrote.

