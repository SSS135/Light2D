#nullable enable
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Editor.Utils;
using UnityEditor;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public static partial class Tool_Script
    {
        public const string ScriptDeleteToolId = "script-delete";
        [McpTool
        (
            ScriptDeleteToolId,
            Title = "Script / Delete",
            DestructiveHint = true
        )]
        [Description("Delete the script file(s). " +
            "Does AssetDatabase.Refresh() and waits for Unity compilation to complete before reporting results. " +
            "Use '" + ScriptReadToolId + "' tool to read existing script files first.")]
        public static McpResponse Delete
        (
            [Description("File paths to the files. Sample: \"Assets/Scripts/MyScript.cs\".")]
            string[] files,
            [RequestID]
            string? requestId = null
        )
        {
            if (requestId == null || string.IsNullOrWhiteSpace(requestId))
                return McpResponse.Error("Original request with valid RequestID must be provided.");

            if (files == null || files.Length == 0)
                return McpResponse.Error(Error.ScriptPathIsEmpty());

            if (files.Any(f => string.IsNullOrEmpty(f)))
                return McpResponse.Error(Error.ScriptPathIsEmpty());

            if (!files.All(f => f.EndsWith(".cs")))
                return McpResponse.Error(Error.FilePathMustEndsWithCs());

            var invalidFiles = files.Where(f => !File.Exists(f)).ToArray();
            if (invalidFiles.Length > 0)
                return McpResponse.Error(Error.ScriptFileNotFound(invalidFiles));

            foreach (var f in files)
            {
                File.Delete(f);
                if (File.Exists(f + ".meta"))
                    File.Delete(f + ".meta");
            }

            MainThread.Instance.RunAsync(async () =>
            {
                await Task.Yield();
                // Schedule notification to be sent after compilation completes (survives domain reload)
                ScriptUtils.SchedulePostCompilationNotification(requestId, string.Join(",", files), "Script deletion");

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            });

            var scriptWord = files.Length > 1 ? "Scripts" : "Script";
            return McpResponse.Processing($"{scriptWord} deleted. Refreshing AssetDatabase and waiting for compilation to complete...");
        }
    }
}
