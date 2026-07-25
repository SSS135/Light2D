using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Light2D
{
    /// <summary>
    /// Render pipeline classification used by Light2D readiness and diagnostics.
    /// </summary>
    public enum Light2DPipeline
    {
        BuiltIn,
        UniversalRenderPipeline,
        UnsupportedScriptableRenderPipeline
    }

    /// <summary>
    /// Main script for lights. Should be attached to camera.
    /// </summary>
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Light2D/Light 2D System")]
    [HelpURL(Light2DDocs.HelpUrl)]
    public class LightingSystem : MonoBehaviour
    {
        [Min(1)]
        [Tooltip("Maximum queued ambient warmup cycles processed in a single frame; caps the per-frame " +
                 "propagation cost while the ambient history catches up (default 8).")]
        public int MaxAmbientCyclesPerFrame = 8;

        [Min(1)]
        [Tooltip("Maximum number of ambient warmup cycles that can be queued at once; a longer queue lets the " +
                 "ambient history re-propagate from further back at the cost of more catch-up frames (default 64).")]
        public int MaxQueuedAmbientCycles = 64;

        [Tooltip("Upper bound on the total pixels of the internal light textures; a frame whose requested " +
                 "textures exceed this budget is skipped rather than allocated (default 16M).")]
        public long MaxLightTexturePixels = 16L * 1024L * 1024L;

        [Min(0)]
        [Tooltip("Warmup cycles queued when the ambient history is invalidated by a rotation/zoom/aspect " +
                 "change or a teleport; mirrors the resource-change warmup so the history re-propagates from " +
                 "the cleared state (default 100).")]
        public int AmbientWarmupCycles = 100;

        [Tooltip("World size of one lighting-texture pixel; larger values lower the lighting resolution " +
                 "and cost (default 0.05).")]
        public float LightPixelSize = 0.05f;

        [Tooltip("Extra orthographic half-size added to the light camera so obstacles and lights just " +
                 "off-screen still affect the visible area (default 3).")]
        public float LightCameraSizeAdd = 3;

        [Tooltip("Extra field of view added to the light camera in perspective mode so off-screen casters " +
                 "still contribute (default 30).")]
        public float LightCameraFovAdd = 30;

        [Tooltip("Enables the temporal ambient-light propagation pass (default on).")]
        public bool EnableAmbientLight = true;

        [Tooltip("Blurs the light-sources texture for softer light edges (default on).")]
        public bool BlurLightSources = true;

        [Tooltip("Blurs the ambient-light texture each propagation cycle for smoother ambient (default on).")]
        public bool BlurAmbientLight = true;

        [Range(0f, 1f)]
        [Tooltip("Ambient bounce tint: how much a colored obstacle tints the indirect ambient fill instead " +
                 "of only darkening it; 0 = off (neutral, existing behavior), 1 = full colored bounce.")]
        public float AmbientColoredBounce = 0f;

        [Min(1)]
        [Tooltip("Ambient bounce: base propagation iterations run each frame; higher spreads ambient farther " +
                 "per frame at more cost (default 1, matching the original single-step behavior).")]
        public int AmbientIterations = 1;

        [Min(0)]
        [Tooltip("Overlay: overall brightness applied to the composited lighting result " +
                 "(shader _LightMul, default 2). Always applied every frame.")]
        public float LightBrightness = 2f;

        [Min(0)]
        [Tooltip("Overlay: brightness applied to the light-sources contribution " +
                 "(shader _LightSourcesMul, default 1). Always applied every frame.")]
        public float LightSourcesBrightness = 1f;

        [Min(0)]
        [Tooltip("Overlay: exponent shaping the additive bloom falloff of bright lights " +
                 "(shader _AdditiveLightPow, default 4). Always applied every frame.")]
        public float BloomPower = 4f;

        [Min(0)]
        [Tooltip("Overlay: additive bloom offset added to bright lights " +
                 "(shader _AdditiveLightAdd, default 0.15). Always applied every frame.")]
        public float BloomAdd = 0.15f;

        [Min(0)]
        [Tooltip("Ambient: how strongly obstacles attenuate the ambient propagation " +
                 "(shader _ObstacleMul, default 1.5). Always applied every frame.")]
        public float AmbientObstacleMul = 1.5f;

        [Min(0)]
        [Tooltip("Ambient: additive term applied alongside the obstacle attenuation " +
                 "(shader _ObstacleAdd, default 0.1). Always applied every frame.")]
        public float AmbientObstacleAdd = 0.1f;

        [Min(0)]
        [Tooltip("Ambient: emission strength contributed by ambient emitters " +
                 "(shader _EmissionColorMul, default 0.1). Always applied every frame.")]
        public float AmbientEmissionMul = 0.1f;

        [Min(0)]
        [Tooltip("Ambient: world-space distance the propagation samples each cycle " +
                 "(shader _SamplingDist, default 0.01). Always applied every frame.")]
        public float AmbientSamplingDistance = 0.01f;

        [Range(0f, 1f)]
        [Tooltip("Ambient: how quickly the ambient history adapts toward the new state each cycle " +
                 "(shader _AmbientAdaptSpeed, default 0.2). Always applied every frame.")]
        public float AmbientAdaptSpeed = 0.2f;

        [Min(0)]
        [Tooltip("Blur: sampling distance of the light-sources and ambient blur passes " +
                 "(shader _Distance, default 2.5). Always applied every frame.")]
        public float BlurDistance = 2.5f;

        [Tooltip("Uses a half-float (ARGBHalf) light buffer for higher dynamic range when the platform " +
                 "supports it (default on).")]
        public bool HDR = true;

        [Tooltip("Renders obstacles at double resolution and downsamples for smoother obstacle edges " +
                 "(default on).")]
        public bool LightObstaclesAntialiasing = true;

        [Tooltip("Distance from the camera to the obstacle/light plane, used to project casters in " +
                 "perspective mode (default 10).")]
        public float LightObstaclesDistance = 10;

        [Tooltip("Global obstacle multiplier: multiplies every obstacle's color and alpha before it occludes " +
                 "light (direct and ambient alike), on top of each obstacle's own Multiplicative Color. White " +
                 "with full alpha (the default) is a no-op that leaves all obstacles unchanged; lowering the " +
                 "alpha makes every wall more transparent with one control, and the RGB tints their " +
                 "transmission. Always applied every frame.")]
        public Color ObstacleColor = Color.white;

        [Tooltip("Filter mode of the internal light textures; Bilinear softens, Point keeps hard pixels " +
                 "(default Bilinear).")]
        public FilterMode LightTexturesFilterMode = FilterMode.Bilinear;

        [Tooltip("Enables normal-mapped light sources; supported on the XY plane with an orthographic " +
                 "camera only (default off).")]
        public bool EnableNormalMapping;

        [Tooltip("Confines the lighting result to this camera through an intermediate target instead of " +
                 "compositing to the screen (default off).")]
        public bool AffectOnlyThisCamera;

        [Range(0f, 1f)]
        [Tooltip("World coverage baseline used as the coverage-mask clear value. 1 = Light2D applies " +
                 "everywhere (default, identical to the legacy behavior and allocation-free); 0 = the " +
                 "world shows the raw game image and Light2DVolumes with higher coverage re-enable " +
                 "lighting locally (the 'bright world, dark caves' flow).")]
        public float DefaultCoverage = 1f;

        [Tooltip("Material running the ambient-light propagation pass; leave empty to use the packaged " +
                 "ambient compute material.")]
        public Material AmbientLightComputeMaterial;

        [Tooltip("Material compositing light sources and ambient over the game image. Required: Light2D " +
                 "does not render without it.")]
        public Material LightOverlayMaterial;

        [Tooltip("Material used to blur the light-sources texture when Blur Light Sources is enabled.")]
        public Material LightSourcesBlurMaterial;

        [Tooltip("Material used to blur the ambient-light texture when Blur Ambient Light is enabled.")]
        public Material AmbientLightBlurMaterial;

        [Tooltip("Treats the world XZ plane as the lighting plane for top-down games instead of the XY " +
                 "plane (default off).")]
        public bool XZPlane;

        private static readonly HashSet<LightingSystem> ActiveInstances = new HashSet<LightingSystem>();
        private static readonly List<LightingSystem> InstanceSnapshot = new List<LightingSystem>();

        private Camera _camera;
        private LightingRenderResources _resources;
        private ObstacleCameraPostPorcessor _obstaclesPostProcessor;
        private readonly List<LightSprite> _lightSpritesCache = new List<LightSprite>();
        private readonly List<Light2DDrawEntry> _sourceSnapshot = new List<Light2DDrawEntry>();
        private readonly List<Light2DDrawEntry> _obstacleSnapshot = new List<Light2DDrawEntry>();
        private readonly List<Light2DDrawEntry> _ambientSnapshot = new List<Light2DDrawEntry>();
        private readonly List<Light2DDrawEntry> _coverageSnapshot = new List<Light2DDrawEntry>();
        private readonly Plane[] _extendedPlanes = new Plane[6];
        private readonly Plane[] _smallPlanes = new Plane[6];
        private MaterialPropertyBlock _normalLightProperties;
        private MaterialPropertyBlock _obstacleProperties;
        private Vector2Int _extendedLightTextureSize;
        private Vector2Int _smallLightTextureSize;
        private Vector3 _oldPos;
        private Vector3 _currPos;
        private Quaternion _prevRotation;
        private float _prevProjectionSize;
        private float _prevAspect;
        private bool _hasPrevTransform;
        private RenderTextureFormat _texFormat;
        private int _aditionalAmbientLightCycles;
        private bool _initialized;
        private int _settingsHash;
        private bool _hasSourceDescriptor;
        private RenderTextureDescriptor _sourceDescriptor;
        private bool _mainCameraStateCaptured;
        private RenderTexture _mainCameraTarget;
        private CameraClearFlags _mainCameraClearFlags;
        private Color _mainCameraBackground;
#pragma warning disable CS0618 // FlareLayer is obsolete from Unity 6000.5 (Built-in Render Pipeline deprecation); Light2D still supports the Built-in backend and hides lens flares from the source camera's intermediate (Affect-Only-This-Camera) render.
        private FlareLayer _flareLayer;
#pragma warning restore CS0618
        private bool _flareLayerEnabled;
        private bool _xzNormalMappingWarningIssued;
        private bool _unsupportedPipelineWarningIssued;
        private int _lastUrpServedFrame = -1000;
        private bool _urpNoFeatureWarningIssued;
        private int _validationDiagnosticHash;
        private int _frameReallocations;
        // Whether the most recent frame took the coverage-composition path; drives the next frame's
        // release of the on-demand coverage render targets when coverage becomes inactive.
        private bool _coverageActive;
        // Obstacle-preview plumbing (Workstream B2): a self-expiring request stamp (mirrors the
        // _lastUrpServedFrame idiom; -1000 is the never-requested sentinel) and the persistent preview target
        // the URP copy pass writes into. Both are plain runtime state (an int + an RTHandle), so the runtime
        // stays editor-independent; the editor preview window reads them through InternalsVisibleTo.
        private int _obstaclePreviewRequestedFrame = -1000;
        private RTHandle _obstaclePreviewHandle;

        private static readonly int ObstacleTexId = Shader.PropertyToID("_ObstacleTex");
        private static readonly int ObstacleGlobalMulId = Shader.PropertyToID("_Light2DObstacleGlobalMul");
        private static readonly int NormalsBufferId = Shader.PropertyToID("_NormalsBuffer");
        private static readonly int NormalTexId = Shader.PropertyToID("_NormalTex");
        private static readonly int PixelsPerBlockId = Shader.PropertyToID("_PixelsPerBlock");
        private static readonly int ExtendedToSmallTextureScaleId = Shader.PropertyToID("_ExtendedToSmallTextureScale");
        private static readonly int PosOffsetId = Shader.PropertyToID("_PosOffset");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int LightSourcesTexId = Shader.PropertyToID("_LightSourcesTex");
        private static readonly int AmbientLightTexId = Shader.PropertyToID("_AmbientLightTex");
        private static readonly int GameTexId = Shader.PropertyToID("_GameTex");
        private static readonly int ShiftId = Shader.PropertyToID("_Shift");
        private static readonly int OffsetId = Shader.PropertyToID("_Offset");
        private static readonly int ScaleId = Shader.PropertyToID("_Scale");
        private static readonly int LightPosId = Shader.PropertyToID("_LightPos");
        private static readonly int CompositeTexId = Shader.PropertyToID("_CompositeTex");
        private static readonly int CoverageTexId = Shader.PropertyToID("_CoverageTex");
        private static readonly int AmbientBounceTintId = Shader.PropertyToID("_AmbientBounceTint");
        private static readonly int LightMulId = Shader.PropertyToID("_LightMul");
        private static readonly int LightSourcesMulId = Shader.PropertyToID("_LightSourcesMul");
        private static readonly int AdditiveLightPowId = Shader.PropertyToID("_AdditiveLightPow");
        private static readonly int AdditiveLightAddId = Shader.PropertyToID("_AdditiveLightAdd");
        private static readonly int ObstacleMulId = Shader.PropertyToID("_ObstacleMul");
        private static readonly int ObstacleAddId = Shader.PropertyToID("_ObstacleAdd");
        private static readonly int EmissionColorMulId = Shader.PropertyToID("_EmissionColorMul");
        private static readonly int SamplingDistId = Shader.PropertyToID("_SamplingDist");
        private static readonly int AmbientAdaptSpeedId = Shader.PropertyToID("_AmbientAdaptSpeed");
        private static readonly int DistanceId = Shader.PropertyToID("_Distance");
        private const string DefaultUnlitMode = "SRPDefaultUnlit";

        public static LightingSystem Instance
        {
            get
            {
                CleanupRegistry();
                foreach (var instance in ActiveInstances)
                    return instance;
                return null;
            }
        }

        public static IReadOnlyList<LightingSystem> Instances
        {
            get
            {
                CleanupRegistry();
                InstanceSnapshot.Clear();
                InstanceSnapshot.AddRange(ActiveInstances);
                return InstanceSnapshot;
            }
        }

        public Camera SourceCamera { get { return _camera != null ? _camera : GetComponent<Camera>(); } }
        public static Light2DPipeline ActivePipeline { get { return ClassifyPipeline(GraphicsSettings.currentRenderPipeline); } }
        public bool IsScriptableRenderPipelineActive { get { return GraphicsSettings.currentRenderPipeline != null; } }
        public bool IsRenderingReady
        {
            get
            {
                if (!isActiveAndEnabled || LightOverlayMaterial == null)
                    return false;
                switch (ActivePipeline)
                {
                    case Light2DPipeline.BuiltIn:
                    case Light2DPipeline.UniversalRenderPipeline:
                        return true;
                    default:
                        return false;
                }
            }
        }
        public float LightPixelsPerUnityMeter { get { return 1f / Mathf.Max(LightPixelSize, 0.00001f); } }
        public Vector2Int SmallLightTextureSize { get { return _smallLightTextureSize; } }
        public Vector2Int ExtendedLightTextureSize { get { return _extendedLightTextureSize; } }
        public RenderTexture ObstaclesTexture { get { return _resources == null ? null : _resources.ObstaclesTexture; } }
        public RenderTexture LightSourcesTexture { get { return _resources == null ? null : _resources.LightSourcesTexture; } }
        public RenderTexture AmbientTexture { get { return _resources == null ? null : _resources.AmbientTexture; } }

        // Obstacle-preview accessors (Workstream B2). RequestObstaclePreview is called by the editor preview
        // window every repaint; ObstaclePreviewRequested self-expires ~2 frames later so the URP copy pass and
        // its owned target stay zero-cost whenever no window is open. ObstaclePreviewTexture exposes the
        // resolved obstacle buffer the URP pass copies into (Built-in reads ObstaclesTexture directly instead).
        internal RenderTexture ObstaclePreviewTexture => _obstaclePreviewHandle != null ? _obstaclePreviewHandle.rt : null;
        internal void RequestObstaclePreview() => _obstaclePreviewRequestedFrame = Time.frameCount;
        internal bool ObstaclePreviewRequested => Time.frameCount - _obstaclePreviewRequestedFrame <= 2;

        // Allocates or reuses the persistent preview target (the URP feature imports this RTHandle into
        // RenderGraph and copies the resolved obstacles into it). Mirrors the feature's ambient-history
        // RenderingUtils.ReAllocateHandleIfNeeded idiom so a descriptor change reallocates in place.
        internal RTHandle EnsureObstaclePreviewTarget(RenderTextureDescriptor desc, FilterMode fm)
        {
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            desc.useMipMap = false;
            desc.autoGenerateMips = false;
            desc.useDynamicScale = false;
            RenderingUtils.ReAllocateHandleIfNeeded(ref _obstaclePreviewHandle, desc, fm, TextureWrapMode.Clamp,
                name: "Light2D Obstacle Preview");
            return _obstaclePreviewHandle;
        }

        internal void ReleaseObstaclePreviewTarget()
        {
            _obstaclePreviewHandle?.Release();
            _obstaclePreviewHandle = null;
        }

        private bool BuiltInNormalMappingEnabled { get { return EnableNormalMapping && !XZPlane; } }

        internal static Light2DPipeline ClassifyPipeline(RenderPipelineAsset pipelineAsset)
        {
            if (pipelineAsset == null)
                return Light2DPipeline.BuiltIn;
            return pipelineAsset is UniversalRenderPipelineAsset
                ? Light2DPipeline.UniversalRenderPipeline
                : Light2DPipeline.UnsupportedScriptableRenderPipeline;
        }

        private float SourceOrthographicSize
        {
            get { return _camera.orthographicSize; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()
        {
            ActiveInstances.Clear();
            InstanceSnapshot.Clear();
            foreach (var instance in FindObjectsByType<LightingSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                ActiveInstances.Add(instance);
        }

        private static void CleanupRegistry()
        {
            ActiveInstances.RemoveWhere(instance => instance == null || !instance.isActiveAndEnabled);
        }

        private void OnEnable()
        {
            ActiveInstances.Add(this);
            _camera = GetComponent<Camera>();
            // Seed the URP-served grace window from enable time so the missing-feature warning cannot fire
            // during the feature's first-frame warmup (the -1000 field initializer is the pre-enable sentinel).
            _lastUrpServedFrame = Time.frameCount;
            _urpNoFeatureWarningIssued = false;
            if (ActivePipeline == Light2DPipeline.UnsupportedScriptableRenderPipeline &&
                !_unsupportedPipelineWarningIssued)
            {
                _unsupportedPipelineWarningIssued = true;
                Debug.LogWarning("Light2D supports the Built-in Render Pipeline and URP only. " +
                                 "The active Scriptable Render Pipeline is unsupported, so Light2D will not render.", this);
            }
        }

        private void Start()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Shader.SetGlobalTexture(ObstacleTexId, Texture2D.whiteTexture);
                return;
            }
#endif
            if (!IsScriptableRenderPipelineActive)
                EnsureInitialized(null);
        }

        private void OnDisable()
        {
            ActiveInstances.Remove(this);
            ReleaseOwnedResources();
        }

        private void OnDestroy()
        {
            ActiveInstances.Remove(this);
            ReleaseOwnedResources();
        }

#if UNITY_EDITOR
        // Editor-only: when the component is first added through the Inspector, populate the composition
        // material slots (hidden from the custom inspector) from the packaged default resources so a freshly
        // added LightingSystem renders without any manual assignment. Reset runs only when the component is
        // added in the editor, so existing serialized systems and sample scenes are never re-written, and a
        // programmatic AddComponent (used by the tests) never triggers it - an unassigned overlay stays null.
        // Since the value fields are always applied at render time, only the default materials' shaders matter.
        private void Reset()
        {
            var defaults = UnityEditor.AssetDatabase.LoadAssetAtPath<Light2DDefaultResources>(
                "Packages/com.sss13594.light2d/Runtime/Light2DDefaultResources.asset");
            if (defaults == null)
                return;
            if (AmbientLightComputeMaterial == null)
                AmbientLightComputeMaterial = defaults.AmbientLightComputeMaterial;
            if (LightOverlayMaterial == null)
                LightOverlayMaterial = defaults.LightOverlayMaterial;
            if (LightSourcesBlurMaterial == null)
                LightSourcesBlurMaterial = defaults.BlurMaterial;
            if (AmbientLightBlurMaterial == null)
                AmbientLightBlurMaterial = defaults.BlurMaterial;
        }
#endif

        private void OnRenderImage(RenderTexture src, RenderTexture dest)
        {
            if (IsScriptableRenderPipelineActive)
            {
                BlitToDestination(src, dest);
                return;
            }
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Shader.SetGlobalTexture(ObstacleTexId, Texture2D.whiteTexture);
                BlitToDestination(src, dest);
                return;
            }
#endif
            var oldActive = RenderTexture.active;
            var shaderState = GlobalShaderState.Capture();
            try
            {
                if (!EnsureInitialized(src.descriptor))
                {
                    BlitToDestination(src, dest);
                    return;
                }

                // Derive the light-camera geometry directly (no helper camera): a snapped position plus
                // an extended (obstacle/ambient) and small (light-source) view/projection, and the frustum
                // planes the registry snapshots cull against. Mirrors the URP feature's per-frame geometry.
                _currPos = Light2DCameraGeometry.CalculateSnappedPosition(_camera, LightPixelsPerUnityMeter);
                var extendedGeom = Light2DCameraGeometry.Calculate(_camera, _currPos, _smallLightTextureSize,
                    _extendedLightTextureSize, true, LightCameraFovAdd, LightObstaclesDistance);
                var smallGeom = Light2DCameraGeometry.Calculate(_camera, _currPos, _smallLightTextureSize,
                    _extendedLightTextureSize, false, LightCameraFovAdd, LightObstaclesDistance);
                GeometryUtility.CalculateFrustumPlanes(extendedGeom.Projection * extendedGeom.View, _extendedPlanes);
                GeometryUtility.CalculateFrustumPlanes(smallGeom.Projection * smallGeom.View, _smallPlanes);

                using (Light2DProfiling.Culling.Auto())
                {
                    var extendedContext = new Light2DCullContext(_extendedPlanes, this);
                    LightSprite.PrepareSnapshot(_lightSpritesCache, XZPlane, this, _smallPlanes);
                    BuildSourceEntries();
                    Light2DRenderRegistry.PrepareObstacleSnapshot(_obstacleSnapshot, extendedContext);
                    Light2DRenderRegistry.PrepareCoverageSnapshot(_coverageSnapshot, extendedContext);
                    if (EnableAmbientLight)
                        Light2DRenderRegistry.PrepareAmbientSnapshot(_ambientSnapshot, extendedContext);
                    else
                        _ambientSnapshot.Clear();
                }
                SetKeyword("LIGHT2D_XZ_PLANE", XZPlane);
                SetKeyword("HDR", _texFormat == RenderTextureFormat.ARGBHalf);
                SetKeyword("PERSPECTIVE_CAMERA", !_camera.orthographic);
                RenderObstacles(extendedGeom);
                SetupShaders();
                RenderNormalBuffer(smallGeom);
                RenderLightSources(smallGeom);
                RenderLightSourcesBlur();
                ResetAmbientHistoryForUnsupportedTransform(extendedGeom);
                int ambientCyclesConsumed = RenderAmbientLight(extendedGeom);
                _coverageActive = _coverageSnapshot.Count > 0 || DefaultCoverage < 1f;
                if (!_coverageActive)
                {
                    // Backward-compat skip (design 1.3(1)): no volumes and a full-coverage world default
                    // take the original overlay-to-destination path verbatim - pixel-identical, and no
                    // coverage render target or material is ever allocated.
                    RenderLightOverlay(src, dest, smallGeom);
                }
                else
                {
                    // Coverage composition (design 1.2): draw the volumes into the coverage mask (cleared
                    // to the world baseline DefaultCoverage), composite the overlay into an intermediate,
                    // then blend final = lerp(rawGame, composite, coverage) into dest.
                    EnsureCoverageTextures(src.descriptor);
                    RenderCoverageMask(extendedGeom);
                    RenderLightOverlay(src, _resources.CompositeTexture, smallGeom);
                    RenderCoverageBlend(src, dest, smallGeom);
                }
                ReportBuiltInFrame(ambientCyclesConsumed);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                BlitToDestination(src, dest);
            }
            finally
            {
                shaderState.Restore();
                RenderTexture.active = oldActive;
            }
        }

        private void OnPreCull()
        {
            if (!Application.isPlaying || IsScriptableRenderPipelineActive || !AffectOnlyThisCamera)
                return;
            EnsureInitialized(null);
            if (_initialized && _resources.CameraTargetTexture != null)
                _camera.targetTexture = _resources.CameraTargetTexture;
        }

        private void OnRenderObject()
        {
            if (!Application.isPlaying || IsScriptableRenderPipelineActive || !AffectOnlyThisCamera ||
                Camera.current != _camera || !_initialized || _resources.CameraTargetTexture == null ||
                !_resources.CameraTargetTexture.IsCreated() || _resources.AlphaBlendedMaterial == null ||
                _camera.targetTexture != _resources.CameraTargetTexture ||
                _resources.CameraTargetTexture == _mainCameraTarget)
                return;

            var currentTarget = _camera.targetTexture;
            try
            {
                _camera.targetTexture = _mainCameraTarget;
                Graphics.Blit(_resources.CameraTargetTexture, _mainCameraTarget, _resources.AlphaBlendedMaterial);
            }
            finally
            {
                _camera.targetTexture = currentTarget;
            }
        }

        private bool EnsureInitialized(RenderTextureDescriptor? sourceDescriptor)
        {
            if (IsScriptableRenderPipelineActive)
                return false;
            if (_camera == null)
                _camera = GetComponent<Camera>();
            RenderTextureDescriptor descriptor;
            LightingFrameConfig frameConfig;
            using (Light2DProfiling.Validation.Auto())
            {
                if (LightOverlayMaterial == null)
                {
                    if (!_initialized)
                        Debug.LogError("LightOverlayMaterial must be assigned for Light2D to render.", this);
                    return false;
                }
                descriptor = sourceDescriptor ?? (_hasSourceDescriptor ? _sourceDescriptor : CreateCameraDescriptor());
                var settings = new LightingFrameSettings(
                    pixelSize: LightPixelSize,
                    orthographicSizeAdd: LightCameraSizeAdd,
                    fieldOfViewAdd: LightCameraFovAdd,
                    obstacleDistance: LightObstaclesDistance,
                    hdr: HDR,
                    enableAmbientLight: EnableAmbientLight,
                    obstacleAntialiasing: LightObstaclesAntialiasing,
                    normalMapping: BuiltInNormalMappingEnabled,
                    maximumTexturePixels: MaxLightTexturePixels);
                if (!TryCreateFrameConfig(_camera, descriptor, settings, out frameConfig,
                        out var validationError))
                {
                    ReportValidationError(validationError);
                    InvalidateRendering();
                    return false;
                }
                _validationDiagnosticHash = 0;
                if (AffectOnlyThisCamera && _camera.targetTexture != null && !_mainCameraStateCaptured)
                {
                    Debug.LogError("\"Affect Only This Camera\" will not work if camera.targetTexture is set.", this);
                    return false;
                }
                if (BuiltInNormalMappingEnabled && !_camera.orthographic)
                {
                    Debug.LogError("Normal mapping is not supported with perspective camera.", this);
                    return false;
                }
                if (EnableNormalMapping && XZPlane)
                {
                    if (!_xzNormalMappingWarningIssued)
                    {
                        Debug.LogWarning("Built-in Light2D normal mapping supports the XY plane only. " +
                                         "Normal mapping is disabled for this camera; disable XZ Plane to enable it.", this);
                        _xzNormalMappingWarningIssued = true;
                    }
                }
                else
                {
                    _xzNormalMappingWarningIssued = false;
                }
            }

            if (_resources == null)
                _resources = new LightingRenderResources();
            _resources.EnsureSerializedMaterials(AmbientLightComputeMaterial, LightOverlayMaterial,
                LightSourcesBlurMaterial, AmbientLightBlurMaterial);

            if (BuiltInNormalMappingEnabled)
            {
                var normalDrawerShader = Shader.Find("Light2D/Internal/Normal Map Drawer");
                var normalMappedShader = Shader.Find("Light2D/Internal/Normal Mapped Light");
                if (normalDrawerShader == null || normalMappedShader == null)
                    return false;
                _resources.EnsureNormalMappingMaterials(normalDrawerShader, normalMappedShader);
                if (_normalLightProperties == null)
                    _normalLightProperties = new MaterialPropertyBlock();
            }
            if (AffectOnlyThisCamera)
            {
                var alphaShader = Shader.Find("Light2D/Internal/Alpha Blended");
                if (alphaShader == null)
                    return false;
                _resources.EnsureAlphaBlendedMaterial(alphaShader);
                CaptureMainCameraState();
            }
            else
            {
                RestoreMainCameraState();
            }

            _texFormat = HDR && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)
                ? RenderTextureFormat.ARGBHalf
                : RenderTextureFormat.ARGB32;
            _smallLightTextureSize = frameConfig.SmallSize;
            _extendedLightTextureSize = frameConfig.ExtendedSize;

            if (sourceDescriptor.HasValue)
            {
                _sourceDescriptor = sourceDescriptor.Value;
                _hasSourceDescriptor = true;
            }
            int settingsHash = CalculateSettingsHash(descriptor);
            bool resourcesChanged = !_initialized || _settingsHash != settingsHash;
            if (resourcesChanged)
            {
                _resources.ReleaseTextures();
                _oldPos = _currPos;
                _settingsHash = settingsHash;
            }
            EnsureFrameResources(descriptor);
            if (_obstaclesPostProcessor == null)
                _obstaclesPostProcessor = new ObstacleCameraPostPorcessor();
            _initialized = true;
            if (resourcesChanged && EnableAmbientLight)
                LoopAmbientLight(AmbientWarmupCycles);
            return true;
        }

        private void EnsureFrameResources(RenderTextureDescriptor sourceDescriptor)
        {
            using (Light2DProfiling.ResourceReconciliation.Auto())
            {
                int reallocations = 0;
                var lightDescriptor = sourceDescriptor;
                lightDescriptor.colorFormat = _texFormat;
                lightDescriptor.depthBufferBits = 0;
                lightDescriptor.msaaSamples = 1;

                SetSize(ref lightDescriptor, _extendedLightTextureSize.x, _extendedLightTextureSize.y);
                if (_resources.EnsureTexture(ref _resources.ObstaclesTexture, lightDescriptor,
                        LightTexturesFilterMode, "Obstacles"))
                    reallocations++;
                if (EnableAmbientLight)
                {
                    bool ambientTextureChanged = _resources.EnsureTexture(ref _resources.AmbientTexture,
                        lightDescriptor, LightTexturesFilterMode, "Ambient");
                    bool previousAmbientTextureChanged = _resources.EnsureTexture(ref _resources.PreviousAmbientTexture,
                        lightDescriptor, LightTexturesFilterMode, "Previous Ambient");
                    if (ambientTextureChanged)
                    {
                        reallocations++;
                        ClearTexture(_resources.AmbientTexture);
                    }
                    if (previousAmbientTextureChanged)
                    {
                        reallocations++;
                        ClearTexture(_resources.PreviousAmbientTexture);
                    }
                    if (_resources.EnsureTexture(ref _resources.AmbientEmissionTexture, lightDescriptor,
                            LightTexturesFilterMode, "Ambient Emission"))
                        reallocations++;
                }
                else
                {
                    _aditionalAmbientLightCycles = 0;
                    _resources.ReleaseAmbientTextures();
                }

                if (LightObstaclesAntialiasing)
                {
                    var upsampledSize = _extendedLightTextureSize * 2;
                    SetSize(ref lightDescriptor, upsampledSize.x, upsampledSize.y);
                    if (_resources.EnsureTexture(ref _resources.ObstaclesUpsampledTexture, lightDescriptor,
                            LightTexturesFilterMode, "Upsampled Obstacles"))
                        reallocations++;
                }
                else
                {
                    _resources.ReleaseAntialiasingTexture();
                }

                SetSize(ref lightDescriptor, BuiltInNormalMappingEnabled ? sourceDescriptor.width : _smallLightTextureSize.x,
                    BuiltInNormalMappingEnabled ? sourceDescriptor.height : _smallLightTextureSize.y);
                if (_resources.EnsureTexture(ref _resources.LightSourcesTexture, lightDescriptor,
                        BuiltInNormalMappingEnabled ? FilterMode.Point : LightTexturesFilterMode, "Light Sources"))
                    reallocations++;

                if (BuiltInNormalMappingEnabled)
                {
                    SetSize(ref lightDescriptor, _smallLightTextureSize.x, _smallLightTextureSize.y);
                    if (_resources.EnsureTexture(ref _resources.SingleLightSourceTexture, lightDescriptor,
                            LightTexturesFilterMode, "Single Light Source"))
                        reallocations++;
                    var normalDescriptor = sourceDescriptor;
                    normalDescriptor.colorFormat = RenderTextureFormat.ARGB32;
                    if (_resources.EnsureTexture(ref _resources.NormalMapBuffer, normalDescriptor,
                            FilterMode.Point, "Normals"))
                        reallocations++;
                }
                else
                {
                    _resources.ReleaseNormalMappingTextures();
                }

                int blurWidth = _resources.LightSourcesTexture.width == _smallLightTextureSize.x
                    ? _resources.LightSourcesTexture.width * 2
                    : _resources.LightSourcesTexture.width;
                int blurHeight = _resources.LightSourcesTexture.height == _smallLightTextureSize.y
                    ? _resources.LightSourcesTexture.height * 2
                    : _resources.LightSourcesTexture.height;
                if (BlurLightSources && _resources.LightSourcesBlurMaterial != null)
                {
                    SetSize(ref lightDescriptor, blurWidth, blurHeight);
                    if (_resources.EnsureTexture(ref _resources.BlurredLightTexture, lightDescriptor,
                            FilterMode.Bilinear, "Blurred Light Sources"))
                        reallocations++;
                }
                else
                {
                    _resources.ReleaseBlurTexture();
                }

                var outputDescriptor = sourceDescriptor;
                outputDescriptor.depthBufferBits = 0;
                outputDescriptor.msaaSamples = 1;
                if (AffectOnlyThisCamera)
                {
                    if (_resources.EnsureTexture(ref _resources.CameraTargetTexture, outputDescriptor,
                            FilterMode.Point, "Camera Target"))
                        reallocations++;
                }
                else
                {
                    _resources.ReleaseCameraTargetTexture();
                }

                // Coverage render targets are allocated on demand by the active coverage branch (see
                // EnsureCoverageTextures). When the previous frame was not coverage-active, reclaim them
                // here so the no-volume path keeps its zero-allocation backward-compat cost.
                if (!_coverageActive)
                    _resources.ReleaseCoverageTextures();

                _frameReallocations += reallocations;
            }
        }

        private void RenderObstacles(in Light2DCameraGeometry geometry)
        {
            using (Light2DProfiling.Obstacles.Auto())
            {
                var obstacleTarget = LightObstaclesAntialiasing
                    ? _resources.ObstaclesUpsampledTexture
                    : _resources.ObstaclesTexture;
                int pass = ResolveSnapshotPass(_obstacleSnapshot, DefaultUnlitMode);
                var borderMesh = _obstaclesPostProcessor.GetBorderMesh(obstacleTarget.width, obstacleTarget.height,
                    LightObstaclesAntialiasing ? 2f : 1f);
                var borderMaterial = _obstaclesPostProcessor.Material;

                // Values-only source of truth: the LightingSystem-wide obstacle multiplier is pushed onto the
                // obstacle draws through a property block every frame (default white = a byte-identical no-op).
                // The block is scoped to the obstacle snapshot only, so the ambient emitter - which shares the
                // dual-color obstacle material - and every other role keep the material's default white. The
                // border draw uses a different shader and is intentionally left untouched (a full-transmission
                // edge guard that must stay put regardless of how transparent obstacles are made).
                if (_obstacleProperties == null)
                    _obstacleProperties = new MaterialPropertyBlock();
                _obstacleProperties.SetColor(ObstacleGlobalMulId, ObstacleColor);

                var commands = CommandBufferPool.Get("Light2D Obstacles");
                try
                {
                    commands.SetRenderTarget(obstacleTarget);
                    commands.ClearRenderTarget(true, true, new Color(1, 1, 1, 0));
                    var raster = CommandBufferHelpers.GetRasterCommandBuffer(commands);
                    // Pass the raw (OpenGL-convention) projection: the command buffer applies the
                    // platform/render-texture GPU adjustment itself when it executes into the bound texture,
                    // exactly as the retired helper camera's Render() did. Pre-applying
                    // GL.GetGPUProjectionMatrix(..., true) here double-adjusts on flipped-UV platforms (D3D)
                    // and renders the light textures vertically mirrored relative to the game image.
                    raster.SetViewProjectionMatrices(geometry.View, geometry.Projection);
                    Light2DDraw.Draw(raster, _obstacleSnapshot, pass, _obstacleProperties);
                    // One-pixel white border so lights with off-screen origins keep sampling obstacles.
                    // The border shader emits clip-space vertices directly, so it ignores view/projection.
                    if (borderMesh != null && borderMaterial != null)
                    {
                        int borderPass = Light2DDraw.ResolvePass(borderMaterial, DefaultUnlitMode);
                        raster.DrawMesh(borderMesh, Matrix4x4.identity, borderMaterial, 0, borderPass);
                    }
                    Graphics.ExecuteCommandBuffer(commands);
                }
                finally
                {
                    CommandBufferPool.Release(commands);
                }
                if (LightObstaclesAntialiasing)
                {
                    _resources.ObstaclesTexture.DiscardContents();
                    Graphics.Blit(_resources.ObstaclesUpsampledTexture, _resources.ObstaclesTexture);
                }
            }
        }

        private void SetupShaders()
        {
            Shader.SetGlobalTexture(ObstacleTexId, _resources.ObstaclesTexture);
            Shader.SetGlobalFloat(PixelsPerBlockId, LightPixelsPerUnityMeter);
            Shader.SetGlobalVector(ExtendedToSmallTextureScaleId, new Vector2(
                _smallLightTextureSize.x / (float)_extendedLightTextureSize.x,
                _smallLightTextureSize.y / (float)_extendedLightTextureSize.y));
            Shader.SetGlobalVector(PosOffsetId, LightObstaclesAntialiasing
                ? (BuiltInNormalMappingEnabled ? _resources.ObstaclesUpsampledTexture.texelSize * 0.75f : _resources.ObstaclesUpsampledTexture.texelSize * 0.25f)
                : (BuiltInNormalMappingEnabled ? _resources.ObstaclesTexture.texelSize : _resources.ObstaclesTexture.texelSize * 0.5f));
        }

        private void RenderNormalBuffer(in Light2DCameraGeometry geometry)
        {
            if (!BuiltInNormalMappingEnabled)
                return;
            using (Light2DProfiling.Normals.Auto())
            {
                var drawerMaterial = _resources.NormalMapDrawerMaterial;
                if (drawerMaterial == null)
                    return;
                int pass = Light2DDraw.ResolvePass(drawerMaterial, DefaultUnlitMode);
                var commands = CommandBufferPool.Get("Light2D Normal Buffer");
                try
                {
                    commands.SetRenderTarget(_resources.NormalMapBuffer);
                    commands.ClearRenderTarget(true, true, new Color(0.5f, 0.5f, 0, 1));
                    var raster = CommandBufferHelpers.GetRasterCommandBuffer(commands);
                    // Raw projection; the command buffer applies the render-texture GPU adjustment itself
                    // (see RenderObstacles). Pre-adjusting here mirrors the normal buffer vertically.
                    raster.SetViewProjectionMatrices(geometry.View, geometry.Projection);
                    // Draw every obstacle with the shared normal-map drawer, feeding each obstacle's own
                    // base/normal textures through a property block. This is the DrawMesh equivalent of the
                    // retired camera RenderWithShader "LightObstacle" replacement, which pulled the same
                    // textures from each obstacle material.
                    for (int i = 0; i < _obstacleSnapshot.Count; i++)
                    {
                        var entry = _obstacleSnapshot[i];
                        if (entry.Mesh == null)
                            continue;
                        _normalLightProperties.Clear();
                        CopyTextureProperty(_normalLightProperties, entry.Material, MainTexId);
                        CopyTextureProperty(_normalLightProperties, entry.Material, NormalTexId);
                        int submeshCount = entry.SubmeshCount > 0 ? entry.SubmeshCount : 1;
                        for (int s = 0; s < submeshCount; s++)
                            raster.DrawMesh(entry.Mesh, entry.Matrix, drawerMaterial, s, pass, _normalLightProperties);
                    }
                    Graphics.ExecuteCommandBuffer(commands);
                }
                finally
                {
                    CommandBufferPool.Release(commands);
                }
                Shader.SetGlobalTexture(NormalsBufferId, _resources.NormalMapBuffer);
            }
        }

        private void RenderLightSources(in Light2DCameraGeometry geometry)
        {
            using (Light2DProfiling.Sources.Auto())
            {
                if (BuiltInNormalMappingEnabled)
                    RenderNormalMappedLightSources(geometry);
                else
                    RunSnapshotPass("Light2D Light Sources", _resources.LightSourcesTexture, Color.clear,
                        geometry, _sourceSnapshot, DefaultUnlitMode);
            }
        }

        // Normal-mapped source path (R2). Renders each registered light individually into a scratch target
        // (shadowed by the global obstacle texture), then modulates it by the normal buffer and accumulates
        // into the light-sources target. Mirrors the URP feature's AddSingleLightPass/AddNormalLightPass but
        // keeps the built-in Normal Mapped Light shader's world-space _LightPos semantics. XY-plane only.
        // This is the ONLY writer of LightSourcesTexture while normal mapping is on (RenderLightSources is
        // either/or), so every light in the snapshot must pass through it - including the generated-falloff
        // lights whose override Sprite is null.
        private void RenderNormalMappedLightSources(in Light2DCameraGeometry geometry)
        {
            var normalMaterial = _resources.NormalMappedLightMaterial;
            if (normalMaterial == null)
                return;
            int normalPass = Light2DDraw.ResolvePass(normalMaterial, DefaultUnlitMode);
            // Raw projection; the command buffer applies the render-texture GPU adjustment itself
            // (see RenderObstacles), so light sources render upright rather than vertically mirrored.
            var projection = geometry.Projection;
            var commands = CommandBufferPool.Get("Light2D Normal-Mapped Sources");
            try
            {
                var raster = CommandBufferHelpers.GetRasterCommandBuffer(commands);
                commands.SetRenderTarget(_resources.LightSourcesTexture);
                commands.ClearRenderTarget(true, true, Color.clear);
                for (int i = 0; i < _lightSpritesCache.Count; i++)
                {
                    var light = _lightSpritesCache[i];
                    if (light == null)
                        continue;
                    // The sprite the light actually draws: the generated falloff sprite, or the Sprite override
                    // when one is assigned. Only the emission-origin footprint below needs it.
                    var activeSprite = light.ActiveLightSprite;
                    if (activeSprite == null)
                        continue;
                    var mesh = light.Mesh;
                    var lightMaterial = light.DrawMaterial;
                    if (mesh == null || lightMaterial == null)
                        continue;
                    var matrix = light.transform.localToWorldMatrix;
                    int submeshCount = mesh.subMeshCount > 0 ? mesh.subMeshCount : 1;
                    int sourcePass = Light2DDraw.ResolvePass(lightMaterial, DefaultUnlitMode);

                    commands.SetRenderTarget(_resources.SingleLightSourceTexture);
                    commands.ClearRenderTarget(true, true, Color.clear);
                    raster.SetViewProjectionMatrices(geometry.View, projection);
                    for (int s = 0; s < submeshCount; s++)
                        raster.DrawMesh(mesh, matrix, lightMaterial, s, sourcePass);

                    var size = activeSprite.bounds.size;
                    var center = matrix.MultiplyPoint3x4(Vector2.Scale(light.LightOrigin, size));
                    _normalLightProperties.Clear();
                    _normalLightProperties.SetVector(LightPosId,
                        new Vector4(center.x, center.y, light.NormalMapHeight, 0f));
                    _normalLightProperties.SetTexture(MainTexId, _resources.SingleLightSourceTexture);
                    commands.SetRenderTarget(_resources.LightSourcesTexture);
                    raster.SetViewProjectionMatrices(geometry.View, projection);
                    for (int s = 0; s < submeshCount; s++)
                        raster.DrawMesh(mesh, matrix, normalMaterial, s, normalPass, _normalLightProperties);
                }
                Graphics.ExecuteCommandBuffer(commands);
            }
            finally
            {
                CommandBufferPool.Release(commands);
            }
        }

        // Boilerplate for a single-target snapshot draw: bind + clear the role target, set the light-camera
        // view/projection, then issue the shared DrawMesh loop.
        private void RunSnapshotPass(string bufferName, RenderTexture target, Color clear,
            in Light2DCameraGeometry geometry, List<Light2DDrawEntry> snapshot, string lightMode)
        {
            int pass = ResolveSnapshotPass(snapshot, lightMode);
            var commands = CommandBufferPool.Get(bufferName);
            try
            {
                commands.SetRenderTarget(target);
                commands.ClearRenderTarget(true, true, clear);
                var raster = CommandBufferHelpers.GetRasterCommandBuffer(commands);
                // Raw projection; the command buffer applies the render-texture GPU adjustment itself
                // (see RenderObstacles). Pre-adjusting here mirrors light sources/ambient vertically.
                raster.SetViewProjectionMatrices(geometry.View, geometry.Projection);
                Light2DDraw.Draw(raster, snapshot, pass);
                Graphics.ExecuteCommandBuffer(commands);
            }
            finally
            {
                CommandBufferPool.Release(commands);
            }
        }

        // Builds this frame's flat light-source draw entries from the already culled, UV1-baked light snapshot.
        private void BuildSourceEntries()
        {
            _sourceSnapshot.Clear();
            for (int i = 0; i < _lightSpritesCache.Count; i++)
            {
                var light = _lightSpritesCache[i];
                if (light == null)
                    continue;
                var mesh = light.Mesh;
                var material = light.DrawMaterial;
                if (mesh == null || material == null)
                    continue;
                _sourceSnapshot.Add(new Light2DDrawEntry(mesh, material, light.transform.localToWorldMatrix,
                    light.WorldBounds, mesh.subMeshCount, light.SortingOrder, light.Owner));
            }
        }

        // Resolves the DrawMesh pass index once per snapshot from its first drawable material. All entries in
        // a role share the same shader family, so a single resolved index is correct for the whole snapshot.
        private static int ResolveSnapshotPass(List<Light2DDrawEntry> snapshot, string lightMode)
        {
            for (int i = 0; i < snapshot.Count; i++)
            {
                if (snapshot[i].Material != null)
                    return Light2DDraw.ResolvePass(snapshot[i].Material, lightMode);
            }
            return 0;
        }

        private static void CopyTextureProperty(MaterialPropertyBlock destination, Material source, int propertyId)
        {
            if (source == null || !source.HasProperty(propertyId))
                return;
            var texture = source.GetTexture(propertyId);
            if (texture != null)
                destination.SetTexture(propertyId, texture);
        }

        private void RenderLightSourcesBlur()
        {
            var blurMaterial = _resources.LightSourcesBlurMaterial;
            if (!BlurLightSources || blurMaterial == null)
                return;
            using (Light2DProfiling.Blur.Auto())
            {
                var originalFilterMode = _resources.LightSourcesTexture.filterMode;
                try
                {
                    _resources.LightSourcesTexture.filterMode = FilterMode.Bilinear;
                    // Values-only source of truth: the blur distance is applied unconditionally every frame.
                    blurMaterial.SetFloat(DistanceId, BlurDistance);
                    blurMaterial.mainTexture = _resources.LightSourcesTexture;
                    Graphics.Blit(null, _resources.BlurredLightTexture, blurMaterial);
                    if (LightTexturesFilterMode == FilterMode.Point)
                        Graphics.Blit(_resources.BlurredLightTexture, _resources.LightSourcesTexture);
                }
                finally
                {
                    _resources.LightSourcesTexture.filterMode = originalFilterMode;
                }
            }
        }

        // Built-in mirror of the URP feature's CameraContext.ResetHistoryForUnsupportedTransform plus the
        // teleport guard. The temporal ambient history is only reprojected by translation (see the shift in
        // RenderAmbientLight), so a camera rotation, zoom (orthographic-size / field-of-view) or aspect
        // change, or a single-frame jump larger than the view cannot be reprojected and would smear. When any
        // of those is detected, clear the history, zero this frame's shift, and re-warm from the cleared
        // state. Zoom usually also changes the size hash and triggers a resource-change reset, but pure
        // rotation at constant size does not, which is the gap this closes.
        private void ResetAmbientHistoryForUnsupportedTransform(in Light2DCameraGeometry geometry)
        {
            float projectionSize = _camera.orthographic ? _camera.orthographicSize : _camera.fieldOfView;
            bool transformChanged = _hasPrevTransform &&
                (Quaternion.Angle(_prevRotation, _camera.transform.rotation) > 0.001f ||
                 !Mathf.Approximately(_prevProjectionSize, projectionSize) ||
                 !Mathf.Approximately(_prevAspect, _camera.aspect));
            // Teleport: a single-frame position jump larger than one full extended-view height cannot be
            // reprojected by scrolling the history, so treat it as a full history invalidation instead.
            bool teleported = (_currPos - _oldPos).magnitude > 2f * geometry.OrthographicSize;

            _prevRotation = _camera.transform.rotation;
            _prevProjectionSize = projectionSize;
            _prevAspect = _camera.aspect;
            _hasPrevTransform = true;

            if (!transformChanged && !teleported)
                return;
            if (!EnableAmbientLight || _resources == null || _resources.AmbientTexture == null)
                return;

            ClearTexture(_resources.AmbientTexture);
            if (_resources.PreviousAmbientTexture != null)
                ClearTexture(_resources.PreviousAmbientTexture);
            _oldPos = _currPos; // zero this frame's reprojection shift
            LoopAmbientLight(AmbientWarmupCycles);
        }

        private int RenderAmbientLight(in Light2DCameraGeometry geometry)
        {
            var computeMaterial = _resources.AmbientLightComputeMaterial;
            if (!EnableAmbientLight || computeMaterial == null)
                return 0;
            using (Light2DProfiling.Ambient.Auto())
            {
                // Values-only source of truth: the colored-bounce tint plus every ambient "feel" knob are
                // pushed onto the cloned compute material unconditionally each frame (AmbientColoredBounce
                // default 0 is the shader's neutral no-op). The component fields are the single source of truth.
                computeMaterial.SetFloat(AmbientBounceTintId, AmbientColoredBounce);
                computeMaterial.SetFloat(ObstacleMulId, AmbientObstacleMul);
                computeMaterial.SetFloat(ObstacleAddId, AmbientObstacleAdd);
                computeMaterial.SetFloat(EmissionColorMulId, AmbientEmissionMul);
                computeMaterial.SetFloat(SamplingDistId, AmbientSamplingDistance);
                computeMaterial.SetFloat(AmbientAdaptSpeedId, AmbientAdaptSpeed);

                RunSnapshotPass("Light2D Ambient Emission", _resources.AmbientEmissionTexture, Color.clear,
                    geometry, _ambientSnapshot, DefaultUnlitMode);
                int additionalCycles = ConsumePendingAmbientLightCycles();
                // Base propagation iterations per frame come from the exposed AmbientIterations (default 1 =
                // the original single-step behavior); queued warmup cycles are still added on top.
                int baseIterations = Mathf.Max(1, AmbientIterations);
                for (int i = 0; i < additionalCycles + baseIterations; i++)
                {
                    Swap(ref _resources.PreviousAmbientTexture, ref _resources.AmbientTexture);
                    var textureSize = new Vector2(_resources.AmbientTexture.width, _resources.AmbientTexture.height);
                    // Reproject the ambient history along the lighting plane's two axes. On the XZ plane the
                    // vertical scroll axis is world z, so the delta must branch z (not y, which the old
                    // (Vector2) cast silently kept). Guard the divisor against a zero LightPixelSize.
                    var delta = _currPos - _oldPos;
                    var pixelShift = new Vector2(delta.x, XZPlane ? delta.z : delta.y) /
                                     Mathf.Max(LightPixelSize, 0.00001f);
                    var positionShift = new Vector2(pixelShift.x / textureSize.x, pixelShift.y / textureSize.y);
                    _oldPos = _currPos;
                    computeMaterial.SetTexture(LightSourcesTexId, _resources.AmbientEmissionTexture);
                    computeMaterial.SetTexture(MainTexId, _resources.PreviousAmbientTexture);
                    computeMaterial.SetVector(ShiftId, positionShift);
                    Graphics.Blit(null, _resources.AmbientTexture, computeMaterial);
                    if (BlurAmbientLight && _resources.AmbientLightBlurMaterial != null)
                    {
                        // Values-only source of truth: the blur distance is applied unconditionally each frame.
                        _resources.AmbientLightBlurMaterial.SetFloat(DistanceId, BlurDistance);
                        _resources.AmbientLightBlurMaterial.mainTexture = _resources.AmbientTexture;
                        Graphics.Blit(null, _resources.PreviousAmbientTexture, _resources.AmbientLightBlurMaterial);
                        Swap(ref _resources.PreviousAmbientTexture, ref _resources.AmbientTexture);
                    }
                }
                return additionalCycles + baseIterations;
            }
        }

        // Computes the game->light-texture sampling transform (offset + scale) from the small light-camera
        // geometry. Shared by the overlay and the coverage blend so the coverage mask is sampled through
        // the exact same _Offset/_Scale the overlay used (design risk R1: value identity by construction).
        private void ComputeOverlayTransform(in Light2DCameraGeometry geometry, out Vector2 offset,
            out Vector2 scale)
        {
            var texelSize = new Vector2(1f / _smallLightTextureSize.x, 1f / _smallLightTextureSize.y);
            Vector2 worldOffset = Quaternion.Inverse(_camera.transform.rotation) *
                                  (geometry.Position - _camera.transform.position);
            offset = Vector2.Scale(texelSize, -worldOffset * LightPixelsPerUnityMeter);
            float sourceOrthographicSize = _camera.orthographic
                ? SourceOrthographicSize
                : Mathf.Tan(_camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * LightObstaclesDistance;
            float scaleY = sourceOrthographicSize / geometry.OrthographicSize;
            scale = new Vector2(scaleY * _camera.aspect / geometry.Aspect, scaleY);
        }

        private void RenderLightOverlay(RenderTexture src, RenderTexture dest, in Light2DCameraGeometry geometry)
        {
            using (Light2DProfiling.Composition.Auto())
            {
                ComputeOverlayTransform(geometry, out var offset, out var scale);
                var lightSources = BlurLightSources && _resources.LightSourcesBlurMaterial != null &&
                                   LightTexturesFilterMode != FilterMode.Point
                    ? _resources.BlurredLightTexture
                    : _resources.LightSourcesTexture;
                var overlay = _resources.LightOverlayMaterial;
                // Values-only source of truth: every overlay "look" knob is pushed onto the cloned material
                // unconditionally each frame, so the component fields are the single source of truth.
                overlay.SetFloat(LightMulId, LightBrightness);
                overlay.SetFloat(LightSourcesMulId, LightSourcesBrightness);
                overlay.SetFloat(AdditiveLightPowId, BloomPower);
                overlay.SetFloat(AdditiveLightAddId, BloomAdd);
                overlay.SetTexture(AmbientLightTexId, EnableAmbientLight ? _resources.AmbientTexture : null);
                overlay.SetTexture(LightSourcesTexId, lightSources);
                overlay.SetTexture(GameTexId, src);
                overlay.SetVector(OffsetId, offset);
                overlay.SetVector(ScaleId, scale);
                Graphics.Blit(null, dest, overlay);
            }
        }

        // Draws the coverage volumes into the coverage mask (design 1.2). Clears to the world baseline
        // DefaultCoverage (1 = lighting everywhere, 0 = raw game) so unpainted regions blend to that
        // baseline, then rasterizes each volume shape - higher priority last - through the shared
        // raw-projection snapshot pass, exactly like the obstacle/ambient extended passes.
        private void RenderCoverageMask(in Light2DCameraGeometry geometry)
        {
            var clear = new Color(DefaultCoverage, DefaultCoverage, DefaultCoverage, DefaultCoverage);
            RunSnapshotPass("Light2D Coverage Mask", _resources.CoverageTexture, clear,
                geometry, _coverageSnapshot, DefaultUnlitMode);
        }

        // Coverage composite (design 1.2): dest = lerp(rawGame, composite, coverage). Binds the raw game
        // image (_GameTex = src), the overlay's intermediate composite and the coverage mask, using the
        // same offset/scale the overlay used plus the extended->small mapping the mask is stored at, then
        // blits into dest. AffectOnlyThisCamera is preserved: the blend consumes the same (src, dest) the
        // overlay path did, so the single-camera intermediate-target flow is unchanged.
        private void RenderCoverageBlend(RenderTexture src, RenderTexture dest, in Light2DCameraGeometry geometry)
        {
            using (Light2DProfiling.Composition.Auto())
            {
                var blend = _resources.CoverageBlendMaterial;
                if (blend == null)
                {
                    // The coverage-blend shader failed to load; fall back to the plain lit composite so the
                    // frame still shows lighting rather than throwing out of the render path.
                    BlitToDestination(_resources.CompositeTexture, dest);
                    return;
                }
                ComputeOverlayTransform(geometry, out var offset, out var scale);
                blend.SetTexture(GameTexId, src);
                blend.SetTexture(CompositeTexId, _resources.CompositeTexture);
                blend.SetTexture(CoverageTexId, _resources.CoverageTexture);
                blend.SetVector(OffsetId, offset);
                blend.SetVector(ScaleId, scale);
                blend.SetVector(ExtendedToSmallTextureScaleId, new Vector2(
                    _smallLightTextureSize.x / (float)_extendedLightTextureSize.x,
                    _smallLightTextureSize.y / (float)_extendedLightTextureSize.y));
                Graphics.Blit(null, dest, blend);
            }
        }

        // Allocates the coverage mask (extended light size, light format) and the overlay-intermediate
        // composite (source descriptor + format) on demand. Only ever reached from the active coverage
        // branch, so the no-volume path never allocates them; EnsureTexture is idempotent, so re-calling
        // each active frame is a no-op once the targets are sized.
        private void EnsureCoverageTextures(RenderTextureDescriptor sourceDescriptor)
        {
            var coverageDescriptor = sourceDescriptor;
            coverageDescriptor.colorFormat = _texFormat;
            coverageDescriptor.depthBufferBits = 0;
            coverageDescriptor.msaaSamples = 1;
            SetSize(ref coverageDescriptor, _extendedLightTextureSize.x, _extendedLightTextureSize.y);
            _resources.EnsureTexture(ref _resources.CoverageTexture, coverageDescriptor,
                LightTexturesFilterMode, "Coverage");

            var compositeDescriptor = sourceDescriptor;
            compositeDescriptor.depthBufferBits = 0;
            compositeDescriptor.msaaSamples = 1;
            _resources.EnsureTexture(ref _resources.CompositeTexture, compositeDescriptor,
                FilterMode.Point, "Composite");
        }

        public void LoopAmbientLight(int cycles)
        {
            if (!EnableAmbientLight || cycles <= 0)
                return;
            _aditionalAmbientLightCycles = Mathf.Min(MaxQueuedAmbientCycles,
                _aditionalAmbientLightCycles + Mathf.Min(cycles, MaxQueuedAmbientCycles));
        }

        public int ConsumePendingAmbientLightCycles()
        {
            if (!EnableAmbientLight)
            {
                _aditionalAmbientLightCycles = 0;
                return 0;
            }
            int cycles = Mathf.Min(MaxAmbientCyclesPerFrame, Mathf.Max(0, _aditionalAmbientLightCycles));
            _aditionalAmbientLightCycles -= cycles;
            return cycles;
        }

        private void CaptureMainCameraState()
        {
            if (_mainCameraStateCaptured)
                return;
            _mainCameraStateCaptured = true;
            _mainCameraTarget = _camera.targetTexture;
            _mainCameraClearFlags = _camera.clearFlags;
            _mainCameraBackground = _camera.backgroundColor;
#pragma warning disable CS0618 // FlareLayer is obsolete from Unity 6000.5; see the _flareLayer field declaration.
            _flareLayer = GetComponent<FlareLayer>();
#pragma warning restore CS0618
            _flareLayerEnabled = _flareLayer != null && _flareLayer.enabled;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.clear;
            if (_flareLayer != null)
                _flareLayer.enabled = false;
        }

        private void RestoreMainCameraState()
        {
            if (!_mainCameraStateCaptured || _camera == null)
                return;
            _camera.targetTexture = _mainCameraTarget;
            _camera.clearFlags = _mainCameraClearFlags;
            _camera.backgroundColor = _mainCameraBackground;
            if (_flareLayer != null)
                _flareLayer.enabled = _flareLayerEnabled;
            _mainCameraStateCaptured = false;
            _mainCameraTarget = null;
            _flareLayer = null;
        }

        private void ReleaseOwnedResources()
        {
            RestoreMainCameraState();
            if (_obstaclesPostProcessor != null)
                _obstaclesPostProcessor.Dispose();
            _obstaclesPostProcessor = null;
            if (_resources != null)
                _resources.Dispose();
            _resources = null;
            ReleaseObstaclePreviewTarget();
            _initialized = false;
            _settingsHash = 0;
            _hasSourceDescriptor = false;
            _aditionalAmbientLightCycles = 0;
        }

        private int CalculateSettingsHash(RenderTextureDescriptor sourceDescriptor)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + _smallLightTextureSize.GetHashCode();
                hash = hash * 31 + _extendedLightTextureSize.GetHashCode();
                hash = hash * 31 + HDR.GetHashCode();
                hash = hash * 31 + _texFormat.GetHashCode();
                hash = hash * 31 + LightTexturesFilterMode.GetHashCode();
                hash = hash * 31 + BuiltInNormalMappingEnabled.GetHashCode();
                hash = hash * 31 + XZPlane.GetHashCode();
                hash = hash * 31 + LightObstaclesAntialiasing.GetHashCode();
                hash = hash * 31 + AffectOnlyThisCamera.GetHashCode();
                hash = hash * 31 + sourceDescriptor.width;
                hash = hash * 31 + sourceDescriptor.height;
                hash = hash * 31 + sourceDescriptor.graphicsFormat.GetHashCode();
                return hash;
            }
        }

        private RenderTextureDescriptor CreateCameraDescriptor()
        {
            var descriptor = new RenderTextureDescriptor(Mathf.Max(1, _camera.pixelWidth),
                Mathf.Max(1, _camera.pixelHeight), _texFormat, 0);
            descriptor.sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear;
            return descriptor;
        }

        private void ReportValidationError(string message)
        {
            int hash = message.GetHashCode();
            if (_validationDiagnosticHash == hash)
                return;
            _validationDiagnosticHash = hash;
            Debug.LogError("Light2D skipped rendering: " + message, this);
        }

        private void InvalidateRendering()
        {
            RestoreMainCameraState();
            if (_resources != null)
                _resources.ReleaseTextures();
            _initialized = false;
            _settingsHash = 0;
            _aditionalAmbientLightCycles = 0;
        }

        private static bool TryCreateFrameConfig(Camera sourceCamera,
            RenderTextureDescriptor sourceDescriptor, in LightingFrameSettings settings,
            out LightingFrameConfig config, out string error)
        {
            config = default(LightingFrameConfig);
            error = null;
            if (sourceCamera == null)
                error = "the source camera must be assigned.";
            else if (sourceDescriptor.width <= 0 || sourceDescriptor.height <= 0 || sourceDescriptor.volumeDepth != 1 ||
                     sourceDescriptor.dimension != TextureDimension.Tex2D || sourceDescriptor.vrUsage != VRTextureUsage.None)
                error = "the source render target must be a non-XR 2D texture with positive dimensions.";
            if (error != null)
                return false;

            if (!LightingSettingsValidator.TryCalculateSizes(sourceCamera, settings.PixelSize,
                    settings.OrthographicSizeAdd, settings.FieldOfViewAdd, settings.ObstacleDistance, settings.Hdr,
                    settings.EnableAmbientLight, settings.ObstacleAntialiasing, settings.NormalMapping,
                    out var estimate, out error))
                return false;

            long largestWidth = (long)estimate.Extended.x * (settings.ObstacleAntialiasing ? 2 : 1);
            long largestHeight = (long)estimate.Extended.y * (settings.ObstacleAntialiasing ? 2 : 1);
            if (sourceDescriptor.width > SystemInfo.maxTextureSize || sourceDescriptor.height > SystemInfo.maxTextureSize ||
                largestWidth * largestHeight > settings.MaximumTexturePixels ||
                (long)sourceDescriptor.width * sourceDescriptor.height > settings.MaximumTexturePixels)
            {
                error = "requested textures exceed the platform size limit or Light2D pixel budget.";
                return false;
            }

            config = new LightingFrameConfig(estimate.Small, estimate.Extended);
            return true;
        }

        private void ReportBuiltInFrame(int ambientCyclesConsumed)
        {
            int reallocations = _frameReallocations;
            _frameReallocations = 0;
            Light2DDiagnostics.ReportFrame(new Light2DFrameMetrics(
                _extendedLightTextureSize.x,
                _extendedLightTextureSize.y,
                Light2DDiagnostics.OwnedTextureBytes,
                _lightSpritesCache.Count,
                ambientCyclesConsumed,
                reallocations,
                Light2DDiagnostics.ContextCount));
        }

        private static void SetSize(ref RenderTextureDescriptor descriptor, int width, int height)
        {
            descriptor.width = Mathf.Max(1, width);
            descriptor.height = Mathf.Max(1, height);
        }

        private static void ClearTexture(RenderTexture texture)
        {
            var oldActive = RenderTexture.active;
            try
            {
                RenderTexture.active = texture;
                GL.Clear(false, true, Color.clear);
            }
            finally
            {
                RenderTexture.active = oldActive;
            }
        }

        private static void BlitToDestination(RenderTexture source, RenderTexture destination)
        {
            if (source != destination)
            {
                Graphics.Blit(source, destination);
                return;
            }

            var descriptor = source.descriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            var temporary = RenderTexture.GetTemporary(descriptor);
            try
            {
                Graphics.Blit(source, temporary);
                Graphics.Blit(temporary, destination);
            }
            finally
            {
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private static void SetKeyword(string keyword, bool enabled)
        {
            if (enabled)
                Shader.EnableKeyword(keyword);
            else
                Shader.DisableKeyword(keyword);
        }

        private static void Swap(ref RenderTexture left, ref RenderTexture right)
        {
            var temporary = left;
            left = right;
            right = temporary;
        }

        private void Update()
        {
            WarnIfUrpFeatureMissing();
            // Reclaim the obstacle-preview target once the editor window stops requesting it (the request
            // stamp expires ~2 frames after the last RequestObstaclePreview call), so it is only allocated
            // while a preview window is actually open.
            if (!ObstaclePreviewRequested && _obstaclePreviewHandle != null)
                ReleaseObstaclePreviewTarget();
        }

        // Called by Light2DUniversalRendererFeature each frame it renders this system's camera, so the
        // Update-time missing-feature warning stays silent while URP is actually serving Light2D.
        internal void NotifyUrpServed()
        {
            _lastUrpServedFrame = Time.frameCount;
            _urpNoFeatureWarningIssued = false;
        }

        // Under URP the drawing happens in Light2DUniversalRendererFeature, which calls NotifyUrpServed each
        // frame it renders this system. If the feature was never added to the active URP Renderer nothing
        // draws and nothing else complains, so warn once. The frame grace (seeded from OnEnable) avoids a
        // false positive during the feature's first-frame warmup.
        private void WarnIfUrpFeatureMissing()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || LightOverlayMaterial == null ||
                ActivePipeline != Light2DPipeline.UniversalRenderPipeline)
                return;
            if (_urpNoFeatureWarningIssued || Time.frameCount - _lastUrpServedFrame <= 4)
                return;
            _urpNoFeatureWarningIssued = true;
            Debug.LogWarning("A LightingSystem is active under URP but no Light2DUniversalRendererFeature " +
                             "rendered this camera. Add the Light2D renderer feature to the active URP " +
                             "Renderer, or Light2D will not render.", this);
        }

        private void LateUpdate()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying ||
                (UnityEditor.SceneView.currentDrawingSceneView != null &&
                 UnityEditor.SceneView.currentDrawingSceneView == UnityEditor.EditorWindow.focusedWindow))
                Shader.SetGlobalTexture(ObstacleTexId, Texture2D.whiteTexture);
#endif
        }

        private readonly struct LightingFrameConfig
        {
            internal readonly Vector2Int SmallSize;
            internal readonly Vector2Int ExtendedSize;

            internal LightingFrameConfig(Vector2Int smallSize, Vector2Int extendedSize)
            {
                SmallSize = smallSize;
                ExtendedSize = extendedSize;
            }
        }

        /// <summary>
        /// Groups the per-frame lighting settings consumed by frame-config validation.
        /// Construct with named arguments to avoid positional-parameter transposition.
        /// </summary>
        private readonly struct LightingFrameSettings
        {
            internal readonly float PixelSize;
            internal readonly float OrthographicSizeAdd;
            internal readonly float FieldOfViewAdd;
            internal readonly float ObstacleDistance;
            internal readonly bool Hdr;
            internal readonly bool EnableAmbientLight;
            internal readonly bool ObstacleAntialiasing;
            internal readonly bool NormalMapping;
            internal readonly long MaximumTexturePixels;

            internal LightingFrameSettings(float pixelSize, float orthographicSizeAdd, float fieldOfViewAdd,
                float obstacleDistance, bool hdr, bool enableAmbientLight, bool obstacleAntialiasing,
                bool normalMapping, long maximumTexturePixels)
            {
                PixelSize = pixelSize;
                OrthographicSizeAdd = orthographicSizeAdd;
                FieldOfViewAdd = fieldOfViewAdd;
                ObstacleDistance = obstacleDistance;
                Hdr = hdr;
                EnableAmbientLight = enableAmbientLight;
                ObstacleAntialiasing = obstacleAntialiasing;
                NormalMapping = normalMapping;
                MaximumTexturePixels = maximumTexturePixels;
            }
        }

        private struct GlobalShaderState
        {
            private readonly bool _xzPlane;
            private readonly bool _hdr;
            private readonly bool _perspectiveCamera;
            private readonly Texture _obstacleTexture;
            private readonly Texture _normalsTexture;

            private GlobalShaderState(bool capture)
            {
                _xzPlane = Shader.IsKeywordEnabled("LIGHT2D_XZ_PLANE");
                _hdr = Shader.IsKeywordEnabled("HDR");
                _perspectiveCamera = Shader.IsKeywordEnabled("PERSPECTIVE_CAMERA");
                _obstacleTexture = Shader.GetGlobalTexture(ObstacleTexId);
                _normalsTexture = Shader.GetGlobalTexture(NormalsBufferId);
            }

            internal static GlobalShaderState Capture()
            {
                return new GlobalShaderState(true);
            }

            internal void Restore()
            {
                SetKeyword("LIGHT2D_XZ_PLANE", _xzPlane);
                SetKeyword("HDR", _hdr);
                SetKeyword("PERSPECTIVE_CAMERA", _perspectiveCamera);
                Shader.SetGlobalTexture(ObstacleTexId, _obstacleTexture);
                Shader.SetGlobalTexture(NormalsBufferId, _normalsTexture);
            }
        }
    }
}
