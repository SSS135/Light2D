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
        public const string SceneOpenToolId = "scene-open";
        [McpTool
        (
            SceneOpenToolId,
            Title = "Scene / Open"
        )]
        [Description("Open scene from the project asset file. " +
            "Use '" + Tool_Assets.AssetsFindToolId + "' tool to find the scene asset first.")]
        public SceneDataShallow[] Open
        (
            AssetObjectRef sceneRef,
            [Description("Open scene mode. " +
                "Single: closes the current scenes and opens a new one. " +
                "Additive: keeps the current scene and opens additional one.")]
            UnityEditor.SceneManagement.OpenSceneMode loadSceneMode = UnityEditor.SceneManagement.OpenSceneMode.Single
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var sceneAsset = sceneRef.FindAssetObject<UnityEditor.SceneAsset>()
                    ?? throw new System.ArgumentException($"Requested scene is not valid or not found.");

                var scenePath = sceneAsset.GetAssetPath();

                var sceneOpened = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, loadSceneMode);

                if (!sceneOpened.IsValid())
                    throw new System.Exception($"Failed to load scene at '{scenePath}'.\n{OpenedScenesText}");

                EditorUtils.RepaintAllEditorWindows();

                return OpenedScenes
                    .Select(scene => scene.ToSceneDataShallow())
                    .ToArray();
            });
        }
    }
}
