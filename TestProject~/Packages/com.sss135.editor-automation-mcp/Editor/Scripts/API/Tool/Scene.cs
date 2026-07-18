#nullable enable
using System.Collections.Generic;
using System.Linq;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Runtime.Utils;

namespace com.SSS135.EditorAutomation.Editor.API
{
    [McpToolType]
    public partial class Tool_Scene
    {
        public static IEnumerable<UnityEngine.SceneManagement.Scene> OpenedScenes => SceneUtils.GetAllOpenedScenes();
        public static string OpenedScenesText
            => $"Opened Scenes:\n{string.Join("\n", SceneUtils.GetAllOpenedScenes().Select(scene => scene.name))}";

        public static class Error
        {
            static string ScenesPrinted => string.Join("\n", SceneUtils.GetAllOpenedScenes().Select(scene => scene.name));

            public static string SceneNameIsEmpty()
                => $"Scene name is empty. Available scenes:\n{ScenesPrinted}";
            public static string NotFoundSceneWithName(string? name)
                => $"Scene '{name ?? "null"}' not found. Available scenes:\n{ScenesPrinted}";
            public static string ScenePathIsEmpty()
                => "Scene path is empty. Please provide a valid path. Sample: \"Assets/Scenes/MyScene.unity\".";
            public static string FilePathMustEndsWithUnity()
                => "File path must end with '.unity'. Please provide a valid path. Sample: \"Assets/Scenes/MyScene.unity\".";
            public static string InvalidLoadSceneMode(int loadSceneMode)
                => $"Invalid load scene mode '{loadSceneMode}'. Valid values are 0 (Single) and 1 (Additive).";
        }
    }
}
