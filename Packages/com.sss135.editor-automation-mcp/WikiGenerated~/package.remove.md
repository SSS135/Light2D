<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/package.remove.md. -->

---
name: package-remove
description: "Remove (uninstall) a package from the Unity project. This removes the package from the project's manifest.json and triggers package resolution. Note: Built-in packages and packages that are dependencies of other installed packages cannot be removed. Note: Package removal may trigger a domain reload. Returns a Processing response carrying a job ticket id; poll 'system-job-status' with that id for the final outcome."
---

# Package Manager / Remove

Remove (uninstall) a package from the Unity project. This removes the package from the project's manifest.json and triggers package resolution. Note: Built-in packages and packages that are dependencies of other installed packages cannot be removed. Note: Package removal may trigger a domain reload. Returns a Processing response carrying a job ticket id; poll 'system-job-status' with that id for the final outcome.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "package.remove", "args": {
  "packageId": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "package.remove" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `packageId` | `string` | Yes | The ID of the package to remove. Example: 'com.unity.textmeshpro'. Do not include version number. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "packageId": {
      "type": "string"
    }
  },
  "required": [
    "packageId"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

