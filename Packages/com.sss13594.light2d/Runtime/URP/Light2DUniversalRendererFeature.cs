using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Light2D
{
    /// <summary>
    /// URP 17 RenderGraph backend for a LightingSystem attached to the camera being rendered.
    /// The same feature is supported by UniversalRendererData and Renderer2DData.
    /// </summary>
    public class Light2DUniversalRendererFeature : ScriptableRendererFeature
    {
        [Serializable]
        public sealed class FeatureSettings
        {
            [Tooltip("Render event used to compose Light2D over the camera color.")]
            public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        }

        [SerializeField] private FeatureSettings _settings = new FeatureSettings();

        private readonly Dictionary<LightingSystem, CameraContext> _contexts =
            new Dictionary<LightingSystem, CameraContext>();
        private readonly Dictionary<LightingSystem, Light2DPass> _passes =
            new Dictionary<LightingSystem, Light2DPass>();
        private readonly List<LightingSystem> _deadContexts = new List<LightingSystem>();
        private readonly HashSet<EntityId> _cameraWarnings = new HashSet<EntityId>();

        public override void Create()
        {
            DisposeContexts();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            CleanupDeadContexts();

            var camera = renderingData.cameraData.camera;
            if (camera == null || camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection)
                return;

            var lighting = camera.GetComponent<LightingSystem>();
            if (lighting == null || !lighting.isActiveAndEnabled)
                return;

            string unsupportedReason;
            if (!IsCameraSupported(camera, ref renderingData.cameraData, out unsupportedReason))
            {
                WarnOnce(camera, lighting, unsupportedReason);
                return;
            }

            Light2DPass pass;
            if (!_passes.TryGetValue(lighting, out pass))
            {
                pass = new Light2DPass(_contexts, lighting, WarnOnce);
                pass.renderPassEvent = _settings.RenderPassEvent;
                pass.requiresIntermediateTexture = true;
                // Declaring a Color input is the only per-pass lever the 2D renderer (Renderer2DData) honors to
                // create an intermediate camera-color texture: it ignores requiresIntermediateTexture, so an
                // offscreen/no-post render (e.g. a SingleCameraRequest to a RenderTexture) otherwise renders
                // straight to the backbuffer, activeColorID stays BackBuffer, and the feature's intermediate
                // guard drops the composite. This forces the intermediate on both renderers; on-screen it is a
                // no-op wherever the intermediate already exists (the composite requires it, so it always does).
                pass.ConfigureInput(ScriptableRenderPassInput.Color);
                _passes.Add(lighting, pass);
            }
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            DisposeContexts();
        }

        private void CleanupDeadContexts()
        {
            _deadContexts.Clear();
            foreach (var pair in _contexts)
            {
                if (pair.Key == null || !pair.Key.isActiveAndEnabled)
                    _deadContexts.Add(pair.Key);
            }
            foreach (var pair in _passes)
            {
                if ((pair.Key == null || !pair.Key.isActiveAndEnabled) && !_deadContexts.Contains(pair.Key))
                    _deadContexts.Add(pair.Key);
            }

            foreach (var lighting in _deadContexts)
            {
                CameraContext context;
                if (_contexts.TryGetValue(lighting, out context))
                    context.Dispose();
                _contexts.Remove(lighting);
                _passes.Remove(lighting);
            }
        }

        private void DisposeContexts()
        {
            foreach (var context in _contexts.Values)
                context.Dispose();
            _contexts.Clear();
            _passes.Clear();
            _deadContexts.Clear();
            _cameraWarnings.Clear();
        }

        private static bool IsCameraSupported(Camera camera, ref CameraData cameraData, out string reason)
        {
            // The stack lookup stays behind the XR/Overlay checks so rejected cameras never touch
            // UniversalAdditionalCameraData.cameraStack, which logs its own warnings for non-Base cameras.
            bool hasActiveCameraStack = !cameraData.xrRendering &&
                cameraData.renderType != CameraRenderType.Overlay && HasActiveCameraStack(camera);
            return IsCameraSupported(cameraData.xrRendering, cameraData.renderType, hasActiveCameraStack,
                out reason);
        }

        internal static bool IsCameraSupported(bool xrRendering, CameraRenderType renderType,
            bool hasActiveCameraStack, out string reason)
        {
            if (xrRendering)
            {
                reason = "Light2D does not support XR cameras.";
                return false;
            }
            if (renderType == CameraRenderType.Overlay)
            {
                reason = "Light2D does not support URP Overlay cameras.";
                return false;
            }
            if (hasActiveCameraStack)
            {
                reason = "Light2D does not support URP Base cameras with an active camera stack.";
                return false;
            }

            reason = null;
            return true;
        }

        internal static bool HasActiveCameraStack(Camera camera)
        {
            UniversalAdditionalCameraData additionalData;
            return camera.TryGetComponent(out additionalData) && additionalData.cameraStack != null &&
                   additionalData.cameraStack.Count != 0;
        }

        internal void WarnOnce(Camera camera, LightingSystem lighting, string message)
        {
            if (_cameraWarnings.Add(camera.GetEntityId()))
                Debug.LogWarning(message + " Rendering was skipped for camera '" + camera.name + "'.", lighting);
        }

        private sealed class CameraContext : IDisposable
        {
            internal readonly LightingSystem Lighting;
            internal Material AmbientMaterial;
            internal Material OverlayMaterial;
            internal Material SourceBlurMaterial;
            internal Material AmbientBlurMaterial;
            // Last serialized-source CRC per cloned material, parallel to the four fields above; gates the
            // per-frame CopyPropertiesFromMaterial in CloneMaterial (mirrors CustomSprite.RefreshMaterial).
            internal int AmbientMaterialCrc;
            internal int OverlayMaterialCrc;
            internal int SourceBlurMaterialCrc;
            internal int AmbientBlurMaterialCrc;
            internal Material NormalLightMaterial;
            internal Mesh BorderMesh;
            internal Material BorderMaterial;
            internal Material CoverageBlendMaterial;
            internal RTHandle AmbientA;
            internal RTHandle AmbientB;
            internal bool AmbientReadIsA = true;
            internal bool HistoryNeedsClear;
            internal int WarmupCycles;
            internal int DescriptorHash;
            internal Vector3 PreviousLightPosition;
            internal bool HasPreviousPosition;
            internal bool ValidationErrorIssued;
            internal Quaternion PreviousRotation;
            internal float PreviousProjectionSize;
            internal float PreviousAspect;
            internal bool HasPreviousTransform;
            private readonly int _diagnosticsGeneration;
            private long _ownedTextureBytes;
            private bool _disposed;
            private int _borderWidth = -1;
            private int _borderHeight = -1;

            internal CameraContext(LightingSystem lighting)
            {
                Lighting = lighting;
                _diagnosticsGeneration = Light2DDiagnostics.RegisterContext();
            }

            internal bool ValidateAndUpdate(int warmupCycles)
            {
                if (Lighting == null || Lighting.SourceCamera == null || Lighting.LightOverlayMaterial == null)
                {
                    if (!ValidationErrorIssued)
                        Debug.LogError("Light2D URP requires a source Camera and Light Overlay Material assignment.", Lighting);
                    ValidationErrorIssued = true;
                    return false;
                }

                if (Lighting.EnableAmbientLight && Lighting.AmbientLightComputeMaterial == null)
                {
                    if (!ValidationErrorIssued)
                        Debug.LogError("Light2D ambient lighting is enabled but Ambient Light Compute Material is missing.", Lighting);
                    ValidationErrorIssued = true;
                    return false;
                }

                if (Lighting.EnableNormalMapping && !Lighting.SourceCamera.orthographic)
                {
                    if (!ValidationErrorIssued)
                        Debug.LogError("Light2D normal mapping is supported only by orthographic cameras.", Lighting);
                    ValidationErrorIssued = true;
                    return false;
                }

                if (Lighting.EnableNormalMapping && Lighting.XZPlane)
                {
                    if (!ValidationErrorIssued)
                        Debug.LogError("Light2D's existing normal-map shaders support the XY plane only. Disable normal mapping for XZ rendering.", Lighting);
                    ValidationErrorIssued = true;
                    return false;
                }

                CloneMaterial(ref AmbientMaterial, ref AmbientMaterialCrc, Lighting.AmbientLightComputeMaterial, "Ambient");
                CloneMaterial(ref OverlayMaterial, ref OverlayMaterialCrc, Lighting.LightOverlayMaterial, "Overlay");
                CloneMaterial(ref SourceBlurMaterial, ref SourceBlurMaterialCrc, Lighting.LightSourcesBlurMaterial, "Source Blur");
                CloneMaterial(ref AmbientBlurMaterial, ref AmbientBlurMaterialCrc, Lighting.AmbientLightBlurMaterial, "Ambient Blur");

                if (Lighting.EnableNormalMapping)
                {
                    var lightShader = Shader.Find("Light2D/Internal/Normal Mapped Light");
                    if (lightShader == null)
                    {
                        if (!ValidationErrorIssued)
                            Debug.LogError("Light2D normal-mapped light shader was not found.", Lighting);
                        ValidationErrorIssued = true;
                        return false;
                    }
                    EnsureMaterial(ref NormalLightMaterial, lightShader, "Normal Mapped Light");
                }

                if (BorderMaterial == null)
                {
                    var shader = Shader.Find("Light2D/Obstacle Texture Post Porcessor");
                    if (shader == null)
                    {
                        if (!ValidationErrorIssued)
                            Debug.LogError("Light2D obstacle border shader was not found.", Lighting);
                        ValidationErrorIssued = true;
                        return false;
                    }
                    EnsureMaterial(ref BorderMaterial, shader, "Obstacle Border");
                    BorderMesh = CreateBorderMesh();
                    // Fresh mesh carries full-quad vertices; force the next UpdateBorderMesh to rebuild it.
                    _borderWidth = -1;
                    _borderHeight = -1;
                }

                if (CoverageBlendMaterial == null)
                {
                    var coverageShader = Shader.Find("Light2D/Coverage Blend");
                    if (coverageShader == null)
                    {
                        if (!ValidationErrorIssued)
                            Debug.LogError("Light2D coverage blend shader was not found.", Lighting);
                        ValidationErrorIssued = true;
                        return false;
                    }
                    EnsureMaterial(ref CoverageBlendMaterial, coverageShader, "Coverage Blend");
                }

                ValidationErrorIssued = false;
                return true;
            }

            internal bool EnsureHistory(RenderTextureDescriptor descriptor, FilterMode filterMode, int warmupCycles)
            {
                descriptor.depthBufferBits = 0;
                descriptor.msaaSamples = 1;
                descriptor.useMipMap = false;
                descriptor.autoGenerateMips = false;
                descriptor.useDynamicScale = false;
                int hash = DescriptorHashOf(descriptor, filterMode);
                bool changed = hash != DescriptorHash || AmbientA == null || AmbientB == null;
                RenderingUtils.ReAllocateHandleIfNeeded(ref AmbientA, descriptor, filterMode, TextureWrapMode.Clamp,
                    name: "Light2D Ambient History A");
                RenderingUtils.ReAllocateHandleIfNeeded(ref AmbientB, descriptor, filterMode, TextureWrapMode.Clamp,
                    name: "Light2D Ambient History B");
                if (!changed)
                    return false;

                long newOwnedTextureBytes = Light2DDiagnostics.EstimateTextureBytes(descriptor.width,
                    descriptor.height, descriptor.volumeDepth, descriptor.graphicsFormat) * 2;
                Light2DDiagnostics.AddOwnedTextureBytes(_diagnosticsGeneration,
                    newOwnedTextureBytes - _ownedTextureBytes);
                _ownedTextureBytes = newOwnedTextureBytes;

                DescriptorHash = hash;
                AmbientReadIsA = true;
                HistoryNeedsClear = true;
                HasPreviousPosition = false;
                HasPreviousTransform = false;
                WarmupCycles = Mathf.Max(WarmupCycles, warmupCycles);
                return true;
            }

            internal void ResetHistoryForUnsupportedTransform(Camera camera, int warmupCycles)
            {
                float projectionSize = camera.orthographic ? camera.orthographicSize : camera.fieldOfView;
                bool changed = HasPreviousTransform &&
                    (Quaternion.Angle(PreviousRotation, camera.transform.rotation) > 0.001f ||
                     !Mathf.Approximately(PreviousProjectionSize, projectionSize) ||
                     !Mathf.Approximately(PreviousAspect, camera.aspect));
                PreviousRotation = camera.transform.rotation;
                PreviousProjectionSize = projectionSize;
                PreviousAspect = camera.aspect;
                HasPreviousTransform = true;
                if (!changed)
                    return;

                HistoryNeedsClear = true;
                HasPreviousPosition = false;
                WarmupCycles = Mathf.Max(WarmupCycles, warmupCycles);
            }

            internal void UpdateBorderMesh(int width, int height)
            {
                // The border geometry depends only on the target size, so rebuild only on a resize; the
                // per-frame AddBorderPass draw still runs. Avoids a per-frame Vector3[16] alloc + mesh upload.
                if (width == _borderWidth && height == _borderHeight)
                    return;
                _borderWidth = width;
                _borderHeight = height;
                float x = 2f / Mathf.Max(1, width);
                float y = 2f / Mathf.Max(1, height);
                var vertices = BorderMesh.vertices;
                SetQuad(vertices, 0, -1, -1, -1 + x, 1);
                SetQuad(vertices, 4, 1 - x, -1, 1, 1);
                SetQuad(vertices, 8, -1, -1, 1, -1 + y);
                SetQuad(vertices, 12, -1, 1 - y, 1, 1);
                BorderMesh.vertices = vertices;
                BorderMesh.RecalculateBounds();
            }

            public void Dispose()
            {
                if (_disposed)
                    return;
                _disposed = true;
                Destroy(ref AmbientMaterial);
                Destroy(ref OverlayMaterial);
                Destroy(ref SourceBlurMaterial);
                Destroy(ref AmbientBlurMaterial);
                Destroy(ref NormalLightMaterial);
                Destroy(ref BorderMaterial);
                Destroy(ref CoverageBlendMaterial);
                if (BorderMesh != null)
                    CoreUtils.Destroy(BorderMesh);
                BorderMesh = null;
                AmbientA?.Release();
                AmbientB?.Release();
                AmbientA = null;
                AmbientB = null;
                Light2DDiagnostics.RemoveOwnedTextureBytes(_diagnosticsGeneration, _ownedTextureBytes);
                _ownedTextureBytes = 0;
                Light2DDiagnostics.UnregisterContext(_diagnosticsGeneration);
            }

            private static int DescriptorHashOf(RenderTextureDescriptor descriptor, FilterMode filterMode)
            {
                unchecked
                {
                    int hash = descriptor.width;
                    hash = hash * 397 ^ descriptor.height;
                    hash = hash * 397 ^ descriptor.dimension.GetHashCode();
                    hash = hash * 397 ^ descriptor.volumeDepth;
                    hash = hash * 397 ^ descriptor.sRGB.GetHashCode();
                    hash = hash * 397 ^ descriptor.graphicsFormat.GetHashCode();
                    hash = hash * 397 ^ filterMode.GetHashCode();
                    return hash;
                }
            }

            private static void CloneMaterial(ref Material clone, ref int lastSourceCrc, Material source, string name)
            {
                if (source == null)
                {
                    Destroy(ref clone);
                    lastSourceCrc = 0;
                    return;
                }

                if (clone == null || clone.shader != source.shader)
                {
                    Destroy(ref clone);
                    clone = new Material(source) { hideFlags = HideFlags.DontSave };
                    clone.name = "Generated Light2D URP " + name;
                    lastSourceCrc = source.ComputeCRC();
                    return;
                }

                // Change-detected live mirroring (mirrors CustomSprite.RefreshMaterial): skip the per-frame
                // property copy unless the serialized source material actually changed since last frame.
                var sourceCrc = source.ComputeCRC();
                if (sourceCrc == lastSourceCrc)
                    return;
                lastSourceCrc = sourceCrc;
                clone.CopyPropertiesFromMaterial(source);
            }

            private static void EnsureMaterial(ref Material material, Shader shader, string name)
            {
                if (material != null && material.shader == shader)
                    return;
                Destroy(ref material);
                material = new Material(shader) { hideFlags = HideFlags.DontSave };
                material.name = "Generated Light2D URP " + name;
            }

            private static void Destroy(ref Material material)
            {
                if (material == null)
                    return;
                CoreUtils.Destroy(material);
                material = null;
            }

            private static Mesh CreateBorderMesh()
            {
                var mesh = new Mesh { name = "Light2D URP Obstacle Border", hideFlags = HideFlags.DontSave };
                var vertices = new Vector3[16];
                var colors = new Color32[16];
                var indices = new int[24];
                int vertex = 0;
                int index = 0;
                AddQuad(vertices, colors, indices, ref vertex, ref index, -1, -1, -1, 1);
                AddQuad(vertices, colors, indices, ref vertex, ref index, 1, -1, 1, 1);
                AddQuad(vertices, colors, indices, ref vertex, ref index, -1, -1, 1, -1);
                AddQuad(vertices, colors, indices, ref vertex, ref index, -1, 1, 1, 1);
                mesh.vertices = vertices;
                mesh.colors32 = colors;
                mesh.triangles = indices;
                return mesh;
            }

            private static void AddQuad(Vector3[] vertices, Color32[] colors, int[] indices, ref int vertex,
                ref int index, float minX, float minY, float maxX, float maxY)
            {
                vertices[vertex] = new Vector3(minX, minY, 0);
                vertices[vertex + 1] = new Vector3(minX, maxY, 0);
                vertices[vertex + 2] = new Vector3(maxX, maxY, 0);
                vertices[vertex + 3] = new Vector3(maxX, minY, 0);
                for (int i = 0; i < 4; i++)
                    colors[vertex + i] = Color.white;
                indices[index++] = vertex;
                indices[index++] = vertex + 1;
                indices[index++] = vertex + 3;
                indices[index++] = vertex + 3;
                indices[index++] = vertex + 1;
                indices[index++] = vertex + 2;
                vertex += 4;
            }

            private static void SetQuad(Vector3[] vertices, int vertex, float minX, float minY, float maxX,
                float maxY)
            {
                vertices[vertex] = new Vector3(minX, minY, 0);
                vertices[vertex + 1] = new Vector3(minX, maxY, 0);
                vertices[vertex + 2] = new Vector3(maxX, maxY, 0);
                vertices[vertex + 3] = new Vector3(maxX, minY, 0);
            }
        }

        private sealed class Light2DPass : ScriptableRenderPass
        {
            // DrawMesh selects a pass by index, not by LightMode, so the register-and-draw path resolves the
            // pass index from these LightMode names via Light2DDraw.ResolvePass. The colored obstacle/source/
            // ambient draw uses SRPDefaultUnlit; the obstacle normal buffer uses Light2DNormals (design R1).
            private const string ForwardLightMode = "SRPDefaultUnlit";
            private const string NormalsLightMode = "Light2DNormals";
            private static readonly int UrpObstacleTex = Shader.PropertyToID("_Light2DURPObstacleTex");
            private static readonly int UrpMainTex = Shader.PropertyToID("_Light2DURPMainTex");
            private static readonly int UrpGameTex = Shader.PropertyToID("_Light2DURPGameTex");
            private static readonly int UrpCompositeTex = Shader.PropertyToID("_Light2DURPCompositeTex");
            private static readonly int UrpCoverageTex = Shader.PropertyToID("_Light2DURPCoverageTex");
            private static readonly int UrpAmbientLightTex = Shader.PropertyToID("_Light2DURPAmbientLightTex");
            private static readonly int UrpLightSourcesTex = Shader.PropertyToID("_Light2DURPLightSourcesTex");
            private static readonly int UrpNormalsBuffer = Shader.PropertyToID("_Light2DURPNormalsBuffer");
            private static readonly int PixelsPerBlock = Shader.PropertyToID("_PixelsPerBlock");
            private static readonly int ExtendedScale = Shader.PropertyToID("_ExtendedToSmallTextureScale");
            private static readonly int PositionOffset = Shader.PropertyToID("_PosOffset");
            private static readonly int Shift = Shader.PropertyToID("_Shift");
            private static readonly int Scale = Shader.PropertyToID("_Scale");
            private static readonly int Offset = Shader.PropertyToID("_Offset");
            private static readonly int LightPosition = Shader.PropertyToID("_LightPos");
            private static readonly int ViewportToWorldX = Shader.PropertyToID("_ViewportToWorldX");
            private static readonly int ViewportToWorldY = Shader.PropertyToID("_ViewportToWorldY");
            private static readonly int MainTexTexelSize = Shader.PropertyToID("_Light2DURPMainTex_TexelSize");
            // Colored-bounce knob shared by both pipelines (declared on the ambient material). Pushed onto the
            // cloned URP ambient material each frame so it matches the Built-in backend; default 0 = the shader's
            // neutral default. Obstacle/emission/sampling tuning is left to the material's own serialized values.
            private static readonly int AmbientBounceTint = Shader.PropertyToID("_AmbientBounceTint");
            // URP-parity material "feel"/"look"/blur knobs, mirrored from the Built-in LightingSystem push and
            // applied to the cloned URP materials unconditionally each frame (values-only source of truth).
            // Names match the shared shader property identifiers.
            private static readonly int ObstacleGlobalMul = Shader.PropertyToID("_Light2DObstacleGlobalMul");
            private static readonly int ObstacleMul = Shader.PropertyToID("_ObstacleMul");
            private static readonly int ObstacleAdd = Shader.PropertyToID("_ObstacleAdd");
            private static readonly int EmissionColorMul = Shader.PropertyToID("_EmissionColorMul");
            private static readonly int SamplingDist = Shader.PropertyToID("_SamplingDist");
            private static readonly int AmbientAdaptSpeed = Shader.PropertyToID("_AmbientAdaptSpeed");
            private static readonly int LightMul = Shader.PropertyToID("_LightMul");
            private static readonly int LightSourcesMul = Shader.PropertyToID("_LightSourcesMul");
            private static readonly int AdditiveLightPow = Shader.PropertyToID("_AdditiveLightPow");
            private static readonly int AdditiveLightAdd = Shader.PropertyToID("_AdditiveLightAdd");
            private static readonly int Distance = Shader.PropertyToID("_Distance");
            // Use GlobalKeyword.Create (not `new GlobalKeyword`) so the keyword is registered if no declaring
            // shader has been loaded yet. `new GlobalKeyword(name)` only references an existing global keyword and
            // logs "Global keyword X doesn't exist" when the first URP frame runs before any Light2D shader is
            // resident (e.g. an isolated test scene) - Create() registers and returns the same shared keyword.
            private static readonly GlobalKeyword HdrKeyword = GlobalKeyword.Create("HDR");
            private static readonly GlobalKeyword XzKeyword = GlobalKeyword.Create("LIGHT2D_XZ_PLANE");
            private static readonly GlobalKeyword XyKeyword = GlobalKeyword.Create("LIGHT2D_XY_PLANE");
            private static readonly GlobalKeyword PerspectiveKeyword = GlobalKeyword.Create("PERSPECTIVE_CAMERA");
            private static readonly GlobalKeyword OrthographicKeyword = GlobalKeyword.Create("ORTHOGRAPHIC_CAMERA");
            private readonly Dictionary<LightingSystem, CameraContext> _contexts;
            private readonly List<LightSprite> _lightSprites = new List<LightSprite>();
            // Register-and-draw snapshots captured on the main thread in RecordRenderGraph and read by the
            // raster render funcs. Obstacles/ambient/sources each own a list; the normals pass reuses the
            // obstacle snapshot. Frustum planes are cached into reusable arrays to avoid per-frame allocation.
            private readonly List<Light2DDrawEntry> _obstacleSnapshot = new List<Light2DDrawEntry>();
            private readonly List<Light2DDrawEntry> _coverageSnapshot = new List<Light2DDrawEntry>();
            private readonly List<Light2DDrawEntry> _ambientSnapshot = new List<Light2DDrawEntry>();
            private readonly List<Light2DDrawEntry> _sourceEntries = new List<Light2DDrawEntry>();
            private readonly Plane[] _extendedPlanes = new Plane[6];
            private readonly Plane[] _smallPlanes = new Plane[6];
            // Per-draw block carrying the obstacle multiplier for this pass's obstacle draw. Each Light2DPass
            // instance serves exactly one LightingSystem (keyed in _passes), so this stays per-camera isolated;
            // it is reused across frames and re-set before each obstacle pass is recorded.
            private MaterialPropertyBlock _obstacleProperties;
            private readonly LightingSystem _lighting;
            private readonly Action<Camera, LightingSystem, string> _warnOnce;

            internal Light2DPass(Dictionary<LightingSystem, CameraContext> contexts,
                LightingSystem lighting, Action<Camera, LightingSystem, string> warnOnce)
            {
                _contexts = contexts;
                _lighting = lighting;
                _warnOnce = warnOnce;
                profilingSampler = new ProfilingSampler("Light2D URP");
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var cameraData = frameData.Get<UniversalCameraData>();
                var resourceData = frameData.Get<UniversalResourceData>();
                if (_lighting == null || cameraData.camera != _lighting.SourceCamera)
                    return;

                if (resourceData.isActiveTargetBackBuffer)
                {
                    _warnOnce(cameraData.camera, _lighting,
                        "Light2D requires URP to provide an intermediate camera-color texture.");
                    return;
                }

                var camera = cameraData.camera;
                var sourceDescriptor = cameraData.cameraTargetDescriptor;
                TextureHandle black = renderGraph.defaultResources.blackTexture;
                Sizes sizes;
                string validationError;
                ScriptableCullingParameters unusedCullingParameters;
                bool frameIsValid;
                using (Light2DProfiling.Validation.Auto())
                {
                    frameIsValid = TryValidateFrame(_lighting, camera, sourceDescriptor, out sizes,
                        out validationError) && camera.TryGetCullingParameters(false, out unusedCullingParameters);
                }
                if (!frameIsValid)
                {
                    _warnOnce(camera, _lighting, validationError ??
                        "Light2D could not obtain culling parameters for this camera.");
                    return;
                }

                CameraContext context;
                bool contextIsReady;
                using (Light2DProfiling.ResourceReconciliation.Auto())
                {
                    if (!_contexts.TryGetValue(_lighting, out context))
                    {
                        context = new CameraContext(_lighting);
                        _contexts.Add(_lighting, context);
                    }
                    contextIsReady = context.ValidateAndUpdate(_lighting.AmbientWarmupCycles);
                }
                if (!contextIsReady)
                    return;
                // B.3 handshake: tell the LightingSystem a URP feature pass served this camera so it can
                // warn if it is ever active under URP with no Light2DUniversalRendererFeature installed.
                _lighting.NotifyUrpServed();

                Vector3 lightPosition = Light2DCameraGeometry.CalculateSnappedPosition(camera,
                    _lighting.LightPixelsPerUnityMeter);
                Light2DCameraGeometry extendedCamera = Light2DCameraGeometry.Calculate(camera, lightPosition,
                    sizes.Small, sizes.Extended, true, _lighting.LightCameraFovAdd, _lighting.LightObstaclesDistance);

                var lightFormat = _lighting.HDR && SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat,
                    GraphicsFormatUsage.Render)
                    ? GraphicsFormat.R16G16B16A16_SFloat
                    : GraphicsFormat.R8G8B8A8_UNorm;
                // HDR composition keys off the ACTUAL light-RT format, not the requested HDR bool: on a
                // platform without FP16 render targets the RTs fall back to LDR, so the overlay must too.
                bool hdrActive = lightFormat == GraphicsFormat.R16G16B16A16_SFloat;
                int aa = _lighting.LightObstaclesAntialiasing ? 2 : 1;
                TextureHandle obstaclesAA = CreateTexture(renderGraph, sizes.Extended.x * aa, sizes.Extended.y * aa,
                    lightFormat, _lighting.LightTexturesFilterMode, "Light2D Obstacles AA");
                context.UpdateBorderMesh(sizes.Extended.x * aa, sizes.Extended.y * aa);
                TextureHandle obstacles = CreateTexture(renderGraph, sizes.Extended.x, sizes.Extended.y, lightFormat,
                    _lighting.LightTexturesFilterMode, "Light2D Obstacles");
                int lightWidth = _lighting.EnableNormalMapping ? sourceDescriptor.width : sizes.Small.x;
                int lightHeight = _lighting.EnableNormalMapping ? sourceDescriptor.height : sizes.Small.y;
                TextureHandle sources = CreateTexture(renderGraph, lightWidth, lightHeight, lightFormat,
                    _lighting.EnableNormalMapping ? FilterMode.Point : _lighting.LightTexturesFilterMode,
                    "Light2D Sources");
                Matrix4x4 extendedView = extendedCamera.View;
                Matrix4x4 extendedProjection = extendedCamera.Projection;
                Matrix4x4 cameraView = cameraData.GetViewMatrix();
                Matrix4x4 cameraProjection = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(), true);

                Light2DCameraGeometry smallCamera = Light2DCameraGeometry.Calculate(camera, lightPosition,
                    sizes.Small, sizes.Extended, false, _lighting.LightCameraFovAdd, _lighting.LightObstaclesDistance);
                Matrix4x4 smallView = smallCamera.View;
                Matrix4x4 smallProjection = smallCamera.Projection;

                // Registry snapshots replace the layer-culled RendererLists. Obstacles and ambient cull against
                // the extended light-camera frustum; sources cull against the same extended frustum the old
                // expanded CullingResults used. Owner scoping (Light2DCullContext.Owner) replaces layer scoping.
                // All snapshots are captured here on the main thread into member lists; the raster render funcs
                // only read them via Light2DDraw.Draw and never touch live component state.
                GeometryUtility.CalculateFrustumPlanes(extendedProjection * extendedView, _extendedPlanes);
                GeometryUtility.CalculateFrustumPlanes(smallProjection * smallView, _smallPlanes);
                var extendedContext = new Light2DCullContext(_extendedPlanes, _lighting);
                Light2DRenderRegistry.PrepareObstacleSnapshot(_obstacleSnapshot, extendedContext);
                Light2DRenderRegistry.PrepareCoverageSnapshot(_coverageSnapshot, extendedContext);
                LightSprite.PrepareSnapshot(_lightSprites, _lighting.XZPlane, _lighting, _extendedPlanes);
                BuildSourceEntries(_lightSprites, _sourceEntries);

                int obstaclePass = ResolvePassForSnapshot(_obstacleSnapshot, ForwardLightMode);
                // Values-only source of truth: the LightingSystem-wide obstacle multiplier is pushed onto the
                // obstacle draws through a property block every frame (default white = a byte-identical no-op).
                // Scoping it to this pass's block keeps the ambient emitter - which shares the dual-color obstacle
                // material - and every other role on the material's default white, with no cross-pass global state.
                if (_obstacleProperties == null)
                    _obstacleProperties = new MaterialPropertyBlock();
                _obstacleProperties.SetColor(ObstacleGlobalMul, _lighting.ObstacleColor);
                AddDrawPass(renderGraph, "Light2D Obstacles", Light2DProfiling.Obstacles, obstaclesAA,
                    _obstacleSnapshot, obstaclePass, extendedView, extendedProjection, cameraView, cameraProjection,
                    new Color(1, 1, 1, 0), context, false, default(TextureHandle), default(TextureHandle), black,
                    _obstacleProperties);
                AddBorderPass(renderGraph, obstaclesAA, context);
                AddBlitPass(renderGraph, "Light2D Resolve Obstacles", Light2DProfiling.Obstacles, obstaclesAA,
                    obstacles, null, black, sizes.Extended.x * aa, sizes.Extended.y * aa);

                // Obstacle-preview copy (Workstream B2). Gated entirely on the LightingSystem's self-expiring
                // request flag so that with no editor preview window open nothing is allocated, imported, or
                // drawn - the frame is byte-identical to before. When requested, copy the resolved obstacle
                // buffer (extended size, light format) into the system's persistent preview RTHandle, which the
                // editor window reads via InternalsVisibleTo. URP-only; Built-in reads ObstaclesTexture directly.
                if (_lighting.ObstaclePreviewRequested)
                {
                    var previewDescriptor = sourceDescriptor;
                    previewDescriptor.width = sizes.Extended.x;
                    previewDescriptor.height = sizes.Extended.y;
                    previewDescriptor.graphicsFormat = lightFormat;
                    var previewTarget = _lighting.EnsureObstaclePreviewTarget(previewDescriptor,
                        _lighting.LightTexturesFilterMode);
                    if (previewTarget != null)
                    {
                        TextureHandle previewImported = renderGraph.ImportTexture(previewTarget);
                        AddObstaclePreviewPass(renderGraph, obstacles, previewImported);
                    }
                }

                TextureHandle normals = default(TextureHandle);
                if (_lighting.EnableNormalMapping)
                {
                    normals = CreateTexture(renderGraph, sourceDescriptor.width, sourceDescriptor.height,
                        GraphicsFormat.R8G8B8A8_UNorm, FilterMode.Point, "Light2D Normals");
                    // Normals draw the obstacle geometry with the Light2DNormals pass. Reuse the obstacle
                    // snapshot (culled by the extended frustum, a superset of the small frustum the old separate
                    // normal cull used); obstacles outside the small viewport clip harmlessly under smallView.
                    int normalPass = ResolvePassForSnapshot(_obstacleSnapshot, NormalsLightMode);
                    AddDrawPass(renderGraph, "Light2D Normals", Light2DProfiling.Normals, normals, _obstacleSnapshot,
                        normalPass, smallView, smallProjection, cameraView, cameraProjection,
                        new Color(0.5f, 0.5f, 0, 1), context, false, default(TextureHandle), default(TextureHandle),
                        black);
                }

                int sourcePass = ResolvePassForSnapshot(_sourceEntries, ForwardLightMode);
                AddDrawPass(renderGraph, "Light2D Sources", Light2DProfiling.Sources, sources, _sourceEntries,
                    sourcePass, smallView, smallProjection, cameraView, cameraProjection, Color.clear, context, true,
                    obstacles, normals, black);
                if (_lighting.EnableNormalMapping)
                {
                    foreach (var lightSprite in _lightSprites)
                    {
                        if (lightSprite == null || lightSprite.Sprite == null ||
                            !TryBuildLightEntry(lightSprite, out var lightEntry) ||
                            !GeometryUtility.TestPlanesAABB(_smallPlanes, lightEntry.WorldBounds))
                            continue;
                        TextureHandle singleLight = CreateTexture(renderGraph, lightWidth, lightHeight, lightFormat,
                            FilterMode.Point, "Light2D Single Normal-Mapped Light");
                        int singleLightPass = Light2DDraw.ResolvePass(lightEntry.Material, ForwardLightMode);
                        AddSingleLightPass(renderGraph, context, lightEntry, singleLightPass, singleLight, obstacles,
                            smallView, smallProjection, cameraView, cameraProjection, sizes, black);
                        AddNormalLightPass(renderGraph, context, lightSprite, singleLight, normals, sources,
                            smallCamera, black);
                    }
                }

                TextureHandle finalSources = sources;
                if (_lighting.BlurLightSources && context.SourceBlurMaterial != null)
                {
                    // Values-only source of truth: the blur distance is applied unconditionally each frame
                    // (mirrors LightingSystem's Built-in blur push).
                    context.SourceBlurMaterial.SetFloat(Distance, _lighting.BlurDistance);
                    int blurWidth = _lighting.EnableNormalMapping ? lightWidth : lightWidth * 2;
                    int blurHeight = _lighting.EnableNormalMapping ? lightHeight : lightHeight * 2;
                    finalSources = CreateTexture(renderGraph, blurWidth, blurHeight, lightFormat, FilterMode.Bilinear,
                        "Light2D Blurred Sources");
                    AddBlitPass(renderGraph, "Light2D Blur Sources", Light2DProfiling.Blur, sources, finalSources,
                        context.SourceBlurMaterial, black, lightWidth, lightHeight);
                }

                int ambientCycles = 0;
                int historyReallocations = 0;
                TextureHandle ambient = renderGraph.defaultResources.blackTexture;
                if (_lighting.EnableAmbientLight)
                {
                    var historyDescriptor = sourceDescriptor;
                    historyDescriptor.width = sizes.Extended.x;
                    historyDescriptor.height = sizes.Extended.y;
                    historyDescriptor.graphicsFormat = lightFormat;
                    using (Light2DProfiling.ResourceReconciliation.Auto())
                    {
                        // Both ambient history handles are reallocated together on a descriptor change.
                        if (context.EnsureHistory(historyDescriptor, _lighting.LightTexturesFilterMode,
                                _lighting.AmbientWarmupCycles))
                            historyReallocations = 2;
                    }
                    TextureHandle ambientEmission = CreateTexture(renderGraph, sizes.Extended.x, sizes.Extended.y,
                        lightFormat, _lighting.LightTexturesFilterMode, "Light2D Ambient Emission");
                    Light2DRenderRegistry.PrepareAmbientSnapshot(_ambientSnapshot, extendedContext);
                    int ambientPass = ResolvePassForSnapshot(_ambientSnapshot, ForwardLightMode);
                    AddDrawPass(renderGraph, "Light2D Ambient Emission", Light2DProfiling.Ambient, ambientEmission,
                        _ambientSnapshot, ambientPass, extendedView, extendedProjection, cameraView, cameraProjection,
                        Color.clear, context, false, default(TextureHandle), default(TextureHandle), black);
                    context.ResetHistoryForUnsupportedTransform(camera, _lighting.AmbientWarmupCycles);
                    ambient = RecordAmbient(renderGraph, context, obstacles, ambientEmission, sizes, black,
                        extendedCamera.Position, extendedCamera.OrthographicSize, out ambientCycles);
                }
                TextureHandle cameraSource = resourceData.activeColorTexture;
                TextureDesc outputDesc = renderGraph.GetTextureDesc(cameraSource);
                outputDesc.name = "Light2D Camera Color";
                outputDesc.clearBuffer = false;
                outputDesc.msaaSamples = MSAASamples.None;
                TextureHandle output = renderGraph.CreateTexture(outputDesc);
                AddOverlayPass(renderGraph, context, camera, cameraSource, output, ambient, finalSources, sizes,
                    smallCamera, black, hdrActive);

                // Coverage blends the raw camera color back in where volumes (or a sub-1 DefaultCoverage) mask
                // Light2D out: final = lerp(rawGame, composite, coverage). Structural mirror of the Built-in
                // coverage flow (§5.2) — URP behavioral/pixel correctness is BLOCKED on this host, not claimed.
                // With no coverage present the branch is skipped entirely so cameraColor = output, byte-identical
                // to today's path (backward compat).
                float defaultCoverage = _lighting.DefaultCoverage;
                bool coverageActive = _coverageSnapshot.Count > 0 || defaultCoverage < 1f;
                if (coverageActive)
                {
                    // Coverage mask: extended-size, light-format target cleared to DefaultCoverage everywhere,
                    // then volumes paint their per-region coverage over it. Identical shape to the ambient
                    // emission draw pass (extended view/projection, SRPDefaultUnlit-resolved pass).
                    TextureHandle coverage = CreateTexture(renderGraph, sizes.Extended.x, sizes.Extended.y,
                        lightFormat, _lighting.LightTexturesFilterMode, "Light2D Coverage");
                    int coveragePass = ResolvePassForSnapshot(_coverageSnapshot, ForwardLightMode);
                    AddDrawPass(renderGraph, "Light2D Coverage Mask", Light2DProfiling.Composition, coverage,
                        _coverageSnapshot, coveragePass, extendedView, extendedProjection, cameraView,
                        cameraProjection,
                        new Color(defaultCoverage, defaultCoverage, defaultCoverage, defaultCoverage), context,
                        false, default(TextureHandle), default(TextureHandle), black);

                    TextureDesc coverageOutputDesc = outputDesc;
                    coverageOutputDesc.name = "Light2D Coverage Color";
                    TextureHandle coverageOutput = renderGraph.CreateTexture(coverageOutputDesc);
                    AddCoverageBlendPass(renderGraph, context, camera, output, cameraSource, coverage,
                        coverageOutput, sizes, smallCamera, black);
                    resourceData.cameraColor = coverageOutput;
                }
                else
                {
                    resourceData.cameraColor = output;
                }

                var metrics = new Light2DFrameMetrics(sizes.Extended.x, sizes.Extended.y,
                    Light2DDiagnostics.OwnedTextureBytes, _lightSprites.Count, ambientCycles,
                    historyReallocations, Light2DDiagnostics.ContextCount);
                Light2DDiagnostics.ReportFrame(in metrics);
            }

            private TextureHandle RecordAmbient(RenderGraph renderGraph, CameraContext context,
                TextureHandle obstacles, TextureHandle emission, Sizes sizes, TextureHandle black,
                Vector3 lightPosition, float extendedOrthographicSize, out int cyclesRun)
            {
                cyclesRun = 0;
                TextureHandle ambientA = renderGraph.ImportTexture(context.AmbientA);
                TextureHandle ambientB = renderGraph.ImportTexture(context.AmbientB);
                if (context.HistoryNeedsClear)
                {
                    AddClearPass(renderGraph, ambientA);
                    AddClearPass(renderGraph, ambientB);
                    context.HistoryNeedsClear = false;
                }
                TextureHandle read = context.AmbientReadIsA ? ambientA : ambientB;
                if (context.AmbientMaterial == null)
                    return read;

                // Values-only source of truth: the colored-bounce tint plus every ambient "feel" knob are
                // pushed onto the cloned compute material unconditionally each frame, matching the Built-in
                // RenderAmbientLight path (AmbientColoredBounce default 0 is the shader's neutral no-op).
                context.AmbientMaterial.SetFloat(AmbientBounceTint, _lighting.AmbientColoredBounce);
                context.AmbientMaterial.SetFloat(ObstacleMul, _lighting.AmbientObstacleMul);
                context.AmbientMaterial.SetFloat(ObstacleAdd, _lighting.AmbientObstacleAdd);
                context.AmbientMaterial.SetFloat(EmissionColorMul, _lighting.AmbientEmissionMul);
                context.AmbientMaterial.SetFloat(SamplingDist, _lighting.AmbientSamplingDistance);
                context.AmbientMaterial.SetFloat(AmbientAdaptSpeed, _lighting.AmbientAdaptSpeed);
                // Ambient-blur distance, applied unconditionally once per frame before the propagation loop
                // (mirrors the Built-in ambient blur push). The loop reuses AmbientBlurMaterial each cycle.
                if (context.AmbientBlurMaterial != null)
                    context.AmbientBlurMaterial.SetFloat(Distance, _lighting.BlurDistance);

                context.WarmupCycles = AddWithoutOverflow(context.WarmupCycles,
                    _lighting.ConsumePendingAmbientLightCycles());
                int maximumCycles = Mathf.Max(1, _lighting.MaxAmbientCyclesPerFrame);
                // Base propagation iterations come from the exposed AmbientIterations (default 1 = the original
                // single-step behavior), clamped to the per-frame maximum; queued warmup cycles fill whatever
                // budget remains. At AmbientIterations=1 this is identical to the previous 1 + warmup formula.
                int baseIterations = Mathf.Clamp(_lighting.AmbientIterations, 1, maximumCycles);
                int warmupConsumed = Mathf.Min(context.WarmupCycles, maximumCycles - baseIterations);
                int cycles = baseIterations + warmupConsumed;
                cyclesRun = cycles;
                context.WarmupCycles = Mathf.Max(0, context.WarmupCycles - warmupConsumed);
                Vector3 current = lightPosition;
                Vector2 shift = Vector2.zero;
                if (context.HasPreviousPosition)
                {
                    Vector3 delta = current - context.PreviousLightPosition;
                    Vector2 planarDelta = new Vector2(delta.x, _lighting.XZPlane ? delta.z : delta.y);
                    // Teleport guard: a single-frame jump beyond one extended-view height cannot be
                    // reprojected without smearing the history across the screen. Hold it in place this frame
                    // (zero shift) and clear + re-warm instead of hard-scrolling the history to the border.
                    if (planarDelta.magnitude > 2f * extendedOrthographicSize)
                    {
                        context.HistoryNeedsClear = true;
                        context.WarmupCycles = Mathf.Max(context.WarmupCycles, _lighting.AmbientWarmupCycles);
                    }
                    else
                    {
                        shift = planarDelta / Mathf.Max(_lighting.LightPixelSize, 0.00001f);
                        shift = Vector2.Scale(shift, new Vector2(1f / sizes.Extended.x, 1f / sizes.Extended.y));
                    }
                }
                context.PreviousLightPosition = current;
                context.HasPreviousPosition = true;

                for (int i = 0; i < cycles; i++)
                {
                    bool writeA = context.AmbientReadIsA;
                    TextureHandle write = writeA ? ambientB : ambientA;
                    AddAmbientPass(renderGraph, context, read, write, obstacles, emission,
                        i == 0 ? shift : Vector2.zero, black);
                    read = write;
                    context.AmbientReadIsA = !context.AmbientReadIsA;

                    if (_lighting.BlurAmbientLight && context.AmbientBlurMaterial != null)
                    {
                        writeA = context.AmbientReadIsA;
                        write = writeA ? ambientB : ambientA;
                        AddBlitPass(renderGraph, "Light2D Blur Ambient", Light2DProfiling.Blur, read, write,
                            context.AmbientBlurMaterial, black, sizes.Extended.x, sizes.Extended.y, false);
                        read = write;
                        context.AmbientReadIsA = !context.AmbientReadIsA;
                    }
                }
                return read;
            }

            private void AddDrawPass(RenderGraph graph, string name, ProfilerMarker marker, TextureHandle target,
                List<Light2DDrawEntry> snapshot, int shaderPass, Matrix4x4 view, Matrix4x4 projection,
                Matrix4x4 cameraView, Matrix4x4 cameraProjection, Color clear,
                CameraContext context, bool setLightState, TextureHandle obstacles, TextureHandle normals,
                TextureHandle black, MaterialPropertyBlock properties = null)
            {
                // Unsafe pass (native command buffer) rather than a raster pass: the register-and-draw geometry
                // is issued with DrawMesh through a buffer that behaves like the Built-in CommandBuffer, so it
                // mirrors the proven Built-in path -- SetGlobalTexture reaches the shader and the buffer applies
                // the render-texture GPU projection adjustment itself (hence the raw projection below, exactly
                // like LightingSystem.cs:801). SetRenderTarget is issued manually (unsafe passes bind no
                // attachment automatically). The dual-pipeline shaders themselves select their URP SubShader via
                // the RenderPipeline tag / SubShader ordering (see Light1.shader and LightOverlay.shader).
                using (var builder = graph.AddUnsafePass<DrawPassData>(name, out var data, profilingSampler))
                {
                    data.Marker = marker;
                    data.Target = target;
                    data.Snapshot = snapshot;
                    data.ShaderPass = shaderPass;
                    data.View = view;
                    // Raw projection: the unsafe pass renders through a native command buffer, which applies the
                    // render-texture GPU adjustment itself (exactly like the Built-in path at LightingSystem.cs:801).
                    // Pre-applying GL.GetGPUProjectionMatrix(..., true) here would double-adjust on flipped-UV
                    // platforms (D3D) and push the light out of the clip volume -> zero fragments -> black light.
                    data.Projection = projection;
                    data.Clear = clear;
                    data.CameraView = cameraView;
                    data.CameraProjection = cameraProjection;
                    data.SetLightState = setLightState;
                    data.Obstacles = obstacles;
                    data.Normals = normals;
                    data.Black = black;
                    data.Properties = properties;
                    data.XZ = context.Lighting.XZPlane;
                    data.Perspective = !context.Lighting.SourceCamera.orthographic;
                    data.PixelsPerMeter = context.Lighting.LightPixelsPerUnityMeter;
                    Vector2Int small = context.Lighting.SmallLightTextureSize;
                    Vector2Int extended = context.Lighting.ExtendedLightTextureSize;
                    if (small.x <= 0 || extended.x <= 0)
                    {
                        Sizes calculated = CalculateSizes(context.Lighting, context.Lighting.SourceCamera);
                        small = calculated.Small;
                        extended = calculated.Extended;
                    }
                    data.ExtendedScale = new Vector2(small.x / (float)extended.x, small.y / (float)extended.y);
                    data.PositionOffset = new Vector2(0.5f / extended.x, 0.5f / extended.y);
                    builder.UseTexture(target, AccessFlags.Write);
                    if (obstacles.IsValid())
                        builder.UseTexture(obstacles, AccessFlags.Read);
                    if (normals.IsValid())
                        builder.UseTexture(normals, AccessFlags.Read);
                    builder.UseTexture(black, AccessFlags.Read);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (DrawPassData pass, UnsafeGraphContext rg) =>
                    {
                        var cmd = rg.cmd;
                        pass.Marker.Begin();
                        cmd.SetRenderTarget(pass.Target);
                        cmd.ClearRenderTarget(RTClearFlags.Color, pass.Clear, 1, 0);
                        cmd.SetViewProjectionMatrices(pass.View, pass.Projection);
                        if (pass.SetLightState)
                        {
                            cmd.SetGlobalTexture(UrpObstacleTex, pass.Obstacles);
                            if (pass.Normals.IsValid())
                                cmd.SetGlobalTexture(UrpNormalsBuffer, pass.Normals);
                            cmd.SetGlobalFloat(PixelsPerBlock, pass.PixelsPerMeter);
                            cmd.SetGlobalVector(ExtendedScale, pass.ExtendedScale);
                            cmd.SetGlobalVector(PositionOffset, pass.PositionOffset);
                            SetKeywords(cmd, pass.XZ, pass.Perspective);
                        }
                        var snap = pass.Snapshot;
                        var props = pass.Properties;
                        for (int i = 0; i < snap.Count; i++)
                        {
                            var entry = snap[i];
                            if (entry.Mesh == null || entry.Material == null)
                                continue;
                            int submeshCount = entry.SubmeshCount > 0 ? entry.SubmeshCount : 1;
                            for (int s = 0; s < submeshCount; s++)
                            {
                                if (props != null)
                                    cmd.DrawMesh(entry.Mesh, entry.Matrix, entry.Material, s, pass.ShaderPass, props);
                                else
                                    cmd.DrawMesh(entry.Mesh, entry.Matrix, entry.Material, s, pass.ShaderPass);
                            }
                        }
                        if (pass.SetLightState)
                        {
                            ClearKeywords(cmd);
                            ClearTextureGlobals(cmd, pass.Black);
                        }
                        cmd.SetViewProjectionMatrices(pass.CameraView, pass.CameraProjection);
                        pass.Marker.End();
                    });
                }
            }

            private static void AddBorderPass(RenderGraph graph, TextureHandle target, CameraContext context)
            {
                using (var builder = graph.AddRasterRenderPass<BorderPassData>("Light2D Obstacle Border", out var data))
                {
                    data.Mesh = context.BorderMesh;
                    data.Material = context.BorderMaterial;
                    builder.SetRenderAttachment(target, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (BorderPassData pass, RasterGraphContext rg) =>
                    {
                        Light2DProfiling.Obstacles.Begin();
                        rg.cmd.DrawMesh(pass.Mesh, Matrix4x4.identity, pass.Material, 0, 0);
                        Light2DProfiling.Obstacles.End();
                    });
                }
            }

            private static void AddBlitPass(RenderGraph graph, string name, ProfilerMarker marker,
                TextureHandle source, TextureHandle destination, Material material, TextureHandle black,
                int sourceWidth, int sourceHeight, bool allowCulling = true)
            {
                using (var builder = graph.AddRasterRenderPass<BlitPassData>(name, out var data))
                {
                    data.Marker = marker;
                    data.Source = source;
                    data.Material = material;
                    data.SourceTexelSize = TexelSize(sourceWidth, sourceHeight);
                    data.Black = black;
                    builder.UseTexture(source, AccessFlags.Read);
                    builder.UseTexture(black, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.AllowPassCulling(allowCulling);
                    builder.SetRenderFunc(static (BlitPassData pass, RasterGraphContext rg) =>
                    {
                        pass.Marker.Begin();
                        if (pass.Material == null)
                        {
                            Blitter.BlitTexture(rg.cmd, pass.Source, Vector2.one, 0, false);
                            pass.Marker.End();
                            return;
                        }
                        rg.cmd.SetGlobalTexture(UrpMainTex, pass.Source);
                        rg.cmd.SetGlobalVector(MainTexTexelSize, pass.SourceTexelSize);
                        Blitter.BlitTexture(rg.cmd, pass.Source, Vector2.one, pass.Material, 0);
                        ClearTextureGlobals(rg.cmd, pass.Black);
                        pass.Marker.End();
                    });
                }
            }

            // Obstacle-preview copy (Workstream B2): copies the resolved obstacle transmission buffer into the
            // LightingSystem's imported preview RTHandle so the editor preview window can show what the light
            // system sees. Modeled on AddBlitPass's null-material branch. Only added when the preview is
            // requested (see RecordRenderGraph), so it is zero-cost whenever no preview window is open. Culling
            // is disabled because the output is consumed on the editor side, not by a downstream graph pass.
            // The URP copy pass itself is not batchmode-verifiable on this host (no runtime pipeline activates).
            private static void AddObstaclePreviewPass(RenderGraph graph, TextureHandle obstacles,
                TextureHandle preview)
            {
                using (var builder = graph.AddRasterRenderPass<ObstaclePreviewPassData>(
                           "Light2D Obstacle Preview Copy", out var data))
                {
                    data.Source = obstacles;
                    builder.UseTexture(obstacles, AccessFlags.Read);
                    builder.SetRenderAttachment(preview, 0, AccessFlags.Write);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (ObstaclePreviewPassData pass, RasterGraphContext rg) =>
                    {
                        Light2DProfiling.Obstacles.Begin();
                        Blitter.BlitTexture(rg.cmd, pass.Source, Vector2.one, 0, false);
                        Light2DProfiling.Obstacles.End();
                    });
                }
            }

            private static void AddClearPass(RenderGraph graph, TextureHandle target)
            {
                using (var builder = graph.AddRasterRenderPass<ClearPassData>("Light2D Clear Ambient History",
                           out var data))
                {
                    builder.SetRenderAttachment(target, 0, AccessFlags.Write);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (ClearPassData pass, RasterGraphContext rg) =>
                    {
                        Light2DProfiling.Ambient.Begin();
                        rg.cmd.ClearRenderTarget(RTClearFlags.Color, Color.clear, 1, 0);
                        Light2DProfiling.Ambient.End();
                    });
                }
            }

            private static void AddAmbientPass(RenderGraph graph, CameraContext context, TextureHandle source,
                TextureHandle destination, TextureHandle obstacles, TextureHandle emission, Vector2 shift,
                TextureHandle black)
            {
                using (var builder = graph.AddRasterRenderPass<AmbientPassData>("Light2D Propagate Ambient", out var data))
                {
                    data.Source = source;
                    data.Obstacles = obstacles;
                    data.Emission = emission;
                    data.Destination = destination;
                    data.Material = context.AmbientMaterial;
                    data.Shift = shift;
                    data.Black = black;
                    builder.UseTexture(source, AccessFlags.Read);
                    builder.UseTexture(obstacles, AccessFlags.Read);
                    builder.UseTexture(emission, AccessFlags.Read);
                    builder.UseTexture(black, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (AmbientPassData pass, RasterGraphContext rg) =>
                    {
                        Light2DProfiling.Ambient.Begin();
                        rg.cmd.SetGlobalTexture(UrpMainTex, pass.Source);
                        rg.cmd.SetGlobalTexture(UrpObstacleTex, pass.Obstacles);
                        rg.cmd.SetGlobalTexture(UrpLightSourcesTex, pass.Emission);
                        rg.cmd.SetGlobalVector(Shift, pass.Shift);
                        Blitter.BlitTexture(rg.cmd, pass.Source, Vector2.one, pass.Material, 0);
                        ClearTextureGlobals(rg.cmd, pass.Black);
                        Light2DProfiling.Ambient.End();
                    });
                }
            }

            private static void AddSingleLightPass(RenderGraph graph, CameraContext context, Light2DDrawEntry lightEntry,
                int shaderPass, TextureHandle target, TextureHandle obstacles, Matrix4x4 view, Matrix4x4 projection,
                Matrix4x4 cameraView, Matrix4x4 cameraProjection, Sizes sizes, TextureHandle black)
            {
                using (var builder = graph.AddRasterRenderPass<SingleLightPassData>("Light2D Draw Normal-Mapped Light",
                           out var data))
                {
                    data.Mesh = lightEntry.Mesh;
                    data.Material = lightEntry.Material;
                    data.Matrix = lightEntry.Matrix;
                    data.SubmeshCount = Mathf.Max(1, lightEntry.SubmeshCount);
                    data.ShaderPass = shaderPass;
                    data.Target = target;
                    data.Obstacles = obstacles;
                    data.View = view;
                    data.Projection = GL.GetGPUProjectionMatrix(projection, true);
                    data.CameraView = cameraView;
                    data.CameraProjection = cameraProjection;
                    data.XZ = context.Lighting.XZPlane;
                    data.PixelsPerMeter = context.Lighting.LightPixelsPerUnityMeter;
                    data.ExtendedScale = new Vector2(sizes.Small.x / (float)sizes.Extended.x,
                        sizes.Small.y / (float)sizes.Extended.y);
                    data.PositionOffset = new Vector2(0.5f / sizes.Extended.x, 0.5f / sizes.Extended.y);
                    data.Black = black;
                    builder.UseTexture(obstacles, AccessFlags.Read);
                    builder.UseTexture(black, AccessFlags.Read);
                    builder.SetRenderAttachment(target, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (SingleLightPassData pass, RasterGraphContext rg) =>
                    {
                        Light2DProfiling.Sources.Begin();
                        rg.cmd.ClearRenderTarget(RTClearFlags.Color, Color.clear, 1, 0);
                        rg.cmd.SetViewProjectionMatrices(pass.View, pass.Projection);
                        rg.cmd.SetGlobalTexture(UrpObstacleTex, pass.Obstacles);
                        rg.cmd.SetGlobalFloat(PixelsPerBlock, pass.PixelsPerMeter);
                        rg.cmd.SetGlobalVector(ExtendedScale, pass.ExtendedScale);
                        rg.cmd.SetGlobalVector(PositionOffset, pass.PositionOffset);
                        SetKeywords(rg.cmd, pass.XZ, false);
                        for (int s = 0; s < pass.SubmeshCount; s++)
                            rg.cmd.DrawMesh(pass.Mesh, pass.Matrix, pass.Material, s, pass.ShaderPass);
                        ClearKeywords(rg.cmd);
                        ClearTextureGlobals(rg.cmd, pass.Black);
                        rg.cmd.SetViewProjectionMatrices(pass.CameraView, pass.CameraProjection);
                        Light2DProfiling.Sources.End();
                    });
                }
            }

            private static void AddNormalLightPass(RenderGraph graph, CameraContext context, LightSprite light,
                TextureHandle singleLight, TextureHandle normals, TextureHandle target,
                Light2DCameraGeometry smallCamera, TextureHandle black)
            {
                Vector2 size = light.Sprite.bounds.size;
                Vector3 center = light.transform.localToWorldMatrix.MultiplyPoint3x4(
                    Vector2.Scale(light.LightOrigin, size));
                Vector4 clipPosition = smallCamera.Projection * smallCamera.View *
                                       new Vector4(center.x, center.y, 0, 1);
                Vector2 viewportPosition = (Vector2)clipPosition / clipPosition.w * 0.5f + Vector2.one * 0.5f;
                var lightPosition = new Vector3(viewportPosition.x, viewportPosition.y, light.NormalMapHeight);
                Matrix4x4 cameraToWorld = smallCamera.View.inverse;
                Vector3 viewportToWorldX = cameraToWorld.MultiplyVector(Vector3.right *
                    (2f * smallCamera.OrthographicSize * smallCamera.Aspect));
                Vector3 viewportToWorldY = cameraToWorld.MultiplyVector(Vector3.up *
                    (2f * smallCamera.OrthographicSize));
                using (var builder = graph.AddRasterRenderPass<NormalLightPassData>("Light2D Apply Normal-Mapped Light",
                           out var data))
                {
                    data.SingleLight = singleLight;
                    data.Normals = normals;
                    data.Material = context.NormalLightMaterial;
                    data.LightPosition = lightPosition;
                    data.ViewportToWorldX = new Vector2(viewportToWorldX.x, viewportToWorldX.y);
                    data.ViewportToWorldY = new Vector2(viewportToWorldY.x, viewportToWorldY.y);
                    data.Black = black;
                    builder.UseTexture(singleLight, AccessFlags.Read);
                    builder.UseTexture(normals, AccessFlags.Read);
                    builder.UseTexture(black, AccessFlags.Read);
                    builder.SetRenderAttachment(target, 0, AccessFlags.ReadWrite);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (NormalLightPassData pass, RasterGraphContext rg) =>
                    {
                        Light2DProfiling.Normals.Begin();
                        rg.cmd.SetGlobalTexture(UrpMainTex, pass.SingleLight);
                        rg.cmd.SetGlobalTexture(UrpNormalsBuffer, pass.Normals);
                        rg.cmd.SetGlobalVector(LightPosition, pass.LightPosition);
                        rg.cmd.SetGlobalVector(ViewportToWorldX, pass.ViewportToWorldX);
                        rg.cmd.SetGlobalVector(ViewportToWorldY, pass.ViewportToWorldY);
                        Blitter.BlitTexture(rg.cmd, pass.SingleLight, Vector2.one, pass.Material, 0);
                        ClearTextureGlobals(rg.cmd, pass.Black);
                        Light2DProfiling.Normals.End();
                    });
                }
            }

            private void AddOverlayPass(RenderGraph graph, CameraContext context, Camera camera,
                TextureHandle source, TextureHandle destination, TextureHandle ambient, TextureHandle sources,
                Sizes sizes, Light2DCameraGeometry lightCamera, TextureHandle black, bool hdrActive)
            {
                using (var builder = graph.AddUnsafePass<OverlayPassData>("Light2D Composite", out var data,
                           profilingSampler))
                {
                    data.Destination = destination;
                    data.Source = source;
                    data.Ambient = ambient;
                    data.Sources = sources;
                    data.Material = context.OverlayMaterial;
                    // Values-only source of truth: every overlay "look" knob is pushed onto the cloned overlay
                    // material unconditionally each frame (mirrors LightingSystem's Built-in overlay push).
                    context.OverlayMaterial.SetFloat(LightMul, _lighting.LightBrightness);
                    context.OverlayMaterial.SetFloat(LightSourcesMul, _lighting.LightSourcesBrightness);
                    context.OverlayMaterial.SetFloat(AdditiveLightPow, _lighting.BloomPower);
                    context.OverlayMaterial.SetFloat(AdditiveLightAdd, _lighting.BloomAdd);
                    data.Hdr = hdrActive;
                    data.ExtendedScale = new Vector2(sizes.Small.x / (float)sizes.Extended.x,
                        sizes.Small.y / (float)sizes.Extended.y);
                    float sourceSize = camera.orthographic
                        ? camera.orthographicSize
                        : Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * _lighting.LightObstaclesDistance;
                    float scaleY = sourceSize / Mathf.Max(0.00001f, lightCamera.OrthographicSize);
                    data.Scale = new Vector2(scaleY * camera.aspect / lightCamera.Aspect, scaleY);
                    Vector3 worldOffset = Quaternion.Inverse(camera.transform.rotation) *
                                          (lightCamera.Position - camera.transform.position);
                    data.Offset = Vector2.Scale(new Vector2(1f / sizes.Small.x, 1f / sizes.Small.y),
                        -(Vector2)worldOffset * _lighting.LightPixelsPerUnityMeter);
                    data.Black = black;
                    builder.UseTexture(source, AccessFlags.Read);
                    builder.UseTexture(ambient, AccessFlags.Read);
                    builder.UseTexture(sources, AccessFlags.Read);
                    builder.UseTexture(black, AccessFlags.Read);
                    builder.UseTexture(destination, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    // Unsafe pass so SetGlobalTexture reaches the composite shader through a native command buffer,
                    // mirroring Built-in's Graphics.Blit path. The overlay shader selects its URP SubShader because
                    // its Built-in SubShader carries a non-UniversalPipeline RenderPipeline tag (see LightOverlay.shader).
                    // Blitter needs the native command buffer.
                    builder.SetRenderFunc(static (OverlayPassData pass, UnsafeGraphContext rg) =>
                    {
                        var cmd = rg.cmd;
                        Light2DProfiling.Composition.Begin();
                        cmd.SetRenderTarget(pass.Destination);
                        cmd.SetGlobalTexture(UrpGameTex, pass.Source);
                        cmd.SetGlobalTexture(UrpAmbientLightTex, pass.Ambient);
                        cmd.SetGlobalTexture(UrpLightSourcesTex, pass.Sources);
                        cmd.SetGlobalVector(ExtendedScale, pass.ExtendedScale);
                        cmd.SetGlobalVector(Scale, pass.Scale);
                        cmd.SetGlobalVector(Offset, pass.Offset);
                        cmd.SetKeyword(HdrKeyword, pass.Hdr);
                        Blitter.BlitTexture(CommandBufferHelpers.GetNativeCommandBuffer(cmd), pass.Source,
                            Vector2.one, pass.Material, 0);
                        cmd.SetKeyword(HdrKeyword, false);
                        ClearTextureGlobals(cmd, pass.Black);
                        Light2DProfiling.Composition.End();
                    });
                }
            }

            // Coverage blend: final = lerp(rawGame, composite, coverage). Runs after AddOverlayPass only when
            // coverage is active. The _Scale/_Offset/_ExtendedToSmallTextureScale globals reproduce the overlay's
            // small-light -> extended UV mapping (§1.4) so the coverage RT samples aligned with the composite.
            // The coverage-blend shader (Light2D/Coverage Blend) samples _Light2DURPGameTex/_Light2DURPCompositeTex/
            // _Light2DURPCoverageTex declared in the self-contained CoverageBlend.hlsl.
            private void AddCoverageBlendPass(RenderGraph graph, CameraContext context, Camera camera,
                TextureHandle composite, TextureHandle game, TextureHandle coverage, TextureHandle destination,
                Sizes sizes, Light2DCameraGeometry lightCamera, TextureHandle black)
            {
                using (var builder = graph.AddRasterRenderPass<CoveragePassData>("Light2D Coverage Blend", out var data,
                           profilingSampler))
                {
                    data.Composite = composite;
                    data.Game = game;
                    data.Coverage = coverage;
                    data.Material = context.CoverageBlendMaterial;
                    data.ExtendedScale = new Vector2(sizes.Small.x / (float)sizes.Extended.x,
                        sizes.Small.y / (float)sizes.Extended.y);
                    // Scale/Offset mirror AddOverlayPass exactly so coverageUV == the overlay's ambientUV mapping.
                    float sourceSize = camera.orthographic
                        ? camera.orthographicSize
                        : Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * _lighting.LightObstaclesDistance;
                    float scaleY = sourceSize / Mathf.Max(0.00001f, lightCamera.OrthographicSize);
                    data.Scale = new Vector2(scaleY * camera.aspect / lightCamera.Aspect, scaleY);
                    Vector3 worldOffset = Quaternion.Inverse(camera.transform.rotation) *
                                          (lightCamera.Position - camera.transform.position);
                    data.Offset = Vector2.Scale(new Vector2(1f / sizes.Small.x, 1f / sizes.Small.y),
                        -(Vector2)worldOffset * _lighting.LightPixelsPerUnityMeter);
                    data.Black = black;
                    builder.UseTexture(composite, AccessFlags.Read);
                    builder.UseTexture(game, AccessFlags.Read);
                    builder.UseTexture(coverage, AccessFlags.Read);
                    builder.UseTexture(black, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (CoveragePassData pass, RasterGraphContext rg) =>
                    {
                        Light2DProfiling.Composition.Begin();
                        rg.cmd.SetGlobalTexture(UrpCompositeTex, pass.Composite);
                        rg.cmd.SetGlobalTexture(UrpGameTex, pass.Game);
                        rg.cmd.SetGlobalTexture(UrpCoverageTex, pass.Coverage);
                        rg.cmd.SetGlobalVector(ExtendedScale, pass.ExtendedScale);
                        rg.cmd.SetGlobalVector(Scale, pass.Scale);
                        rg.cmd.SetGlobalVector(Offset, pass.Offset);
                        Blitter.BlitTexture(rg.cmd, pass.Composite, Vector2.one, pass.Material, 0);
                        rg.cmd.SetGlobalTexture(UrpCompositeTex, pass.Black);
                        rg.cmd.SetGlobalTexture(UrpCoverageTex, pass.Black);
                        ClearTextureGlobals(rg.cmd, pass.Black);
                        Light2DProfiling.Composition.End();
                    });
                }
            }

            // Resolves the DrawMesh pass index for a snapshot from a representative material. Obstacle, source
            // and ambient materials within one role share a shader family, so the first entry's pass layout
            // applies to the whole snapshot. Returns 0 for an empty snapshot (no draw happens anyway).
            private static int ResolvePassForSnapshot(List<Light2DDrawEntry> snapshot, string lightMode)
            {
                return snapshot.Count > 0 ? Light2DDraw.ResolvePass(snapshot[0].Material, lightMode) : 0;
            }

            // Converts the LightSprite snapshot into draw entries the shared Light2DDraw routine can consume.
            private static void BuildSourceEntries(List<LightSprite> lights, List<Light2DDrawEntry> dst)
            {
                dst.Clear();
                for (var i = 0; i < lights.Count; i++)
                {
                    if (TryBuildLightEntry(lights[i], out var entry))
                        dst.Add(entry);
                }
            }

            // Builds a draw entry from a light's owned mesh + generated material + world matrix. The mesh,
            // material, and world bounds come from the LightSprite's register-and-draw accessors: there is no
            // owned Renderer/MeshFilter at all (the no-renderer invariant that replaced layer exclusion).
            private static bool TryBuildLightEntry(LightSprite light, out Light2DDrawEntry entry)
            {
                entry = default;
                if (light == null)
                    return false;
                var mesh = light.Mesh;
                var material = light.DrawMaterial;
                if (mesh == null || material == null)
                    return false;
                entry = new Light2DDrawEntry(mesh, material, light.transform.localToWorldMatrix, light.WorldBounds,
                    Mathf.Max(1, mesh.subMeshCount), light.SortingOrder, null);
                return true;
            }

            private static TextureHandle CreateTexture(RenderGraph graph, int width, int height,
                GraphicsFormat format, FilterMode filter, string name)
            {
                var desc = new TextureDesc(Mathf.Max(1, width), Mathf.Max(1, height))
                {
                    colorFormat = format,
                    depthBufferBits = DepthBits.None,
                    msaaSamples = MSAASamples.None,
                    clearBuffer = false,
                    filterMode = filter,
                    wrapMode = TextureWrapMode.Clamp,
                    name = name
                };
                return graph.CreateTexture(desc);
            }

            private static bool TryValidateFrame(LightingSystem lighting, Camera camera,
                RenderTextureDescriptor descriptor, out Sizes sizes, out string reason)
            {
                sizes = default(Sizes);
                if (camera == null || lighting.SourceCamera != camera)
                {
                    reason = "Light2D must reference the camera currently being rendered.";
                    return false;
                }
                LightingSizeEstimate estimate;
                if (!TryCalculateSizes(lighting, camera, out estimate, out reason))
                    return false;
                if (descriptor.width <= 0 || descriptor.height <= 0 || descriptor.dimension != TextureDimension.Tex2D ||
                    descriptor.graphicsFormat == GraphicsFormat.None ||
                    !SystemInfo.IsFormatSupported(descriptor.graphicsFormat, GraphicsFormatUsage.Render))
                {
                    reason = "Light2D received an unsupported camera target descriptor.";
                    return false;
                }

                sizes = new Sizes(estimate.Small, estimate.Extended);
                int maximumSize = SystemInfo.maxTextureSize;
                int aa = lighting.LightObstaclesAntialiasing ? 2 : 1;
                long extendedWidth = (long)sizes.Extended.x * aa;
                long extendedHeight = (long)sizes.Extended.y * aa;
                if (extendedWidth > maximumSize || extendedHeight > maximumSize ||
                    descriptor.width > maximumSize || descriptor.height > maximumSize ||
                    extendedWidth * extendedHeight > lighting.MaxLightTexturePixels ||
                    (long)descriptor.width * descriptor.height > lighting.MaxLightTexturePixels)
                {
                    reason = "Light2D requested textures exceed the GPU size limit or the package pixel budget.";
                    return false;
                }

                reason = null;
                return true;
            }

            private static Vector4 TexelSize(int width, int height)
            {
                return new Vector4(1f / width, 1f / height, width, height);
            }

            private static Sizes CalculateSizes(LightingSystem lighting, Camera camera)
            {
                LightingSizeEstimate estimate;
                string error;
                if (TryCalculateSizes(lighting, camera, out estimate, out error))
                    return new Sizes(estimate.Small, estimate.Extended);
                return new Sizes(new Vector2Int(4, 4), new Vector2Int(4, 4));
            }

            /// <summary>
            /// Single forwarding point for the validator's size estimate so its argument list cannot
            /// drift between frame validation and fallback size calculation.
            /// </summary>
            private static bool TryCalculateSizes(LightingSystem lighting, Camera camera,
                out LightingSizeEstimate estimate, out string error)
            {
                return LightingSettingsValidator.TryCalculateSizes(camera, lighting.LightPixelSize,
                    lighting.LightCameraSizeAdd, lighting.LightCameraFovAdd, lighting.LightObstaclesDistance,
                    lighting.HDR, lighting.EnableAmbientLight, lighting.LightObstaclesAntialiasing,
                    lighting.EnableNormalMapping, out estimate, out error);
            }

            private static int AddWithoutOverflow(int left, int right)
            {
                return left > int.MaxValue - right ? int.MaxValue : left + right;
            }

            private static void SetKeywords(IBaseCommandBuffer cmd, bool xz, bool perspective)
            {
                cmd.SetKeyword(XzKeyword, xz);
                cmd.SetKeyword(XyKeyword, !xz);
                cmd.SetKeyword(PerspectiveKeyword, perspective);
                cmd.SetKeyword(OrthographicKeyword, !perspective);
            }

            private static void ClearKeywords(IBaseCommandBuffer cmd)
            {
                cmd.SetKeyword(XzKeyword, false);
                cmd.SetKeyword(XyKeyword, false);
                cmd.SetKeyword(PerspectiveKeyword, false);
                cmd.SetKeyword(OrthographicKeyword, false);
            }

            private static void ClearTextureGlobals(IBaseCommandBuffer cmd, TextureHandle black)
            {
                cmd.SetGlobalTexture(UrpObstacleTex, black);
                cmd.SetGlobalTexture(UrpMainTex, black);
                cmd.SetGlobalTexture(UrpGameTex, black);
                cmd.SetGlobalTexture(UrpAmbientLightTex, black);
                cmd.SetGlobalTexture(UrpLightSourcesTex, black);
                cmd.SetGlobalTexture(UrpNormalsBuffer, black);
            }

            private readonly struct Sizes
            {
                internal readonly Vector2Int Small;
                internal readonly Vector2Int Extended;

                internal Sizes(Vector2Int small, Vector2Int extended)
                {
                    Small = small;
                    Extended = extended;
                }
            }

            private sealed class DrawPassData
            {
                internal ProfilerMarker Marker;
                internal TextureHandle Target;
                internal List<Light2DDrawEntry> Snapshot;
                internal int ShaderPass;
                internal Matrix4x4 View;
                internal Matrix4x4 Projection;
                internal Matrix4x4 CameraView;
                internal Matrix4x4 CameraProjection;
                internal Color Clear;
                internal bool SetLightState;
                internal bool XZ;
                internal bool Perspective;
                internal float PixelsPerMeter;
                internal Vector2 ExtendedScale;
                internal Vector2 PositionOffset;
                internal TextureHandle Obstacles;
                internal TextureHandle Normals;
                internal TextureHandle Black;
                // Optional per-draw property block; non-null only for the obstacle pass, where it carries the
                // LightingSystem-wide obstacle multiplier (_Light2DObstacleGlobalMul).
                internal MaterialPropertyBlock Properties;
            }

            private sealed class BorderPassData
            {
                internal Mesh Mesh;
                internal Material Material;
            }

            private sealed class BlitPassData
            {
                internal ProfilerMarker Marker;
                internal TextureHandle Source;
                internal Material Material;
                internal Vector4 SourceTexelSize;
                internal TextureHandle Black;
            }

            private sealed class ObstaclePreviewPassData
            {
                internal TextureHandle Source;
            }

            private sealed class ClearPassData
            {
            }

            private sealed class AmbientPassData
            {
                internal TextureHandle Source;
                internal TextureHandle Destination;
                internal TextureHandle Obstacles;
                internal TextureHandle Emission;
                internal Material Material;
                internal Vector2 Shift;
                internal TextureHandle Black;
            }

            private sealed class SingleLightPassData
            {
                internal Mesh Mesh;
                internal Matrix4x4 Matrix;
                internal int SubmeshCount;
                internal int ShaderPass;
                internal Material Material;
                internal TextureHandle Target;
                internal TextureHandle Obstacles;
                internal Matrix4x4 View;
                internal Matrix4x4 Projection;
                internal Matrix4x4 CameraView;
                internal Matrix4x4 CameraProjection;
                internal bool XZ;
                internal float PixelsPerMeter;
                internal Vector2 ExtendedScale;
                internal Vector2 PositionOffset;
                internal TextureHandle Black;
            }

            private sealed class NormalLightPassData
            {
                internal TextureHandle SingleLight;
                internal TextureHandle Normals;
                internal Material Material;
                internal Vector3 LightPosition;
                internal Vector2 ViewportToWorldX;
                internal Vector2 ViewportToWorldY;
                internal TextureHandle Black;
            }

            private sealed class OverlayPassData
            {
                internal TextureHandle Destination;
                internal TextureHandle Source;
                internal TextureHandle Ambient;
                internal TextureHandle Sources;
                internal Material Material;
                internal bool Hdr;
                internal Vector2 ExtendedScale;
                internal Vector2 Scale;
                internal Vector2 Offset;
                internal TextureHandle Black;
            }

            private sealed class CoveragePassData
            {
                internal TextureHandle Composite;
                internal TextureHandle Game;
                internal TextureHandle Coverage;
                internal Material Material;
                internal Vector2 ExtendedScale;
                internal Vector2 Scale;
                internal Vector2 Offset;
                internal TextureHandle Black;
            }
        }
    }
}
