using System;
using System.Collections.Generic;
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

            [Min(0)]
            [Tooltip("Extra ambient propagation steps when a camera context is created or resized.")]
            public int AmbientWarmupCycles = 8;

            [Min(1)]
            [Tooltip("Maximum ambient propagation steps recorded in one frame.")]
            public int MaximumAmbientCyclesPerFrame = 16;
        }

        [SerializeField] private FeatureSettings _settings = new FeatureSettings();

        private readonly Dictionary<LightingSystem, CameraContext> _contexts =
            new Dictionary<LightingSystem, CameraContext>();
        private readonly List<LightingSystem> _deadContexts = new List<LightingSystem>();
        private Light2DPass _pass;

        public override void Create()
        {
            DisposeContexts();
            _pass = new Light2DPass(_contexts, _settings);
            _pass.renderPassEvent = _settings.RenderPassEvent;
            _pass.requiresIntermediateTexture = true;
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

            _pass.Setup(lighting);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            DisposeContexts();
            _pass = null;
        }

        private void CleanupDeadContexts()
        {
            _deadContexts.Clear();
            foreach (var pair in _contexts)
            {
                if (pair.Key == null || !pair.Key.isActiveAndEnabled)
                    _deadContexts.Add(pair.Key);
            }

            foreach (var lighting in _deadContexts)
            {
                CameraContext context;
                if (_contexts.TryGetValue(lighting, out context))
                    context.Dispose();
                _contexts.Remove(lighting);
            }
        }

        private void DisposeContexts()
        {
            foreach (var context in _contexts.Values)
                context.Dispose();
            _contexts.Clear();
            _deadContexts.Clear();
        }

        private sealed class CameraContext : IDisposable
        {
            internal readonly LightingSystem Lighting;
            internal Material AmbientMaterial;
            internal Material OverlayMaterial;
            internal Material SourceBlurMaterial;
            internal Material AmbientBlurMaterial;
            internal Material NormalLightMaterial;
            internal Mesh BorderMesh;
            internal Material BorderMaterial;
            internal RTHandle AmbientA;
            internal RTHandle AmbientB;
            internal bool AmbientReadIsA = true;
            internal bool HistoryNeedsClear;
            internal int WarmupCycles;
            internal int DescriptorHash;
            internal Vector3 PreviousLightPosition;
            internal bool HasPreviousPosition;
            internal bool ValidationErrorIssued;

            internal CameraContext(LightingSystem lighting)
            {
                Lighting = lighting;
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

                CloneMaterial(ref AmbientMaterial, Lighting.AmbientLightComputeMaterial, "Ambient");
                CloneMaterial(ref OverlayMaterial, Lighting.LightOverlayMaterial, "Overlay");
                CloneMaterial(ref SourceBlurMaterial, Lighting.LightSourcesBlurMaterial, "Source Blur");
                CloneMaterial(ref AmbientBlurMaterial, Lighting.AmbientLightBlurMaterial, "Ambient Blur");

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
                }

                ValidationErrorIssued = false;
                return true;
            }

            internal void EnsureHistory(RenderTextureDescriptor descriptor, FilterMode filterMode, int warmupCycles)
            {
                descriptor.depthBufferBits = 0;
                descriptor.msaaSamples = 1;
                descriptor.useMipMap = false;
                descriptor.autoGenerateMips = false;
                int hash = DescriptorHashOf(descriptor, filterMode);
                bool changed = hash != DescriptorHash || AmbientA == null || AmbientB == null;
                RenderingUtils.ReAllocateHandleIfNeeded(ref AmbientA, descriptor, filterMode, TextureWrapMode.Clamp,
                    name: "Light2D Ambient History A");
                RenderingUtils.ReAllocateHandleIfNeeded(ref AmbientB, descriptor, filterMode, TextureWrapMode.Clamp,
                    name: "Light2D Ambient History B");
                if (!changed)
                    return;

                DescriptorHash = hash;
                AmbientReadIsA = true;
                HistoryNeedsClear = true;
                HasPreviousPosition = false;
                WarmupCycles = Mathf.Max(WarmupCycles, warmupCycles);
            }

            internal void UpdateBorderMesh(int width, int height)
            {
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
                Destroy(ref AmbientMaterial);
                Destroy(ref OverlayMaterial);
                Destroy(ref SourceBlurMaterial);
                Destroy(ref AmbientBlurMaterial);
                Destroy(ref NormalLightMaterial);
                Destroy(ref BorderMaterial);
                if (BorderMesh != null)
                    CoreUtils.Destroy(BorderMesh);
                BorderMesh = null;
                AmbientA?.Release();
                AmbientB?.Release();
                AmbientA = null;
                AmbientB = null;
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

            private static void CloneMaterial(ref Material clone, Material source, string name)
            {
                if (source == null)
                {
                    Destroy(ref clone);
                    return;
                }

                if (clone == null || clone.shader != source.shader)
                {
                    Destroy(ref clone);
                    clone = new Material(source) { hideFlags = HideFlags.DontSave };
                    clone.name = "Generated Light2D URP " + name;
                }
                else
                {
                    clone.CopyPropertiesFromMaterial(source);
                }
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
            private static readonly List<ShaderTagId> ForwardTags = new List<ShaderTagId>
            {
                new ShaderTagId("SRPDefaultUnlit"),
                new ShaderTagId("UniversalForward"),
                new ShaderTagId("UniversalForwardOnly")
            };
            private static readonly List<ShaderTagId> NormalTags = new List<ShaderTagId>
            {
                new ShaderTagId("Light2DNormals")
            };
            private static readonly int UrpObstacleTex = Shader.PropertyToID("_Light2DURPObstacleTex");
            private static readonly int UrpMainTex = Shader.PropertyToID("_Light2DURPMainTex");
            private static readonly int UrpGameTex = Shader.PropertyToID("_Light2DURPGameTex");
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

            private readonly Dictionary<LightingSystem, CameraContext> _contexts;
            private readonly FeatureSettings _settings;
            private readonly List<LightSprite> _lightSprites = new List<LightSprite>();
            private readonly List<Renderer> _temporarilyCulledLights = new List<Renderer>();
            private LightingSystem _lighting;

            internal Light2DPass(Dictionary<LightingSystem, CameraContext> contexts, FeatureSettings settings)
            {
                _contexts = contexts;
                _settings = settings;
                profilingSampler = new ProfilingSampler("Light2D URP");
            }

            internal void Setup(LightingSystem lighting)
            {
                _lighting = lighting;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var cameraData = frameData.Get<UniversalCameraData>();
                var resourceData = frameData.Get<UniversalResourceData>();
                if (_lighting == null || cameraData.camera != _lighting.SourceCamera ||
                    resourceData.isActiveTargetBackBuffer)
                    return;

                CameraContext context;
                if (!_contexts.TryGetValue(_lighting, out context))
                {
                    context = new CameraContext(_lighting);
                    _contexts.Add(_lighting, context);
                }
                if (!context.ValidateAndUpdate(_settings.AmbientWarmupCycles))
                    return;

                var camera = cameraData.camera;
                LightSprite.PrepareSnapshot(_lightSprites, _lighting.XZPlane);
                int lightSourcesLayer = _lighting.LightSourcesLayer;
                _lightSprites.RemoveAll(light => light == null || light.gameObject.layer != lightSourcesLayer);
                var sizes = CalculateSizes(_lighting, camera);
                Vector3 lightPosition = CalculateSnappedPosition(_lighting, camera);
                LightCameraGeometry extendedCamera = CalculateLightCameraGeometry(_lighting, camera, sizes, true,
                    lightPosition);

                var sourceDescriptor = cameraData.cameraTargetDescriptor;
                var lightFormat = _lighting.HDR && SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat,
                    GraphicsFormatUsage.Render)
                    ? GraphicsFormat.R16G16B16A16_SFloat
                    : GraphicsFormat.R8G8B8A8_UNorm;
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
                if (_lighting.EnableNormalMapping)
                    HideLightSpritesFromCulling();
                CullingResults expandedCull;
                try
                {
                    expandedCull = Cull(frameData, camera, extendedProjection * extendedView,
                        extendedCamera.Position, LayerMaskFor(_lighting));
                }
                finally
                {
                    if (_lighting.EnableNormalMapping)
                        RestoreLightSpriteCulling();
                }

                RendererListHandle obstaclesList = CreateRendererList(renderGraph, frameData, expandedCull,
                    1 << _lighting.LightObstaclesLayer);
                AddDrawPass(renderGraph, "Light2D Obstacles", obstaclesAA, obstaclesList, extendedView,
                    extendedProjection, new Color(1, 1, 1, 0), context, false, default(TextureHandle),
                    default(TextureHandle));
                AddBorderPass(renderGraph, obstaclesAA, context);
                AddBlitPass(renderGraph, "Light2D Resolve Obstacles", obstaclesAA, obstacles, null);

                LightCameraGeometry smallCamera = CalculateLightCameraGeometry(_lighting, camera, sizes, false,
                    lightPosition);
                Matrix4x4 smallView = smallCamera.View;
                Matrix4x4 smallProjection = smallCamera.Projection;
                RendererListHandle sourcesList = CreateRendererList(renderGraph, frameData, expandedCull,
                    1 << _lighting.LightSourcesLayer);

                TextureHandle normals = default(TextureHandle);
                if (_lighting.EnableNormalMapping)
                {
                    normals = CreateTexture(renderGraph, sourceDescriptor.width, sourceDescriptor.height,
                        GraphicsFormat.R8G8B8A8_UNorm, FilterMode.Point, "Light2D Normals");
                    CullingResults normalCull = Cull(frameData, camera,
                        smallProjection * smallView, smallCamera.Position,
                        1 << _lighting.LightObstaclesLayer);
                    RendererListHandle normalsList = CreateRendererList(renderGraph, frameData, normalCull,
                        1 << _lighting.LightObstaclesLayer, NormalTags);
                    AddDrawPass(renderGraph, "Light2D Normals", normals, normalsList, smallView, smallProjection,
                        new Color(0.5f, 0.5f, 0, 1), context, false, default(TextureHandle), default(TextureHandle));
                }

                AddDrawPass(renderGraph, "Light2D Sources", sources, sourcesList, smallView, smallProjection,
                    Color.clear, context, true, obstacles, normals);
                if (_lighting.EnableNormalMapping)
                {
                    Plane[] planes = GeometryUtility.CalculateFrustumPlanes(smallProjection * smallView);
                    foreach (var lightSprite in _lightSprites)
                    {
                        if (lightSprite == null || lightSprite.Renderer == null || !lightSprite.Renderer.enabled ||
                            lightSprite.Renderer.sharedMaterial == null || lightSprite.Sprite == null ||
                            !GeometryUtility.TestPlanesAABB(planes, lightSprite.Renderer.bounds))
                            continue;
                        TextureHandle singleLight = CreateTexture(renderGraph, lightWidth, lightHeight, lightFormat,
                            FilterMode.Point, "Light2D Single Normal-Mapped Light");
                        AddSingleLightPass(renderGraph, context, lightSprite, singleLight, obstacles,
                            smallView, smallProjection, sizes);
                        AddNormalLightPass(renderGraph, context, lightSprite, singleLight, normals, sources,
                            smallCamera);
                    }
                }

                TextureHandle finalSources = sources;
                if (_lighting.BlurLightSources && context.SourceBlurMaterial != null)
                {
                    int blurWidth = _lighting.EnableNormalMapping ? lightWidth : lightWidth * 2;
                    int blurHeight = _lighting.EnableNormalMapping ? lightHeight : lightHeight * 2;
                    finalSources = CreateTexture(renderGraph, blurWidth, blurHeight, lightFormat, FilterMode.Bilinear,
                        "Light2D Blurred Sources");
                    AddBlitPass(renderGraph, "Light2D Blur Sources", sources, finalSources,
                        context.SourceBlurMaterial);
                }

                TextureHandle ambient = renderGraph.defaultResources.blackTexture;
                if (_lighting.EnableAmbientLight)
                {
                    var historyDescriptor = sourceDescriptor;
                    historyDescriptor.width = sizes.Extended.x;
                    historyDescriptor.height = sizes.Extended.y;
                    historyDescriptor.graphicsFormat = lightFormat;
                    context.EnsureHistory(historyDescriptor, _lighting.LightTexturesFilterMode,
                        _settings.AmbientWarmupCycles);
                    TextureHandle ambientEmission = CreateTexture(renderGraph, sizes.Extended.x, sizes.Extended.y,
                        lightFormat, _lighting.LightTexturesFilterMode, "Light2D Ambient Emission");
                    RendererListHandle ambientList = CreateRendererList(renderGraph, frameData, expandedCull,
                        1 << _lighting.AmbientLightLayer);
                    AddDrawPass(renderGraph, "Light2D Ambient Emission", ambientEmission, ambientList, extendedView,
                        extendedProjection, Color.clear, context, false, default(TextureHandle),
                        default(TextureHandle));
                    ambient = RecordAmbient(renderGraph, context, obstacles, ambientEmission, sizes,
                        extendedCamera.Position);
                }
                TextureHandle cameraSource = resourceData.activeColorTexture;
                TextureDesc outputDesc = renderGraph.GetTextureDesc(cameraSource);
                outputDesc.name = "Light2D Camera Color";
                outputDesc.clearBuffer = false;
                outputDesc.msaaSamples = MSAASamples.None;
                TextureHandle output = renderGraph.CreateTexture(outputDesc);
                AddOverlayPass(renderGraph, context, camera, cameraSource, output, ambient, finalSources, sizes,
                    smallCamera);
                resourceData.cameraColor = output;
            }

            private TextureHandle RecordAmbient(RenderGraph renderGraph, CameraContext context,
                TextureHandle obstacles, TextureHandle emission, Sizes sizes, Vector3 lightPosition)
            {
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

                context.WarmupCycles = AddWithoutOverflow(context.WarmupCycles,
                    _lighting.ConsumePendingAmbientLightCycles());
                int maximumCycles = Mathf.Max(1, _settings.MaximumAmbientCyclesPerFrame);
                int cycles = 1 + Mathf.Min(context.WarmupCycles, maximumCycles - 1);
                context.WarmupCycles = Mathf.Max(0, context.WarmupCycles - (cycles - 1));
                Vector3 current = lightPosition;
                Vector2 shift = Vector2.zero;
                if (context.HasPreviousPosition)
                {
                    Vector3 delta = current - context.PreviousLightPosition;
                    shift = new Vector2(delta.x, _lighting.XZPlane ? delta.z : delta.y) /
                            Mathf.Max(_lighting.LightPixelSize, 0.00001f);
                    shift = Vector2.Scale(shift, new Vector2(1f / sizes.Extended.x, 1f / sizes.Extended.y));
                }
                context.PreviousLightPosition = current;
                context.HasPreviousPosition = true;

                for (int i = 0; i < cycles; i++)
                {
                    bool writeA = context.AmbientReadIsA;
                    RTHandle writeHandle = writeA ? context.AmbientB : context.AmbientA;
                    TextureHandle write = renderGraph.ImportTexture(writeHandle);
                    AddAmbientPass(renderGraph, context, read, write, obstacles, emission, i == 0 ? shift : Vector2.zero);
                    read = write;
                    context.AmbientReadIsA = !context.AmbientReadIsA;

                    if (_lighting.BlurAmbientLight && context.AmbientBlurMaterial != null)
                    {
                        writeA = context.AmbientReadIsA;
                        writeHandle = writeA ? context.AmbientB : context.AmbientA;
                        write = renderGraph.ImportTexture(writeHandle);
                        AddBlitPass(renderGraph, "Light2D Blur Ambient", read, write, context.AmbientBlurMaterial);
                        read = write;
                        context.AmbientReadIsA = !context.AmbientReadIsA;
                    }
                }
                return read;
            }

            private void AddDrawPass(RenderGraph graph, string name, TextureHandle target,
                RendererListHandle rendererList, Matrix4x4 view, Matrix4x4 projection, Color clear,
                CameraContext context, bool setLightState, TextureHandle obstacles, TextureHandle normals)
            {
                using (var builder = graph.AddRasterRenderPass<DrawPassData>(name, out var data, profilingSampler))
                {
                    data.RendererList = rendererList;
                    data.View = view;
                    data.Projection = projection;
                    data.Clear = clear;
                    data.CameraView = context.Lighting.SourceCamera.worldToCameraMatrix;
                    data.CameraProjection = context.Lighting.SourceCamera.projectionMatrix;
                    data.SetLightState = setLightState;
                    data.Obstacles = obstacles;
                    data.Normals = normals;
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
                    builder.UseRendererList(rendererList);
                    builder.SetRenderAttachment(target, 0, AccessFlags.Write);
                    if (obstacles.IsValid())
                        builder.UseTexture(obstacles, AccessFlags.Read);
                    if (normals.IsValid())
                        builder.UseTexture(normals, AccessFlags.Read);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (DrawPassData pass, RasterGraphContext rg) =>
                    {
                        rg.cmd.ClearRenderTarget(RTClearFlags.Color, pass.Clear, 1, 0);
                        rg.cmd.SetViewProjectionMatrices(pass.View, pass.Projection);
                        if (pass.SetLightState)
                        {
                            rg.cmd.SetGlobalTexture(UrpObstacleTex, pass.Obstacles);
                            if (pass.Normals.IsValid())
                                rg.cmd.SetGlobalTexture(UrpNormalsBuffer, pass.Normals);
                            rg.cmd.SetGlobalFloat(PixelsPerBlock, pass.PixelsPerMeter);
                            rg.cmd.SetGlobalVector(ExtendedScale, pass.ExtendedScale);
                            rg.cmd.SetGlobalVector(PositionOffset, pass.PositionOffset);
                            SetKeywords(rg.cmd, pass.XZ, pass.Perspective);
                        }
                        rg.cmd.DrawRendererList(pass.RendererList);
                        if (pass.SetLightState)
                            ClearKeywords(rg.cmd);
                        rg.cmd.SetViewProjectionMatrices(pass.CameraView, pass.CameraProjection);
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
                        rg.cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                        rg.cmd.DrawMesh(pass.Mesh, Matrix4x4.identity, pass.Material, 0, 0);
                    });
                }
            }

            private static void AddBlitPass(RenderGraph graph, string name, TextureHandle source,
                TextureHandle destination, Material material)
            {
                using (var builder = graph.AddRasterRenderPass<BlitPassData>(name, out var data))
                {
                    data.Source = source;
                    data.Material = material;
                    builder.UseTexture(source, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (BlitPassData pass, RasterGraphContext rg) =>
                    {
                        if (pass.Material == null)
                        {
                            Blitter.BlitTexture(rg.cmd, pass.Source, Vector2.one, 0, false);
                            return;
                        }
                        rg.cmd.SetGlobalTexture(UrpMainTex, pass.Source);
                        rg.cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                        Blitter.BlitTexture(rg.cmd, pass.Source, Vector2.one, pass.Material, 0);
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
                        rg.cmd.ClearRenderTarget(RTClearFlags.Color, Color.clear, 1, 0));
                }
            }

            private static void AddAmbientPass(RenderGraph graph, CameraContext context, TextureHandle source,
                TextureHandle destination, TextureHandle obstacles, TextureHandle emission, Vector2 shift)
            {
                using (var builder = graph.AddRasterRenderPass<AmbientPassData>("Light2D Propagate Ambient", out var data))
                {
                    data.Source = source;
                    data.Obstacles = obstacles;
                    data.Emission = emission;
                    data.Destination = destination;
                    data.Material = context.AmbientMaterial;
                    data.Shift = shift;
                    builder.UseTexture(source, AccessFlags.Read);
                    builder.UseTexture(obstacles, AccessFlags.Read);
                    builder.UseTexture(emission, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (AmbientPassData pass, RasterGraphContext rg) =>
                    {
                        rg.cmd.SetGlobalTexture(UrpMainTex, pass.Source);
                        rg.cmd.SetGlobalTexture(UrpObstacleTex, pass.Obstacles);
                        rg.cmd.SetGlobalTexture(UrpLightSourcesTex, pass.Emission);
                        rg.cmd.SetGlobalVector(Shift, pass.Shift);
                        rg.cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                        Blitter.BlitTexture(rg.cmd, pass.Source, Vector2.one, pass.Material, 0);
                    });
                }
            }

            private static void AddSingleLightPass(RenderGraph graph, CameraContext context, LightSprite light,
                TextureHandle target, TextureHandle obstacles, Matrix4x4 view, Matrix4x4 projection, Sizes sizes)
            {
                using (var builder = graph.AddRasterRenderPass<SingleLightPassData>("Light2D Draw Normal-Mapped Light",
                           out var data))
                {
                    data.Renderer = light.Renderer;
                    data.Material = light.Renderer.sharedMaterial;
                    data.Target = target;
                    data.Obstacles = obstacles;
                    data.View = view;
                    data.Projection = projection;
                    data.XZ = context.Lighting.XZPlane;
                    data.PixelsPerMeter = context.Lighting.LightPixelsPerUnityMeter;
                    data.ExtendedScale = new Vector2(sizes.Small.x / (float)sizes.Extended.x,
                        sizes.Small.y / (float)sizes.Extended.y);
                    data.PositionOffset = new Vector2(0.5f / sizes.Extended.x, 0.5f / sizes.Extended.y);
                    builder.UseTexture(obstacles, AccessFlags.Read);
                    builder.SetRenderAttachment(target, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (SingleLightPassData pass, RasterGraphContext rg) =>
                    {
                        rg.cmd.ClearRenderTarget(RTClearFlags.Color, Color.clear, 1, 0);
                        rg.cmd.SetViewProjectionMatrices(pass.View, pass.Projection);
                        rg.cmd.SetGlobalTexture(UrpObstacleTex, pass.Obstacles);
                        rg.cmd.SetGlobalFloat(PixelsPerBlock, pass.PixelsPerMeter);
                        rg.cmd.SetGlobalVector(ExtendedScale, pass.ExtendedScale);
                        rg.cmd.SetGlobalVector(PositionOffset, pass.PositionOffset);
                        SetKeywords(rg.cmd, pass.XZ, false);
                        rg.cmd.DrawRenderer(pass.Renderer, pass.Material, 0, 0);
                        ClearKeywords(rg.cmd);
                    });
                }
            }

            private static void AddNormalLightPass(RenderGraph graph, CameraContext context, LightSprite light,
                TextureHandle singleLight, TextureHandle normals, TextureHandle target,
                LightCameraGeometry smallCamera)
            {
                Vector2 size = light.Sprite.bounds.size;
                Vector3 center = light.transform.localToWorldMatrix.MultiplyPoint3x4(
                    ((Vector2)light.LightOrigin).Mul(size));
                Vector4 clipPosition = smallCamera.Projection * smallCamera.View *
                                       new Vector4(center.x, center.y, 0, 1);
                Vector2 viewportPosition = (Vector2)clipPosition / clipPosition.w * 0.5f + Vector2.one * 0.5f;
                var lightPosition = new Vector3(viewportPosition.x, viewportPosition.y, light.LightOrigin.z);
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
                    builder.UseTexture(singleLight, AccessFlags.Read);
                    builder.UseTexture(normals, AccessFlags.Read);
                    builder.SetRenderAttachment(target, 0, AccessFlags.ReadWrite);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (NormalLightPassData pass, RasterGraphContext rg) =>
                    {
                        rg.cmd.SetGlobalTexture(UrpMainTex, pass.SingleLight);
                        rg.cmd.SetGlobalTexture(UrpNormalsBuffer, pass.Normals);
                        rg.cmd.SetGlobalVector(LightPosition, pass.LightPosition);
                        rg.cmd.SetGlobalVector(ViewportToWorldX, pass.ViewportToWorldX);
                        rg.cmd.SetGlobalVector(ViewportToWorldY, pass.ViewportToWorldY);
                        rg.cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                        Blitter.BlitTexture(rg.cmd, pass.SingleLight, Vector2.one, pass.Material, 0);
                    });
                }
            }

            private void AddOverlayPass(RenderGraph graph, CameraContext context, Camera camera,
                TextureHandle source, TextureHandle destination, TextureHandle ambient, TextureHandle sources,
                Sizes sizes, LightCameraGeometry lightCamera)
            {
                using (var builder = graph.AddRasterRenderPass<OverlayPassData>("Light2D Composite", out var data,
                           profilingSampler))
                {
                    data.Source = source;
                    data.Ambient = ambient;
                    data.Sources = sources;
                    data.Material = context.OverlayMaterial;
                    data.Hdr = _lighting.HDR;
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
                    builder.UseTexture(source, AccessFlags.Read);
                    builder.UseTexture(ambient, AccessFlags.Read);
                    builder.UseTexture(sources, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (OverlayPassData pass, RasterGraphContext rg) =>
                    {
                        rg.cmd.SetGlobalTexture(UrpGameTex, pass.Source);
                        rg.cmd.SetGlobalTexture(UrpAmbientLightTex, pass.Ambient);
                        rg.cmd.SetGlobalTexture(UrpLightSourcesTex, pass.Sources);
                        rg.cmd.SetGlobalVector(ExtendedScale, pass.ExtendedScale);
                        rg.cmd.SetGlobalVector(Scale, pass.Scale);
                        rg.cmd.SetGlobalVector(Offset, pass.Offset);
                        rg.cmd.SetKeyword(new GlobalKeyword("HDR"), pass.Hdr);
                        rg.cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                        Blitter.BlitTexture(rg.cmd, pass.Source, Vector2.one, pass.Material, 0);
                        rg.cmd.SetKeyword(new GlobalKeyword("HDR"), false);
                    });
                }
            }

            private static CullingResults Cull(ContextContainer frameData, Camera camera, Matrix4x4 cullingMatrix,
                Vector3 origin, int layerMask)
            {
                ScriptableCullingParameters parameters;
                camera.TryGetCullingParameters(false, out parameters);
                parameters.cullingMatrix = cullingMatrix;
                parameters.origin = origin;
                parameters.cullingMask = (uint)layerMask;
                parameters.shadowDistance = 0;
                parameters.cullingOptions &= ~CullingOptions.OcclusionCull;
                return frameData.Get<CullContextData>().Cull(ref parameters);
            }

            private static RendererListHandle CreateRendererList(RenderGraph graph, ContextContainer frameData,
                CullingResults cullingResults, int layerMask, List<ShaderTagId> shaderTags = null)
            {
                var renderingData = frameData.Get<UniversalRenderingData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                var lightData = frameData.Get<UniversalLightData>();
                DrawingSettings drawing = RenderingUtils.CreateDrawingSettings(shaderTags ?? ForwardTags, renderingData,
                    cameraData, lightData, SortingCriteria.CommonTransparent);
                var filtering = new FilteringSettings(RenderQueueRange.all, layerMask);
                return graph.CreateRendererList(new RendererListParams(cullingResults, drawing, filtering));
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

            private static Sizes CalculateSizes(LightingSystem lighting, Camera camera)
            {
                float pixels = lighting.LightPixelsPerUnityMeter;
                Vector2Int extended;
                Vector2Int small;
                if (camera.orthographic)
                {
                    float height = (camera.orthographicSize + lighting.LightCameraSizeAdd) * 2f;
                    float width = (camera.orthographicSize * camera.aspect + lighting.LightCameraSizeAdd) * 2f;
                    extended = new Vector2Int(Mathf.RoundToInt(width * pixels), Mathf.RoundToInt(height * pixels));
                    int smallHeight = Mathf.RoundToInt(camera.orthographicSize * 2f * pixels);
                    small = new Vector2Int(Mathf.RoundToInt(smallHeight * camera.aspect), smallHeight);
                }
                else
                {
                    float height = Mathf.Tan((camera.fieldOfView + lighting.LightCameraFovAdd) * Mathf.Deg2Rad * 0.5f) *
                                   lighting.LightObstaclesDistance * 2f;
                    int extendedHeight = Mathf.RoundToInt(height * pixels);
                    extended = new Vector2Int(Mathf.RoundToInt(extendedHeight * camera.aspect), extendedHeight);
                    float smallHeightWorld = Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f) *
                                             lighting.LightObstaclesDistance * 2f;
                    int smallHeight = Mathf.RoundToInt(smallHeightWorld * pixels);
                    small = new Vector2Int(Mathf.RoundToInt(smallHeight * camera.aspect), smallHeight);
                }
                extended.x = Mathf.Max(4, extended.x + (extended.x & 1));
                extended.y = Mathf.Max(4, extended.y + (extended.y & 1));
                small.x = Mathf.Max(4, small.x);
                small.y = Mathf.Max(4, small.y);
                return new Sizes(small, extended);
            }

            private static LightCameraGeometry CalculateLightCameraGeometry(LightingSystem lighting, Camera camera,
                Sizes sizes, bool extended, Vector3 position)
            {
                float aspect = extended ? sizes.Extended.x / (float)sizes.Extended.y :
                    sizes.Small.x / (float)sizes.Small.y;
                float orthographicSize;
                float fieldOfView;
                if (extended)
                {
                    orthographicSize = camera.orthographicSize * sizes.Extended.y / sizes.Small.y;
                    fieldOfView = camera.fieldOfView + lighting.LightCameraFovAdd;
                }
                else
                {
                    orthographicSize = camera.orthographic ? camera.orthographicSize :
                        Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * lighting.LightObstaclesDistance;
                    fieldOfView = camera.fieldOfView;
                }

                Matrix4x4 view = camera.worldToCameraMatrix *
                                 Matrix4x4.Translate(camera.transform.position - position);
                Matrix4x4 projection = camera.orthographic
                    ? Matrix4x4.Ortho(-orthographicSize * aspect, orthographicSize * aspect,
                        -orthographicSize, orthographicSize, camera.nearClipPlane, camera.farClipPlane)
                    : Matrix4x4.Perspective(fieldOfView, aspect, camera.nearClipPlane, camera.farClipPlane);
                return new LightCameraGeometry(position, view, projection, orthographicSize, aspect);
            }

            private static Vector3 CalculateSnappedPosition(LightingSystem lighting, Camera camera)
            {
                float pixels = lighting.LightPixelsPerUnityMeter;
                Vector3 position = camera.transform.position;
                Quaternion rotation = camera.transform.rotation;
                Vector3 local = Quaternion.Inverse(rotation) * position;
                var snapped = new Vector2(Mathf.Round(local.x * pixels) / pixels,
                    Mathf.Round(local.y * pixels) / pixels);
                return rotation * (snapped - (Vector2)local) + position;
            }

            private static int AddWithoutOverflow(int left, int right)
            {
                return left > int.MaxValue - right ? int.MaxValue : left + right;
            }

            private static int LayerMaskFor(LightingSystem lighting)
            {
                return (1 << lighting.LightSourcesLayer) | (1 << lighting.LightObstaclesLayer) |
                       (1 << lighting.AmbientLightLayer);
            }

            private void HideLightSpritesFromCulling()
            {
                _temporarilyCulledLights.Clear();
                foreach (var light in _lightSprites)
                {
                    if (light == null || light.Renderer == null || light.Renderer.forceRenderingOff)
                        continue;
                    light.Renderer.forceRenderingOff = true;
                    _temporarilyCulledLights.Add(light.Renderer);
                }
            }

            private void RestoreLightSpriteCulling()
            {
                foreach (var renderer in _temporarilyCulledLights)
                {
                    if (renderer != null)
                        renderer.forceRenderingOff = false;
                }
                _temporarilyCulledLights.Clear();
            }

            private static void SetKeywords(RasterCommandBuffer cmd, bool xz, bool perspective)
            {
                cmd.SetKeyword(new GlobalKeyword("LIGHT2D_XZ_PLANE"), xz);
                cmd.SetKeyword(new GlobalKeyword("LIGHT2D_XY_PLANE"), !xz);
                cmd.SetKeyword(new GlobalKeyword("PERSPECTIVE_CAMERA"), perspective);
                cmd.SetKeyword(new GlobalKeyword("ORTHOGRAPHIC_CAMERA"), !perspective);
            }

            private static void ClearKeywords(RasterCommandBuffer cmd)
            {
                cmd.SetKeyword(new GlobalKeyword("LIGHT2D_XZ_PLANE"), false);
                cmd.SetKeyword(new GlobalKeyword("LIGHT2D_XY_PLANE"), false);
                cmd.SetKeyword(new GlobalKeyword("PERSPECTIVE_CAMERA"), false);
                cmd.SetKeyword(new GlobalKeyword("ORTHOGRAPHIC_CAMERA"), false);
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

            private readonly struct LightCameraGeometry
            {
                internal readonly Vector3 Position;
                internal readonly Matrix4x4 View;
                internal readonly Matrix4x4 Projection;
                internal readonly float OrthographicSize;
                internal readonly float Aspect;

                internal LightCameraGeometry(Vector3 position, Matrix4x4 view, Matrix4x4 projection,
                    float orthographicSize, float aspect)
                {
                    Position = position;
                    View = view;
                    Projection = projection;
                    OrthographicSize = orthographicSize;
                    Aspect = aspect;
                }
            }

            private sealed class DrawPassData
            {
                internal RendererListHandle RendererList;
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
            }

            private sealed class BorderPassData
            {
                internal Mesh Mesh;
                internal Material Material;
            }

            private sealed class BlitPassData
            {
                internal TextureHandle Source;
                internal Material Material;
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
            }

            private sealed class SingleLightPassData
            {
                internal Renderer Renderer;
                internal Material Material;
                internal TextureHandle Target;
                internal TextureHandle Obstacles;
                internal Matrix4x4 View;
                internal Matrix4x4 Projection;
                internal bool XZ;
                internal float PixelsPerMeter;
                internal Vector2 ExtendedScale;
                internal Vector2 PositionOffset;
            }

            private sealed class NormalLightPassData
            {
                internal TextureHandle SingleLight;
                internal TextureHandle Normals;
                internal Material Material;
                internal Vector3 LightPosition;
                internal Vector2 ViewportToWorldX;
                internal Vector2 ViewportToWorldY;
            }

            private sealed class OverlayPassData
            {
                internal TextureHandle Source;
                internal TextureHandle Ambient;
                internal TextureHandle Sources;
                internal Material Material;
                internal bool Hdr;
                internal Vector2 ExtendedScale;
                internal Vector2 Scale;
                internal Vector2 Offset;
            }
        }
    }
}
