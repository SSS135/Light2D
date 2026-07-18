using System;
using UnityEngine;

namespace Light2D
{
    internal sealed class LightingRenderResources : IDisposable
    {
        private Material _ambientLightComputeMaterial;
        private Material _lightOverlayMaterial;
        private Material _lightSourcesBlurMaterial;
        private Material _ambientLightBlurMaterial;
        private Material _normalMappedLightMaterial;
        private Material _lightCombiningMaterial;
        private Material _alphaBlendedMaterial;
        private Camera _normalMapCamera;

        internal RenderTexture AmbientEmissionTexture;
        internal RenderTexture AmbientTexture;
        internal RenderTexture PreviousAmbientTexture;
        internal RenderTexture BlurredLightTexture;
        internal RenderTexture ObstaclesUpsampledTexture;
        internal RenderTexture LightSourcesTexture;
        internal RenderTexture ObstaclesTexture;
        internal RenderTexture ScreenBlitTexture;
        internal RenderTexture NormalMapBuffer;
        internal RenderTexture SingleLightSourceTexture;
        internal RenderTexture CameraTargetTexture;

        internal Material AmbientLightComputeMaterial { get { return _ambientLightComputeMaterial; } }
        internal Material LightOverlayMaterial { get { return _lightOverlayMaterial; } }
        internal Material LightSourcesBlurMaterial { get { return _lightSourcesBlurMaterial; } }
        internal Material AmbientLightBlurMaterial { get { return _ambientLightBlurMaterial; } }
        internal Material NormalMappedLightMaterial { get { return _normalMappedLightMaterial; } }
        internal Material LightCombiningMaterial { get { return _lightCombiningMaterial; } }
        internal Material AlphaBlendedMaterial { get { return _alphaBlendedMaterial; } }
        internal Camera NormalMapCamera { get { return _normalMapCamera; } }

        internal void EnsureSerializedMaterials(Material ambientCompute, Material overlay, Material sourceBlur,
            Material ambientBlur)
        {
            EnsureClone(ref _ambientLightComputeMaterial, ambientCompute, "Ambient Light Compute");
            EnsureClone(ref _lightOverlayMaterial, overlay, "Light Overlay");
            EnsureClone(ref _lightSourcesBlurMaterial, sourceBlur, "Light Sources Blur");
            EnsureClone(ref _ambientLightBlurMaterial, ambientBlur, "Ambient Light Blur");
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

        internal void EnsureNormalMappingMaterials(Shader normalMappedLightShader, Shader lightCombiningShader)
        {
            EnsureInternalMaterial(ref _normalMappedLightMaterial, normalMappedLightShader, "Normal Mapped Light");
            EnsureInternalMaterial(ref _lightCombiningMaterial, lightCombiningShader, "Light Blender");
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
                return true;
            }
            return false;
        }

        internal Camera EnsureNormalMapCamera(Transform parent)
        {
            if (_normalMapCamera != null)
                return _normalMapCamera;

            var cameraObject = new GameObject("Light2D Normals Camera");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            cameraObject.transform.SetParent(parent, false);
            _normalMapCamera = cameraObject.AddComponent<Camera>();
            _normalMapCamera.enabled = false;
            return _normalMapCamera;
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
            ReleaseTexture(ref ScreenBlitTexture);
            ReleaseTexture(ref NormalMapBuffer);
            ReleaseTexture(ref SingleLightSourceTexture);
            ReleaseTexture(ref CameraTargetTexture);
        }

        public void Dispose()
        {
            ReleaseTextures();
            Destroy(ref _ambientLightComputeMaterial);
            Destroy(ref _lightOverlayMaterial);
            Destroy(ref _lightSourcesBlurMaterial);
            Destroy(ref _ambientLightBlurMaterial);
            Destroy(ref _normalMappedLightMaterial);
            Destroy(ref _lightCombiningMaterial);
            Destroy(ref _alphaBlendedMaterial);
            if (_normalMapCamera != null)
                Util.Destroy(_normalMapCamera.gameObject);
            _normalMapCamera = null;
        }

        private static void EnsureClone(ref Material clone, Material source, string name)
        {
            if (source == null)
            {
                Destroy(ref clone);
                return;
            }
            if (clone != null && clone.shader == source.shader && clone.name == CloneName(source, name))
            {
                clone.CopyPropertiesFromMaterial(source);
                return;
            }

            Destroy(ref clone);
            clone = new Material(source);
            clone.name = CloneName(source, name);
            clone.hideFlags = HideFlags.DontSave;
        }

        private static string CloneName(Material source, string name)
        {
#if UNITY_6000_0_OR_NEWER
            return "Generated Light2D " + name + " (" + source.GetEntityId() + ")";
#else
            return "Generated Light2D " + name + " (" + source.GetInstanceID() + ")";
#endif
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

        private static void ReleaseTexture(ref RenderTexture texture)
        {
            if (texture == null)
                return;
            texture.Release();
            Util.Destroy(texture);
            texture = null;
        }

        private static void Destroy(ref Material material)
        {
            if (material == null)
                return;
            Util.Destroy(material);
            material = null;
        }
    }
}
