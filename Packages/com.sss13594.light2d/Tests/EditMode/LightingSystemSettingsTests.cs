using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Light2D.Tests
{
    // Guards the default values of the LightingSystem tuning fields that the URP feature settings were
    // folded into (the ambient-warmup / per-frame-cycle knobs now live here alongside the overlay, ambient,
    // and blur value fields - the values-only source of truth, always applied). A component is constructed
    // the same way ObstaclePreviewTests does - the [RequireComponent(typeof(Camera))] attribute adds the
    // camera automatically - and only the freshly deserialized inspector defaults are asserted, so the
    // fields cannot silently drift from the documented values callers depend on.
    public class LightingSystemSettingsTests
    {
        [Test]
        public void FreshLightingSystemExposesDocumentedDefaults()
        {
            var gameObject = new GameObject("Lighting System Defaults");
            try
            {
                var system = gameObject.AddComponent<LightingSystem>();

                // Ambient propagation / bounce.
                Assert.That(system.AmbientColoredBounce, Is.EqualTo(0f).Within(1e-6f));
                Assert.That(system.AmbientIterations, Is.EqualTo(1));

                // Coverage baseline.
                Assert.That(system.DefaultCoverage, Is.EqualTo(1f).Within(1e-6f));

                // Global obstacle multiplier: white with full alpha is the no-op default that leaves the
                // obstacle write byte-identical to the legacy behavior (multiplying every obstacle by 1).
                Assert.That(system.ObstacleColor, Is.EqualTo(Color.white));

                // Ambient-cycle scheduling (moved from the URP FeatureSettings).
                Assert.That(system.MaxAmbientCyclesPerFrame, Is.EqualTo(8));
                Assert.That(system.AmbientWarmupCycles, Is.EqualTo(100));
                Assert.That(system.MaxQueuedAmbientCycles, Is.EqualTo(64));
                Assert.That(system.MaxLightTexturePixels, Is.EqualTo(16L * 1024L * 1024L));

                // Overlay look values (always applied - the values-only source of truth).
                Assert.That(system.LightBrightness, Is.EqualTo(2f).Within(1e-6f));
                Assert.That(system.LightSourcesBrightness, Is.EqualTo(1f).Within(1e-6f));
                Assert.That(system.BloomPower, Is.EqualTo(4f).Within(1e-6f));
                Assert.That(system.BloomAdd, Is.EqualTo(0.15f).Within(1e-6f));

                // Ambient feel values (always applied - the values-only source of truth).
                Assert.That(system.AmbientObstacleMul, Is.EqualTo(1.5f).Within(1e-6f));
                Assert.That(system.AmbientObstacleAdd, Is.EqualTo(0.1f).Within(1e-6f));
                Assert.That(system.AmbientEmissionMul, Is.EqualTo(0.1f).Within(1e-6f));
                Assert.That(system.AmbientSamplingDistance, Is.EqualTo(0.01f).Within(1e-6f));
                Assert.That(system.AmbientAdaptSpeed, Is.EqualTo(0.2f).Within(1e-6f));

                // Blur value (always applied - the values-only source of truth).
                Assert.That(system.BlurDistance, Is.EqualTo(2.5f).Within(1e-6f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
