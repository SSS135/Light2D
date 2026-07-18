<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/assets.create-folder.md. -->

---
name: assets-create-folder
description: Creates a new folder in the specified parent folder. The parent folder string must start with the 'Assets' folder, and all folders within the parent folder string must already exist. For example, when specifying 'Assets/ParentFolder1/ParentFolder2/', the new folder will be created in 'ParentFolder2' only if ParentFolder1 and ParentFolder2 already exist. Use it to organize scripts and assets in the project. Does AssetDatabase.Refresh() at the end. Returns the GUID of the newly created folder, if successful.
---

# Assets / Create Folder

Creates a new folder in the specified parent folder. The parent folder string must start with the 'Assets' folder, and all folders within the parent folder string must already exist. For example, when specifying 'Assets/ParentFolder1/ParentFolder2/', the new folder will be created in 'ParentFolder2' only if ParentFolder1 and ParentFolder2 already exist. Use it to organize scripts and assets in the project. Does AssetDatabase.Refresh() at the end. Returns the GUID of the newly created folder, if successful.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "assets.create-folder", "args": {
  "inputs": "string_value"
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "assets.create-folder" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `inputs` | `any` | Yes | The paths for the folders to create. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "inputs": {
      "$ref": "#/$defs/EA.CreateFolderInput[]"
    }
  },
  "$defs": {
    "EA.CreateFolderInput": {
      "type": "object",
      "properties": {
        "ParentFolderPath": {
          "type": "string",
          "description": "The parent folder path where the new folder will be created."
        },
        "NewFolderName": {
          "type": "string",
          "description": "The name of the new folder to create."
        }
      }
    },
    "EA.CreateFolderInput[]": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/EA.CreateFolderInput"
      }
    }
  },
  "required": [
    "inputs"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

