#nullable enable
using System.ComponentModel;
using System.Linq;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.IvanMurzak.ReflectorNet.Utils;
using EA;
using com.SSS135.EditorAutomation.Runtime.Extensions;
using UnityEditor;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Editor_Selection
    {
        public const string EditorSelectionSetToolId = "editor-selection-set";
        [McpTool
        (
            EditorSelectionSetToolId,
            Title = "Editor / Selection / Set",
            IdempotentHint = true
        )]
        [Description("Set the current Selection in the Unity Editor to the provided objects. " +
            "Use '" + EditorSelectionGetToolId + "' tool to get the current selection first.")]
        public SelectionData Set(ObjectRef[] select)
        {
            return MainThread.Instance.Run(() =>
            {
                var objects = select.Select(o => o.FindObject()).ToArray();
                if (objects.Any(o => o == null))
                    throw new System.Exception("One or more objects could not be found. Please ensure all provided ObjectRefs are valid.");

                Selection.objects = objects;

                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

                return SelectionData.FromSelection();
            });
        }
    }
}
