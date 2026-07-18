#nullable enable
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using com.SSS135.EditorAutomation.Editor.Framework;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public static partial class Tool_Script
    {
        public const string ScriptReadToolId = "script-read";
        [McpTool
        (
            ScriptReadToolId,
            Title = "Script / Read",
            ReadOnlyHint = true,
            IdempotentHint = true
        )]
        [Description("Reads the content of a script file and returns it as a string. " +
            "Use '" + ScriptUpdateOrCreateToolId + "' tool to update or create script files.")]
        public static string Read
        (
            [Description("The path to the file. Sample: \"Assets/Scripts/MyScript.cs\".")]
            string filePath,
            [Description("The line number to start reading from (1-based).")]
            int lineFrom = 1,
            [Description("The line number to stop reading at (1-based, -1 for all lines).")]
            int lineTo = -1
        )
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException(Error.ScriptPathIsEmpty(), nameof(filePath));

            if (!filePath.EndsWith(".cs"))
                throw new ArgumentException(Error.FilePathMustEndsWithCs(), nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException(Error.ScriptFileNotFound(filePath), filePath);

            var lines = File.ReadAllLines(filePath);

            if (lineFrom < 1 || lineFrom > lines.Length)
                lineFrom = 1;
            if (lineTo == -1 || lineTo > lines.Length)
                lineTo = lines.Length;
            if (lineTo < 1)
                lineTo = lines.Length;
            if (lineFrom > lineTo)
                lineFrom = lineTo;

            int startIndex = lineFrom - 1; // Convert from 1-based to 0-based indexing
            int count = lineTo - lineFrom + 1; // Inclusive range: count of lines to take

            return string.Join("\n", lines.Skip(startIndex).Take(count));
        }
    }
}
