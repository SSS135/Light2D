#nullable enable
using System.ComponentModel;
using System.Linq;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.Utils;
using EA;
using com.SSS135.EditorAutomation.Runtime.Extensions;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Scene
    {
        public const string SceneSetActiveToolId = "scene-set-active";
        [McpTool
        (
            SceneSetActiveToolId,
            Title = "Scene / Set Active",
            IdempotentHint = true
        )]
        [Description("Set the specified opened scene as the active scene. " +
            "Use '" + SceneListOpenedToolId + "' tool to get the list of all opened scenes.")]
        public OpenedScenesResult SetActive(AssetObjectRef sceneRef)
        {
            return MainThread.Instance.Run(() =>
            {
                var sceneAsset = sceneRef.FindAssetObject<UnityEditor.SceneAsset>()
                    ?? throw new System.ArgumentException($"Requested scene is not valid or not found.");

                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(sceneAsset.name);
                if (scene.IsValid() == false)
                {
                    var scenePath = sceneAsset.GetAssetPath();
                    if (string.IsNullOrEmpty(scenePath))
                        throw new System.Exception(Error.ScenePathIsEmpty());

                    scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
                    if (scene.IsValid() == false)
                        throw new System.Exception($"Scene at '{scenePath}' is not opened.");
                }

                // If the scene is already active, just return opened scenes
                if (UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene() == scene)
                {
                    var alreadyActiveScenes = OpenedScenes
                        .Select(scene => scene.ToSceneDataShallow())
                        .ToArray();
                    return new OpenedScenesResult { Scenes = alreadyActiveScenes, Count = alreadyActiveScenes.Length };
                }

                var success = UnityEditor.SceneManagement.EditorSceneManager.SetActiveScene(scene);
                if (!success)
                    throw new System.Exception($"Failed to set active scene to '{scene.name}'.");

                EditorUtils.RepaintAllEditorWindows();

                var openedScenes = OpenedScenes
                    .Select(scene => scene.ToSceneDataShallow())
                    .ToArray();
                return new OpenedScenesResult { Scenes = openedScenes, Count = openedScenes.Length };
            });
        }
    }
}
