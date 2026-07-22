using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Light2D.Tests
{
    public class BuiltInRenderingTests
    {
        [UnityTest, Timeout(30000)]
        public IEnumerator ProceduralRenderIsNonuniformAndObstacleAttenuatesLight()
        {
            RenderPipelineAsset oldDefault = GraphicsSettings.defaultRenderPipeline;
            RenderPipelineAsset oldQuality = QualitySettings.renderPipeline;
            RenderFixture fixture = null;
            try
            {
                QualitySettings.renderPipeline = null;
                GraphicsSettings.defaultRenderPipeline = null;
                yield return WaitForBuiltInPipeline();
                fixture = new RenderFixture(false);
                yield return null;

                fixture.Obstacle.SetActive(false);
                RenderMetrics unobstructed = fixture.RenderAndMeasure();
                fixture.Obstacle.SetActive(true);
                yield return null;
                RenderMetrics obstructed = fixture.RenderAndMeasure();

                Assert.That(unobstructed.LightSources.Range, Is.GreaterThan(0.08f), unobstructed.ToString());
                Assert.That(unobstructed.LightSources.Center,
                    Is.GreaterThan(unobstructed.LightSources.Corner + 0.05f), unobstructed.ToString());
                Assert.That(obstructed.Obstacles.Center,
                    Is.LessThan(unobstructed.Obstacles.Center - 0.05f),
                    "Obstacle was not rendered into the obstacle texture. before=" + unobstructed +
                    ", after=" + obstructed);
                Assert.That(obstructed.Obstacles.CenterAlpha,
                    Is.GreaterThan(unobstructed.Obstacles.CenterAlpha + 0.05f),
                    "Obstacle alpha was not rendered into the obstacle texture. before=" + unobstructed +
                    ", after=" + obstructed);
                Assert.That(obstructed.LightSources.Center,
                    Is.LessThan(unobstructed.LightSources.Center - 0.05f),
                    "Obstacle did not attenuate the light source texture. before=" + unobstructed +
                    ", after=" + obstructed);
                Assert.That(unobstructed.SourceTarget.Range, Is.GreaterThan(0.08f), unobstructed.ToString());
                Assert.That(unobstructed.SourceTarget.Center,
                    Is.GreaterThan(unobstructed.SourceTarget.Corner + 0.05f), unobstructed.ToString());
                Assert.That(obstructed.SourceTarget.Center,
                    Is.LessThan(unobstructed.SourceTarget.Center - 0.05f),
                    "Obstacle did not measurably attenuate the light. before=" + unobstructed + ", after=" + obstructed);
            }
            finally
            {
                fixture?.Dispose();
                QualitySettings.renderPipeline = oldQuality;
                GraphicsSettings.defaultRenderPipeline = oldDefault;
            }
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator DisableAndDestroyReleaseOwnedTexturesAndMaterials()
        {
            RenderPipelineAsset oldDefault = GraphicsSettings.defaultRenderPipeline;
            RenderPipelineAsset oldQuality = QualitySettings.renderPipeline;
            RenderFixture fixture = null;
            try
            {
                QualitySettings.renderPipeline = null;
                GraphicsSettings.defaultRenderPipeline = null;
                yield return WaitForBuiltInPipeline();
                fixture = new RenderFixture(true);
                yield return null;
                fixture.RenderAndMeasure();

                var lighting = fixture.Lighting;
                RenderTexture obstacles = lighting.ObstaclesTexture;
                RenderTexture sources = lighting.LightSourcesTexture;
                RenderTexture ambient = lighting.AmbientTexture;
                Assert.That(obstacles, Is.Not.Null);
                Assert.That(sources, Is.Not.Null);
                Assert.That(ambient, Is.Null, "Disabled ambient lighting allocated textures");
                Material[] ownedMaterials = FindOwnedMaterials();
                Assert.That(ownedMaterials, Is.Not.Empty);

                lighting.enabled = false;
                yield return null;
                Assert.That(lighting.ObstaclesTexture, Is.Null);
                Assert.That(lighting.LightSourcesTexture, Is.Null);
                Assert.That(lighting.AmbientTexture, Is.Null);
                Assert.That(obstacles == null, Is.True);
                Assert.That(sources == null, Is.True);
                Assert.That(ownedMaterials.All(material => material == null), Is.True,
                    "A generated material survived disable");

                lighting.enabled = true;
                fixture.RenderAndMeasure();
                ownedMaterials = FindOwnedMaterials();
                Object.Destroy(lighting);
                yield return null;
                Assert.That(ownedMaterials.All(material => material == null), Is.True,
                    "A generated material survived destroy");
            }
            finally
            {
                fixture?.Dispose();
                QualitySettings.renderPipeline = oldQuality;
                GraphicsSettings.defaultRenderPipeline = oldDefault;
            }
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator InvalidSettingsCopySourceAndDoNotAllocateTextures()
        {
            RenderPipelineAsset oldDefault = GraphicsSettings.defaultRenderPipeline;
            RenderPipelineAsset oldQuality = QualitySettings.renderPipeline;
            RenderFixture fixture = null;
            try
            {
                QualitySettings.renderPipeline = null;
                GraphicsSettings.defaultRenderPipeline = null;
                yield return WaitForBuiltInPipeline();
                fixture = new RenderFixture(false);
                fixture.Lighting.LightPixelSize = float.NaN;
                LogAssert.Expect(LogType.Error,
                    "Light2D skipped rendering: Light Pixel Size must be finite and greater than zero.");

                PixelMetrics output = fixture.RenderSourceAndMeasure();

                Assert.That(output.Range, Is.LessThan(0.001f), output.ToString());
                Assert.That(output.Center, Is.EqualTo(1f).Within(0.01f), output.ToString());
                Assert.That(fixture.Lighting.ObstaclesTexture, Is.Null);
                Assert.That(fixture.Lighting.LightSourcesTexture, Is.Null);
                Assert.That(fixture.Lighting.AmbientTexture, Is.Null);
            }
            finally
            {
                fixture?.Dispose();
                QualitySettings.renderPipeline = oldQuality;
                GraphicsSettings.defaultRenderPipeline = oldDefault;
            }
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator PointBlurKeepsTextureIdentityAndFeatureTogglesReleaseAmbientTextures()
        {
            RenderPipelineAsset oldDefault = GraphicsSettings.defaultRenderPipeline;
            RenderPipelineAsset oldQuality = QualitySettings.renderPipeline;
            RenderFixture fixture = null;
            try
            {
                QualitySettings.renderPipeline = null;
                GraphicsSettings.defaultRenderPipeline = null;
                yield return WaitForBuiltInPipeline();
                fixture = new RenderFixture(false);
                fixture.Lighting.BlurLightSources = true;
                fixture.Lighting.LightTexturesFilterMode = FilterMode.Point;

                fixture.RenderAndMeasure();
                RenderTexture sources = fixture.Lighting.LightSourcesTexture;
                fixture.RenderAndMeasure();
                Assert.That(fixture.Lighting.LightSourcesTexture, Is.SameAs(sources));
                Assert.That(sources.filterMode, Is.EqualTo(FilterMode.Point));

                fixture.Lighting.EnableAmbientLight = true;
                fixture.RenderAndMeasure();
                Assert.That(fixture.Lighting.AmbientTexture, Is.Not.Null);
                fixture.Lighting.EnableAmbientLight = false;
                fixture.RenderAndMeasure();
                Assert.That(fixture.Lighting.AmbientTexture, Is.Null);
            }
            finally
            {
                fixture?.Dispose();
                QualitySettings.renderPipeline = oldQuality;
                GraphicsSettings.defaultRenderPipeline = oldDefault;
            }
        }

        private static Material[] FindOwnedMaterials()
        {
            return Resources.FindObjectsOfTypeAll<Material>()
                .Where(material => material != null && material.name.StartsWith("Generated Light2D "))
                .ToArray();
        }

        private static IEnumerator WaitForBuiltInPipeline()
        {
            const int frameLimit = 30;
            int frames = 0;
            while (RenderPipelineManager.currentPipeline != null && frames < frameLimit)
            {
                frames++;
                yield return null;
            }

            Assert.That(RenderPipelineManager.currentPipeline, Is.Null,
                "The active scriptable render pipeline did not shut down within " + frameLimit + " player frames");
            Assert.That(GraphicsSettings.currentRenderPipeline, Is.Null);
        }

        private sealed class RenderFixture
        {
            internal readonly LightingSystem Lighting;
            internal readonly GameObject Obstacle;

            private readonly GameObject _cameraObject;
            private readonly GameObject _lightObject;
            private readonly GameObject _obstacleSource;
            private readonly RenderTexture _target;
            private readonly Material _lightMaterial;
            private readonly Material _obstacleMaterial;
            private readonly Material _overlayMaterial;
            private readonly Material _ambientMaterial;
            private readonly Material _blurMaterial;

            internal RenderFixture(bool normalMapping)
            {
                _cameraObject = new GameObject("Built-in Light2D Camera");
                _cameraObject.SetActive(false);
                var camera = _cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 4;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.white;
                // v3 has no layers: the light/obstacle MeshRenderers are disabled and drawn only through
                // the registry, so the source camera draws none of them. cullingMask 0 keeps the game
                // image to the composited overlay, so the SourceTarget metrics stay comparable to before.
                camera.cullingMask = 0;
                _target = new RenderTexture(96, 96, 0, RenderTextureFormat.ARGB32);
                _target.Create();
                camera.targetTexture = _target;

                Lighting = _cameraObject.AddComponent<LightingSystem>();
                // v3 retires the Built-in helper camera and the "Lighting Camera" prefab; the composition
                // materials come straight from their shaders (as the light/obstacle materials do below).
                _overlayMaterial = new Material(Shader.Find("Light2D/Light Overlay"));
                _ambientMaterial = new Material(Shader.Find("Light2D/Ambient Light Computer"));
                _blurMaterial = new Material(Shader.Find("Light2D/Fast Blur"));
                Lighting.AmbientLightComputeMaterial = _ambientMaterial;
                Lighting.LightOverlayMaterial = _overlayMaterial;
                Lighting.LightSourcesBlurMaterial = _blurMaterial;
                Lighting.AmbientLightBlurMaterial = _blurMaterial;
                Lighting.EnableAmbientLight = false;
                Lighting.BlurLightSources = false;
                Lighting.BlurAmbientLight = false;
                Lighting.HDR = false;
                Lighting.EnableNormalMapping = normalMapping;
                Lighting.LightPixelSize = 0.1f;

                Sprite sprite = Resources.Load<Sprite>("DefaultLight");
                _lightObject = new GameObject("Procedural Light");
                var light = _lightObject.AddComponent<LightSprite>();
                light.Sprite = sprite;
                _lightMaterial = new Material(Shader.Find("Light2D/Light 1 Point"));
                light.Material = _lightMaterial;
                light.Color = Color.white;
                // Radius sizes the light mesh now (decoupled from Transform scale); Radius = 5 on a
                // unit-scaled transform preserves the old localScale = 5 geometry.
                light.Radius = 5f;
                light.enabled = false;
                light.enabled = true;

                _obstacleSource = new GameObject("Obstacle Source");
                var sourceRenderer = _obstacleSource.AddComponent<SpriteRenderer>();
                sourceRenderer.sprite = sprite;
                sourceRenderer.enabled = false;
                Obstacle = new GameObject("Light Obstacle");
                Obstacle.transform.SetParent(_obstacleSource.transform, false);
                Obstacle.transform.localScale = new Vector3(0.7f, 5, 1);
                var obstacle = Obstacle.AddComponent<LightObstacleSprite>();
                obstacle.Owner = Lighting;
                obstacle.GameSpriteRenderer = sourceRenderer;
                obstacle.Sprite = sprite;
                obstacle.Color = Color.black;
                obstacle.AdditiveColor = new Color(0, 0, 0, 0.4f);
                _obstacleMaterial = new Material(Shader.Find("Light2D/Transparent Dual Color Normal Mapped"));
                obstacle.Material = _obstacleMaterial;
                obstacle.enabled = false;
                obstacle.enabled = true;
                _cameraObject.SetActive(true);
            }

            internal RenderMetrics RenderAndMeasure()
            {
                _cameraObject.GetComponent<Camera>().Render();
                Assert.That(Lighting.ObstaclesTexture, Is.Not.Null,
                    "LightingSystem did not create its obstacle texture");
                Assert.That(Lighting.LightSourcesTexture, Is.Not.Null,
                    "LightingSystem did not create its light source texture");
                return new RenderMetrics(
                    Measure(_target),
                    Measure(Lighting.ObstaclesTexture),
                    Measure(Lighting.LightSourcesTexture));
            }

            internal PixelMetrics RenderSourceAndMeasure()
            {
                _cameraObject.GetComponent<Camera>().Render();
                return Measure(_target);
            }

            private static PixelMetrics Measure(RenderTexture target)
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
                    float minimumAlpha = pixels.Min(pixel => pixel.a);
                    float maximumAlpha = pixels.Max(pixel => pixel.a);
                    Color center = pixels[(target.height / 2) * target.width + target.width / 2];
                    int cornerCoordinate = Mathf.Min(4, Mathf.Min(target.width, target.height) - 1);
                    Color corner = pixels[cornerCoordinate * target.width + cornerCoordinate];
                    return new PixelMetrics(minimum, maximum,
                        center.grayscale, center.a, corner.grayscale, minimumAlpha, maximumAlpha,
                        target.width, target.height);
                }
                finally
                {
                    RenderTexture.active = oldActive;
                    Object.Destroy(texture);
                }
            }

            internal void Dispose()
            {
                if (_cameraObject != null)
                    Object.DestroyImmediate(_cameraObject);
                if (_lightObject != null)
                    Object.DestroyImmediate(_lightObject);
                if (_obstacleSource != null)
                    Object.DestroyImmediate(_obstacleSource);
                if (_target != null)
                    Object.DestroyImmediate(_target);
                if (_lightMaterial != null)
                    Object.DestroyImmediate(_lightMaterial);
                if (_obstacleMaterial != null)
                    Object.DestroyImmediate(_obstacleMaterial);
                if (_overlayMaterial != null)
                    Object.DestroyImmediate(_overlayMaterial);
                if (_ambientMaterial != null)
                    Object.DestroyImmediate(_ambientMaterial);
                if (_blurMaterial != null)
                    Object.DestroyImmediate(_blurMaterial);
            }
        }

        private readonly struct RenderMetrics
        {
            internal readonly PixelMetrics SourceTarget;
            internal readonly PixelMetrics Obstacles;
            internal readonly PixelMetrics LightSources;

            internal RenderMetrics(PixelMetrics sourceTarget, PixelMetrics obstacles, PixelMetrics lightSources)
            {
                SourceTarget = sourceTarget;
                Obstacles = obstacles;
                LightSources = lightSources;
            }

            public override string ToString()
            {
                return "sourceTarget={" + SourceTarget + "}, obstacles={" + Obstacles +
                       "}, lightSources={" + LightSources + "}";
            }
        }

        private readonly struct PixelMetrics
        {
            internal readonly float Range;
            internal readonly float Center;
            internal readonly float CenterAlpha;
            internal readonly float Corner;
            internal readonly float AlphaRange;
            private readonly int _width;
            private readonly int _height;

            internal PixelMetrics(float minimum, float maximum, float center, float centerAlpha, float corner,
                float minimumAlpha, float maximumAlpha, int width, int height)
            {
                Range = maximum - minimum;
                Center = center;
                CenterAlpha = centerAlpha;
                Corner = corner;
                AlphaRange = maximumAlpha - minimumAlpha;
                _width = width;
                _height = height;
            }

            public override string ToString()
            {
                return string.Format("size={0}x{1}, range={2:F3}, center={3:F3}, centerAlpha={4:F3}, " +
                                     "corner={5:F3}, alphaRange={6:F3}",
                    _width, _height, Range, Center, CenterAlpha, Corner, AlphaRange);
            }
        }
    }
}
