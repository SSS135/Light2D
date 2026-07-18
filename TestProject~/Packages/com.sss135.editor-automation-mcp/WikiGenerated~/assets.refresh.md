<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/assets.refresh.md. -->

---
name: assets-refresh
description: Refreshes the AssetDatabase. Use it if any file was added or updated in the project outside of Unity API. Use it if need to force scripts recompilation when '.cs' file changed.
---

# Assets / Refresh

Refreshes the AssetDatabase. Use it if any file was added or updated in the project outside of Unity API. Use it if need to force scripts recompilation when '.cs' file changed.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "assets.refresh", "args": {
  "options": "string_value",
  "requestId": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "assets.refresh" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `options` | `any` | No | Asset import options. |
| `requestId` | `string` | No |  |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "options": {
      "$ref": "#/$defs/UnityEditor.ImportAssetOptions"
    },
    "requestId": {
      "type": "string"
    }
  },
  "$defs": {
    "UnityEditor.ImportAssetOptions": {
      "type": "string",
      "enum": [
        "Default",
        "ForceUpdate",
        "ForceSynchronousImport",
        "ImportRecursive",
        "DontDownloadFromCacheServer",
        "ForceUncompressedImport"
      ]
    }
  }
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

## Notes

Forces an `AssetDatabase.Refresh()` from C# instead of waiting for the
Editor to notice file changes on next focus. Use this after a tool wrote
`.prefab`, `.asset`, or other non-script files on disk that Unity must
re-import before subsequent calls observe them. For `.cs` compile checks,
prefer `compile.request`.

When the Editor's `kAutoRefreshMode` preference is disabled (Preferences →
Asset Pipeline → Auto Refresh = Disabled), the response includes a
`warnings` array noting that background script reload will not run until
the user manually focuses the Editor or invokes a recompile. The tool
itself still runs synchronously; the warning is purely advisory.

