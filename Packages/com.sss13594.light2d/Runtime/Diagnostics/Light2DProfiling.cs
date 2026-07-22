using Unity.Profiling;

namespace Light2D
{
    public static class Light2DProfiling
    {
        public const string ValidationName = "Light2D.Validation";
        public const string ResourceReconciliationName = "Light2D.ResourceReconciliation";
        public const string CullingName = "Light2D.Culling";
        public const string ObstaclesName = "Light2D.Obstacles";
        public const string SourcesName = "Light2D.Sources";
        public const string NormalsName = "Light2D.Normals";
        public const string BlurName = "Light2D.Blur";
        public const string AmbientName = "Light2D.Ambient";
        public const string CompositionName = "Light2D.Composition";
        public const string LightTextureGenName = "Light2D.LightTextureGen";

        public static readonly ProfilerMarker Validation = new ProfilerMarker(ValidationName);
        public static readonly ProfilerMarker ResourceReconciliation = new ProfilerMarker(ResourceReconciliationName);
        public static readonly ProfilerMarker Culling = new ProfilerMarker(CullingName);
        public static readonly ProfilerMarker Obstacles = new ProfilerMarker(ObstaclesName);
        public static readonly ProfilerMarker Sources = new ProfilerMarker(SourcesName);
        public static readonly ProfilerMarker Normals = new ProfilerMarker(NormalsName);
        public static readonly ProfilerMarker Blur = new ProfilerMarker(BlurName);
        public static readonly ProfilerMarker Ambient = new ProfilerMarker(AmbientName);
        public static readonly ProfilerMarker Composition = new ProfilerMarker(CompositionName);
        public static readonly ProfilerMarker LightTextureGen = new ProfilerMarker(LightTextureGenName);
    }
}
