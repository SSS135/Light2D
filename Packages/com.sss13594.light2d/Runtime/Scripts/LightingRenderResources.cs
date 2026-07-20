using System;
using UnityEngine;

namespace Light2D
{
    internal sealed class LightingRenderResources : IDisposable
    {
        private MaterialClone _ambientLightComputeClone;
        private MaterialClone _lightOverlayClone;
        private MaterialClone _lightSourcesBlurClone;
        private MaterialClone _ambientLightBlurClone;
        private Material _normalMappedLightMaterial;
        private Material _normalMapDrawerMaterial;
        private Material _alphaBlendedMaterial;
        private readonly int _diagnosticsGeneration;
        private bool _disposed;

        internal RenderTexture AmbientEmissionTexture;
        internal RenderTexture AmbientTexture;
        internal RenderTexture PreviousAmbientTexture;
        internal RenderTexture BlurredLightTexture;
        internal RenderTexture ObstaclesUpsampledTexture;
        internal RenderTexture LightSourcesTexture;
        internal RenderTexture ObstaclesTexture;
        internal RenderTexture NormalMapBuffer;
        internal RenderTexture SingleLightSourceTexture;
        internal RenderTexture CameraTargetTexture;

        internal Material AmbientLightComputeMaterial { get { return _ambientLightComputeClone.Clone; } }
        internal Material LightOverlayMaterial { get { return _lightOverlayClone.Clone; } }
        internal Material LightSourcesBlurMaterial { get { return _lightSourcesBlurClone.Clone; } }
        internal Material AmbientLightBlurMaterial { get { return _ambientLightBlurClone.Clone; } }
        internal Material NormalMappedLightMaterial { get { return _normalMappedLightMaterial; } }
        internal Material NormalMapDrawerMaterial { get { return _normalMapDrawerMaterial; } }
        internal Material AlphaBlendedMaterial { get { return _alphaBlendedMaterial; } }

        internal LightingRenderResources()
        {
            _diagnosticsGeneration = Light2DDiagnostics.RegisterContext();
        }

        internal void EnsureSerializedMaterials(Material ambientCompute, Material overlay, Material sourceBlur,
            Material ambientBlur)
        {
            EnsureClone(ref _ambientLightComputeClone, ambientCompute, "Ambient Light Compute");
            EnsureClone(ref _lightOverlayClone, overlay, "Light Overlay");
            EnsureClone(ref _lightSourcesBlurClone, sourceBlur, "Light Sources Blur");
            EnsureClone(ref _ambientLightBlurClone, ambientBlur, "Ambient Light Blur");
        }

        internal Material EnsureInternalMaterial(ref Material material, Shader shader, string name)
        {
            if (material == null && shader != null)
            {
                material = new Material(shader);
                material.name = "Generated Light2D " + name + " Material";
                material.hideFlags = HideFlags.DontSave;
            }
            return material;
        }

        internal void EnsureNormalMappingMaterials(Shader normalDrawerShader, Shader normalMappedLightShader)
        {
            EnsureInternalMaterial(ref _normalMapDrawerMaterial, normalDrawerShader, "Normal Map Drawer");
            EnsureInternalMaterial(ref _normalMappedLightMaterial, normalMappedLightShader, "Normal Mapped Light");
        }

        internal void EnsureAlphaBlendedMaterial(Shader shader)
        {
            EnsureInternalMaterial(ref _alphaBlendedMaterial, shader, "Alpha Blended");
        }

        internal bool EnsureTexture(ref RenderTexture texture, RenderTextureDescriptor descriptor,
            FilterMode filterMode, string name)
        {
            descriptor.width = Mathf.Max(1, descriptor.width);
            descriptor.height = Mathf.Max(1, descriptor.height);
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.bindMS = false;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;

            if (texture == null || !DescriptorsEqual(texture.descriptor, descriptor) || texture.filterMode != filterMode)
            {
                ReleaseTexture(ref texture);
                texture = new RenderTexture(descriptor);
                texture.name = "Light2D " + name;
                texture.filterMode = filterMode;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.hideFlags = HideFlags.DontSave;
                texture.Create();
                Light2DDiagnostics.AddOwnedTextureBytes(_diagnosticsGeneration,
                    Light2DDiagnostics.EstimateTextureBytes(texture.width, texture.height, texture.volumeDepth,
                        texture.graphicsFormat));
                return true;
            }
            return false;
        }

        internal void ReleaseTextures()
        {
            ReleaseTexture(ref AmbientEmissionTexture);
            ReleaseTexture(ref AmbientTexture);
            ReleaseTexture(ref PreviousAmbientTexture);
            ReleaseTexture(ref BlurredLightTexture);
            ReleaseTexture(ref ObstaclesUpsampledTexture);
            ReleaseTexture(ref LightSourcesTexture);
            ReleaseTexture(ref ObstaclesTexture);
            ReleaseTexture(ref NormalMapBuffer);
            ReleaseTexture(ref SingleLightSourceTexture);
            ReleaseTexture(ref CameraTargetTexture);
        }

        internal void ReleaseAmbientTextures()
        {
            ReleaseTexture(ref AmbientEmissionTexture);
            ReleaseTexture(ref AmbientTexture);
            ReleaseTexture(ref PreviousAmbientTexture);
        }

        internal void ReleaseBlurTexture()
        {
            ReleaseTexture(ref BlurredLightTexture);
        }

        internal void ReleaseAntialiasingTexture()
        {
            ReleaseTexture(ref ObstaclesUpsampledTexture);
        }

        internal void ReleaseNormalMappingTextures()
        {
            ReleaseTexture(ref NormalMapBuffer);
            ReleaseTexture(ref SingleLightSourceTexture);
        }

        internal void ReleaseCameraTargetTexture()
        {
            ReleaseTexture(ref CameraTargetTexture);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            ReleaseTextures();
            DestroyClone(ref _ambientLightComputeClone);
            DestroyClone(ref _lightOverlayClone);
            DestroyClone(ref _lightSourcesBlurClone);
            DestroyClone(ref _ambientLightBlurClone);
            Destroy(ref _normalMappedLightMaterial);
            Destroy(ref _normalMapDrawerMaterial);
            Destroy(ref _alphaBlendedMaterial);
            Light2DDiagnostics.UnregisterContext(_diagnosticsGeneration);
        }

        private static void EnsureClone(ref MaterialClone entry, Material source, string name)
        {
            if (source == null)
            {
                DestroyClone(ref entry);
                return;
            }

            // Track clone identity via non-string fields (source shader + EntityId) so the per-frame
            // staleness check allocates nothing (the old CloneName concat ran every frame just to compare).
            var sourceId = source.GetEntityId();
            if (entry.Clone != null && entry.SourceShader == source.shader && entry.SourceId == sourceId)
            {
                // Change-detected live mirroring, mirroring CustomSprite.RefreshMaterial exactly: skip the
                // per-frame CopyPropertiesFromMaterial unless the serialized source material actually changed.
                var sourceCrc = source.ComputeCRC();
                if (sourceCrc == entry.SourceCrc)
                    return;
                entry.Clone.CopyPropertiesFromMaterial(source);
                entry.SourceCrc = sourceCrc;
                return;
            }

            DestroyClone(ref entry);
            // Frozen contract: the clone name MUST start with "Generated Light2D " (pinned by
            // CustomSprite.IsGeneratedMaterial and BuiltInRenderingTests' owned-material filter).
            entry.Clone = new Material(source);
            entry.Clone.name = "Generated Light2D " + name;
            entry.Clone.hideFlags = HideFlags.DontSave;
            entry.SourceShader = source.shader;
            entry.SourceId = sourceId;
            entry.SourceCrc = source.ComputeCRC();
        }

        private static void DestroyClone(ref MaterialClone entry)
        {
            Destroy(ref entry.Clone);
            entry.SourceShader = null;
            entry.SourceId = default;
            entry.SourceCrc = 0;
        }

        private static bool DescriptorsEqual(RenderTextureDescriptor left, RenderTextureDescriptor right)
        {
            return left.width == right.width && left.height == right.height &&
                   left.graphicsFormat == right.graphicsFormat && left.colorFormat == right.colorFormat &&
                   left.depthStencilFormat == right.depthStencilFormat && left.dimension == right.dimension &&
                   left.volumeDepth == right.volumeDepth && left.vrUsage == right.vrUsage &&
                   left.sRGB == right.sRGB && left.enableRandomWrite == right.enableRandomWrite &&
                   left.memoryless == right.memoryless && left.shadowSamplingMode == right.shadowSamplingMode;
        }

        private void ReleaseTexture(ref RenderTexture texture)
        {
            if (texture == null)
                return;
            Light2DDiagnostics.RemoveOwnedTextureBytes(_diagnosticsGeneration,
                Light2DDiagnostics.EstimateTextureBytes(texture.width, texture.height, texture.volumeDepth,
                    texture.graphicsFormat));
            texture.Release();
            Light2DObjectUtility.Destroy(texture);
            texture = null;
        }

        private static void Destroy(ref Material material)
        {
            if (material == null)
                return;
            Light2DObjectUtility.Destroy(material);
            material = null;
        }

        // Per-clone bookkeeping for a serialized-material clone: the clone plus the source identity
        // (shader + EntityId) and CRC used to detect swaps and gate the per-frame property copy.
        private struct MaterialClone
        {
            public Material Clone;
            public Shader SourceShader;
            public EntityId SourceId;
            public int SourceCrc;
        }
    }
}
