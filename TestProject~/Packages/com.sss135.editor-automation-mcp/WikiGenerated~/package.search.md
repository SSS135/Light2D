<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/package.search.md. -->

---
name: package-search
description: "Search for packages in both Unity Package Manager registry and installed packages. Use this to find packages by name before installing them. Returns available versions and installation status. Searches both the Unity registry and locally installed packages (including Git, local, and embedded sources). Results are prioritized: exact name match, exact display name match, name substring, display name substring, description substring. Note: Online mode fetches exact matches from live registry, then supplements with cached substring matches."
---

# Package Manager / Search

Search for packages in both Unity Package Manager registry and installed packages. Use this to find packages by name before installing them. Returns available versions and installation status. Searches both the Unity registry and locally installed packages (including Git, local, and embedded sources). Results are prioritized: exact name match, exact display name match, name substring, display name substring, description substring. Note: Online mode fetches exact matches from live registry, then supplements with cached substring matches.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "package.search", "args": {
  "query": "string_value",
  "maxResults": 0,
  "offlineMode": false
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "package.search" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `query` | `string` | Yes | The package id, name, or description. Can be: Full package id 'com.unity.textmeshpro', Full package name 'TextMesh Pro', Partial name 'TextMesh' (will search in Unity registry and installed packages), Description keyword 'rendering' (searches in package descriptions). |
| `maxResults` | `integer` | No | Maximum number of results to return. Default: 10 |
| `offlineMode` | `boolean` | No | Whether to perform the search in offline mode (uses cached registry data only). Default: true. Set to false to fetch latest exact matches from Unity registry. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "query": {
      "type": "string"
    },
    "maxResults": {
      "type": "integer"
    },
    "offlineMode": {
      "type": "boolean"
    }
  },
  "required": [
    "query"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

