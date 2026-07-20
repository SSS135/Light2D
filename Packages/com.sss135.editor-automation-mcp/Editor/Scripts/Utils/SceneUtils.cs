#nullable enable
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace com.SSS135.EditorAutomation.Runtime.Utils
{
    public static partial class SceneUtils
    {
        public static Scene GetActiveScene() => SceneManager.GetActiveScene();

        public static IEnumerable<Scene> GetAllOpenedScenes()
        {
            var sceneCount = SceneManager.sceneCount;
            for (var i = 0; i < sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                    yield return scene;
            }
        }
    }
}
