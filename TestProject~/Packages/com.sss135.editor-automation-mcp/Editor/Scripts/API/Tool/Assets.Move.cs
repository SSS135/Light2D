#nullable enable
using EA;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.Utils;
using UnityEditor;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Assets
    {
        public const string AssetsMoveToolId = "assets-move";
        [McpTool
        (
            AssetsMoveToolId,
            Title = "Assets / Move"
        )]
        [Description("Move the assets at paths in the project. " +
            "Should be used for asset rename. " +
            "Does AssetDatabase.Refresh() at the end. " +
            "Use '" + AssetsFindToolId + "' tool to find assets before moving.")]
        public MoveAssetsResponse Move
        (
            [Description("The paths of the assets to move.")]
            string[] sourcePaths,
            [Description("The paths of moved assets.")]
            string[] destinationPaths
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (sourcePaths.Length == 0)
                    throw new ArgumentException(Error.SourcePathsArrayIsEmpty(), nameof(sourcePaths));

                if (sourcePaths.Length != destinationPaths.Length)
                    throw new ArgumentException(Error.SourceAndDestinationPathsArrayMustBeOfTheSameLength());

                var response = new MoveAssetsResponse();

                for (int i = 0; i < sourcePaths.Length; i++)
                {
                    var error = AssetDatabase.MoveAsset(sourcePaths[i], destinationPaths[i]);
                    if (string.IsNullOrEmpty(error))
                    {
                        response.MovedPaths ??= new();
                        response.MovedPaths.Add(destinationPaths[i]);
                    }
                    else
                    {
                        response.Errors ??= new();
                        response.Errors.Add($"Failed to move asset from {sourcePaths[i]} to {destinationPaths[i]}: {error}.");
                    }
                }
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                EditorUtils.RepaintAllEditorWindows();
                return response;
            });
        }

    }
}
