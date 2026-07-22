using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Light2D.Tests
{
    /// <summary>
    /// Coverage-behaviour regression tests for the Built-in backend and the <see cref="Light2DVolume"/>
    /// feature. These are the primary verification that the coverage composition is correct: the volume
    /// mask (R1 orientation/precision of the coverage-blend pass) and the spatial "lighting only enables
    /// in the cave" guarantee.
    ///
    /// Composition math being asserted (design 1.2/1.4): the Built-in path ends with
    /// <c>final = lerp(rawGameImage, light2dComposite, coverage)</c>. The fixture camera clears WHITE with
    /// <c>cullingMask = 0</c>, so the raw game image is uniformly bright (~1.0) and reads through wherever a
    /// region is bypassed (coverage 0). The light2d composite is <c>white * lightField</c>: black where no
    /// light reaches, bright near a light source. Therefore:
    ///   - coverage 1 (lighting applies) with NO lights  => composite = black (~0.0)
    ///   - coverage 0 (bypassed)                          => raw game   = white (~1.0)
    ///   - coverage 1 with a light inside                 => composite = bright core, dark rim
    /// The camera is forced onto the Built-in pipeline (SRP torn down + WaitForBuiltInPipeline), matching
    /// BuiltInRenderingTests.RenderFixture; region sampling follows LightAlignmentTests. Deterministic.
    ///
    /// Backward-compat note: with a UNIFORM white game image the _GameTex vertical-flip (R1) is not directly
    /// observable in code (a flip of a uniform texture is a no-op). What IS observable here is the
    /// COMPOSITE/COVERAGE orientation and blend precision - see (a2), which places the light off-centre so
    /// the composite is vertically asymmetric and a flipped composite would swap the top/bottom regions. The
    /// _GameTex flip specifically is covered by the demo scene's non-uniform art (Chunk 6).
    /// </summary>
    public class VolumeCoverageTests
    {
        // (a1) BACKWARD-COMPAT: with zero volumes and DefaultCoverage == 1 the coverage pass is skipped
        // entirely (design 1.3(1)). The skip path must render deterministically AND allocate no coverage
        // render targets - any always-on coverage allocation is a regression (R2).
        [UnityTest, Timeout(60000)]
        public IEnumerator NoVolumesSkipPathIsDeterministicAndAllocatesNoCoverageTextures()
        {
            RenderPipelineAsset oldDefault = GraphicsSettings.defaultRenderPipeline;
            RenderPipelineAsset oldQuality = QualitySettings.renderPipeline;
            CoverageFixture fixture = null;
            try
            {
                QualitySettings.renderPipeline = null;
                GraphicsSettings.defaultRenderPipeline = null;
                yield return WaitForBuiltInPipeline();
                fixture = new CoverageFixture(withLight: true);
                fixture.SetLightWorldPosition(new Vector3(0f, 2.2f, 0f));
                yield return null;

                Color[] first = fixture.Render();

                // Backward-compat allocation guarantee: the skip path never touches the coverage targets.
                (RenderTexture coverage, RenderTexture composite) probed = ProbeCoverageTextures(fixture.Lighting);
                Assert.That(probed.coverage, Is.Null,
                    "The no-volume skip path allocated a CoverageTexture (backward-compat regression, R2).");
                Assert.That(probed.composite, Is.Null,
                    "The no-volume skip path allocated a CompositeTexture (backward-compat regression, R2).");

                // The capture must be a meaningful frame (the off-centre light makes it non-uniform), so the
                // determinism comparison below is not vacuously comparing two flat images.
                Assert.That(fixture.RegionMax(0, 0, 128, 128) - fixture.RegionMin(0, 0, 128, 128),
                    Is.GreaterThan(0.1f), "The skip-path frame was uniform; the light did not composite.");

                Color[] second = fixture.Render();
                Assert.That(MaxAbsGrayDiff(first, second), Is.LessThan(0.02f),
                    "The no-volume skip path did not render deterministically (backward-compat).");

                // Still nothing allocated after re-render.
                probed = ProbeCoverageTextures(fixture.Lighting);
                Assert.That(probed.coverage, Is.Null);
                Assert.That(probed.composite, Is.Null);
            }
            finally
            {
                fixture?.Dispose();
                QualitySettings.renderPipeline = oldQuality;
                GraphicsSettings.defaultRenderPipeline = oldDefault;
            }
        }

        // (a2) BACKWARD-COMPAT + R1: a single full-coverage volume FORCES the coverage pass on (snapshot
        // count > 0) while keeping coverage == 1 everywhere, so the blended result must equal the skip-path
        // composite within a tight epsilon. This isolates the coverage-blend shader (composite/coverage
        // orientation + precision, R1) from the skip optimisation. The light is placed OFF-CENTRE so the
        // reference composite is vertically asymmetric: a flipped _CompositeTex would swap the top/bottom
        // region means and fail here even though a symmetric frame would hide the defect.
        [UnityTest, Timeout(60000)]
        public IEnumerator FullCoverageVolumeMatchesSkipPathReference()
        {
            RenderPipelineAsset oldDefault = GraphicsSettings.defaultRenderPipeline;
            RenderPipelineAsset oldQuality = QualitySettings.renderPipeline;
            CoverageFixture fixture = null;
            try
            {
                QualitySettings.renderPipeline = null;
                GraphicsSettings.defaultRenderPipeline = null;
                yield return WaitForBuiltInPipeline();
                fixture = new CoverageFixture(withLight: true);
                fixture.SetLightWorldPosition(new Vector3(0f, 2.2f, 0f));
                yield return null;

                // Reference = the skip path (no volumes, DefaultCoverage == 1).
                fixture.Render();
                float refTop = fixture.RegionMean(32, 96, 96, 120);
                float refBottom = fixture.RegionMean(32, 8, 96, 32);
                float refCenter = fixture.RegionMean(48, 48, 80, 80);
                float refMean = fixture.RegionMean(0, 0, 128, 128);

                // Orientation guard: the reference MUST be vertically asymmetric, or the top/bottom equality
                // below could not detect a composite flip.
                Assert.That(refTop, Is.GreaterThan(refBottom + 0.1f),
                    "The reference composite is not vertically asymmetric; the orientation check is vacuous. " +
                    "top=" + refTop.ToString("F3") + " bottom=" + refBottom.ToString("F3"));

                (RenderTexture coverage, RenderTexture composite) skipProbe = ProbeCoverageTextures(fixture.Lighting);
                Assert.That(skipProbe.coverage, Is.Null, "Skip path allocated a CoverageTexture.");

                // Force the coverage pass on with a screen-covering, full-coverage global volume.
                fixture.AddGlobalVolume(coverage: 1f, priority: 0);
                yield return null;
                fixture.Render();

                // The coverage pass now ran, so the targets must be allocated (proves the path was taken).
                (RenderTexture coverage, RenderTexture composite) coverProbe = ProbeCoverageTextures(fixture.Lighting);
                Assert.That(coverProbe.coverage, Is.Not.Null,
                    "A full-coverage volume did not force the coverage pass on (no CoverageTexture allocated).");
                Assert.That(coverProbe.composite, Is.Not.Null,
                    "A full-coverage volume did not allocate the CompositeTexture.");

                float covTop = fixture.RegionMean(32, 96, 96, 120);
                float covBottom = fixture.RegionMean(32, 8, 96, 32);
                float covCenter = fixture.RegionMean(48, 48, 80, 80);
                float covMean = fixture.RegionMean(0, 0, 128, 128);

                const float epsilon = 0.02f;
                Assert.That(covTop, Is.EqualTo(refTop).Within(epsilon),
                    "Coverage-blend top region diverged from the skip-path composite (R1 orientation/precision). " +
                    "ref=" + refTop.ToString("F3") + " coverage=" + covTop.ToString("F3"));
                Assert.That(covBottom, Is.EqualTo(refBottom).Within(epsilon),
                    "Coverage-blend bottom region diverged from the skip-path composite (R1). " +
                    "ref=" + refBottom.ToString("F3") + " coverage=" + covBottom.ToString("F3"));
                Assert.That(covCenter, Is.EqualTo(refCenter).Within(epsilon),
                    "Coverage-blend centre region diverged from the skip-path composite (R1). " +
                    "ref=" + refCenter.ToString("F3") + " coverage=" + covCenter.ToString("F3"));
                Assert.That(covMean, Is.EqualTo(refMean).Within(epsilon),
                    "Coverage-blend whole-frame mean diverged from the skip-path composite (R1). " +
                    "ref=" + refMean.ToString("F3") + " coverage=" + covMean.ToString("F3"));
            }
            finally
            {
                fixture?.Dispose();
                QualitySettings.renderPipeline = oldQuality;
                GraphicsSettings.defaultRenderPipeline = oldDefault;
            }
        }

        // (b) COVERAGE - spatial "cave only": DefaultCoverage == 0 makes the world raw white (bypassed); a
        // Box coverage-1 volume over the RIGHT half re-enables Light2D there. With no light sources the
        // covered half composites to black (white * 0) while the bypassed half stays raw white, with a sharp
        // spatial separation. This is the real "only enables in the cave" guarantee.
        [UnityTest, Timeout(60000)]
        public IEnumerator CoverageConfinesLightingToTheVolumeRegion()
        {
            RenderPipelineAsset oldDefault = GraphicsSettings.defaultRenderPipeline;
            RenderPipelineAsset oldQuality = QualitySettings.renderPipeline;
            CoverageFixture fixture = null;
            try
            {
                QualitySettings.renderPipeline = null;
                GraphicsSettings.defaultRenderPipeline = null;
                yield return WaitForBuiltInPipeline();
                fixture = new CoverageFixture(withLight: false);
                fixture.Lighting.DefaultCoverage = 0f; // world default = bypass (raw white)
                yield return null;

                // Box interior world x in [0,4] -> screen right half; sharp edge (tiny blend distance).
                fixture.AddBoxVolume(new Vector2(2f, 0f), new Vector2(4f, 20f),
                    coverage: 1f, priority: 1, blendDistance: 0.05f);
                yield return null;
                fixture.Render();

                float left = fixture.RegionMean(8, 40, 48, 88);    // outside the box -> bypass -> white
                float right = fixture.RegionMean(80, 40, 120, 88); // inside the box, no light -> black

                Assert.That(left, Is.GreaterThan(0.8f),
                    "The bypassed (coverage 0) region did not read as the raw white game image. left=" +
                    left.ToString("F3"));
                Assert.That(right, Is.LessThan(0.2f),
                    "The covered (coverage 1) region was not darkened by Light2D. right=" + right.ToString("F3"));
                Assert.That(left - right, Is.GreaterThan(0.6f),
                    "Coverage did not confine lighting sharply to the volume region. left=" +
                    left.ToString("F3") + " right=" + right.ToString("F3"));
            }
            finally
            {
                fixture?.Dispose();
                QualitySettings.renderPipeline = oldQuality;
                GraphicsSettings.defaultRenderPipeline = oldDefault;
            }
        }

        // (b, variant) A LightSprite inside the covered box: the cave shows a bright core with a dark rim
        // (Light2D applied) while the surrounding world stays raw white (bypassed). Proves the region is
        // genuinely LIT, not merely darkened.
        [UnityTest, Timeout(60000)]
        public IEnumerator LitVolumeShowsBrightCoreWhileWorldStaysRaw()
        {
            RenderPipelineAsset oldDefault = GraphicsSettings.defaultRenderPipeline;
            RenderPipelineAsset oldQuality = QualitySettings.renderPipeline;
            CoverageFixture fixture = null;
            try
            {
                QualitySettings.renderPipeline = null;
                GraphicsSettings.defaultRenderPipeline = null;
                yield return WaitForBuiltInPipeline();
                fixture = new CoverageFixture(withLight: true);
                fixture.Lighting.DefaultCoverage = 0f;      // world = raw white bypass
                fixture.SetLightScale(2f);                  // keep the glow inside the cave
                fixture.SetLightWorldPosition(Vector3.zero);
                yield return null;

                // Cave world x,y in [-2.5, 2.5] -> screen [24,104].
                fixture.AddBoxVolume(Vector2.zero, new Vector2(5f, 5f),
                    coverage: 1f, priority: 1, blendDistance: 0.1f);
                yield return null;
                fixture.Render();

                float coreMax = fixture.RegionMax(58, 58, 70, 70);   // bright torch core inside the cave
                float caveMean = fixture.RegionMean(28, 28, 100, 100); // whole cave: core + dark rim
                float outside = fixture.RegionMean(6, 6, 22, 22);      // corner, clearly outside the cave

                Assert.That(coreMax, Is.GreaterThan(0.35f),
                    "No lit core inside the covered cave; lighting is not being applied there. coreMax=" +
                    coreMax.ToString("F3"));
                Assert.That(outside, Is.GreaterThan(0.8f),
                    "The world outside the cave was not the raw white game image. outside=" +
                    outside.ToString("F3"));
                Assert.That(caveMean, Is.LessThan(outside - 0.15f),
                    "The cave was not darkened relative to the bypassed world (it should be dark except near " +
                    "the torch). caveMean=" + caveMean.ToString("F3") + " outside=" + outside.ToString("F3"));
            }
            finally
            {
                fixture?.Dispose();
                QualitySettings.renderPipeline = oldQuality;
                GraphicsSettings.defaultRenderPipeline = oldDefault;
            }
        }

        // (c) PRIORITY: two overlapping boxes over the same area, one coverage-1 and one coverage-0. The
        // painter mask draws ascending by Priority, so the HIGHER-priority volume wins the overlap. Swapping
        // which volume is higher must flip the overlap between lit-black (coverage 1) and raw-white
        // (coverage 0), proving priority - not registration/entity order or the coverage value itself -
        // decides the outcome.
        [UnityTest, Timeout(60000)]
        public IEnumerator PriorityDeterminesOverlappingCoverage()
        {
            RenderPipelineAsset oldDefault = GraphicsSettings.defaultRenderPipeline;
            RenderPipelineAsset oldQuality = QualitySettings.renderPipeline;
            CoverageFixture fixture = null;
            try
            {
                QualitySettings.renderPipeline = null;
                GraphicsSettings.defaultRenderPipeline = null;
                yield return WaitForBuiltInPipeline();
                fixture = new CoverageFixture(withLight: false);
                fixture.Lighting.DefaultCoverage = 0f; // surroundings = white bypass in both configs
                yield return null;

                // Two coincident boxes over the frame centre (world [-1,1] -> screen [48,80]).
                Light2DVolume coverOn = fixture.AddBoxVolume(Vector2.zero, new Vector2(2f, 2f),
                    coverage: 1f, priority: 1, blendDistance: 0.05f);
                Light2DVolume coverOff = fixture.AddBoxVolume(Vector2.zero, new Vector2(2f, 2f),
                    coverage: 0f, priority: 0, blendDistance: 0.05f);
                yield return null;

                // Config A: coverage-1 has the higher priority -> it wins -> overlap is lit-black.
                fixture.Render();
                float centerA = fixture.RegionMean(56, 56, 72, 72);
                Assert.That(centerA, Is.LessThan(0.2f),
                    "The higher-priority coverage-1 volume did not win the overlap (expected lit-black). center=" +
                    centerA.ToString("F3"));

                // Config B: swap priorities so coverage-0 is higher -> it wins -> overlap is raw-white.
                coverOn.Priority = 0;
                coverOff.Priority = 1;
                yield return null;
                fixture.Render();
                float centerB = fixture.RegionMean(56, 56, 72, 72);
                Assert.That(centerB, Is.GreaterThan(0.8f),
                    "The higher-priority coverage-0 volume did not win the overlap (expected raw-white). center=" +
                    centerB.ToString("F3"));

                Assert.That(centerB - centerA, Is.GreaterThan(0.6f),
                    "Priority did not control the overlap outcome (the two configs should differ strongly). A=" +
                    centerA.ToString("F3") + " B=" + centerB.ToString("F3"));
            }
            finally
            {
                fixture?.Dispose();
                QualitySettings.renderPipeline = oldQuality;
                GraphicsSettings.defaultRenderPipeline = oldDefault;
            }
        }

        // (c) BLEND: a single coverage-1 box with a wide BlendDistance over a coverage-0 world. Sampling a
        // horizontal profile that crosses the box's blend edge must show a MONOTONIC gradient with
        // intermediate values (a soft feather), not a hard 0->1 step. final = lerp(white, black, coverage),
        // so as coverage ramps 1 (interior) -> 0 (exterior) the profile ramps 0 (black) -> 1 (white).
        [UnityTest, Timeout(60000)]
        public IEnumerator BlendDistanceProducesMonotonicSoftEdge()
        {
            RenderPipelineAsset oldDefault = GraphicsSettings.defaultRenderPipeline;
            RenderPipelineAsset oldQuality = QualitySettings.renderPipeline;
            CoverageFixture fixture = null;
            try
            {
                QualitySettings.renderPipeline = null;
                GraphicsSettings.defaultRenderPipeline = null;
                yield return WaitForBuiltInPipeline();
                fixture = new CoverageFixture(withLight: false);
                fixture.Lighting.DefaultCoverage = 0f;
                yield return null;

                // Interior world x in [-1,1] -> screen [48,80]; the right blend band world x in [1,2.5] ->
                // screen [80,104]; exterior x > 2.5 -> screen > 104.
                fixture.AddBoxVolume(Vector2.zero, new Vector2(2f, 20f),
                    coverage: 1f, priority: 1, blendDistance: 1.5f);
                yield return null;
                fixture.Render();

                // Per-column mean over a central horizontal strip (averaging rows suppresses pixel noise).
                float[] profile = fixture.ColumnMeanProfile(56, 72);
                const int start = 66;  // inside the box (interior, black)
                const int end = 120;   // outside the box (exterior, white)

                float interior = profile[start];
                float exterior = profile[end];
                Assert.That(interior, Is.LessThan(0.2f),
                    "The covered interior did not read as lit-black. interior=" + interior.ToString("F3"));
                Assert.That(exterior, Is.GreaterThan(0.8f),
                    "The bypassed exterior did not read as raw white. exterior=" + exterior.ToString("F3"));
                Assert.That(exterior - interior, Is.GreaterThan(0.7f),
                    "The profile did not span interior->exterior across the blend edge.");

                // Monotonic non-decreasing (small tolerance for bilinear/quantisation noise) AND at least one
                // genuinely intermediate sample -> proves a feather, not a hard step.
                const float tolerance = 0.06f;
                float runningMax = float.NegativeInfinity;
                bool hasIntermediate = false;
                for (int c = start; c <= end; c++)
                {
                    float value = profile[c];
                    Assert.That(value, Is.GreaterThanOrEqualTo(runningMax - tolerance),
                        "The coverage edge was not monotonic at column " + c + " (value=" + value.ToString("F3") +
                        ", runningMax=" + runningMax.ToString("F3") + "); the blend reversed direction.");
                    if (value > runningMax)
                        runningMax = value;
                    if (value > 0.25f && value < 0.75f)
                        hasIntermediate = true;
                }

                Assert.That(hasIntermediate, Is.True,
                    "The coverage edge was a hard step (no intermediate values); BlendDistance did not feather.");
            }
            finally
            {
                fixture?.Dispose();
                QualitySettings.renderPipeline = oldQuality;
                GraphicsSettings.defaultRenderPipeline = oldDefault;
            }
        }

        // Reads the LightingSystem's private render-resource holder and returns its coverage/composite
        // targets. The runtime assembly exposes internals to Light2D.EditMode.Tests and Light2D.Editor only
        // (not Light2D.PlayMode.Tests), so the fields are probed by reflection rather than a direct
        // reference - this is the "reflection" branch the design (6a1) sanctions.
        private static (RenderTexture coverage, RenderTexture composite) ProbeCoverageTextures(LightingSystem lighting)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            FieldInfo resourcesField = typeof(LightingSystem).GetField("_resources", flags);
            Assert.That(resourcesField, Is.Not.Null,
                "LightingSystem._resources was not found; the coverage-allocation probe cannot run.");
            object resources = resourcesField.GetValue(lighting);
            if (resources == null)
                return (null, null);

            FieldInfo coverageField = resources.GetType().GetField("CoverageTexture", flags);
            FieldInfo compositeField = resources.GetType().GetField("CompositeTexture", flags);
            Assert.That(coverageField, Is.Not.Null, "LightingRenderResources.CoverageTexture was not found.");
            Assert.That(compositeField, Is.Not.Null, "LightingRenderResources.CompositeTexture was not found.");
            return (coverageField.GetValue(resources) as RenderTexture,
                compositeField.GetValue(resources) as RenderTexture);
        }

        private static float MaxAbsGrayDiff(Color[] a, Color[] b)
        {
            Assert.That(a.Length, Is.EqualTo(b.Length), "Frame captures differ in size.");
            float max = 0f;
            for (int i = 0; i < a.Length; i++)
            {
                float diff = Mathf.Abs(a[i].grayscale - b[i].grayscale);
                if (diff > max)
                    max = diff;
            }
            return max;
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

        // A Built-in coverage test rig: a white-clearing, cullingMask-0 orthographic camera driving a
        // LightingSystem, an optional LightSprite, and a set of Light2DVolumes. Modelled on
        // BuiltInRenderingTests.RenderFixture (composition materials from their shaders, forced Built-in) and
        // LightAlignmentTests region sampling.
        private sealed class CoverageFixture
        {
            private const int TargetSize = 128;

            internal readonly LightingSystem Lighting;

            private readonly GameObject _cameraObject;
            private readonly GameObject _lightObject;
            private readonly Camera _camera;
            private readonly RenderTexture _target;
            private readonly Material _lightMaterial;
            private readonly Material _overlayMaterial;
            private readonly Material _ambientMaterial;
            private readonly Material _blurMaterial;
            private readonly List<GameObject> _volumes = new List<GameObject>();
            private readonly LightSprite _light;

            private Color[] _pixels;
            private int _width;
            private int _height;

            internal CoverageFixture(bool withLight)
            {
                _cameraObject = new GameObject("Coverage Light2D Camera");
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
                // White game image + cullingMask 0: the source camera draws no scene geometry, so bypass
                // regions (coverage 0) read the raw white and lit regions read the composited overlay.
                _camera.backgroundColor = Color.white;
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
                Lighting.EnableAmbientLight = false;
                Lighting.BlurLightSources = false;
                Lighting.BlurAmbientLight = false;
                Lighting.HDR = false;
                Lighting.EnableNormalMapping = false;
                Lighting.LightObstaclesAntialiasing = false;
                Lighting.LightTexturesFilterMode = FilterMode.Bilinear;
                Lighting.LightPixelSize = 0.05f;

                if (withLight)
                {
                    Sprite sprite = Resources.Load<Sprite>("DefaultLight");
                    _lightObject = new GameObject("Coverage Light");
                    _light = _lightObject.AddComponent<LightSprite>();
                    _light.Sprite = sprite;
                    _lightMaterial = new Material(Shader.Find("Light2D/Light 1 Point"));
                    _light.Material = _lightMaterial;
                    _light.Color = Color.white;
                    // Radius sizes the light mesh now (decoupled from Transform scale); Radius = 3 on a
                    // unit-scaled transform preserves the old localScale = 3 geometry.
                    _light.Radius = 3f;
                    _light.enabled = false;
                    _light.enabled = true;
                }

                _cameraObject.SetActive(true);
            }

            internal void SetLightWorldPosition(Vector3 worldPosition)
            {
                if (_lightObject != null)
                    _lightObject.transform.position = worldPosition;
            }

            internal void SetLightScale(float scale)
            {
                // Radius sizes the light mesh now; the transform stays unit-scaled.
                if (_light != null)
                    _light.Radius = scale;
            }

            internal Light2DVolume AddBoxVolume(Vector2 worldPosition, Vector2 size, float coverage, int priority,
                float blendDistance)
            {
                Light2DVolume volume = CreateVolume("Coverage Box Volume");
                volume.transform.position = new Vector3(worldPosition.x, worldPosition.y, 0f);
                volume.Shape = Light2DVolume.VolumeShape.Box;
                volume.Size = size;
                volume.Coverage = coverage;
                volume.Priority = priority;
                volume.BlendDistance = blendDistance;
                return volume;
            }

            internal Light2DVolume AddGlobalVolume(float coverage, int priority)
            {
                Light2DVolume volume = CreateVolume("Coverage Global Volume");
                volume.Shape = Light2DVolume.VolumeShape.Global;
                volume.Coverage = coverage;
                volume.Priority = priority;
                return volume;
            }

            private Light2DVolume CreateVolume(string name)
            {
                var go = new GameObject(name);
                _volumes.Add(go);
                Light2DVolume volume = go.AddComponent<Light2DVolume>();
                // Scope to this system so a stray volume from another fixture can never leak into this frame,
                // and this volume can never leak into another system's snapshot.
                volume.Owner = Lighting;
                return volume;
            }

            // Renders twice (the first render allocates/initialises resources, the second is the measured,
            // steady-state frame) and reads the composited target.
            internal Color[] Render()
            {
                _camera.Render();
                _camera.Render();
                _pixels = ReadTarget();
                _width = _target.width;
                _height = _target.height;
                return _pixels;
            }

            // Mean grayscale over the pixel rectangle [x0,x1) x [y0,y1) (y is bottom-up, matching ReadPixels).
            internal float RegionMean(int x0, int y0, int x1, int y1)
            {
                double sum = 0d;
                int count = 0;
                ForEach(x0, y0, x1, y1, value => { sum += value; count++; });
                return count == 0 ? 0f : (float)(sum / count);
            }

            internal float RegionMax(int x0, int y0, int x1, int y1)
            {
                float max = float.NegativeInfinity;
                ForEach(x0, y0, x1, y1, value => { if (value > max) max = value; });
                return max;
            }

            internal float RegionMin(int x0, int y0, int x1, int y1)
            {
                float min = float.PositiveInfinity;
                ForEach(x0, y0, x1, y1, value => { if (value < min) min = value; });
                return min;
            }

            // For each column, the mean grayscale over rows [y0,y1); index the result by pixel x.
            internal float[] ColumnMeanProfile(int y0, int y1)
            {
                var profile = new float[_width];
                int clampedY0 = Mathf.Clamp(y0, 0, _height);
                int clampedY1 = Mathf.Clamp(y1, 0, _height);
                for (int x = 0; x < _width; x++)
                {
                    double sum = 0d;
                    int count = 0;
                    for (int y = clampedY0; y < clampedY1; y++)
                    {
                        sum += _pixels[y * _width + x].grayscale;
                        count++;
                    }
                    profile[x] = count == 0 ? 0f : (float)(sum / count);
                }
                return profile;
            }

            private void ForEach(int x0, int y0, int x1, int y1, System.Action<float> action)
            {
                int cx0 = Mathf.Clamp(x0, 0, _width);
                int cy0 = Mathf.Clamp(y0, 0, _height);
                int cx1 = Mathf.Clamp(x1, 0, _width);
                int cy1 = Mathf.Clamp(y1, 0, _height);
                for (int y = cy0; y < cy1; y++)
                    for (int x = cx0; x < cx1; x++)
                        action(_pixels[y * _width + x].grayscale);
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
                foreach (GameObject volume in _volumes)
                    if (volume != null)
                        Object.DestroyImmediate(volume);
                _volumes.Clear();
                if (_cameraObject != null)
                    Object.DestroyImmediate(_cameraObject);
                if (_lightObject != null)
                    Object.DestroyImmediate(_lightObject);
                if (_target != null)
                    Object.DestroyImmediate(_target);
                if (_lightMaterial != null)
                    Object.DestroyImmediate(_lightMaterial);
                if (_overlayMaterial != null)
                    Object.DestroyImmediate(_overlayMaterial);
                if (_ambientMaterial != null)
                    Object.DestroyImmediate(_ambientMaterial);
                if (_blurMaterial != null)
                    Object.DestroyImmediate(_blurMaterial);
            }
        }
    }
}
