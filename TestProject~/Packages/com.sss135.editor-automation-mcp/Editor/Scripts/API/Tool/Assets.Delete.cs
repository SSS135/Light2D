#nullable enable
using EA;
using System.Collections.Generic;
using System.ComponentModel;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.Utils;
using com.SSS135.EditorAutomation.Utils;
using Microsoft.Extensions.Logging;
using UnityEditor;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Assets
    {
        public const string AssetsDeleteToolId = "assets-delete";
        [McpTool
        (
            AssetsDeleteToolId,
            Title = "Assets / Delete",
            DestructiveHint = true
        )]
        [Description("Delete the assets at paths from the project. " +
            "Does AssetDatabase.Refresh() at the end. " +
            "Use '" + AssetsFindToolId + "' tool to find assets before deleting.")]
        public DeleteAssetsResponse Delete
        (
            [Description("The paths of the assets")]
            string[] paths
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var logger = UnityLoggerFactory.LoggerFactory.CreateLogger<Tool_Assets>();

                if (paths.Length == 0)
                    throw new System.Exception(Error.SourcePathsArrayIsEmpty());

                logger.LogInformation("Deleting {Count} asset(s): {Paths}", paths.Length, string.Join(", ", paths));

                var response = new DeleteAssetsResponse();
                var outFailedPaths = new List<string>();
                var success = AssetDatabase.DeleteAssets(paths, outFailedPaths);

                if (!success)
                {
                    response.Errors ??= new();
                    foreach (var failedPath in outFailedPaths)
                    {
                        logger.LogWarning("Failed to delete asset at '{Path}'", failedPath);
                        response.Errors.Add($"Failed to delete asset at {failedPath}.");
                    }
                }

                // Add successfully deleted paths
                foreach (var path in paths)
                {
                    if (!outFailedPaths.Contains(path))
                    {
                        logger.LogInformation("Successfully deleted asset at '{Path}'", path);
                        response.DeletedPaths ??= new();
                        response.DeletedPaths.Add(path);
                    }
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                EditorUtils.RepaintAllEditorWindows();

                return response;
            });
        }

    }
}
