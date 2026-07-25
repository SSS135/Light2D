using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Light2D.Tests
{
    /// <summary>
    /// Spatial-alignment regression tests for the Built-in backend. Unlike BuiltInRenderingTests, which
    /// only asserts mean-luminance / brightness distribution, these assert that a light at a KNOWN world
    /// position composites at the correct SCREEN pixel, and that the rendered position TRACKS the source
    /// camera when it translates. A light-camera geometry that shifts/scales or mis-tracks the source
    /// camera makes the measured centroid diverge from the WorldToViewportPoint expectation, turning these
    /// red where the brightness-only tests stay green.
    /// </summary>
    public class LightAlignmentTests
    {
        // Composited-frame tolerance in pixels on a 128x128 target. A correct pipeline lands the light
        // within a couple of pixels; a shift/scale/mis-track bug diverges by many pixels.
        private const float PixelTolerance = 5f;

        [UnityTest, Timeout(60000)]
        public IEnumerator LightCompositesAtExpectedScreenPositionAndTracksCameraTranslation()
        {
            RenderPipelineAsset oldDefault = GraphicsSettings.defaultRenderPipeline;
            RenderPipelineAsset oldQuality = QualitySettings.renderPipeline;
            AlignmentFixture fixture = null;
            try
            {
                QualitySettings.renderPipeline = null;
                GraphicsSettings.defaultRenderPipeline = null;
                yield return WaitForBuiltInPipeline();
                fixture = new AlignmentFixture(enableAmbient: false);
                yield return null;

                // Light stays at a fixed world position throughout; only the camera moves.
                var lightWorld = new Vector3(1.2f, 0.9f, 0f);
                fixture.SetLightWorldPosition(lightWorld);

                LightLocation centered = fixture.RenderAndLocateLight(new Vector3(0f, 0f, -10f), lightWorld);
                LightLocation moved = fixture.RenderAndLocateLight(new Vector3(1.8f, -0.4f, -10f), lightWorld);

                Assert.That(centered.Found, Is.True,
                    "No bright light region was found in the centered frame. " + centered);
                Assert.That(moved.Found, Is.True,
                    "No bright light region was found in the camera-moved frame. " + moved);

                // Centered case: the light must composite where the source camera projects its world point.
                Assert.That(centered.Error, Is.LessThan(PixelTolerance),
                    "Centered light is misaligned with its world position. " + centered);

                // Dynamic-drift case: with the camera translated and the light fixed in the world, the
                // rendered centroid must still land on the (new) projected screen position. If the
                // light-camera geometry mis-tracks the source camera, this diverges even when centered passes.
                Assert.That(moved.Error, Is.LessThan(PixelTolerance),
                    "Light did not track the source camera translation. " + moved);

                // Guard against a drift that scales with camera motion: the error must not grow when the
                // camera moves. (A perfectly tracking pipeline keeps both errors near zero.)
                Assert.That(moved.Error, Is.LessThan(centered.Error + PixelTolerance),
                    "Alignment error grew with camera translation (dynamic drift). centered=" + centered +
                    " moved=" + moved);
            }
            finally
            {
                fixture?.Dispose();
                QualitySettings.renderPipeline = oldQuality;
                GraphicsSettings.defaultRenderPipeline = oldDefault;
            }
        }

        /// <summary>
        /// Normal mapping must not move the light. With EnableNormalMapping on, the flat light-sources pass is
        /// replaced by the per-light single-light + normal-modulation pair, and that pair renders through its own
        /// scratch target - one extra render-target hop where a doubly-applied render-texture GPU projection
        /// adjustment cancels the Y flip and stores the light upside down. This test pins the screen position of
        /// a deliberately off-centre light so such a mirror is a hard failure, and reports the mirrored
        /// expectation explicitly when it happens. Built-in is the backend a batchmode host can measure; the URP
        /// normal path shares the same raw-projection convention (both draw through a native command buffer that
        /// applies the adjustment itself) but cannot be measured here (no runtime URP pipeline in batchmode).
        /// </summary>
        [UnityTest, Timeout(60000)]
        public IEnumerator NormalMappedLightCompositesAtExpectedScreenPositionAndIsNotMirrored()
        {
            RenderPipelineAsset oldDefault = GraphicsSettings.defaultRenderPipeline;
            RenderPipelineAsset oldQuality = QualitySettings.renderPipeline;
            AlignmentFixture fixture = null;
            try
            {
                QualitySettings.renderPipeline = null;
                GraphicsSettings.defaultRenderPipeline = null;
                yield return WaitForBuiltInPipeline();
                fixture = new AlignmentFixture(enableAmbient: false, enableNormalMapping: true);
                yield return null;

                // Off-centre in BOTH axes so a horizontal or vertical mirror lands far outside the tolerance.
                var lightWorld = new Vector3(0.8f, 1f, 0f);
                fixture.SetLightWorldPosition(lightWorld);

                LightLocation lit = fixture.RenderAndLocateLight(new Vector3(0f, 0f, -10f), lightWorld);

                Assert.That(lit.Found, Is.True,
                    "No bright light region was found with normal mapping enabled. " + lit);

                // Asserted before the generic alignment check so a flipped light-input render target reports the
                // mirror explicitly instead of a nondescript misalignment.
                var mirrored = new Vector2(lit.Expected.x, fixture.TargetHeightPixels - lit.Expected.y);
                float mirroredError = Vector2.Distance(mirrored, lit.Measured);
                Assert.That(mirroredError, Is.GreaterThan(lit.Error),
                    "Normal-mapped light composited at the vertically MIRRORED screen position (mirror px=(" +
                    mirrored.x.ToString("F1") + "," + mirrored.y.ToString("F1") + ")). " + lit);

                Assert.That(lit.Error, Is.LessThan(PixelTolerance),
                    "Normal-mapped light is misaligned with its world position. " + lit);
            }
            finally
            {
                fixture?.Dispose();
                QualitySettings.renderPipeline = oldQuality;
                GraphicsSettings.defaultRenderPipeline = oldDefault;
            }
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator AmbientFillKeepsNonShadowedRegionAboveDarkFloor()
        {
            RenderPipelineAsset oldDefault = GraphicsSettings.defaultRenderPipeline;
            RenderPipelineAsset oldQuality = QualitySettings.renderPipeline;
            AlignmentFixture fixture = null;
            try
            {
                QualitySettings.renderPipeline = null;
                GraphicsSettings.defaultRenderPipeline = null;
                yield return WaitForBuiltInPipeline();
                fixture = new AlignmentFixture(enableAmbient: true);
                yield return null;

                fixture.SetLightWorldPosition(new Vector3(0f, 0f, 0f));

                // Warm the temporal ambient propagation, then measure. The emitter covers the frame centre,
                // so with no obstacle the centre is a non-shadowed region that must receive ambient fill.
                float centreLuminance = 0f;
                for (int frame = 0; frame < 24; frame++)
                {
                    centreLuminance = fixture.RenderAndMeasureCentreLuminance(new Vector3(0f, 0f, -10f));
                    yield return null;
                }

                Assert.That(centreLuminance, Is.GreaterThan(0.03f),
                    "Ambient light did not fill a non-shadowed region; it rendered near-black " +
                    "(centre luminance=" + centreLuminance.ToString("F4") + "). Ambient fill is missing.");
            }
            finally
            {
                fixture?.Dispose();
                QualitySettings.renderPipeline = oldQuality;
                GraphicsSettings.defaultRenderPipeline = oldDefault;
            }
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ShapedBoxFillKeepsRegionAboveDarkFloor()
        {
            RenderPipelineAsset oldDefault = GraphicsSettings.defaultRenderPipeline;
            RenderPipelineAsset oldQuality = QualitySettings.renderPipeline;
            AlignmentFixture fixture = null;
            try
            {
                QualitySettings.renderPipeline = null;
                GraphicsSettings.defaultRenderPipeline = null;
                yield return WaitForBuiltInPipeline();
                fixture = new AlignmentFixture(enableAmbient: true, useBoxFill: true);
                yield return null;

                // Push the point light off-screen so the frame centre is lit ONLY by the procedural box fill,
                // not by the light source. A working shaped fill still raises the centre above the dark floor.
                fixture.SetLightWorldPosition(new Vector3(1000f, 1000f, 0f));

                // Warm the temporal ambient propagation, then measure the centre of the box interior.
                float centreLuminance = 0f;
                for (int frame = 0; frame < 24; frame++)
                {
                    centreLuminance = fixture.RenderAndMeasureCentreLuminance(new Vector3(0f, 0f, -10f));
                    yield return null;
                }

                Assert.That(centreLuminance, Is.GreaterThan(0.03f),
                    "The shaped Box fill did not raise its region above the dark floor; it rendered near-black " +
                    "(centre luminance=" + centreLuminance.ToString("F4") + "). Procedural ambient fill is missing.");
            }
            finally
            {
                fixture?.Dispose();
                QualitySettings.renderPipeline = oldQuality;
                GraphicsSettings.defaultRenderPipeline = oldDefault;
            }
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

        private readonly struct LightLocation
        {
            internal readonly bool Found;
            internal readonly Vector2 Expected;
            internal readonly Vector2 Measured;
            internal readonly float MaxLuminance;
            internal readonly Vector3 CameraPosition;

            internal LightLocation(bool found, Vector2 expected, Vector2 measured, float maxLuminance,
                Vector3 cameraPosition)
            {
                Found = found;
                Expected = expected;
                Measured = measured;
                MaxLuminance = maxLuminance;
                CameraPosition = cameraPosition;
            }

            internal float Error { get { return Vector2.Distance(Expected, Measured); } }

            public override string ToString()
            {
                return string.Format(
                    "cameraPos={0}, expectedPx=({1:F1},{2:F1}), measuredPx=({3:F1},{4:F1}), errorPx={5:F2}, maxLum={6:F3}",
                    CameraPosition, Expected.x, Expected.y, Measured.x, Measured.y, Error, MaxLuminance);
            }
        }

        private sealed class AlignmentFixture
        {
            private const int TargetSize = 128;

            internal readonly LightingSystem Lighting;

            // Frame height in pixels, so a test can express the vertically MIRRORED expectation
            // (height - expectedY) that a flipped light-input render target would produce.
            internal int TargetHeightPixels { get { return TargetSize; } }

            private readonly GameObject _cameraObject;
            private readonly GameObject _lightObject;
            private readonly GameObject _ambientObject;
            private readonly Camera _camera;
            private readonly RenderTexture _target;
            private readonly Material _lightMaterial;
            private readonly Material _overlayMaterial;
            private readonly Material _ambientMaterial;
            private readonly Material _emitterMaterial;
            private readonly Material _blurMaterial;

            internal AlignmentFixture(bool enableAmbient, bool useBoxFill = false,
                bool enableNormalMapping = false)
            {
                _cameraObject = new GameObject("Alignment Light2D Camera");
                _cameraObject.SetActive(false);
                _camera = _cameraObject.AddComponent<Camera>();
                _camera.enabled = false;
                _camera.orthographic = true;
                _camera.orthographicSize = 4f;
                _camera.aspect = 1f;
                _camera.nearClipPlane = 0.1f;
                _camera.farClipPlane = 100f;
                _camera.transform.position = new Vector3(0f, 0f, -10f);
                _camera.clearFlags = CameraClearFlags.SolidColor;
                // A dim, uniform game image: where the light field is ~0 the composite is ~0 (dark), and the
                // light blob is the brightest region, so its centroid is a clean measurement of screen position.
                _camera.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);
                _camera.cullingMask = 0;
                _target = new RenderTexture(TargetSize, TargetSize, 0, RenderTextureFormat.ARGB32);
                _target.Create();
                _camera.targetTexture = _target;

                Lighting = _cameraObject.AddComponent<LightingSystem>();
                _overlayMaterial = new Material(Shader.Find("Light2D/Light Overlay"));
                _ambientMaterial = new Material(Shader.Find("Light2D/Ambient Light Computer"));
                _blurMaterial = new Material(Shader.Find("Light2D/Fast Blur"));
                Lighting.AmbientLightComputeMaterial = _ambientMaterial;
                Lighting.LightOverlayMaterial = _overlayMaterial;
                Lighting.LightSourcesBlurMaterial = _blurMaterial;
                Lighting.AmbientLightBlurMaterial = _blurMaterial;
                Lighting.EnableAmbientLight = enableAmbient;
                Lighting.BlurLightSources = false;
                Lighting.BlurAmbientLight = false;
                Lighting.HDR = false;
                Lighting.EnableNormalMapping = enableNormalMapping;
                Lighting.LightObstaclesAntialiasing = false;
                Lighting.LightTexturesFilterMode = FilterMode.Bilinear;
                Lighting.LightPixelSize = 0.05f;

                Sprite sprite = Resources.Load<Sprite>("DefaultLight");
                _lightObject = new GameObject("Alignment Light");
                var light = _lightObject.AddComponent<LightSprite>();
                light.Sprite = sprite;
                _lightMaterial = new Material(Shader.Find("Light2D/Light 1 Point"));
                light.Material = _lightMaterial;
                light.Color = Color.white;
                // Radius sizes the light mesh directly now (decoupled from the Transform scale); the
                // old localScale = 3 becomes Radius = 3 and produces the same mesh geometry.
                light.Radius = 3f;
                light.enabled = false;
                light.enabled = true;

                if (enableAmbient)
                {
                    _ambientObject = new GameObject("Alignment Ambient Emitter");
                    var emitter = _ambientObject.AddComponent<LightAmbientEmitter>();
                    emitter.EmissionColor = Color.white;
                    _emitterMaterial = new Material(Shader.Find("Light2D/Transparent Dual Color Normal Mapped"));
                    emitter.Material = _emitterMaterial;
                    if (useBoxFill)
                    {
                        // Procedural shaped-fill emitter: a feathered box centred on the frame, drawn straight
                        // into the ambient emission texture with no mirrored sprite.
                        emitter.Fill = LightAmbientEmitter.FillShape.Box;
                        emitter.FillSize = new Vector2(6f, 6f);
                        emitter.FillBlendDistance = 1f;
                    }
                    else
                    {
                        emitter.Sprite = sprite;
                        _ambientObject.transform.localScale = Vector3.one * 8f;
                    }
                    emitter.enabled = false;
                    emitter.enabled = true;
                }

                _cameraObject.SetActive(true);
            }

            internal void SetLightWorldPosition(Vector3 worldPosition)
            {
                _lightObject.transform.position = worldPosition;
            }

            internal LightLocation RenderAndLocateLight(Vector3 cameraPosition, Vector3 lightWorldPosition)
            {
                _camera.transform.position = cameraPosition;
                Vector3 viewport = _camera.WorldToViewportPoint(lightWorldPosition);
                var expected = new Vector2(viewport.x * _target.width, viewport.y * _target.height);

                // Two renders: the first allocates/initialises resources, the second is the measured frame.
                _camera.Render();
                _camera.Render();

                bool found = TryLocateBrightCentroid(out Vector2 measured, out float maxLuminance);
                return new LightLocation(found, expected, measured, maxLuminance, cameraPosition);
            }

            internal float RenderAndMeasureCentreLuminance(Vector3 cameraPosition)
            {
                _camera.transform.position = cameraPosition;
                _camera.Render();
                return MeasureCentreLuminance();
            }

            // Weighted centroid of the bright core (pixels above half the peak luminance). The DefaultLight
            // glow is radially symmetric, so the core centroid coincides with the light's true centre and is
            // robust to faint-tail clipping near the frame edge.
            private bool TryLocateBrightCentroid(out Vector2 centroid, out float maxLuminance)
            {
                Color[] pixels = ReadTarget();
                int width = _target.width;
                int height = _target.height;

                maxLuminance = 0f;
                for (int i = 0; i < pixels.Length; i++)
                {
                    float luminance = pixels[i].grayscale;
                    if (luminance > maxLuminance)
                        maxLuminance = luminance;
                }

                centroid = Vector2.zero;
                if (maxLuminance <= 0.05f)
                    return false;

                float threshold = maxLuminance * 0.5f;
                double sumX = 0d;
                double sumY = 0d;
                double sumWeight = 0d;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float luminance = pixels[y * width + x].grayscale;
                        if (luminance < threshold)
                            continue;
                        double weight = luminance - threshold;
                        sumX += (x + 0.5d) * weight;
                        sumY += (y + 0.5d) * weight;
                        sumWeight += weight;
                    }
                }

                if (sumWeight <= 0d)
                    return false;
                centroid = new Vector2((float)(sumX / sumWeight), (float)(sumY / sumWeight));
                return true;
            }

            private float MeasureCentreLuminance()
            {
                Color[] pixels = ReadTarget();
                int width = _target.width;
                int height = _target.height;
                // Average a small window around the frame centre to reduce single-pixel noise.
                double sum = 0d;
                int count = 0;
                for (int y = height / 2 - 3; y <= height / 2 + 3; y++)
                {
                    for (int x = width / 2 - 3; x <= width / 2 + 3; x++)
                    {
                        sum += pixels[y * width + x].grayscale;
                        count++;
                    }
                }
                return (float)(sum / count);
            }

            private Color[] ReadTarget()
            {
                RenderTexture oldActive = RenderTexture.active;
                var texture = new Texture2D(_target.width, _target.height, TextureFormat.RGBA32, false);
                try
                {
                    RenderTexture.active = _target;
                    texture.ReadPixels(new Rect(0, 0, _target.width, _target.height), 0, 0);
                    texture.Apply();
                    return texture.GetPixels();
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
                if (_ambientObject != null)
                    Object.DestroyImmediate(_ambientObject);
                if (_target != null)
                    Object.DestroyImmediate(_target);
                if (_lightMaterial != null)
                    Object.DestroyImmediate(_lightMaterial);
                if (_overlayMaterial != null)
                    Object.DestroyImmediate(_overlayMaterial);
                if (_ambientMaterial != null)
                    Object.DestroyImmediate(_ambientMaterial);
                if (_emitterMaterial != null)
                    Object.DestroyImmediate(_emitterMaterial);
                if (_blurMaterial != null)
                    Object.DestroyImmediate(_blurMaterial);
            }
        }
    }
}
