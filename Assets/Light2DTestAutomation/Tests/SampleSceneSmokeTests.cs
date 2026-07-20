using System.Collections;
using Light2D.Examples;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using Light2D.TestProject.Editor;
#endif

namespace Light2D.TestProject.Tests
{
    // Frame-advance smoke tests: both imported sample scenes must run under the Built-in
    // pipeline for several seconds of deterministic game time without unexpected errors while
    // the lighting pipeline keeps its resources alive. The Rocket craft is teleported onto a
    // real cave floor early (its randomized spawn clearing can lack a reachable floor, and an
    // endlessly free-falling craft is not a representative smoke state). No input is ever
    // injected because the sample InputManager would reload the scene through build settings
    // it is not part of.
    public class SampleSceneSmokeTests
    {
        private const int TargetWidth = 256;
        private const int TargetHeight = 144;
        private const int SmallExamplesFrameCount = 120;
        // The craft turns dynamic after one second of game time (frame 60 at the fixed 1/60 s
        // capture delta); the teleport happens shortly after, then the craft lands and rests.
        private const int RocketWarmupFrames = 90;
        private const int RocketSettleFrames = 150;
        private const int LandingSearchExtent = 288;

        [UnityTest, Timeout(120000)]
        public IEnumerator SmallExamplesSceneAdvancesWithoutUnexpectedErrors()
        {
#if UNITY_EDITOR
            IEnumerator run = SampleTestSupport.RunSampleScene(
                SampleAutomation.SmallExamplesSceneName, TargetWidth, TargetHeight, SmallExamplesFrameCount,
                AssertStandardSmokeState);
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
            Assert.Ignore("Sample smoke tests load scenes through EditorSceneManager and require the Unity editor.");
            yield break;
#endif
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator RocketSceneAdvancesWithoutUnexpectedErrors()
        {
#if UNITY_EDITOR
            IEnumerator run = SampleTestSupport.RunSampleScene(
                SampleAutomation.RocketSceneName, TargetWidth, TargetHeight, RocketWarmupFrames,
                AssertRocketSmokeState, RelocateRocketOntoCaveFloor);
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
            Assert.Ignore("Sample smoke tests load scenes through EditorSceneManager and require the Unity editor.");
            yield break;
#endif
        }

#if UNITY_EDITOR
        private static IEnumerator RelocateRocketOntoCaveFloor(SampleTestSupport.SampleSceneRunContext context)
        {
            MapGenerator generator = Object.FindAnyObjectByType<MapGenerator>();
            Assert.That(generator, Is.Not.Null, "The Rocket scene has no active MapGenerator.");
            Spacecraft spacecraft = Object.FindAnyObjectByType<Spacecraft>();
            Assert.That(spacecraft, Is.Not.Null, "The Rocket scene has no active Spacecraft.");

            var map = new SampleTestSupport.MapProbe(generator);
            Assert.That(SampleTestSupport.TryFindNearestLandingPocket(
                    map, spacecraft.MainRigidbody.position, LandingSearchExtent, out Vector2 pocket), Is.True,
                $"No landing pocket found within {LandingSearchExtent} units of spawn for map seed {generator.Seed}.");
            Debug.Log($"Rocket smoke landing pocket: {pocket} (map seed {generator.Seed}).");
            SampleTestSupport.TeleportSpacecraft(spacecraft, pocket);
            SampleTestSupport.SnapCamera(context.SourceCamera.transform, pocket);

            for (var frame = 0; frame < RocketSettleFrames; frame++)
                yield return null;
        }

        private static void AssertStandardSmokeState(SampleTestSupport.SampleSceneRunContext context)
        {
            Assert.That(context.Lighting != null && context.Lighting.isActiveAndEnabled, Is.True,
                "The LightingSystem did not stay active through the frame advancement.");
            Assert.That(Object.FindObjectsByType<LightingSystem>(FindObjectsSortMode.None), Has.Length.EqualTo(1),
                "The scene no longer has exactly one active LightingSystem after the frame advancement.");
            Assert.That(context.Lighting.IsRenderingReady, Is.True,
                "The LightingSystem is no longer ready to render (overlay material lost or pipeline unsupported).");
            Assert.That(context.Lighting.ObstaclesTexture, Is.Not.Null,
                "The obstacle texture was not allocated during rendering.");
            Assert.That(context.Lighting.LightSourcesTexture, Is.Not.Null,
                "The light source texture was not allocated during rendering.");
            Assert.That(context.Lighting.AmbientTexture, Is.Not.Null,
                "The ambient texture was not allocated during rendering.");

            var enabledLightSprites = 0;
            foreach (LightSprite lightSprite in Object.FindObjectsByType<LightSprite>(FindObjectsSortMode.None))
            {
                if (lightSprite.isActiveAndEnabled)
                    enabledLightSprites++;
            }

            Assert.That(enabledLightSprites, Is.GreaterThanOrEqualTo(1),
                "The sample scene has no enabled LightSprite after the frame advancement.");
        }

        private static void AssertRocketSmokeState(SampleTestSupport.SampleSceneRunContext context)
        {
            AssertStandardSmokeState(context);

            Spacecraft spacecraft = Object.FindAnyObjectByType<Spacecraft>();
            Assert.That(spacecraft, Is.Not.Null, "The Rocket scene has no active Spacecraft.");
            Assert.That(spacecraft.MainRigidbody, Is.Not.Null, "The Spacecraft has no main rigidbody assigned.");
            Assert.That(spacecraft.MainRigidbody.bodyType, Is.EqualTo(RigidbodyType2D.Dynamic),
                "The Spacecraft rigidbody did not become dynamic after the scripted intro delay.");
        }
#endif
    }
}
