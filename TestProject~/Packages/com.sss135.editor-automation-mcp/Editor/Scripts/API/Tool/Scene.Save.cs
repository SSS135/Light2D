#nullable enable
using System;
using System.ComponentModel;
using System.Linq;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.Utils;
using com.SSS135.EditorAutomation.Runtime.Utils;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Scene
    {
        public const string SceneSaveToolId = "scene-save";
        [McpTool
        (
            SceneSaveToolId,
            Title = "Scene / Save",
            IdempotentHint = true
        )]
        [Description("Save Opened scene to the asset file. " +
            "Use '" + SceneListOpenedToolId + "' tool to get the list of all opened scenes.")]
        public void Save
        (
            [Description("Name of the opened scene that should be saved. Could be empty if need to save the current active scene.")]
            string? openedSceneName = null,
            [Description("Path to the scene file. Should end with \".unity\". If null or empty save to the existed scene asset file.")]
            string? path = null
        )
        {
            MainThread.Instance.Run(() =>
            {
                var scene = string.IsNullOrEmpty(openedSceneName)
                    ? SceneUtils.GetActiveScene()
                    : SceneUtils.GetAllOpenedScenes()
                        .FirstOrDefault(scene => scene.name == openedSceneName);

                if (!scene.IsValid())
                    throw new Exception(Error.NotFoundSceneWithName(openedSceneName));

                if (string.IsNullOrEmpty(path))
                    path = scene.path;

                if (string.IsNullOrEmpty(path))
                    throw new Exception($"Scene '{scene.name}' has no path. Please provide a path to save the scene.");

                if (!path!.EndsWith(".unity"))
                    throw new Exception(Error.FilePathMustEndsWithUnity());

                bool saved = UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, path);
                if (!saved)
                    throw new Exception($"Failed to save scene at '{path}'.\n{OpenedScenesText}");

                EditorUtils.RepaintAllEditorWindows();
            });
        }
    }
}
