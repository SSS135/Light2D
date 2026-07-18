#nullable enable
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.IvanMurzak.ReflectorNet.Utils;
using EA;
using com.SSS135.EditorAutomation.Runtime.Utils;
using com.SSS135.EditorAutomation.Utils;
using Microsoft.Extensions.Logging;
using UnityEditor;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Scene
    {
        public const string SceneUnloadToolId = "scene-unload";
        [McpTool
        (
            SceneUnloadToolId,
            Title = "Scene / Unload"
        )]
        [Description("Unload scene from the Opened scenes in Unity Editor. " +
            "Use '" + SceneListOpenedToolId + "' tool to get the list of all opened scenes.")]
        public Task<UnloadSceneResult> Unload
        (
            [Description("Name of the loaded scene.")]
            string name
        )
        {
            return MainThread.Instance.Run(async () =>
            {
                var logger = UnityLoggerFactory.LoggerFactory.CreateLogger<Tool_Scene>();

                if (string.IsNullOrEmpty(name))
                    throw new ArgumentException(Error.SceneNameIsEmpty(), nameof(name));

                var scene = SceneUtils.GetAllOpenedScenes()
                    .FirstOrDefault(scene => scene.name == name);

                if (!scene.IsValid())
                    throw new ArgumentException(Error.NotFoundSceneWithName(name), nameof(name));

                var scenePath = scene.path;
                logger.LogInformation("Unloading scene '{Name}' at path '{Path}'", name, scenePath);

                var asyncOperation = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);

                while (!asyncOperation.isDone)
                    await Task.Yield();

                logger.LogInformation("Successfully unloaded scene '{Name}'", name);

                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);

                return new UnloadSceneResult
                {
                    Name = name,
                    AssetObjectRef = sceneAsset == null
                        ? null
                        : new AssetObjectRef(sceneAsset)
                };
            });
        }

    }
}
