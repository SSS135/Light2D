#nullable enable
#if UNITY_EDITOR
using com.SSS135.EditorAutomation.Runtime.Extensions;
using UnityEngine.SceneManagement;

namespace com.SSS135.EditorAutomation.Runtime.Utils
{
    public static partial class GameObjectUtils
    {
        /// <summary>
        /// Find Root GameObject in opened Prefab. Of array of GameObjects in a scene.
        /// </summary>
        /// <param name="scene">Scene for the search, if null the current active scene would be used</param>
        /// <returns>Array of root GameObjects</returns>
        public static UnityEngine.GameObject[] FindRootGameObjects(Scene? scene = null)
        {
            var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
                return prefabStage.prefabContentsRoot.MakeArray();

            if (scene == null)
            {
                var rootGos = UnityEditor.SceneManagement.EditorSceneManager
                    .GetActiveScene()
                    .GetRootGameObjects();

                return rootGos;
            }
            else
            {
                return scene.Value.GetRootGameObjects();
            }
        }
        public static UnityEngine.GameObject? FindByInstanceID(UnityEngine.EntityId instanceID)
        {
            if (instanceID == UnityEngine.EntityId.None)
                return null;

            var obj = UnityEditor.EditorUtility.EntityIdToObject(instanceID);
            if (obj is not UnityEngine.GameObject go)
                return null;

            return go;
        }
    }
}
#endif
