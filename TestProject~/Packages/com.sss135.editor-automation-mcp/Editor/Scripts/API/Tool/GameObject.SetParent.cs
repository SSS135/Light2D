#nullable enable
using System.ComponentModel;
using System.Text;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.Utils;
using EA;
using com.SSS135.EditorAutomation.Runtime.Extensions;
using UnityEditor.SceneManagement;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_GameObject
    {
        public const string GameObjectSetParentToolId = "gameobject-set-parent";
        [McpTool
        (
            GameObjectSetParentToolId,
            Title = "GameObject / Set Parent",
            IdempotentHint = true
        )]
        [Description("Set parent GameObject to list of GameObjects in opened Prefab or in a Scene. " +
            "Use '" + GameObjectFindToolId + "' tool to find the target GameObjects first.")]
        public string SetParent
        (
            [Description("List of references to the GameObjects to set new parent.")]
            GameObjectRefList gameObjectRefs,
            [Description("Reference to the parent GameObject.")]
            GameObjectRef parentGameObjectRef,
            [Description("A boolean flag indicating whether the GameObject's world position should remain unchanged when setting its parent.")]
            bool worldPositionStays = true
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var stringBuilder = new StringBuilder();
                int changedCount = 0;

                var parentGo = parentGameObjectRef.FindGameObject(out var error);
                if (error != null)
                {
                    stringBuilder.AppendLine(error);
                    return stringBuilder.ToString();
                }
                if (parentGo == null)
                {
                    stringBuilder.AppendLine($"[Error] GameObject by {nameof(parentGameObjectRef)} not found.");
                    return stringBuilder.ToString();
                }

                for (var i = 0; i < gameObjectRefs.Count; i++)
                {
                    var targetGo = gameObjectRefs[i].FindGameObject(out error);
                    if (error != null)
                    {
                        stringBuilder.AppendLine(error);
                        continue;
                    }
                    if (targetGo == null)
                    {
                        stringBuilder.AppendLine($"[Error] GameObject by {nameof(gameObjectRefs)}[{i}] not found.");
                        continue;
                    }

                    targetGo.transform.SetParent(parentGo.transform, worldPositionStays: worldPositionStays);
                    changedCount++;

                    stringBuilder.AppendLine(@$"[Success] Set parent of {gameObjectRefs[i]} to {parentGameObjectRef}.");
                }

                if (changedCount > 0)
                {
                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                    EditorUtils.RepaintAllEditorWindows();
                }

                return stringBuilder.ToString();
            });
        }
    }
}
