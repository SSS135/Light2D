using System.Collections.Generic;
using UnityEngine;

namespace Light2D
{
    /// <summary>
    /// Maps a light's <see cref="LightSprite.ShadowQuality"/> to the ray-march material it draws through, and
    /// resolves that material so a <see cref="LightSprite"/> with no assigned <see cref="CustomSprite.Material"/>
    /// "just works". <see cref="GetMaterialGuid"/> remains a pure, editor-independent GUID lookup (used by the
    /// inspector to reverse-map an assigned material back to a preset). <see cref="ResolvePresetMaterial"/> adds
    /// the actual material: in the editor it loads the packaged <c>Runtime/Materials/*.mat</c> asset verbatim
    /// (byte-exact authored properties); in a player build - where those .mat assets are not loadable - it
    /// builds an equivalent cached material from the packaged light shaders (which live under
    /// <c>Runtime/Resources/Shaders</c> and are <c>Shader.Find</c>-able) plus the authored property values.
    /// <see cref="LightSprite.ShadowQuality.Custom"/> has no preset material and returns null so the raw
    /// <see cref="CustomSprite.Material"/> slot is used verbatim.
    /// </summary>
    internal static class Light2DLightPresets
    {
        // Verified GUIDs of the retained Runtime/Materials/*.mat assets.
        internal const string Light8PointsGuid = "9a4db11e5d304784685eaee65120aff9";
        internal const string Light40PointsGuid = "ab37f3042fbc56e4b9288c3ba9cd32d8";
        internal const string Light90PointsGuid = "eba84647c4f5b25448790591a30ca17e";
        internal const string LightAutoPointsGuid = "04a39e1d85156e34bb45d05db1a5eb00";
        internal const string LightIgnoreObstaclesGuid = "dd2043e5cd5311e4fbe905ebe0644ed1";

        // Shader.Find names backing each preset, matching the m_Shader GUID authored into the .mat assets.
        private const string LightPointsShader = "Light2D/Light Points";
        private const string LightAutoPointsShader = "Light2D/Light Auto Points";
        private const string LightIgnoreObstaclesShader = "Light2D/Light Ignoring Obstacles";

        // Build-safe per-preset materials created from the packaged light shaders (no editor asset load),
        // keyed by quality. HideAndDontSave so they persist without being saved; created once and reused
        // across every auto-resolving light (mirrors CustomSprite.GetRuntimeDefaultObstacleMaterial).
        private static readonly Dictionary<LightSprite.ShadowQuality, Material> RuntimePresets =
            new Dictionary<LightSprite.ShadowQuality, Material>();

        private static readonly int ObstacleMulId = Shader.PropertyToID("_ObstacleMul");
        private static readonly int EmissionColorMulId = Shader.PropertyToID("_EmissionColorMul");
        private static readonly int PathTrackingSamplesId = Shader.PropertyToID("_PathTrackingSamples");
        private static readonly int StepCountMulId = Shader.PropertyToID("_StepCountMul");

        // GUID of the material asset backing a shadow-quality level, or null for Custom (raw slot).
        internal static string GetMaterialGuid(LightSprite.ShadowQuality quality)
        {
            switch (quality)
            {
                case LightSprite.ShadowQuality.Fast:
                    return Light8PointsGuid;
                case LightSprite.ShadowQuality.Balanced:
                    return Light40PointsGuid;
                case LightSprite.ShadowQuality.High:
                    return Light90PointsGuid;
                case LightSprite.ShadowQuality.Auto:
                    return LightAutoPointsGuid;
                case LightSprite.ShadowQuality.IgnoreObstacles:
                    return LightIgnoreObstaclesGuid;
                default:
                    return null;
            }
        }

        // The light material for a shadow-quality level, resolved for the current context. Editor: the
        // packaged .mat asset. Build: a cached material built from the shader + authored values. Custom
        // returns null so the raw Material slot is used verbatim.
        internal static Material ResolvePresetMaterial(LightSprite.ShadowQuality quality)
        {
            if (quality == LightSprite.ShadowQuality.Custom)
                return null;
#if UNITY_EDITOR
            var asset = LoadPresetAsset(quality);
            if (asset != null)
                return asset;
#endif
            return GetOrBuildRuntimePreset(quality);
        }

#if UNITY_EDITOR
        // Editor path: the packaged .mat asset (byte-exact authored properties), the same asset the inspector
        // reverse-maps and would assign explicitly. Mirrors LightObstacleSprite.OnEnable's GUID-load fallback.
        private static Material LoadPresetAsset(LightSprite.ShadowQuality quality)
        {
            var guid = GetMaterialGuid(quality);
            if (string.IsNullOrEmpty(guid))
                return null;
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path)
                ? null
                : UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
        }
#endif

        // Build path: construct the preset material from its shader + authored property values, matching the
        // packaged .mat for the properties the light shaders actually read.
        private static Material GetOrBuildRuntimePreset(LightSprite.ShadowQuality quality)
        {
            if (RuntimePresets.TryGetValue(quality, out var cached) && cached != null)
                return cached;
            RuntimePresets.Remove(quality);

            var shader = Shader.Find(GetShaderName(quality));
            if (shader == null)
                return null;

            var material = new Material(shader)
            {
                name = "Generated Light Preset Material (" + quality + ")",
                hideFlags = HideFlags.HideAndDontSave
            };
            ApplyPresetProperties(material, quality);
            RuntimePresets[quality] = material;
            return material;
        }

        private static string GetShaderName(LightSprite.ShadowQuality quality)
        {
            switch (quality)
            {
                case LightSprite.ShadowQuality.Auto:
                    return LightAutoPointsShader;
                case LightSprite.ShadowQuality.IgnoreObstacles:
                    return LightIgnoreObstaclesShader;
                default:
                    // Fast / Balanced / High share the collapsed ray-marched shader, differing only in
                    // _PathTrackingSamples (8 / 40 / 90).
                    return LightPointsShader;
            }
        }

        // The authored float values from the packaged .mat assets that the light shaders actually read. Only
        // properties the target shader declares are set: legacy serialized props the collapsed shaders no
        // longer read (_MinStepCount, _Shininess, _Color/_Emission/_SpecColor, and IgnoreObstacles' unused
        // _ObstacleMul) are intentionally omitted - they do not affect the rendered result.
        private static void ApplyPresetProperties(Material material, LightSprite.ShadowQuality quality)
        {
            switch (quality)
            {
                case LightSprite.ShadowQuality.Fast:
                    material.SetFloat(ObstacleMulId, 500f);
                    material.SetFloat(EmissionColorMulId, 1f);
                    material.SetFloat(PathTrackingSamplesId, 8f);
                    break;
                case LightSprite.ShadowQuality.Balanced:
                    material.SetFloat(ObstacleMulId, 500f);
                    material.SetFloat(EmissionColorMulId, 1f);
                    material.SetFloat(PathTrackingSamplesId, 40f);
                    break;
                case LightSprite.ShadowQuality.High:
                    material.SetFloat(ObstacleMulId, 500f);
                    material.SetFloat(EmissionColorMulId, 1f);
                    material.SetFloat(PathTrackingSamplesId, 90f);
                    break;
                case LightSprite.ShadowQuality.Auto:
                    material.SetFloat(ObstacleMulId, 100f);
                    material.SetFloat(EmissionColorMulId, 1f);
                    material.SetFloat(StepCountMulId, 0.5f);
                    break;
                case LightSprite.ShadowQuality.IgnoreObstacles:
                    material.SetFloat(EmissionColorMulId, 1f);
                    break;
            }
        }
    }
}
