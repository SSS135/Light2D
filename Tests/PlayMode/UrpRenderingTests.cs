using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Light2D.Tests
{
    public class UrpRenderingTests
    {
        [UnityTest, Timeout(30000)]
        public IEnumerator UniversalRendererProducesNonuniformLightOutput()
        {
            RenderPipelineAsset oldDefault = GraphicsSettings.defaultRenderPipeline;
            RenderPipelineAsset oldQuality = QualitySettings.renderPipeline;
            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            var feature = ScriptableObject.CreateInstance<Light2DUniversalRendererFeature>();
            UniversalRenderPipelineAsset pipelineAsset = null;
            GameObject cameraObject = null;
            GameObject lightObject = null;
            RenderTexture target = null;
            Material lightMaterial = null;
            try
            {
                rendererData.rendererFeatures.Add(feature);
                feature.Create();
                rendererData.SetDirty();
                pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
                QualitySettings.renderPipeline = pipelineAsset;
                GraphicsSettings.defaultRenderPipeline = pipelineAsset;

                const int initializationFrameLimit = 30;
                int initializationFrames = 0;
                do
                {
                    initializationFrames++;
                    yield return null;
                }
                while (RenderPipelineManager.currentPipeline == null && initializationFrames < initializationFrameLimit);

                Assert.That(RenderPipelineManager.currentPipeline, Is.InstanceOf<UniversalRenderPipeline>(),
                    "The assigned URP asset did not initialize within " + initializationFrameLimit + " player frames");
                Assert.That(GraphicsSettings.currentRenderPipeline, Is.SameAs(pipelineAsset));

                cameraObject = new GameObject("URP Light2D Camera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 4;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.white;
                camera.cullingMask = 0;
                target = new RenderTexture(96, 96, 0, RenderTextureFormat.ARGB32);
                target.Create();
                camera.targetTexture = target;

                var lighting = cameraObject.AddComponent<LightingSystem>();
                ConfigureLightingFromPrefab(lighting);
                lighting.EnableAmbientLight = false;
                lighting.BlurLightSources = false;
                lighting.HDR = false;
                lighting.LightSourcesLayer = 29;
                lighting.AmbientLightLayer = 28;
                lighting.LightObstaclesLayer = 27;
                lighting.LightPixelSize = 0.1f;
                Assert.That(lighting.LightCamera, Is.Null,
                    "The URP renderer feature must derive culling and matrices from the source camera");

                lightObject = new GameObject("URP Procedural Light");
                lightObject.layer = lighting.LightSourcesLayer;
                var light = lightObject.AddComponent<LightSprite>();
                light.Sprite = Resources.Load<Sprite>("DefaultLight");
                lightMaterial = new Material(Shader.Find("Light2D/Light 1 Point"));
                light.Material = lightMaterial;
                light.Color = Color.white;
                lightObject.transform.localScale = Vector3.one * 5;
                light.enabled = false;
                light.enabled = true;

                yield return new WaitForEndOfFrame();

                PixelMetrics metrics = ReadMetrics(target);
                Assert.That(metrics.Range, Is.GreaterThan(0.08f), metrics.ToString());
                Assert.That(metrics.Center, Is.GreaterThan(metrics.Corner + 0.05f), metrics.ToString());
            }
            finally
            {
                QualitySettings.renderPipeline = oldQuality;
                GraphicsSettings.defaultRenderPipeline = oldDefault;
                if (cameraObject != null)
                    Object.DestroyImmediate(cameraObject);
                if (lightObject != null)
                    Object.DestroyImmediate(lightObject);
                if (target != null)
                    Object.DestroyImmediate(target);
                if (lightMaterial != null)
                    Object.DestroyImmediate(lightMaterial);
                Object.DestroyImmediate(feature);
                Object.DestroyImmediate(rendererData);
                Object.DestroyImmediate(pipelineAsset);
            }
        }

        private static void ConfigureLightingFromPrefab(LightingSystem lighting)
        {
            var prefab = Resources.Load<GameObject>("Lighting Camera");
            var config = prefab.GetComponent<LightingSystemPrefabConfig>();
            lighting.AmbientLightComputeMaterial = config.AmbientLightComputeMaterial;
            lighting.LightOverlayMaterial = config.LightOverlayMaterial;
            lighting.LightSourcesBlurMaterial = config.BlurMaterial;
            lighting.AmbientLightBlurMaterial = config.BlurMaterial;
        }

        private static PixelMetrics ReadMetrics(RenderTexture target)
        {
            var oldActive = RenderTexture.active;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                texture.Apply();
                Color[] pixels = texture.GetPixels();
                float minimum = pixels.Min(pixel => pixel.grayscale);
                float maximum = pixels.Max(pixel => pixel.grayscale);
                float center = texture.GetPixel(target.width / 2, target.height / 2).grayscale;
                float corner = texture.GetPixel(4, 4).grayscale;
                return new PixelMetrics(minimum, maximum, center, corner);
            }
            finally
            {
                RenderTexture.active = oldActive;
                Object.DestroyImmediate(texture);
            }
        }

        private readonly struct PixelMetrics
        {
            internal readonly float Range;
            internal readonly float Center;
            internal readonly float Corner;

            internal PixelMetrics(float minimum, float maximum, float center, float corner)
            {
                Range = maximum - minimum;
                Center = center;
                Corner = corner;
            }

            public override string ToString()
            {
                return string.Format("range={0:F3}, center={1:F3}, corner={2:F3}", Range, Center, Corner);
            }
        }
    }
}
