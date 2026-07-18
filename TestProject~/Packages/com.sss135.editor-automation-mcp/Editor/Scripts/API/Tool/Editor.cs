#nullable enable
using EA;
using System.ComponentModel;
using com.SSS135.EditorAutomation.Editor.Framework;
using UnityEditor;

namespace com.SSS135.EditorAutomation.Editor.API
{
    [McpToolType]
    public partial class Tool_Editor
    {
        public static class Error
        {
            public static string ScriptPathIsEmpty()
                => "Script path is empty. Please provide a valid path. Sample: \"Assets/Scripts/MyScript.cs\".";
        }

    }
}
