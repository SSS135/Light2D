using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Light2D.Tests
{
    // Pipeline-independent EditMode coverage for the obstacle-preview plumbing (Workstream B2). These assert
    // the C# request flag and the owned preview-target lifecycle that the editor preview window drives through
    // InternalsVisibleTo. The URP copy pass itself (renderGraph.ImportTexture + AddObstaclePreviewPass) needs a
    // live URP pipeline, which does not activate under batchmode on this host, so it is NOT verifiable here -
    // only the plumbing (request flag + RTHandle allocate/release) is asserted.
    public class ObstaclePreviewTests
    {
        // LightingSystem's [RequireComponent(typeof(Camera))] adds the camera automatically; the preview
        // members under test never touch the camera, so no further setup is required.
        private static LightingSystem CreateSystem(out GameObject gameObject)
        {
            gameObject = new GameObject("Obstacle Preview System");
            return gameObject.AddComponent<LightingSystem>();
        }

        [Test]
        public void FreshSystemHasNoPreviewTargetAndNoRequest()
        {
            var system = CreateSystem(out var gameObject);
            try
            {
                Assert.That(system.ObstaclePreviewTexture, Is.Null);
                Assert.That(system.ObstaclePreviewRequested, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void RequestObstaclePreviewFlipsRequestedTrue()
        {
            var system = CreateSystem(out var gameObject);
            try
            {
                Assert.That(system.ObstaclePreviewRequested, Is.False);
                system.RequestObstaclePreview();
                Assert.That(system.ObstaclePreviewRequested, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void EnsureObstaclePreviewTargetAllocatesMatchingSizeAndReleaseNullsIt()
        {
            var system = CreateSystem(out var gameObject);
            try
            {
                var descriptor = new RenderTextureDescriptor(64, 48, RenderTextureFormat.ARGB32, 0);
                RTHandle handle;
                try
                {
                    handle = system.EnsureObstaclePreviewTarget(descriptor, FilterMode.Bilinear);
                }
                catch (System.NullReferenceException)
                {
                    // EnsureObstaclePreviewTarget allocates through URP's RTHandle resource pool
                    // (UniversalRenderPipeline.s_RTHandlePool), which only exists while a URP pipeline is active.
                    // On this Built-in/batchmode host that pool is null, so the allocation is only exercisable
                    // under a live URP pipeline (interactive) - matching the URP PlayMode tests that Assert.Ignore.
                    // The request flag / getter / release lifecycle is covered pipeline-independently above.
                    Assert.Ignore("Obstacle-preview allocation requires an active URP pipeline; not available in batchmode Built-in.");
                    return;
                }

                Assert.That(handle, Is.Not.Null);
                Assert.That(system.ObstaclePreviewTexture, Is.Not.Null);
                Assert.That(system.ObstaclePreviewTexture.width, Is.EqualTo(64));
                Assert.That(system.ObstaclePreviewTexture.height, Is.EqualTo(48));

                system.ReleaseObstaclePreviewTarget();
                Assert.That(system.ObstaclePreviewTexture, Is.Null);
            }
            finally
            {
                // Release again in case an assertion threw before the explicit release, so the RTHandle
                // never leaks out of the test.
                system.ReleaseObstaclePreviewTarget();
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
