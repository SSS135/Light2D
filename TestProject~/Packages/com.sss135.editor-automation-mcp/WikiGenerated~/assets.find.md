<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/assets.find.md. -->

---
name: assets-find
description: Search the asset database using the search filter string. Allows you to search for Assets. The string argument can provide names, labels or types (classnames).
---

# Assets / Find

Search the asset database using the search filter string. Allows you to search for Assets. The string argument can provide names, labels or types (classnames).

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "assets.find", "args": {
  "filter": "string_value",
  "searchInFolders": "string_value",
  "maxResults": 0
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "assets.find" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `filter` | `string` | No | The filter string can contain search data. Could be empty. Name: Filter assets by their filename (without extension). Words separated by whitespace are treated as separate name searches. Labels (l:): Assets can have labels attached to them. Use 'l:' before each label. Types (t:): Find assets based on explicitly identified types. Use 't:' keyword. Available types: AnimationClip, AudioClip, AudioMixer, ComputeShader, Font, GUISkin, Material, Mesh, Model, PhysicMaterial, Prefab, Scene, Script, Shader, Sprite, Texture, VideoClip, VisualEffectAsset, VisualEffectSubgraph. AssetBundles (b:): Find assets which are part of an Asset bundle. Area (a:): Find assets in a specific area. Valid values are 'all', 'assets', and 'packages'. Globbing (glob:): Use globbing to match specific rules. Note: Searching is case insensitive. |
| `searchInFolders` | `any` | No | The folders where the search will start. If null, the search will be performed in all folders. |
| `maxResults` | `integer` | No | Maximum number of assets to return. If the number of found assets exceeds this limit, the result will be truncated. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "filter": {
      "type": "string"
    },
    "searchInFolders": {
      "$ref": "#/$defs/System.String[]"
    },
    "maxResults": {
      "type": "integer"
    }
  },
  "$defs": {
    "System.String[]": {
      "type": "array",
      "items": {
        "type": "string"
      }
    }
  }
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

