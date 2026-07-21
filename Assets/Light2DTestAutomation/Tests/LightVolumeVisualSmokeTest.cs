using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using Light2D.TestProject.Editor;
#endif

namespace Light2D.TestProject.Tests
{
    // Spatial visual regression for the authored "Light Volume" sample scene (design §7). The scene is a
    // large bright world (LightingSystem.DefaultCoverage = 0, so the world baseline shows the raw, full
    // brightness game image) with a small dark cave carved out by a Shape=Box, Coverage=1 Light2DVolume and
    // lit only by two torches inside it. Unlike a mean-only gate this test asserts a SPATIAL property: the
    // bypassed WORLD region (left of the volume) samples bright while the covered CAVE region (inside the
    // volume) samples dark-with-a-bright-core. A bright cave or a dark world fails the gate — that is the
    // coverage regression guard, not a threshold to loosen. The PNG artifact is written before any assertion
    // so a failing run still leaves an image for review. The scene asset is produced by
    // LightVolumeDemoBuilder; run "Light2D/Samples/Build Light Volume Scene" (or -executeMethod
    // Light2D.Examples.LightVolumeDemoBuilder.Build) before this test.
    public class LightVolumeVisualSmokeTest
    {
        private const int TargetWidth = 640;
        private const int TargetHeight = 360;
        private const int WarmupFrames = 5;

        // Ambient light converges iteratively; accelerate it through the sample API and capture once the
        // rendered mean luminance stabilises (two consecutive small deltas) or a hard frame cap.
        private const int AmbientSampleIntervalFrames = 30;
        private const int MaxAmbientSettleFrames = 600;
        private const float AmbientConvergenceDelta = 0.005f;
        private const int AmbientCyclesPerFrameRequest = 8;
        private const int ConvergedSampleStreak = 2;

        // Pixel regions sampled from the 640x360 capture. These rectangles are COUPLED to the scene layout
        // authored by LightVolumeDemoBuilder (orthographic size 5 -> 36 px/world-unit; the cave box is a
        // 5x5 region centred at world (5, 0), i.e. box pixels x[410,590] y[90,270]). The world rect stays
        // fully left of the box (raw bright); the cave rect stays inside the box, with the warm torch at the
        // cave-rect centre so its bright core lands in the central-third max.
        private static readonly RectInt WorldRegion = new RectInt(20, 40, 310, 280);
        private static readonly RectInt CaveRegion = new RectInt(425, 105, 150, 150);

        // Spatial gates. The bypassed world is raw-bright; the covered cave is mostly dark with a bright
        // torch core. Do NOT loosen these to accommodate a rendering regression.
        private const float WorldMinMeanLuminance = 0.6f;
        private const float CaveMaxMeanLuminance = 0.25f;
        private const float CaveMinCoreLuminance = 0.4f;

        [UnityTest, Timeout(240000)]
        public IEnumerator LightVolumeSceneRendersBrightWorldAndDarkCave()
        {
#if UNITY_EDITOR
            IEnumerator run = SampleTestSupport.RunSampleScene(
                SampleAutomation.LightVolumeSceneName, TargetWidth, TargetHeight, WarmupFrames,
                AssertVolumeVisualState, SettleAmbientLight);
            try
            {
                while (run.MoveNext())
                    yield return run.Current;
            }
            finally
            {
                (run as System.IDisposable)?.Dispose();
            }
#else
            Assert.Ignore("The Light Volume visual smoke test loads scenes through EditorSceneManager and " +
                          "requires the Unity editor.");
            yield break;
#endif
        }

#if UNITY_EDITOR
        // Adaptive ambient convergence: sample the rendered mean luminance periodically and stop once it is
        // stable or at the frame cap; the full curve is logged so a bright plateau proves steady-state
        // emission rather than insufficient settling.
        private static IEnumerator SettleAmbientLight(SampleTestSupport.SampleSceneRunContext context)
        {
            var previousMean = float.MaxValue;
            var stableSamples = 0;
            var settledFrames = 0;
            var curve = new System.Text.StringBuilder("Light Volume ambient convergence (frame, meanLum):");
            while (settledFrames < MaxAmbientSettleFrames && stableSamples < ConvergedSampleStreak)
            {
                for (var frame = 0; frame < AmbientSampleIntervalFrames; frame++)
                {
                    context.Lighting.LoopAmbientLight(AmbientCyclesPerFrameRequest);
                    yield return null;
                }

                settledFrames += AmbientSampleIntervalFrames;
                float mean = MeasureMeanLuminance(context.Target);
                curve.Append($" ({settledFrames}, {mean:F4})");
                stableSamples = Mathf.Abs(mean - previousMean) < AmbientConvergenceDelta ? stableSamples + 1 : 0;
                previousMean = mean;
            }

            Debug.Log(curve + " -> " +
                      (stableSamples >= ConvergedSampleStreak ? "converged" : "hit frame cap") +
                      $" after {settledFrames} frames.");
        }

        private static float MeasureMeanLuminance(RenderTexture target)
        {
            Texture2D capture = SampleTestSupport.CaptureTexture(target);
            try
            {
                Color[] pixels = capture.GetPixels();
                var luminanceSum = 0f;
                foreach (Color pixel in pixels)
                    luminanceSum += pixel.grayscale;
                return pixels.Length > 0 ? luminanceSum / pixels.Length : 0f;
            }
            finally
            {
                Object.Destroy(capture);
            }
        }

        private static void AssertVolumeVisualState(SampleTestSupport.SampleSceneRunContext context)
        {
            Texture2D capture = SampleTestSupport.CaptureTexture(context.Target);
            try
            {
                string artifactPath = WriteArtifact(capture);
                Debug.Log($"Wrote the Light Volume visual smoke artifact to '{artifactPath}'.");

                Color[] pixels = capture.GetPixels();
                VisualStatistics worldStats = ComputeRegionStatistics(pixels, capture.width, capture.height, WorldRegion);
                VisualStatistics caveStats = ComputeRegionStatistics(pixels, capture.width, capture.height, CaveRegion);
                Debug.Log("Light Volume world-region statistics: " + worldStats);
                Debug.Log("Light Volume cave-region statistics: " + caveStats);

                // The bypassed world (Coverage = 0) shows the raw bright game image.
                Assert.That(worldStats.MeanLuminance, Is.GreaterThan(WorldMinMeanLuminance),
                    "The bypassed world region should read bright (raw game image); a dim world means coverage " +
                    "is being applied where DefaultCoverage = 0 should pass the raw image through. " + worldStats);

                // The covered cave (Coverage = 1) is mostly dark: Light2D applies and there is little light
                // away from the torches.
                Assert.That(caveStats.MeanLuminance, Is.LessThan(CaveMaxMeanLuminance),
                    "The covered cave region should read dark overall; a bright cave means the coverage volume " +
                    "is not enabling Light2D over the region or a torch is flooding it. " + caveStats);

                // ...but a torch keeps a bright core inside the cave, proving local lighting is present.
                Assert.That(caveStats.CentralThirdMaxLuminance, Is.GreaterThan(CaveMinCoreLuminance),
                    "The cave should keep a bright torch core in its centre; a missing core means the local " +
                    "lights are not being composited inside the coverage region. " + caveStats);
            }
            finally
            {
                Object.Destroy(capture);
            }
        }

        // Crops a rectangular region out of the full capture and reuses SampleTestSupport.ComputeStatistics
        // so the region gates use the same luminance model as the other sample visual tests.
        private static VisualStatistics ComputeRegionStatistics(Color[] pixels, int width, int height, RectInt region)
        {
            Assert.That(region.xMin, Is.GreaterThanOrEqualTo(0));
            Assert.That(region.yMin, Is.GreaterThanOrEqualTo(0));
            Assert.That(region.xMax, Is.LessThanOrEqualTo(width));
            Assert.That(region.yMax, Is.LessThanOrEqualTo(height));

            var regionPixels = new Color[region.width * region.height];
            for (var y = 0; y < region.height; y++)
            {
                for (var x = 0; x < region.width; x++)
                    regionPixels[y * region.width + x] = pixels[(region.yMin + y) * width + (region.xMin + x)];
            }

            return SampleTestSupport.ComputeStatistics(regionPixels, region.width, region.height);
        }

        private static string WriteArtifact(Texture2D capture)
        {
            string directory = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "TestResults", "Light2DSampleVisual"));
            Directory.CreateDirectory(directory);
            string artifactPath = Path.Combine(directory, "light-volume-smoke.png");
            File.WriteAllBytes(artifactPath, capture.EncodeToPNG());
            return artifactPath;
        }
#endif
    }
}
