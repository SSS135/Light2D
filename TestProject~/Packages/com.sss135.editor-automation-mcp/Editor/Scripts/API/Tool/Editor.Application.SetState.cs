#nullable enable
using EA;
using System;
using System.ComponentModel;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.Utils;
using UnityEditor;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Editor
    {
        public const string EditorApplicationSetStateToolId = "editor-application-set-state";
        [McpTool
        (
            EditorApplicationSetStateToolId,
            Title = "Editor / Application / Set State",
            IdempotentHint = true
        )]
        [Description("Control the Unity Editor application state. " +
            "You can start, stop, or pause the 'playmode'. " +
            "Use '" + EditorApplicationGetStateToolId + "' tool to get the current state first.")]
        public EditorStatsData? SetApplicationState
        (
            [Description("If true, the 'playmode' will be started. If false, the 'playmode' will be stopped.")]
            bool isPlaying = false,
            [Description("If true, the 'playmode' will be paused. If false, the 'playmode' will be resumed.")]
            bool isPaused = false
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (UnityEditor.EditorUtility.scriptCompilationFailed)
                {
                    var compilationErrorDetails = ScriptUtils.GetCompilationErrorDetails();
                    throw new Exception($"Unity project has compilation error. Please fix all compilation errors before doing this operation.\n{compilationErrorDetails}");
                }
                EditorApplication.isPlaying = isPlaying;
                EditorApplication.isPaused = isPaused;

                return EditorStatsData.FromEditor();
            });
        }
    }
}
