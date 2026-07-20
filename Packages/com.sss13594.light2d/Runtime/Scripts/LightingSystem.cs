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
    public class LightingSystem : MonoBehaviour
    {
        private const int MaximumAmbientCyclesPerFrame = 8;
        private const int MaximumQueuedAmbientCycles = 64;
        private const long MaximumOwnedTexturePixels = 16L * 1024L * 1024L;

        public float LightPixelSize = 0.05f;
        public float LightCameraSizeAdd = 3;
        public float LightCameraFovAdd = 30;
        public bool EnableAmbientLight = true;
        public bool BlurLightSources = true;
        public bool BlurAmbientLight = true;
        public bool HDR = true;
        public bool LightObstaclesAntialiasing = true;
        public float LightObstaclesDistance = 10;
        public FilterMode LightTexturesFilterMode = FilterMode.Bilinear;
        public bool EnableNormalMapping;
        public bool AffectOnlyThisCamera;
        public Material AmbientLightComputeMaterial;
        public Material LightOverlayMaterial;
        public Material LightSourcesBlurMaterial;
        public Material AmbientLightBlurMaterial;
        public Camera LightCamera;
        public int LightSourcesLayer;
        public int AmbientLightLayer;
        public int LightObstaclesLayer;
        public bool XZPlane;

        private static readonly HashSet<LightingSystem> ActiveInstances = new HashSet<LightingSystem>();
        private static readonly List<LightingSystem> InstanceSnapshot = new List<LightingSystem>();

        private Camera _camera;
        private LightingRenderResources _resources;
        private ObstacleCameraPostPorcessor _obstaclesPostProcessor;
        private readonly List<LightSprite> _lightSpritesCache = new List<LightSprite>();
        private readonly List<MeshRenderer> _flatPassHiddenRenderers = new List<MeshRenderer>();
        private readonly Plane[] _frustumPlanes = new Plane[6];
        private Vector2Int _extendedLightTextureSize;
        private Vector2Int _smallLightTextureSize;
        private Vector3 _oldPos;
        private Vector3 _currPos;
        private RenderTextureFormat _texFormat;
        private int _aditionalAmbientLightCycles;
        private Shader _normalMapRenderShader;
        private bool _initialized;
        private int _settingsHash;
        private bool _hasSourceDescriptor;
        private RenderTextureDescriptor _sourceDescriptor;
        private bool _mainCameraStateCaptured;
        private RenderTexture _mainCameraTarget;
        private CameraClearFlags _mainCameraClearFlags;
        private Color _mainCameraBackground;
#pragma warning disable CS0618 // FlareLayer is obsolete from Unity 6000.5 (Built-in Render Pipeline deprecation); Light2D still supports the Built-in backend and hides lens flares from the helper-camera render.
        private FlareLayer _flareLayer;
#pragma warning restore CS0618
        private bool _flareLayerEnabled;
        private bool _xzNormalMappingWarningIssued;
        private bool _unsupportedPipelineWarningIssued;
        private int _validationDiagnosticHash;
        private int _frameReallocations;

        private static readonly int ObstacleTexId = Shader.PropertyToID("_ObstacleTex");
        private static readonly int NormalsBufferId = Shader.PropertyToID("_NormalsBuffer");
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
                        return LightCamera != null;
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
        private bool BuiltInNormalMappingEnabled { get { return EnableNormalMapping && !XZPlane; } }

        internal static Light2DPipeline ClassifyPipeline(RenderPipelineAsset pipelineAsset)
        {
            if (pipelineAsset == null)
                return Light2DPipeline.BuiltIn;
            return pipelineAsset is UniversalRenderPipelineAsset
                ? Light2DPipeline.UniversalRenderPipeline
                : Light2DPipeline.UnsupportedScriptableRenderPipeline;
        }

        internal static bool IsNormalMappingLightCamera(Camera camera)
        {
            if (camera == null)
                return false;
            CleanupRegistry();
            foreach (var instance in ActiveInstances)
            {
                if (!instance.IsScriptableRenderPipelineActive && instance.BuiltInNormalMappingEnabled &&
                    instance.LightCamera == camera)
                    return true;
            }
            return false;
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
            foreach (var instance in FindObjectsByType<LightingSystem>(FindObjectsInactive.Exclude))
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
            var lightCameraState = LightCameraState.Capture(LightCamera);
            var shaderState = GlobalShaderState.Capture();
            try
            {
                if (!EnsureInitialized(src.descriptor))
                {
                    BlitToDestination(src, dest);
                    return;
                }

                EnsureFrameResources(src.descriptor);
                UpdateCamera();
                using (Light2DProfiling.Culling.Auto())
                    LightSprite.PrepareSnapshot(_lightSpritesCache, XZPlane, LightSourcesLayer, _camera);
                SetKeyword("LIGHT2D_XZ_PLANE", XZPlane);
                SetKeyword("HDR", _texFormat == RenderTextureFormat.ARGBHalf);
                SetKeyword("PERSPECTIVE_CAMERA", !_camera.orthographic);
                RenderObstacles();
                SetupShaders();
                RenderNormalBuffer();
                RenderLightSources();
                RenderLightSourcesBlur();
                int ambientCyclesConsumed = RenderAmbientLight();
                RenderLightOverlay(src, dest);
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
                lightCameraState.Restore(LightCamera);
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
                if (LightCamera == null || LightOverlayMaterial == null)
                {
                    if (!_initialized)
                        Debug.LogError("Lighting Camera and LightOverlayMaterial must be assigned for Light2D to render.", this);
                    return false;
                }
                descriptor = sourceDescriptor ?? (_hasSourceDescriptor ? _sourceDescriptor : CreateCameraDescriptor());
                var settings = new LightingFrameSettings(
                    pixelSize: LightPixelSize,
                    orthographicSizeAdd: LightCameraSizeAdd,
                    fieldOfViewAdd: LightCameraFovAdd,
                    obstacleDistance: LightObstaclesDistance,
                    lightSourcesLayer: LightSourcesLayer,
                    ambientLightLayer: AmbientLightLayer,
                    lightObstaclesLayer: LightObstaclesLayer,
                    hdr: HDR,
                    enableAmbientLight: EnableAmbientLight,
                    obstacleAntialiasing: LightObstaclesAntialiasing,
                    normalMapping: BuiltInNormalMappingEnabled,
                    maximumTexturePixels: MaximumOwnedTexturePixels);
                if (!TryCreateFrameConfig(_camera, LightCamera, descriptor, settings, out frameConfig,
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

            _normalMapRenderShader = Shader.Find("Light2D/Internal/Normal Map Drawer");
            if (BuiltInNormalMappingEnabled)
            {
                var normalMappedShader = Shader.Find("Light2D/Internal/Normal Mapped Light");
                var combiningShader = Shader.Find("Light2D/Internal/Light Blender");
                if (_normalMapRenderShader == null || normalMappedShader == null || combiningShader == null)
                    return false;
                _resources.EnsureNormalMappingMaterials(normalMappedShader, combiningShader);
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
                LoopAmbientLight(100);
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

                _frameReallocations += reallocations;
            }
        }

        private void RenderObstacles()
        {
            using (Light2DProfiling.Obstacles.Auto())
            {
                ConfigLightCamera(true);
                LightCamera.enabled = false;
                var obstacleTarget = LightObstaclesAntialiasing
                    ? _resources.ObstaclesUpsampledTexture
                    : _resources.ObstaclesTexture;
                LightCamera.targetTexture = obstacleTarget;
                LightCamera.cullingMask = 1 << LightObstaclesLayer;
                LightCamera.backgroundColor = new Color(1, 1, 1, 0);
                var borderCommand = _obstaclesPostProcessor.GetDrawCommand(LightCamera,
                    LightObstaclesAntialiasing ? 2f : 1f);
                if (borderCommand != null)
                    LightCamera.AddCommandBuffer(CameraEvent.AfterEverything, borderCommand);
                try
                {
                    LightCamera.Render();
                }
                finally
                {
                    if (borderCommand != null)
                        LightCamera.RemoveCommandBuffer(CameraEvent.AfterEverything, borderCommand);
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

        private void RenderNormalBuffer()
        {
            SetKeyword("NORMAL_MAPPED_LIGHTS", BuiltInNormalMappingEnabled);
            if (!BuiltInNormalMappingEnabled)
                return;
            using (Light2DProfiling.Normals.Auto())
            {
                var normalCamera = _resources.EnsureNormalMapCamera(_camera.transform);
                normalCamera.CopyFrom(_camera);
                normalCamera.transform.position = LightCamera.transform.position;
                normalCamera.clearFlags = CameraClearFlags.SolidColor;
                normalCamera.targetTexture = _resources.NormalMapBuffer;
                normalCamera.cullingMask = 1 << LightObstaclesLayer;
                normalCamera.backgroundColor = new Color(0.5f, 0.5f, 0, 1);
                normalCamera.enabled = false;
                normalCamera.RenderWithShader(_normalMapRenderShader, "LightObstacle");
                Shader.SetGlobalTexture(NormalsBufferId, _resources.NormalMapBuffer);
            }
        }

        private void RenderLightSources()
        {
            using (Light2DProfiling.Sources.Auto())
                RenderLightSourcesCore();
        }

        private void RenderLightSourcesCore()
        {
            ConfigLightCamera(false);
            LightCamera.backgroundColor = Color.clear;
            if (!BuiltInNormalMappingEnabled)
            {
                LightCamera.targetTexture = _resources.LightSourcesTexture;
                LightCamera.cullingMask = 1 << LightSourcesLayer;
                LightCamera.Render();
                return;
            }

            var normalMaterial = _resources.NormalMappedLightMaterial;
            var combiningMaterial = _resources.LightCombiningMaterial;
            normalMaterial.SetTexture("_MainTex", _resources.SingleLightSourceTexture);
            combiningMaterial.SetTexture("_MainTex", _resources.SingleLightSourceTexture);
            var oldActive = RenderTexture.active;
            try
            {
                var clearCommands = CommandBufferPool.Get("Light2D Clear Light Sources");
                try
                {
                    clearCommands.SetRenderTarget(_resources.LightSourcesTexture);
                    clearCommands.ClearRenderTarget(false, true, LightCamera.backgroundColor);
                    Graphics.ExecuteCommandBuffer(clearCommands);
                }
                finally
                {
                    CommandBufferPool.Release(clearCommands);
                }
                GeometryUtility.CalculateFrustumPlanes(_camera, _frustumPlanes);
                var lightCameraLocalPosition = LightCamera.transform.localPosition;
                LightCamera.targetTexture = _resources.SingleLightSourceTexture;
                LightCamera.cullingMask = 0;
                LightCamera.backgroundColor = Color.clear;
                foreach (var lightSprite in _lightSpritesCache)
                {
                    if (!lightSprite.RendererEnabled ||
                        !GeometryUtility.TestPlanesAABB(_frustumPlanes, lightSprite.Renderer.bounds))
                        continue;
                    LightCamera.RenderWithShader(_normalMapRenderShader, "f84j");
                    Graphics.SetRenderTarget(_resources.SingleLightSourceTexture);
                    lightSprite.DrawLightingNow(lightCameraLocalPosition);
                    Graphics.SetRenderTarget(_resources.LightSourcesTexture);
                    lightSprite.DrawLightNormalsNow(normalMaterial);
                }
                // Light sprites were already drawn manually above; hide their renderers so the
                // flat camera pass draws only the remaining light-sources layer content.
                var hiddenRenderers = _flatPassHiddenRenderers;
                try
                {
                    foreach (var lightSprite in _lightSpritesCache)
                    {
                        var renderer = lightSprite.Renderer;
                        if (renderer != null && renderer.enabled)
                        {
                            renderer.enabled = false;
                            hiddenRenderers.Add(renderer);
                        }
                    }
                    LightCamera.cullingMask = 1 << LightSourcesLayer;
                    LightCamera.Render();
                }
                finally
                {
                    foreach (var renderer in hiddenRenderers)
                    {
                        if (renderer != null)
                            renderer.enabled = true;
                    }
                    hiddenRenderers.Clear();
                }
                Graphics.Blit(_resources.SingleLightSourceTexture, _resources.LightSourcesTexture, combiningMaterial);
            }
            finally
            {
                RenderTexture.active = oldActive;
            }
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

        private int RenderAmbientLight()
        {
            var computeMaterial = _resources.AmbientLightComputeMaterial;
            if (!EnableAmbientLight || computeMaterial == null)
                return 0;
            using (Light2DProfiling.Ambient.Auto())
            {
                ConfigLightCamera(true);
                LightCamera.targetTexture = _resources.AmbientEmissionTexture;
                LightCamera.cullingMask = 1 << AmbientLightLayer;
                LightCamera.backgroundColor = Color.clear;
                LightCamera.Render();
                int additionalCycles = ConsumePendingAmbientLightCycles();
                for (int i = 0; i < additionalCycles + 1; i++)
                {
                    Swap(ref _resources.PreviousAmbientTexture, ref _resources.AmbientTexture);
                    var textureSize = new Vector2(_resources.AmbientTexture.width, _resources.AmbientTexture.height);
                    var pixelShift = (Vector2)(_currPos - _oldPos) / LightPixelSize;
                    var positionShift = new Vector2(pixelShift.x / textureSize.x, pixelShift.y / textureSize.y);
                    _oldPos = _currPos;
                    computeMaterial.SetTexture(LightSourcesTexId, _resources.AmbientEmissionTexture);
                    computeMaterial.SetTexture(MainTexId, _resources.PreviousAmbientTexture);
                    computeMaterial.SetVector(ShiftId, positionShift);
                    Graphics.Blit(null, _resources.AmbientTexture, computeMaterial);
                    if (BlurAmbientLight && _resources.AmbientLightBlurMaterial != null)
                    {
                        _resources.AmbientLightBlurMaterial.mainTexture = _resources.AmbientTexture;
                        Graphics.Blit(null, _resources.PreviousAmbientTexture, _resources.AmbientLightBlurMaterial);
                        Swap(ref _resources.PreviousAmbientTexture, ref _resources.AmbientTexture);
                    }
                }
                return additionalCycles + 1;
            }
        }

        private void RenderLightOverlay(RenderTexture src, RenderTexture dest)
        {
            using (Light2DProfiling.Composition.Auto())
            {
                ConfigLightCamera(false);
                var texelSize = new Vector2(1f / _smallLightTextureSize.x, 1f / _smallLightTextureSize.y);
                Vector2 worldOffset = Quaternion.Inverse(_camera.transform.rotation) *
                                      (LightCamera.transform.position - _camera.transform.position);
                Vector2 offset = Vector2.Scale(texelSize, -worldOffset * LightPixelsPerUnityMeter);
                var lightSources = BlurLightSources && _resources.LightSourcesBlurMaterial != null &&
                                   LightTexturesFilterMode != FilterMode.Point
                    ? _resources.BlurredLightTexture
                    : _resources.LightSourcesTexture;
                float sourceOrthographicSize = _camera.orthographic
                    ? SourceOrthographicSize
                    : Mathf.Tan(_camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * LightObstaclesDistance;
                float scaleY = sourceOrthographicSize / LightCamera.orthographicSize;
                var overlay = _resources.LightOverlayMaterial;
                overlay.SetTexture(AmbientLightTexId, EnableAmbientLight ? _resources.AmbientTexture : null);
                overlay.SetTexture(LightSourcesTexId, lightSources);
                overlay.SetTexture(GameTexId, src);
                overlay.SetVector(OffsetId, offset);
                overlay.SetVector(ScaleId, new Vector2(scaleY * _camera.aspect / LightCamera.aspect, scaleY));
                Graphics.Blit(null, dest, overlay);
            }
        }

        private void UpdateCamera()
        {
            float pixelsPerMeter = LightPixelsPerUnityMeter;
            Vector3 mainPosition = _camera.transform.position;
            Quaternion cameraRotation = _camera.transform.rotation;
            Vector3 unrotatedPosition = Quaternion.Inverse(cameraRotation) * mainPosition;
            var gridPosition = new Vector2(
                Mathf.Round(unrotatedPosition.x * pixelsPerMeter) / pixelsPerMeter,
                Mathf.Round(unrotatedPosition.y * pixelsPerMeter) / pixelsPerMeter);
            Vector2 positionDifference = gridPosition - (Vector2)unrotatedPosition;
            Vector3 position = cameraRotation * positionDifference + mainPosition;
            LightCamera.transform.position = position;
            _currPos = position;
        }

        public void LoopAmbientLight(int cycles)
        {
            if (!EnableAmbientLight || cycles <= 0)
                return;
            _aditionalAmbientLightCycles = Mathf.Min(MaximumQueuedAmbientCycles,
                _aditionalAmbientLightCycles + Mathf.Min(cycles, MaximumQueuedAmbientCycles));
        }

        public int ConsumePendingAmbientLightCycles()
        {
            if (!EnableAmbientLight)
            {
                _aditionalAmbientLightCycles = 0;
                return 0;
            }
            int cycles = Mathf.Min(MaximumAmbientCyclesPerFrame, Mathf.Max(0, _aditionalAmbientLightCycles));
            _aditionalAmbientLightCycles -= cycles;
            return cycles;
        }

        private void ConfigLightCamera(bool extended)
        {
            LightCamera.orthographic = _camera.orthographic;
            if (extended)
            {
                LightCamera.orthographicSize = SourceOrthographicSize *
                                               (_extendedLightTextureSize.y / (float)_smallLightTextureSize.y);
                LightCamera.fieldOfView = _camera.fieldOfView + LightCameraFovAdd;
                LightCamera.aspect = _extendedLightTextureSize.x / (float)_extendedLightTextureSize.y;
            }
            else
            {
                LightCamera.orthographicSize = _camera.orthographic
                    ? SourceOrthographicSize
                    : Mathf.Tan(_camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * LightObstaclesDistance;
                LightCamera.fieldOfView = _camera.fieldOfView;
                LightCamera.aspect = _smallLightTextureSize.x / (float)_smallLightTextureSize.y;
            }
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

        private static bool TryCreateFrameConfig(Camera sourceCamera, Camera lightCamera,
            RenderTextureDescriptor sourceDescriptor, in LightingFrameSettings settings,
            out LightingFrameConfig config, out string error)
        {
            config = default(LightingFrameConfig);
            error = null;
            if (sourceCamera == null || lightCamera == null)
                error = "source and helper cameras must be assigned.";
            else if (sourceCamera == lightCamera || lightCamera.GetComponent<LightingSystem>() != null)
                error = "the helper camera must not be a LightingSystem source camera.";
            else if (LightingSettingsValidator.ValidateLayers(settings.LightSourcesLayer, settings.AmbientLightLayer,
                         settings.LightObstaclesLayer, false) is var layerError && layerError != null)
                error = layerError;
            else if ((sourceCamera.cullingMask & ((1 << settings.LightSourcesLayer) | (1 << settings.AmbientLightLayer) |
                                                  (1 << settings.LightObstaclesLayer))) != 0)
                error = "the source camera culling mask must exclude all Light2D layers.";
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

        private void LateUpdate()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && !IsScriptableRenderPipelineActive && LightCamera != null)
            {
                _camera = GetComponent<Camera>();
                LightCamera.orthographic = _camera.orthographic;
                if (_camera.orthographic)
                    LightCamera.orthographicSize = SourceOrthographicSize + LightCameraSizeAdd;
                else
                    LightCamera.fieldOfView = _camera.fieldOfView + LightCameraFovAdd;
            }
            if (!Application.isPlaying ||
                (UnityEditor.SceneView.currentDrawingSceneView != null &&
                 UnityEditor.SceneView.currentDrawingSceneView == UnityEditor.EditorWindow.focusedWindow))
                Shader.SetGlobalTexture(ObstacleTexId, Texture2D.whiteTexture);
#endif
        }

        private struct LightCameraState
        {
            private readonly bool _valid;
            private readonly bool _enabled;
            private readonly RenderTexture _targetTexture;
            private readonly int _cullingMask;
            private readonly Color _backgroundColor;
            private readonly CameraClearFlags _clearFlags;
            private readonly bool _orthographic;
            private readonly float _orthographicSize;
            private readonly float _fieldOfView;
            private readonly float _aspect;
            private readonly Vector3 _position;
            private readonly Quaternion _rotation;

            private LightCameraState(Camera camera)
            {
                _valid = camera != null;
                _enabled = _valid && camera.enabled;
                _targetTexture = _valid ? camera.targetTexture : null;
                _cullingMask = _valid ? camera.cullingMask : 0;
                _backgroundColor = _valid ? camera.backgroundColor : default(Color);
                _clearFlags = _valid ? camera.clearFlags : default(CameraClearFlags);
                _orthographic = _valid && camera.orthographic;
                _orthographicSize = _valid ? camera.orthographicSize : 0;
                _fieldOfView = _valid ? camera.fieldOfView : 0;
                _aspect = _valid ? camera.aspect : 0;
                _position = _valid ? camera.transform.position : default(Vector3);
                _rotation = _valid ? camera.transform.rotation : default(Quaternion);
            }

            internal static LightCameraState Capture(Camera camera)
            {
                return new LightCameraState(camera);
            }

            internal void Restore(Camera camera)
            {
                if (!_valid || camera == null)
                    return;
                camera.enabled = _enabled;
                camera.targetTexture = _targetTexture;
                camera.cullingMask = _cullingMask;
                camera.backgroundColor = _backgroundColor;
                camera.clearFlags = _clearFlags;
                camera.orthographic = _orthographic;
                camera.orthographicSize = _orthographicSize;
                camera.fieldOfView = _fieldOfView;
                camera.aspect = _aspect;
                camera.transform.position = _position;
                camera.transform.rotation = _rotation;
            }
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
            internal readonly int LightSourcesLayer;
            internal readonly int AmbientLightLayer;
            internal readonly int LightObstaclesLayer;
            internal readonly bool Hdr;
            internal readonly bool EnableAmbientLight;
            internal readonly bool ObstacleAntialiasing;
            internal readonly bool NormalMapping;
            internal readonly long MaximumTexturePixels;

            internal LightingFrameSettings(float pixelSize, float orthographicSizeAdd, float fieldOfViewAdd,
                float obstacleDistance, int lightSourcesLayer, int ambientLightLayer, int lightObstaclesLayer,
                bool hdr, bool enableAmbientLight, bool obstacleAntialiasing, bool normalMapping,
                long maximumTexturePixels)
            {
                PixelSize = pixelSize;
                OrthographicSizeAdd = orthographicSizeAdd;
                FieldOfViewAdd = fieldOfViewAdd;
                ObstacleDistance = obstacleDistance;
                LightSourcesLayer = lightSourcesLayer;
                AmbientLightLayer = ambientLightLayer;
                LightObstaclesLayer = lightObstaclesLayer;
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
            private readonly bool _normalMappedLights;
            private readonly Texture _obstacleTexture;
            private readonly Texture _normalsTexture;

            private GlobalShaderState(bool capture)
            {
                _xzPlane = Shader.IsKeywordEnabled("LIGHT2D_XZ_PLANE");
                _hdr = Shader.IsKeywordEnabled("HDR");
                _perspectiveCamera = Shader.IsKeywordEnabled("PERSPECTIVE_CAMERA");
                _normalMappedLights = Shader.IsKeywordEnabled("NORMAL_MAPPED_LIGHTS");
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
                SetKeyword("NORMAL_MAPPED_LIGHTS", _normalMappedLights);
                Shader.SetGlobalTexture(ObstacleTexId, _obstacleTexture);
                Shader.SetGlobalTexture(NormalsBufferId, _normalsTexture);
            }
        }
    }
}
