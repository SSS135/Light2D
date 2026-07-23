using UnityEngine;

namespace Light2D
{
    /// <summary>
    /// ScriptableObject holding the default composition materials that the editor creation window assigns
    /// to a new LightingSystem, plus the default obstacle material the obstacle components seed into their
    /// hidden Material slot on Reset. Edit-time-only: it lives under Runtime/ (not a Resources folder) so it
    /// is never bundled into player builds.
    /// </summary>
    internal sealed class Light2DDefaultResources : ScriptableObject
    {
        public Material AmbientLightComputeMaterial;
        public Material LightOverlayMaterial;
        public Material BlurMaterial;

        // Obstacle-writing shader carrier. The obstacle components hide their Material slot and seed it from
        // here when unset (edit-time Reset); the slot supplies only the shader, so there are no
        // user-meaningful values to expose.
        public Material ObstacleMaterial;

#if UNITY_EDITOR
        // Edit-time load of the packaged default-resources asset. Player builds compile this out (the asset
        // is never bundled), so runtime code paths must fall back separately.
        internal static Light2DDefaultResources Load()
        {
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Light2DDefaultResources>(
                "Packages/com.sss13594.light2d/Runtime/Light2DDefaultResources.asset");
        }
#endif
    }
}
