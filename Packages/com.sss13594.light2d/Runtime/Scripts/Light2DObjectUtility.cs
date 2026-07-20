using UnityEngine;

namespace Light2D
{
    internal static class Light2DObjectUtility
    {
        internal static void Destroy(Object value)
        {
            if (value == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(value);
            else
                Object.DestroyImmediate(value);
        }

        internal static LightingSystem ResolveOwner(LightingSystem explicitOwner)
        {
            if (explicitOwner != null)
                return explicitOwner;

            LightingSystem resolved = null;
            var instances = LightingSystem.Instances;
            for (var i = 0; i < instances.Count; i++)
            {
                var candidate = instances[i];
                if (candidate == null || !candidate.isActiveAndEnabled)
                    continue;
                if (resolved != null)
                    return null;
                resolved = candidate;
            }
            return resolved;
        }

        internal static bool IsInPrefabStage(GameObject gameObject)
        {
#if UNITY_EDITOR
            return UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject) != null;
#else
            return false;
#endif
        }
    }
}
