#nullable enable
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Editor.Utils;
using UnityEditor;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public static partial class Tool_Script
    {
        public const string ScriptUpdateOrCreateToolId = "script-update-or-create";
        [McpTool
        (
            ScriptUpdateOrCreateToolId,
            Title = "Script / Update or Create",
            DestructiveHint = true,
            OpenWorldHint = false
        )]
        [Description("Updates or creates script file with the provided C# code. " +
            "Does AssetDatabase.Refresh() at the end. " +
            "Provides compilation error details if the code has syntax errors. " +
            "Use '" + ScriptReadToolId + "' tool to read existing script files first.")]
        public static McpResponse UpdateOrCreate
        (
            [Description("The path to the file. Sample: \"Assets/Scripts/MyScript.cs\".")]
            string filePath,
            [Description("C# code - content of the file.")]
            string content,
            [RequestID]
            string? requestId = null
        )
        {
            if (requestId == null || string.IsNullOrWhiteSpace(requestId))
                return McpResponse.Error("[Error] Original request with valid RequestID must be provided.");

            if (string.IsNullOrEmpty(filePath))
                return McpResponse.Error(Error.ScriptPathIsEmpty());

            if (!filePath.EndsWith(".cs"))
                return McpResponse.Error(Error.FilePathMustEndsWithCs());

            if (!ScriptUtils.IsValidCSharpSyntax(content, out var errors))
                return McpResponse.Error($"[Error] Invalid C# syntax:\n{string.Join("\n", errors)}");

            var dirPath = Path.GetDirectoryName(filePath)!;
            if (Directory.Exists(dirPath) == false)
                Directory.CreateDirectory(dirPath);

            var exists = File.Exists(filePath);

            File.WriteAllText(filePath, content);

            var scriptWord = exists
                ? "Script updated"
                : "Script created";

            MainThread.Instance.RunAsync(async () =>
            {
                await Task.Yield();

                // Schedule notification to be sent after compilation completes (survives domain reload)
                ScriptUtils.SchedulePostCompilationNotification(requestId, filePath, $"{scriptWord}");

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            });

            return McpResponse.Processing($"{scriptWord}. Refreshing AssetDatabase and waiting for compilation to complete...");
        }
    }
}
