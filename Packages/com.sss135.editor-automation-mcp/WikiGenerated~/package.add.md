<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/package.add.md. -->

---
name: package-add
description: "Install a package from the Unity Package Manager registry, Git URL, or local path. This operation modifies the project's manifest.json and triggers package resolution. Note: Package installation may trigger a domain reload. The tool returns a Processing response carrying a job ticket id; poll 'system-job-status' with that id for the final outcome."
---

# Package Manager / Add

Install a package from the Unity Package Manager registry, Git URL, or local path. This operation modifies the project's manifest.json and triggers package resolution. Note: Package installation may trigger a domain reload. The tool returns a Processing response carrying a job ticket id; poll 'system-job-status' with that id for the final outcome.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "package.add", "args": {
  "packageId": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "package.add" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `packageId` | `string` | Yes | The package ID to install. Formats: Package ID 'com.unity.textmeshpro' (installs latest compatible version), Package ID with version 'com.unity.textmeshpro@3.0.6', Git URL 'https://github.com/user/repo.git', Git URL with branch/tag 'https://github.com/user/repo.git#v1.0.0', Local path 'file:../MyPackage'. |

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

## Notes

Resolves the package, writes `Packages/manifest.json`, and (when the
manifest changed) triggers the domain reload above. The response arrives
**after** the reload completes — do not chain dependent RPCs into the same
batch.

`packageId` accepts four shapes (exact syntax lives in the generated
parameter schema): **registry latest** (`com.unity.textmeshpro`), **registry
pinned** (`com.unity.textmeshpro@3.0.6`), **Git URL** with optional
`#branch-or-tag` revision suffix, and **local path** (`file:…` directory or
tarball). Pin a version or Git revision whenever the package is
load-bearing — leaving the install at "latest" makes future project
reproductions susceptible to upstream version drift.

