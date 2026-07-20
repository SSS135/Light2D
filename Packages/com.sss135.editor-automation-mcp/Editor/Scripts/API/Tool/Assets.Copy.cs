#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.Utils;
using EA;
using UnityEditor;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Assets
    {
        public const string AssetsCopyToolId = "assets-copy";
        [McpTool
        (
            AssetsCopyToolId,
            Title = "Assets / Copy"
        )]
        [Description("Copy assets at given paths and store them at new paths. " +
            "Does AssetDatabase.Refresh() at the end. " +
            "Use '" + AssetsFindToolId + "' tool to find assets before copying.")]
        public CopyAssetsResponse Copy
        (
            [Description("The paths of the assets to copy.")]
            string[] sourcePaths,
            [Description("The paths to store the copied assets.")]
            string[] destinationPaths
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (sourcePaths.Length == 0)
                    throw new System.Exception(Error.SourcePathsArrayIsEmpty());

                if (sourcePaths.Length != destinationPaths.Length)
                    throw new System.Exception(Error.SourceAndDestinationPathsArrayMustBeOfTheSameLength());

                var response = new CopyAssetsResponse();

                for (var i = 0; i < sourcePaths.Length; i++)
                {
                    var sourcePath = sourcePaths[i];
                    var destinationPath = destinationPaths[i];

                    if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destinationPath))
                    {
                        response.Errors ??= new();
                        response.Errors.Add(Error.SourceOrDestinationPathIsEmpty());
                        continue;
                    }
                    if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
                    {
                        response.Errors ??= new();
                        response.Errors.Add($"Failed to copy asset from {sourcePath} to {destinationPath}.");
                        continue;
                    }
                    var newAssetType = AssetDatabase.GetMainAssetTypeAtPath(destinationPath);
                    var newAsset = AssetDatabase.LoadAssetAtPath(destinationPath, newAssetType);

                    response.CopiedAssets ??= new();
                    response.CopiedAssets.Add(new AssetObjectRef(newAsset));
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                EditorUtils.RepaintAllEditorWindows();

                return response;
            });
        }

    }
}
