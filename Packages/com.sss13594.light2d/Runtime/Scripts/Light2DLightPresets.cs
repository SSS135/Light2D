namespace Light2D
{
    /// <summary>
    /// Runtime, data-only map from a light's <see cref="LightSprite.ShadowQuality"/> to the GUID of the
    /// ray-march material asset it uses. It holds GUID strings only (no <c>AssetDatabase</c>) so the
    /// runtime assembly stays editor-independent (ContractTests); editor callers resolve the GUID to the
    /// material asset. <see cref="LightSprite.ShadowQuality.Custom"/> has no preset material and returns
    /// null so the raw <see cref="CustomSprite.Material"/> slot is used verbatim.
    /// </summary>
    internal static class Light2DLightPresets
    {
        // Verified GUIDs of the retained Runtime/Materials/*.mat assets.
        internal const string Light8PointsGuid = "9a4db11e5d304784685eaee65120aff9";
        internal const string Light40PointsGuid = "ab37f3042fbc56e4b9288c3ba9cd32d8";
        internal const string Light90PointsGuid = "eba84647c4f5b25448790591a30ca17e";
        internal const string LightAutoPointsGuid = "04a39e1d85156e34bb45d05db1a5eb00";
        internal const string LightIgnoreObstaclesGuid = "dd2043e5cd5311e4fbe905ebe0644ed1";

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
    }
}
