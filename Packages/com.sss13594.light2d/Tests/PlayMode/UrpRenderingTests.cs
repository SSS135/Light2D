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
        // These tests build an in-memory URP asset, then drive rendering explicitly with
        // RenderPipeline.SubmitRenderRequest against the source camera rather than waiting on player
        // frames: WaitForEndOfFrame and passive frame yields never advance a runtime-created URP pipeline
        // to "current" in -batchmode, but an explicit SingleCameraRequest composes the pipeline on demand
        // and writes into the request's destination target. There is no skip path - two requests are
        // issued per camera (the first warms the ambient history, the second captures the settled frame),
        // and every pixel assertion runs for real, so a genuine URP render regression fails the test.

        [UnityTest, Timeout(30000)]
        public IEnumerator UniversalRendererProducesAlignedNonSquareTargetTextureOutput()
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

                // Let the render-pipeline asset assignment settle before issuing render requests.
                yield return null;

                target = new RenderTexture(128, 80, 0, RenderTextureFormat.ARGB32);
                target.Create();
                var camera = CreateSourceCamera("URP Light2D Camera", new Vector3(0, 0, -10), target);
                cameraObject = camera.gameObject;

                var lighting = AddConfiguredLighting(camera);

                lightMaterial = new Material(Shader.Find("Light2D/Light 1 Point"));
                var light = CreateLightSprite("URP Procedural Light", Vector3.zero, Color.white, lightMaterial);
                lightObject = light.gameObject;

                var req = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera, req), Is.True,
                    "URP must support single-camera render requests on this host.");
                RenderPipeline.SubmitRenderRequest(camera, req);  // warm ambient
                RenderPipeline.SubmitRenderRequest(camera, req);  // settled frame

                PixelMetrics metrics = ReadMetrics(target);
                Assert.That(metrics.Range, Is.GreaterThan(0.08f), metrics.ToString());
                Assert.That(metrics.Center, Is.GreaterThan(metrics.Corner + 0.05f), metrics.ToString());
                Assert.That(metrics.CornerSpread, Is.LessThan(0.04f), metrics.ToString());
                Assert.That(metrics.Center, Is.GreaterThan(metrics.MaxCorner + 0.05f), metrics.ToString());
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

        [UnityTest, Timeout(30000)]
        public IEnumerator UniversalRendererAlignsOutputWithinPartialViewportRect()
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

                // Let the render-pipeline asset assignment settle before issuing render requests.
                yield return null;

                target = new RenderTexture(256, 160, 0, RenderTextureFormat.ARGB32);
                target.Create();
                FillRenderTexture(target, new Color(0f, 0f, 1f, 1f));

                var camera = CreateSourceCamera("URP Partial Viewport Camera", new Vector3(0, 0, -10), target);
                cameraObject = camera.gameObject;
                camera.rect = new Rect(0.5f, 0.5f, 0.5f, 0.5f);

                var lighting = AddConfiguredLighting(camera);

                lightMaterial = new Material(Shader.Find("Light2D/Light 1 Point"));
                var light = CreateLightSprite("URP Viewport Light", Vector3.zero, Color.white, lightMaterial);
                lightObject = light.gameObject;

                var req = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera, req), Is.True,
                    "URP must support single-camera render requests on this host.");
                RenderPipeline.SubmitRenderRequest(camera, req);  // warm ambient
                RenderPipeline.SubmitRenderRequest(camera, req);  // settled frame

                // The viewport rect occupies the top-right quadrant: pixels x in [128, 256) and
                // y in [80, 160) with the bottom-left origin of ReadPixels.
                Color[] samples = ReadColorsAt(target,
                    new Vector2Int(192, 120), // viewport center
                    new Vector2Int(132, 84), // viewport inner corner, 4px inset
                    new Vector2Int(64, 40)); // center of an untouched quadrant
                float viewportCenter = samples[0].grayscale;
                float viewportCorner = samples[1].grayscale;
                float untouchedCenter = samples[2].grayscale;
                string description = string.Format(
                    "viewportCenter={0:F3}, viewportCorner={1:F3}, untouchedCenter={2}",
                    viewportCenter, viewportCorner, samples[2]);
                Assert.That(viewportCenter, Is.GreaterThan(viewportCorner + 0.05f), description);
                Assert.That(viewportCenter, Is.GreaterThan(untouchedCenter + 0.05f), description);
                // Secondary guard: the quadrant outside the camera rect keeps its pre-filled blue.
                // Relaxable if URP is proven to legitimately clear the attachment outside the viewport.
                Assert.That(samples[2].b, Is.GreaterThan(samples[2].r + 0.2f), description);
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

        [UnityTest, Timeout(30000)]
        public IEnumerator UniversalRendererComposesAtReducedRenderScale()
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
                // Reduced render scale must still compose lighting aligned onto the full-size target.
                pipelineAsset.renderScale = 0.5f;
                QualitySettings.renderPipeline = pipelineAsset;
                GraphicsSettings.defaultRenderPipeline = pipelineAsset;

                // Let the render-pipeline asset assignment settle before issuing render requests.
                yield return null;

                target = new RenderTexture(128, 80, 0, RenderTextureFormat.ARGB32);
                target.Create();
                var camera = CreateSourceCamera("URP Render Scale Camera", new Vector3(0, 0, -10), target);
                cameraObject = camera.gameObject;

                var lighting = AddConfiguredLighting(camera);

                lightMaterial = new Material(Shader.Find("Light2D/Light 1 Point"));
                var light = CreateLightSprite("URP Render Scale Light", Vector3.zero, Color.white, lightMaterial);
                lightObject = light.gameObject;

                var req = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera, req), Is.True,
                    "URP must support single-camera render requests on this host.");
                RenderPipeline.SubmitRenderRequest(camera, req);  // warm ambient
                RenderPipeline.SubmitRenderRequest(camera, req);  // settled frame

                PixelMetrics metrics = ReadMetrics(target);
                Assert.That(metrics.Range, Is.GreaterThan(0.08f), metrics.ToString());
                // Upscale filtering softens edges, so corner spread gets a wider tolerance than native scale.
                Assert.That(metrics.CornerSpread, Is.LessThan(0.06f), metrics.ToString());
                Assert.That(metrics.Center, Is.GreaterThan(metrics.MaxCorner + 0.05f), metrics.ToString());
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

        [UnityTest, Timeout(60000)]
        public IEnumerator TwoBaseCamerasEachCompositeOwnLightingOnce()
        {
            RenderPipelineAsset oldDefault = GraphicsSettings.defaultRenderPipeline;
            RenderPipelineAsset oldQuality = QualitySettings.renderPipeline;
            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            var feature = ScriptableObject.CreateInstance<Light2DUniversalRendererFeature>();
            UniversalRenderPipelineAsset pipelineAsset = null;
            GameObject cameraObjectA = null;
            GameObject cameraObjectB = null;
            GameObject lightObjectA = null;
            GameObject lightObjectB = null;
            RenderTexture targetA = null;
            RenderTexture targetB = null;
            Material lightMaterialA = null;
            Material lightMaterialB = null;
            try
            {
                rendererData.rendererFeatures.Add(feature);
                feature.Create();
                rendererData.SetDirty();
                pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
                QualitySettings.renderPipeline = pipelineAsset;
                GraphicsSettings.defaultRenderPipeline = pipelineAsset;

                // Let the render-pipeline asset assignment settle before issuing render requests.
                yield return null;

                // v3 has no layers: both rigs use identically configured lighting and are isolated purely
                // spatially (1000 units apart). Each light's Owner is left null, so the registry snapshot
                // for each source camera includes only the light within that camera's frustum.
                targetA = new RenderTexture(128, 80, 0, RenderTextureFormat.ARGB32);
                targetA.Create();
                var cameraA = CreateSourceCamera("URP Rig A Camera", new Vector3(0, 0, -10), targetA);
                cameraObjectA = cameraA.gameObject;
                var lightingA = AddConfiguredLighting(cameraA);

                targetB = new RenderTexture(128, 80, 0, RenderTextureFormat.ARGB32);
                targetB.Create();
                var cameraB = CreateSourceCamera("URP Rig B Camera", new Vector3(1000, 0, -10), targetB);
                cameraObjectB = cameraB.gameObject;
                var lightingB = AddConfiguredLighting(cameraB);

                lightMaterialA = new Material(Shader.Find("Light2D/Light 1 Point"));
                var lightA = CreateLightSprite("URP Rig A Light", Vector3.zero, Color.red, lightMaterialA);
                lightObjectA = lightA.gameObject;

                lightMaterialB = new Material(Shader.Find("Light2D/Light 1 Point"));
                var lightB = CreateLightSprite("URP Rig B Light", new Vector3(1000, 0, 0), Color.green, lightMaterialB);
                lightObjectB = lightB.gameObject;

                var reqA = new UniversalRenderPipeline.SingleCameraRequest { destination = targetA };
                var reqB = new UniversalRenderPipeline.SingleCameraRequest { destination = targetB };
                Assert.That(RenderPipeline.SupportsRenderRequest(cameraA, reqA), Is.True,
                    "URP must support single-camera render requests on this host.");
                Assert.That(RenderPipeline.SupportsRenderRequest(cameraB, reqB), Is.True,
                    "URP must support single-camera render requests on this host.");
                RenderPipeline.SubmitRenderRequest(cameraA, reqA);  // rig A warm ambient
                RenderPipeline.SubmitRenderRequest(cameraB, reqB);  // rig B warm ambient
                RenderPipeline.SubmitRenderRequest(cameraA, reqA);  // rig A settled frame
                RenderPipeline.SubmitRenderRequest(cameraB, reqB);  // rig B settled frame

                Assert.That(Light2DDiagnostics.ContextCount, Is.EqualTo(2),
                    "Each base camera must own exactly one URP lighting context");

                ColorMetrics a = ReadColorMetrics(targetA);
                ColorMetrics b = ReadColorMetrics(targetB);

                Assert.That(a.Center.r, Is.GreaterThan(0.3f), "Rig A: " + a);
                Assert.That(a.Center.r, Is.GreaterThan(a.Center.g + 0.15f), "Rig A: " + a);
                Assert.That(a.CenterGrayscale, Is.GreaterThan(a.MaxCornerGrayscale + 0.05f), "Rig A: " + a);

                Assert.That(b.Center.g, Is.GreaterThan(0.3f), "Rig B: " + b);
                Assert.That(b.Center.g, Is.GreaterThan(b.Center.r + 0.15f), "Rig B: " + b);
                Assert.That(b.CenterGrayscale, Is.GreaterThan(b.MaxCornerGrayscale + 0.05f), "Rig B: " + b);

                // Cross-contamination guards: the other rig's light color must not lift the center
                // above the background level measured at the corners.
                Assert.That(Mathf.Abs(a.Center.g - a.MeanCornerChannel(1)), Is.LessThanOrEqualTo(0.05f),
                    "Rig A center green must stay at corner background level: " + a);
                Assert.That(Mathf.Abs(b.Center.r - b.MeanCornerChannel(0)), Is.LessThanOrEqualTo(0.05f),
                    "Rig B center red must stay at corner background level: " + b);
            }
            finally
            {
                QualitySettings.renderPipeline = oldQuality;
                GraphicsSettings.defaultRenderPipeline = oldDefault;
                if (cameraObjectA != null)
                    Object.DestroyImmediate(cameraObjectA);
                if (cameraObjectB != null)
                    Object.DestroyImmediate(cameraObjectB);
                if (lightObjectA != null)
                    Object.DestroyImmediate(lightObjectA);
                if (lightObjectB != null)
                    Object.DestroyImmediate(lightObjectB);
                if (targetA != null)
                    Object.DestroyImmediate(targetA);
                if (targetB != null)
                    Object.DestroyImmediate(targetB);
                if (lightMaterialA != null)
                    Object.DestroyImmediate(lightMaterialA);
                if (lightMaterialB != null)
                    Object.DestroyImmediate(lightMaterialB);
                Object.DestroyImmediate(feature);
                Object.DestroyImmediate(rendererData);
                Object.DestroyImmediate(pipelineAsset);
            }
        }

        private static Camera CreateSourceCamera(string name, Vector3 position, RenderTexture target)
        {
            var cameraObject = new GameObject(name);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4;
            camera.transform.position = position;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.white;
            camera.cullingMask = 0;
            camera.targetTexture = target;
            return camera;
        }

        private static LightingSystem AddConfiguredLighting(Camera camera)
        {
            var lighting = camera.gameObject.AddComponent<LightingSystem>();
            AssignCompositionMaterials(lighting);
            lighting.EnableAmbientLight = false;
            lighting.BlurLightSources = false;
            lighting.HDR = false;
            lighting.LightPixelSize = 0.1f;
            return lighting;
        }

        private static LightSprite CreateLightSprite(string name, Vector3 position, Color color, Material material)
        {
            // v3 has no light layer: the LightSprite registers itself on enable and the URP feature
            // draws the registry snapshot, so no gameObject.layer assignment is needed.
            var lightObject = new GameObject(name);
            lightObject.transform.position = position;
            var light = lightObject.AddComponent<LightSprite>();
            light.Sprite = Resources.Load<Sprite>("DefaultLight");
            light.Material = material;
            light.Color = color;
            // Radius sizes the light mesh now (decoupled from Transform scale); Radius = 5 on a
            // unit-scaled transform preserves the old localScale = 5 geometry.
            light.Radius = 5f;
            light.enabled = false;
            light.enabled = true;
            return light;
        }

        // The retired "Lighting Camera" prefab no longer bridges these materials through Resources, so
        // build them from their shaders directly (mirrors how the light material is created above). They
        // are shared, hidden, dont-save statics so repeated tests do not leak a Material per run; the
        // LightingSystem clones them before mutating, leaving these source materials untouched.
        private static Material _overlayMaterial;
        private static Material _ambientComputeMaterial;
        private static Material _blurMaterial;

        private static void AssignCompositionMaterials(LightingSystem lighting)
        {
            lighting.LightOverlayMaterial = SharedMaterial(ref _overlayMaterial, "Light2D/Light Overlay");
            lighting.AmbientLightComputeMaterial =
                SharedMaterial(ref _ambientComputeMaterial, "Light2D/Ambient Light Computer");
            var blur = SharedMaterial(ref _blurMaterial, "Light2D/Fast Blur");
            lighting.LightSourcesBlurMaterial = blur;
            lighting.AmbientLightBlurMaterial = blur;
        }

        private static Material SharedMaterial(ref Material cache, string shaderName)
        {
            if (cache == null)
                cache = new Material(Shader.Find(shaderName)) { hideFlags = HideFlags.HideAndDontSave };
            return cache;
        }

        private static void FillRenderTexture(RenderTexture target, Color color)
        {
            var oldActive = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                GL.Clear(true, true, color);
            }
            finally
            {
                RenderTexture.active = oldActive;
            }
        }

        private static Color[] ReadAllPixels(RenderTexture target)
        {
            var oldActive = RenderTexture.active;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                texture.Apply();
                return texture.GetPixels();
            }
            finally
            {
                RenderTexture.active = oldActive;
                Object.DestroyImmediate(texture);
            }
        }

        // 4px-inset corners: (4,4), (w-5,4), (4,h-5), (w-5,h-5), indexed in the bulk GetPixels array.
        private static int[] CornerIndices(int width, int height)
        {
            return new[]
            {
                4 * width + 4,
                4 * width + (width - 5),
                (height - 5) * width + 4,
                (height - 5) * width + (width - 5),
            };
        }

        private static PixelMetrics ReadMetrics(RenderTexture target)
        {
            Color[] pixels = ReadAllPixels(target);
            int width = target.width;
            int height = target.height;
            float minimum = pixels.Min(pixel => pixel.grayscale);
            float maximum = pixels.Max(pixel => pixel.grayscale);
            float center = pixels[height / 2 * width + width / 2].grayscale;
            float[] corners = CornerIndices(width, height).Select(index => pixels[index].grayscale).ToArray();
            return new PixelMetrics(minimum, maximum, center, corners);
        }

        private static ColorMetrics ReadColorMetrics(RenderTexture target)
        {
            Color[] pixels = ReadAllPixels(target);
            int width = target.width;
            int height = target.height;
            Color center = pixels[height / 2 * width + width / 2];
            Color[] corners = CornerIndices(width, height).Select(index => pixels[index]).ToArray();
            return new ColorMetrics(center, corners);
        }

        private static Color[] ReadColorsAt(RenderTexture target, params Vector2Int[] points)
        {
            Color[] pixels = ReadAllPixels(target);
            return points.Select(point => pixels[point.y * target.width + point.x]).ToArray();
        }

        private readonly struct PixelMetrics
        {
            internal readonly float Range;
            internal readonly float Center;
            internal readonly float Corner;
            internal readonly float CornerSpread;
            internal readonly float MaxCorner;

            internal PixelMetrics(float minimum, float maximum, float center, float[] corners)
            {
                Range = maximum - minimum;
                Center = center;
                Corner = corners[0];
                MaxCorner = corners.Max();
                CornerSpread = MaxCorner - corners.Min();
            }

            public override string ToString()
            {
                return string.Format(
                    "range={0:F3}, center={1:F3}, corner={2:F3}, cornerSpread={3:F3}, maxCorner={4:F3}",
                    Range, Center, Corner, CornerSpread, MaxCorner);
            }
        }

        private readonly struct ColorMetrics
        {
            internal readonly Color Center;
            internal readonly Color[] Corners;

            internal ColorMetrics(Color center, Color[] corners)
            {
                Center = center;
                Corners = corners;
            }

            internal float CenterGrayscale => Center.grayscale;
            internal float MaxCornerGrayscale => Corners.Max(corner => corner.grayscale);

            internal float MeanCornerChannel(int channel)
            {
                return Corners.Average(corner => corner[channel]);
            }

            public override string ToString()
            {
                return string.Format("center={0}, corners=[{1}]",
                    Center, string.Join("; ", Corners.Select(corner => corner.ToString())));
            }
        }
    }
}
