#nullable enable
using EA;
using System.Collections.Generic;
using System.ComponentModel;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Editor.Framework.Util;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.Utils;
using UnityEditor;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Assets
    {
        public const string AssetsCreateFolderToolId = "assets-create-folder";
        [McpTool
        (
            AssetsCreateFolderToolId,
            Title = "Assets / Create Folder"
        )]
        [Description("Creates a new folder in the specified parent folder. " +
            "The parent folder string must start with the 'Assets' folder, and all folders within the parent folder string must already exist. " +
            "For example, when specifying 'Assets/ParentFolder1/ParentFolder2/', the new folder will be created in 'ParentFolder2' only if ParentFolder1 and ParentFolder2 already exist. " +
            "Use it to organize scripts and assets in the project. " +
            "Does AssetDatabase.Refresh() at the end. " +
            "Returns the GUID of the newly created folder, if successful.")]
        public CreateFolderResponse CreateFolders
        (
            [Description("The paths for the folders to create.")]
            CreateFolderInput[] inputs
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (inputs.Length == 0)
                    throw new System.Exception("The input array is empty.");

                var response = new CreateFolderResponse();

                foreach (var input in inputs)
                {
                    if (string.IsNullOrWhiteSpace(input.NewFolderName))
                    {
                        response.Errors ??= new();
                        response.Errors.Add($"Cannot create folder in '{input.ParentFolderPath}': folder name is empty or whitespace.");
                        continue;
                    }

                    var invalidIndex = input.NewFolderName.IndexOfAny(ArtifactStore.InvalidFileNameChars);
                    if (invalidIndex >= 0)
                    {
                        response.Errors ??= new();
                        response.Errors.Add($"Cannot create folder '{input.NewFolderName}' in '{input.ParentFolderPath}': folder name contains invalid character '{input.NewFolderName[invalidIndex]}'.");
                        continue;
                    }

                    if (!AssetDatabase.IsValidFolder(input.ParentFolderPath))
                    {
                        response.Errors ??= new();
                        response.Errors.Add($"Cannot create folder '{input.NewFolderName}': invalid parent folder path '{input.ParentFolderPath}'. The path must start with 'Assets/' and all folders in the path must already exist.");
                        continue;
                    }

                    var targetPath = $"{input.ParentFolderPath}/{input.NewFolderName}";
                    if (AssetDatabase.IsValidFolder(targetPath))
                    {
                        response.Errors ??= new();
                        response.Errors.Add($"Cannot create folder '{input.NewFolderName}' in '{input.ParentFolderPath}': a folder with the same name already exists at '{targetPath}'.");
                        continue;
                    }

                    var guid = AssetDatabase.CreateFolder(input.ParentFolderPath, input.NewFolderName);
                    if (string.IsNullOrEmpty(guid))
                    {
                        response.Errors ??= new();
                        response.Errors.Add($"Failed to create folder '{input.NewFolderName}' in '{input.ParentFolderPath}'.");
                        continue;
                    }

                    response.CreatedFolderGuids ??= new();
                    response.CreatedFolderGuids.Add(guid);
                }

                if (response.CreatedFolderGuids is { Count: > 0 })
                {
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    EditorUtils.RepaintAllEditorWindows();
                }

                return response;
            });
        }

    }
}
