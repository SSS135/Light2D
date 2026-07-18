#nullable enable
using System.ComponentModel;
using System.Linq;
using com.SSS135.EditorAutomation.Editor.Framework;
using EA;
using UnityEditor;

namespace com.SSS135.EditorAutomation.Editor.API
{
    [McpToolType]
    public partial class Tool_Editor_Selection
    {
        public static class Error
        {
            public static string ScriptPathIsEmpty()
                => "Script path is empty. Please provide a valid path. Sample: \"Assets/Scripts/MyScript.cs\".";
        }

    }
}
