#nullable enable
using System.ComponentModel;
using System.Linq;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.IvanMurzak.ReflectorNet.Utils;
using EA;
using UnityEditor;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Editor_Selection
    {
        public const string EditorSelectionGetToolId = "editor-selection-get";
        [McpTool
        (
            EditorSelectionGetToolId,
            Title = "Editor / Selection / Get",
            ReadOnlyHint = true,
            IdempotentHint = true
        )]
        [Description("Get information about the current Selection in the Unity Editor. " +
            "Use '" + EditorSelectionSetToolId + "' tool to set the selection.")]
        public SelectionData Get(
            bool includeGameObjects = false,
            bool includeTransforms = false,
            bool includeInstanceIDs = false,
            bool includeAssetGUIDs = false,
            bool includeActiveObject = true,
            bool includeActiveTransform = true)
        {
            return MainThread.Instance.Run(() =>
            {
                var response = new SelectionData()
                {
                    ActiveGameObject = Selection.activeGameObject != null
                        ? new GameObjectRef(Selection.activeGameObject)
                        : null,
                    ActiveInstanceID = Selection.activeEntityId
                };

                if (includeGameObjects)
                    response.GameObjects = Selection.gameObjects?.Select(go => new GameObjectRef(go)).ToArray();

                if (includeTransforms)
                    response.Transforms = Selection.transforms?.Select(t => new ComponentRef(t)).ToArray();

                if (includeInstanceIDs)
                    response.InstanceIDs = Selection.entityIds.Select(x => x).ToArray();

                if (includeAssetGUIDs)
                    response.AssetGUIDs = Selection.assetGUIDs;

                if (includeActiveObject)
                    response.ActiveObject = Selection.activeObject != null
                        ? new ObjectRef(Selection.activeObject)
                        : null;

                if (includeActiveTransform)
                    response.ActiveTransform = Selection.activeTransform != null
                        ? new ComponentRef(Selection.activeTransform)
                        : null;

                return response;
            });
        }
    }
}
