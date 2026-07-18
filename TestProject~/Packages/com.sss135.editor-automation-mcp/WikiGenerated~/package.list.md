<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/package.list.md. -->

---
name: package-list
description: List all packages installed in the Unity project (UPM packages). Returns information about each installed package including name, version, source, and description. Use this to check which packages are currently installed before adding or removing packages.
---

# Package Manager / List Installed

List all packages installed in the Unity project (UPM packages). Returns information about each installed package including name, version, source, and description. Use this to check which packages are currently installed before adding or removing packages.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "package.list", "args": {
  "sourceFilter": "string_value",
  "nameFilter": "string_value",
  "directDependenciesOnly": false
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "package.list" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `sourceFilter` | `string` | No | Filter packages by source. |
| `nameFilter` | `string` | No | Filter packages by name, display name, or description (case-insensitive). Results are prioritized: exact name match, exact display name match, name substring, display name substring, description substring. |
| `directDependenciesOnly` | `boolean` | No | Include only direct dependencies (packages in manifest.json). If false, includes all resolved packages. Default: false |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "sourceFilter": {
      "type": "string",
      "enum": [
        "All",
        "Registry",
        "Embedded",
        "Local",
        "Git",
        "BuiltIn",
        "LocalTarball"
      ]
    },
    "nameFilter": {
      "type": "string"
    },
    "directDependenciesOnly": {
      "type": "boolean"
    }
  }
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

