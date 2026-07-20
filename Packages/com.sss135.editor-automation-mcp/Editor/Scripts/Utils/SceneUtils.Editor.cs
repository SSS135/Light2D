#nullable enable
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace com.SSS135.EditorAutomation.Runtime.Utils
{
    public static partial class SceneUtils
    {
        public static IEnumerable<Scene> GetAllLoadedScenesInUnityEditor()
        {
            var sceneCount = UnityEditor.SceneManagement.EditorSceneManager.sceneCount;
            for (var i = 0; i < sceneCount; i++)
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                    yield return scene;
            }
        }
    }
}
#endif
