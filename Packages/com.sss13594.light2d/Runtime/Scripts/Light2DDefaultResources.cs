using UnityEngine;

namespace Light2D
{
    /// <summary>
    /// ScriptableObject holding the three default composition materials that the editor creation window
    /// assigns to a new LightingSystem. Edit-time-only: it lives under Runtime/ (not a Resources folder)
    /// so it is never bundled into player builds.
    /// </summary>
    internal sealed class Light2DDefaultResources : ScriptableObject
    {
        public Material AmbientLightComputeMaterial;
        public Material LightOverlayMaterial;
        public Material BlurMaterial;
    }
}
