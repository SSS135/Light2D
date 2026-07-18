using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace Light2D
{
    /// <summary>
    /// Main script for lights. Should be attached to camera.
    /// </summary>
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    public class LightingSystem : MonoBehaviour
    {
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
        private Point2 _extendedLightTextureSize;
        private Point2 _smallLightTextureSize;
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
        private FlareLayer _flareLayer;
        private bool _flareLayerEnabled;
        private bool _xzNormalMappingWarningIssued;

#if LIGHT2D_2DTK
        private tk2dCamera _tk2dCamera;
#endif

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
        public bool IsScriptableRenderPipelineActive { get { return GraphicsSettings.currentRenderPipeline != null; } }
        public bool IsRenderingReady
        {
            get
            {
                return isActiveAndEnabled && LightOverlayMaterial != null &&
                       (IsScriptableRenderPipelineActive || LightCamera != null);
            }
        }
        public float LightPixelsPerUnityMeter { get { return 1f / Mathf.Max(LightPixelSize, 0.00001f); } }
        public Vector2Int SmallLightTextureSize { get { return new Vector2Int(_smallLightTextureSize.x, _smallLightTextureSize.y); } }
        public Vector2Int ExtendedLightTextureSize { get { return new Vector2Int(_extendedLightTextureSize.x, _extendedLightTextureSize.y); } }
        public RenderTexture ObstaclesTexture { get { return _resources == null ? null : _resources.ObstaclesTexture; } }
        public RenderTexture LightSourcesTexture { get { return _resources == null ? null : _resources.LightSourcesTexture; } }
        public RenderTexture AmbientTexture { get { return _resources == null ? null : _resources.AmbientTexture; } }
        private bool BuiltInNormalMappingEnabled { get { return EnableNormalMapping && !XZPlane; } }

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
            get
            {
#if LIGHT2D_2DTK
                if (_tk2dCamera != null &&
                    _tk2dCamera.CameraSettings.projection == tk2dCameraSettings.ProjectionType.Orthographic)
                    return _tk2dCamera.ScreenExtents.yMax;
#endif
                return _camera.orthographicSize;
            }
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
        }

        private void Start()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Shader.SetGlobalTexture("_ObstacleTex", Texture2D.whiteTexture);
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
                Shader.SetGlobalTexture("_ObstacleTex", Texture2D.whiteTexture);
                BlitToDestination(src, dest);
                return;
            }
#endif
            var oldActive = RenderTexture.active;
            var lightCameraState = LightCameraState.Capture(LightCamera);
            var shaderState = GlobalShaderState.Capture();
            try
            {
                EnsureInitialized(src.descriptor);
                if (!_initialized)
                {
                    BlitToDestination(src, dest);
                    return;
                }

                EnsureFrameResources(src.descriptor);
                Update2DTK();
                UpdateCamera();
                LightSprite.PrepareSnapshot(_lightSpritesCache, XZPlane, LightSourcesLayer, _camera);
                SetKeyword("LIGHT2D_XZ_PLANE", XZPlane);
                SetKeyword("HDR", _texFormat == RenderTextureFormat.ARGBHalf);
                SetKeyword("PERSPECTIVE_CAMERA", !_camera.orthographic);
                RenderObstacles();
                SetupShaders();
                RenderNormalBuffer();
                RenderLightSources();
                RenderLightSourcesBlur();
                RenderAmbientLight();
                RenderLightOverlay(src, dest);
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
            if (LightCamera == null || LightOverlayMaterial == null)
            {
                if (!_initialized)
                    Debug.LogError("Lighting Camera and LightOverlayMaterial must be assigned for Light2D to render.", this);
                return false;
            }
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
            InitTK2D();
            CalculateTextureSizes();

            if (sourceDescriptor.HasValue)
            {
                _sourceDescriptor = sourceDescriptor.Value;
                _hasSourceDescriptor = true;
            }
            var descriptor = _hasSourceDescriptor ? _sourceDescriptor : CreateCameraDescriptor();
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
            if (resourcesChanged)
                LoopAmbientLight(100);
            return true;
        }

        private void CalculateTextureSizes()
        {
            float pixelsPerMeter = LightPixelsPerUnityMeter;
            if (_camera.orthographic)
            {
                float rawHeight = (SourceOrthographicSize + LightCameraSizeAdd) * 2f;
                float rawWidth = (SourceOrthographicSize * _camera.aspect + LightCameraSizeAdd) * 2f;
                _extendedLightTextureSize = new Point2(Mathf.RoundToInt(rawWidth * pixelsPerMeter),
                    Mathf.RoundToInt(rawHeight * pixelsPerMeter));
                float smallHeight = SourceOrthographicSize * 2f * pixelsPerMeter;
                _smallLightTextureSize = new Point2(Mathf.RoundToInt(smallHeight * _camera.aspect),
                    Mathf.RoundToInt(smallHeight));
            }
            else
            {
                float extendedSize = Mathf.Tan((_camera.fieldOfView + LightCameraFovAdd) * Mathf.Deg2Rad * 0.5f) *
                                     LightObstaclesDistance * 2f;
                int extendedHeight = Mathf.RoundToInt(extendedSize / Mathf.Max(LightPixelSize, 0.00001f));
                _extendedLightTextureSize = Point2.Round(new Vector2(extendedHeight * _camera.aspect, extendedHeight));
                float smallSize = Mathf.Tan(_camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * LightObstaclesDistance * 2f;
                int smallHeight = Mathf.RoundToInt(smallSize / Mathf.Max(LightPixelSize, 0.00001f));
                _smallLightTextureSize = Point2.Round(new Vector2(smallHeight * _camera.aspect, smallHeight));
            }
            _extendedLightTextureSize.x = Mathf.Max(4, _extendedLightTextureSize.x + (_extendedLightTextureSize.x & 1));
            _extendedLightTextureSize.y = Mathf.Max(4, _extendedLightTextureSize.y + (_extendedLightTextureSize.y & 1));
            _smallLightTextureSize.x = Mathf.Max(4, _smallLightTextureSize.x);
            _smallLightTextureSize.y = Mathf.Max(4, _smallLightTextureSize.y);
        }

        private void EnsureFrameResources(RenderTextureDescriptor sourceDescriptor)
        {
            var lightDescriptor = sourceDescriptor;
            lightDescriptor.colorFormat = _texFormat;
            lightDescriptor.depthBufferBits = 0;
            lightDescriptor.msaaSamples = 1;

            SetSize(ref lightDescriptor, _extendedLightTextureSize.x, _extendedLightTextureSize.y);
            _resources.EnsureTexture(ref _resources.ObstaclesTexture, lightDescriptor, LightTexturesFilterMode, "Obstacles");
            bool ambientTextureChanged = _resources.EnsureTexture(ref _resources.AmbientTexture, lightDescriptor,
                LightTexturesFilterMode, "Ambient");
            bool previousAmbientTextureChanged = _resources.EnsureTexture(ref _resources.PreviousAmbientTexture,
                lightDescriptor, LightTexturesFilterMode, "Previous Ambient");
            if (ambientTextureChanged)
                ClearTexture(_resources.AmbientTexture);
            if (previousAmbientTextureChanged)
                ClearTexture(_resources.PreviousAmbientTexture);
            _resources.EnsureTexture(ref _resources.AmbientEmissionTexture, lightDescriptor, LightTexturesFilterMode, "Ambient Emission");

            var upsampledSize = _extendedLightTextureSize * (LightObstaclesAntialiasing ? 2 : 1);
            SetSize(ref lightDescriptor, upsampledSize.x, upsampledSize.y);
            _resources.EnsureTexture(ref _resources.ObstaclesUpsampledTexture, lightDescriptor, LightTexturesFilterMode,
                "Upsampled Obstacles");

            SetSize(ref lightDescriptor, BuiltInNormalMappingEnabled ? sourceDescriptor.width : _smallLightTextureSize.x,
                BuiltInNormalMappingEnabled ? sourceDescriptor.height : _smallLightTextureSize.y);
            _resources.EnsureTexture(ref _resources.LightSourcesTexture, lightDescriptor,
                BuiltInNormalMappingEnabled ? FilterMode.Point : LightTexturesFilterMode, "Light Sources");

            if (BuiltInNormalMappingEnabled)
            {
                SetSize(ref lightDescriptor, _smallLightTextureSize.x, _smallLightTextureSize.y);
                _resources.EnsureTexture(ref _resources.SingleLightSourceTexture, lightDescriptor,
                    LightTexturesFilterMode, "Single Light Source");
                var normalDescriptor = sourceDescriptor;
                normalDescriptor.colorFormat = RenderTextureFormat.ARGB32;
                _resources.EnsureTexture(ref _resources.NormalMapBuffer, normalDescriptor, FilterMode.Point, "Normals");
            }

            int blurWidth = _resources.LightSourcesTexture.width == _smallLightTextureSize.x
                ? _resources.LightSourcesTexture.width * 2
                : _resources.LightSourcesTexture.width;
            int blurHeight = _resources.LightSourcesTexture.height == _smallLightTextureSize.y
                ? _resources.LightSourcesTexture.height * 2
                : _resources.LightSourcesTexture.height;
            SetSize(ref lightDescriptor, blurWidth, blurHeight);
            _resources.EnsureTexture(ref _resources.BlurredLightTexture, lightDescriptor, FilterMode.Bilinear,
                "Blurred Light Sources");

            var outputDescriptor = sourceDescriptor;
            outputDescriptor.depthBufferBits = 0;
            outputDescriptor.msaaSamples = 1;
            _resources.EnsureTexture(ref _resources.ScreenBlitTexture, outputDescriptor, FilterMode.Point,
                "Final Temporary Output");
            if (AffectOnlyThisCamera)
                _resources.EnsureTexture(ref _resources.CameraTargetTexture, outputDescriptor, FilterMode.Point,
                    "Camera Target");
        }

        private void RenderObstacles()
        {
            ConfigLightCamera(true);
            LightCamera.enabled = false;
            LightCamera.targetTexture = _resources.ObstaclesUpsampledTexture;
            LightCamera.cullingMask = 1 << LightObstaclesLayer;
            LightCamera.backgroundColor = new Color(1, 1, 1, 0);
            _obstaclesPostProcessor.DrawMesh(LightCamera, LightObstaclesAntialiasing ? 2f : 1f,
                LightObstaclesLayer);
            LightCamera.Render();
            _resources.ObstaclesTexture.DiscardContents();
            Graphics.Blit(_resources.ObstaclesUpsampledTexture, _resources.ObstaclesTexture);
        }

        private void SetupShaders()
        {
            Shader.SetGlobalTexture("_ObstacleTex", _resources.ObstaclesTexture);
            Shader.SetGlobalFloat("_PixelsPerBlock", LightPixelsPerUnityMeter);
            Shader.SetGlobalVector("_ExtendedToSmallTextureScale", new Vector2(
                _smallLightTextureSize.x / (float)_extendedLightTextureSize.x,
                _smallLightTextureSize.y / (float)_extendedLightTextureSize.y));
            Shader.SetGlobalVector("_PosOffset", LightObstaclesAntialiasing
                ? (BuiltInNormalMappingEnabled ? _resources.ObstaclesUpsampledTexture.texelSize * 0.75f : _resources.ObstaclesUpsampledTexture.texelSize * 0.25f)
                : (BuiltInNormalMappingEnabled ? _resources.ObstaclesTexture.texelSize : _resources.ObstaclesTexture.texelSize * 0.5f));
        }

        private void RenderNormalBuffer()
        {
            SetKeyword("NORMAL_MAPPED_LIGHTS", BuiltInNormalMappingEnabled);
            if (!BuiltInNormalMappingEnabled)
                return;
            var normalCamera = _resources.EnsureNormalMapCamera(_camera.transform);
            normalCamera.CopyFrom(_camera);
            normalCamera.transform.position = LightCamera.transform.position;
            normalCamera.clearFlags = CameraClearFlags.SolidColor;
            normalCamera.targetTexture = _resources.NormalMapBuffer;
            normalCamera.cullingMask = int.MaxValue;
            normalCamera.backgroundColor = new Color(0.5f, 0.5f, 0, 1);
            normalCamera.enabled = false;
            normalCamera.RenderWithShader(_normalMapRenderShader, "LightObstacle");
            Shader.SetGlobalTexture("_NormalsBuffer", _resources.NormalMapBuffer);
        }

        private void RenderLightSources()
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
                Graphics.SetRenderTarget(_resources.LightSourcesTexture);
                GL.Clear(false, true, LightCamera.backgroundColor);
                var planes = GeometryUtility.CalculateFrustumPlanes(_camera);
                var lightCameraLocalPosition = LightCamera.transform.localPosition;
                LightCamera.targetTexture = _resources.SingleLightSourceTexture;
                LightCamera.cullingMask = 0;
                LightCamera.backgroundColor = Color.clear;
                foreach (var lightSprite in _lightSpritesCache)
                {
                    if (!lightSprite.RendererEnabled || !GeometryUtility.TestPlanesAABB(planes, lightSprite.Renderer.bounds))
                        continue;
                    LightCamera.RenderWithShader(_normalMapRenderShader, "f84j");
                    Graphics.SetRenderTarget(_resources.SingleLightSourceTexture);
                    lightSprite.DrawLightingNow(lightCameraLocalPosition);
                    Graphics.SetRenderTarget(_resources.LightSourcesTexture);
                    lightSprite.DrawLightNormalsNow(normalMaterial);
                }
                LightCamera.cullingMask = 1 << LightSourcesLayer;
                LightCamera.Render();
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
            Profiler.BeginSample("LightingSystem.OnRenderImage Bluring Light Sources");
            try
            {
                _resources.LightSourcesTexture.filterMode = FilterMode.Bilinear;
                blurMaterial.mainTexture = _resources.LightSourcesTexture;
                Graphics.Blit(null, _resources.BlurredLightTexture, blurMaterial);
                if (LightTexturesFilterMode == FilterMode.Point)
                {
                    _resources.LightSourcesTexture.filterMode = FilterMode.Point;
                    Graphics.Blit(_resources.BlurredLightTexture, _resources.LightSourcesTexture);
                }
            }
            finally
            {
                Profiler.EndSample();
            }
        }

        private void RenderAmbientLight()
        {
            var computeMaterial = _resources.AmbientLightComputeMaterial;
            if (!EnableAmbientLight || computeMaterial == null)
                return;
            Profiler.BeginSample("LightingSystem.OnRenderImage Ambient Light");
            try
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
                    var positionShift = ((Vector2)(_currPos - _oldPos) / LightPixelSize).Div(textureSize);
                    _oldPos = _currPos;
                    computeMaterial.SetTexture("_LightSourcesTex", _resources.AmbientEmissionTexture);
                    computeMaterial.SetTexture("_MainTex", _resources.PreviousAmbientTexture);
                    computeMaterial.SetVector("_Shift", positionShift);
                    Graphics.Blit(null, _resources.AmbientTexture, computeMaterial);
                    if (BlurAmbientLight && _resources.AmbientLightBlurMaterial != null)
                    {
                        _resources.AmbientLightBlurMaterial.mainTexture = _resources.AmbientTexture;
                        Graphics.Blit(null, _resources.PreviousAmbientTexture, _resources.AmbientLightBlurMaterial);
                        Swap(ref _resources.PreviousAmbientTexture, ref _resources.AmbientTexture);
                    }
                }
            }
            finally
            {
                Profiler.EndSample();
            }
        }

        private void RenderLightOverlay(RenderTexture src, RenderTexture dest)
        {
            Profiler.BeginSample("LightingSystem.OnRenderImage Light Overlay");
            try
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
                overlay.SetTexture("_AmbientLightTex", EnableAmbientLight ? _resources.AmbientTexture : null);
                overlay.SetTexture("_LightSourcesTex", lightSources);
                overlay.SetTexture("_GameTex", src);
                overlay.SetVector("_Offset", offset);
                overlay.SetVector("_Scale", new Vector2(scaleY * _camera.aspect / LightCamera.aspect, scaleY));
                Graphics.Blit(null, _resources.ScreenBlitTexture, overlay);
                BlitToDestination(_resources.ScreenBlitTexture, dest);
            }
            finally
            {
                Profiler.EndSample();
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
            if (cycles <= 0)
                return;
            _aditionalAmbientLightCycles = _aditionalAmbientLightCycles > int.MaxValue - cycles
                ? int.MaxValue
                : _aditionalAmbientLightCycles + cycles;
        }

        public int ConsumePendingAmbientLightCycles()
        {
            int cycles = Mathf.Max(0, _aditionalAmbientLightCycles);
            _aditionalAmbientLightCycles = 0;
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
            _flareLayer = GetComponent<FlareLayer>();
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

        private void InitTK2D()
        {
#if LIGHT2D_2DTK
            _tk2dCamera = GetComponent<tk2dCamera>();
            Update2DTK();
#endif
        }

        private void Update2DTK()
        {
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
            if (!Application.isPlaying || Util.IsSceneViewFocused)
                Shader.SetGlobalTexture("_ObstacleTex", Texture2D.whiteTexture);
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
                _obstacleTexture = Shader.GetGlobalTexture("_ObstacleTex");
                _normalsTexture = Shader.GetGlobalTexture("_NormalsBuffer");
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
                Shader.SetGlobalTexture("_ObstacleTex", _obstacleTexture);
                Shader.SetGlobalTexture("_NormalsBuffer", _normalsTexture);
            }
        }
    }
}
