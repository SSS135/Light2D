using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using Light2D.Examples;
using Light2D.TestProject.Editor;
#endif

namespace Light2D.TestProject.Tests
{
    // Statistical visual regression for the baked static "Rocket Tilemap" sample scene (Phase 2, §5.2).
    // Unlike the procedural RocketVisualSmokeTest, this scene is authored once by RocketTilemapBaker and
    // is pre-framed on a rock-dense cave pocket with a bright central light and two distinctly coloured
    // crystal lights, so the test needs no MapProbe, candidate search, or relocation: it just loads the
    // scene, lets ambient light settle, and asserts the same dark-cave gate the Rocket reference uses
    // (a mostly dark cave, a small bright fraction, high contrast, at least two hue clusters, and a
    // bright peak in the central third). This test has NO tilemap dependency and is NOT gated on
    // LIGHT2D_TILEMAP — it only loads a scene and reads pixels; the baked scene's obstacle component is
    // already serialised. The PNG artifact is written before any assertion so a failing run still leaves
    // an image for review. A bright or rockless capture signals a rendering regression, not a threshold
    // to loosen.
    public class RocketTilemapVisualSmokeTest
    {
        private const int TargetWidth = 640;
        private const int TargetHeight = 360;
        // A short warmup lets the scene's Awake/Start run and the first frames render; the ship is then
        // frozen at the start of the scenario phase so the long ambient settle and the capture stay
        // deterministic (a physics-driven ship would move out of frame and make the capture non-repeatable).
        private const int WarmupFrames = 5;
        // A few frames after freezing let physics register the kinematic state and the tilemap collider
        // generate its composite geometry before the no-wall-overlap check reads it.
        private const int FreezeSettleFrames = 5;
        // Ambient light converges iteratively; accelerate it through the sample API and capture once the
        // rendered mean luminance stabilises (two consecutive small deltas) or a hard frame cap.
        private const int AmbientSampleIntervalFrames = 30;
        private const int MaxAmbientSettleFrames = 900;
        private const float AmbientConvergenceDelta = 0.005f;
        private const int AmbientCyclesPerFrameRequest = 8;
        private const int ConvergedSampleStreak = 2;
        private const float MinHueBucketFraction = 0.005f;

        [UnityTest, Timeout(240000)]
        public IEnumerator RocketTilemapSceneRendersDarkCaveWithColoredLights()
        {
#if UNITY_EDITOR
            IEnumerator run = SampleTestSupport.RunSampleScene(
                SampleAutomation.RocketTilemapSceneName, TargetWidth, TargetHeight, WarmupFrames,
                AssertTilemapVisualState, FreezeShipThenSettle);
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
            Assert.Ignore("The Rocket Tilemap visual smoke test loads scenes through EditorSceneManager and " +
                          "requires the Unity editor.");
            yield break;
#endif
        }

#if UNITY_EDITOR
        // Freezes the flyable ship at its spawn (disables its input/thrust, makes every body kinematic, and
        // pins the camera) so the physics-driven craft cannot drift out of frame, THEN settles the ambient
        // light. Freezing before the settle is what keeps the visual capture deterministic and repeatable.
        private static IEnumerator FreezeShipThenSettle(SampleTestSupport.SampleSceneRunContext context)
        {
            FreezeScene(context);
            for (var frame = 0; frame < FreezeSettleFrames; frame++)
                yield return null;

            IEnumerator settle = SettleAmbientLight(context);
            while (settle.MoveNext())
                yield return settle.Current;
        }

        // Stops the scripted intro (which would otherwise flip the craft to Dynamic after one second),
        // cancels input-driven thrust, makes every craft body kinematic with zeroed velocities, and pins the
        // camera on the spawn so nothing moves during the settle/capture.
        private static void FreezeScene(SampleTestSupport.SampleSceneRunContext context)
        {
            Spacecraft spacecraft = Object.FindAnyObjectByType<Spacecraft>();
            Assert.That(spacecraft, Is.Not.Null, "The Rocket Tilemap scene has no active Spacecraft.");
            Assert.That(spacecraft.MainRigidbody, Is.Not.Null, "The Spacecraft has no MainRigidbody assigned.");

            InputManager inputManager = Object.FindAnyObjectByType<InputManager>();
            if (inputManager != null)
            {
                inputManager.StopAllCoroutines();
                inputManager.enabled = false;
            }

            foreach (RocketEngine engine in spacecraft.GetComponentsInChildren<RocketEngine>(true))
            {
                engine.ForcePercent = 0f;
                engine.IsEnabled = false;
            }

            foreach (Rigidbody2D body in spacecraft.GetComponentsInChildren<Rigidbody2D>(true))
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.bodyType = RigidbodyType2D.Kinematic;
            }

            CameraFollower follower = Object.FindAnyObjectByType<CameraFollower>();
            if (follower != null)
                follower.enabled = false;

            Vector2 focus = spacecraft.MainRigidbody.position;
            Transform cameraTransform = context.SourceCamera.transform;
            Vector3 cameraPosition = cameraTransform.position;
            cameraTransform.position = new Vector3(focus.x, focus.y, cameraPosition.z);
        }

        // Playability guard: the ship must spawn in an OPEN pocket, not wedged inside a cave wall. Every ship
        // collider is checked against the tilemap's composite wall collider; any overlap fails the test
        // (catches "spawned inside a wall").
        private static void AssertShipClearOfWalls()
        {
            Spacecraft spacecraft = Object.FindAnyObjectByType<Spacecraft>();
            Assert.That(spacecraft, Is.Not.Null, "The Rocket Tilemap scene has no active Spacecraft.");

            var wallCollider = (Collider2D)Object.FindAnyObjectByType<CompositeCollider2D>();
            Assert.That(wallCollider, Is.Not.Null,
                "The tilemap cave has no CompositeCollider2D; the ship would have no walls to collide with.");

            foreach (Collider2D shipCollider in spacecraft.GetComponentsInChildren<Collider2D>(true))
            {
                if (!shipCollider.enabled)
                    continue;

                ColliderDistance2D separation = Physics2D.Distance(shipCollider, wallCollider);
                Assert.That(separation.isOverlapped, Is.False,
                    $"The ship spawned overlapping a cave wall: collider '{shipCollider.name}' overlaps the " +
                    $"tilemap walls (overlap depth {-separation.distance:F3}). The spawn pocket is not clear.");
            }
        }

        // Adaptive ambient convergence: sample the rendered mean luminance periodically and stop once it
        // is stable or at the frame cap; the full curve is logged so a bright plateau proves steady-state
        // emission rather than insufficient settling.
        private static IEnumerator SettleAmbientLight(SampleTestSupport.SampleSceneRunContext context)
        {
            var previousMean = float.MaxValue;
            var stableSamples = 0;
            var settledFrames = 0;
            var curve = new System.Text.StringBuilder("Rocket Tilemap ambient convergence (frame, meanLum):");
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

        private static void AssertTilemapVisualState(SampleTestSupport.SampleSceneRunContext context)
        {
            // Playability first: the frozen ship must not be spawned inside a wall.
            AssertShipClearOfWalls();

            Texture2D capture = SampleTestSupport.CaptureTexture(context.Target);
            try
            {
                string artifactPath = WriteArtifact(capture);
                Debug.Log($"Wrote the Rocket Tilemap visual smoke artifact to '{artifactPath}'.");

                Color[] pixels = capture.GetPixels();
                VisualStatistics statistics =
                    SampleTestSupport.ComputeStatistics(pixels, capture.width, capture.height);
                Debug.Log("Rocket Tilemap visual statistics: " + statistics);

                int brightRegions = SampleTestSupport.CountBrightRegions(
                    pixels, capture.width, capture.height, SampleTestSupport.BrightLuminanceThreshold);
                Debug.Log($"Rocket Tilemap visual connected bright-region count (log-only): {brightRegions}");

                // The same dark-cave gate as the Rocket reference: the occluding tilemap keeps the frame
                // dark while the colored crystal lights and the central light carve bright, contrasty pools.
                Assert.That(statistics.MeanLuminance, Is.InRange(0.015f, 0.2f),
                    "The tilemap cave shot should stay dark; a mean above 0.2 means the occluding tiles are " +
                    "not darkening the frame or a light is flooding it. " + statistics);
                Assert.That(statistics.BrightFraction, Is.GreaterThan(0.0001f).And.LessThan(0.12f),
                    "Bright pixels (light cores, lit rock faces) should exist but stay a limited fraction of " +
                    "the frame. " + statistics);
                Assert.That(statistics.LuminanceRange, Is.GreaterThan(0.4f),
                    "The frame should span from dark occluded rock to bright light cores. " + statistics);
                Assert.That(statistics.LuminanceStdDev, Is.GreaterThan(0.03f),
                    "The frame should not be uniform. " + statistics);
                Assert.That(statistics.CountHueBucketsAtOrAbove(MinHueBucketFraction), Is.GreaterThanOrEqualTo(2),
                    "At least two distinct hue clusters are expected from the differently coloured crystal " +
                    "lights. Histogram: " + statistics.FormatHueHistogram() + ". " + statistics);
                Assert.That(statistics.CentralThirdMaxLuminance, Is.GreaterThan(0.3f),
                    "The central light should keep a bright peak within the central third of the frame. " +
                    statistics);
            }
            finally
            {
                Object.Destroy(capture);
            }
        }

        private static string WriteArtifact(Texture2D capture)
        {
            string directory = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "TestResults", "Light2DSampleVisual"));
            Directory.CreateDirectory(directory);
            string artifactPath = Path.Combine(directory, "rocket-tilemap-visual-smoke.png");
            File.WriteAllBytes(artifactPath, capture.EncodeToPNG());
            return artifactPath;
        }
#endif
    }
}
