#nullable enable
using EA;
using System.ComponentModel;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Editor
    {
        public const string EditorApplicationGetStateToolId = "editor-application-get-state";
        [McpTool
        (
            EditorApplicationGetStateToolId,
            Title = "Editor / Application / Get State",
            ReadOnlyHint = true,
            IdempotentHint = true
        )]
        [Description("Returns available information about 'UnityEditor.EditorApplication'. " +
            "Use it to get information about the current state of the Unity Editor application. " +
            "Such as: playmode, paused state, compilation state, etc.")]
        public EditorStatsData? GetApplicationState(string? nothing = null)
        {
            return MainThread.Instance.Run(() =>
            {
                return EditorStatsData.FromEditor();
            });
        }
    }
}
