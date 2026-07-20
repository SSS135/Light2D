using System;
using NUnit.Framework;
using UnityEngine;

namespace Light2D.Tests
{
    public class DiagnosticsTests
    {
        [SetUp]
        public void SetUp()
        {
            Light2DDiagnostics.Reset();
        }

        [Test]
        public void ProfilerMarkersExposeStableNames()
        {
            Assert.That(Light2DProfiling.ValidationName, Is.EqualTo("Light2D.Validation"));
            Assert.That(Light2DProfiling.ResourceReconciliationName, Is.EqualTo("Light2D.ResourceReconciliation"));
            Assert.That(Light2DProfiling.CullingName, Is.EqualTo("Light2D.Culling"));
            Assert.That(Light2DProfiling.ObstaclesName, Is.EqualTo("Light2D.Obstacles"));
            Assert.That(Light2DProfiling.SourcesName, Is.EqualTo("Light2D.Sources"));
            Assert.That(Light2DProfiling.NormalsName, Is.EqualTo("Light2D.Normals"));
            Assert.That(Light2DProfiling.BlurName, Is.EqualTo("Light2D.Blur"));
            Assert.That(Light2DProfiling.AmbientName, Is.EqualTo("Light2D.Ambient"));
            Assert.That(Light2DProfiling.CompositionName, Is.EqualTo("Light2D.Composition"));
        }

        [Test]
        public void FrameMetricsPreserveReportedValues()
        {
            var metrics = new Light2DFrameMetrics(640, 360, 7_372_800, 24, 3, 2, 4);

            Light2DDiagnostics.ReportFrame(in metrics);

            Assert.That(Light2DDiagnostics.LastFrame.TextureWidth, Is.EqualTo(640));
            Assert.That(Light2DDiagnostics.LastFrame.TextureHeight, Is.EqualTo(360));
            Assert.That(Light2DDiagnostics.LastFrame.EstimatedOwnedTextureBytes, Is.EqualTo(7_372_800));
            Assert.That(Light2DDiagnostics.LastFrame.VisibleLights, Is.EqualTo(24));
            Assert.That(Light2DDiagnostics.LastFrame.AmbientCycles, Is.EqualTo(3));
            Assert.That(Light2DDiagnostics.LastFrame.Reallocations, Is.EqualTo(2));
            Assert.That(Light2DDiagnostics.LastFrame.ContextCount, Is.EqualTo(4));
        }

        [Test]
        public void RenderResourceAccountingFollowsAllocationReallocationAndDisposal()
        {
            var resources = new LightingRenderResources();
            var descriptor = new RenderTextureDescriptor(16, 8, RenderTextureFormat.ARGB32, 0);
            Assert.That(Light2DDiagnostics.ContextCount, Is.EqualTo(1));

            Assert.That(resources.EnsureTexture(ref resources.ObstaclesTexture, descriptor, FilterMode.Point,
                "Diagnostics Test"), Is.True);
            long initialBytes = Light2DDiagnostics.OwnedTextureBytes;
            Assert.That(initialBytes, Is.GreaterThan(0));
            Assert.That(resources.EnsureTexture(ref resources.ObstaclesTexture, descriptor, FilterMode.Point,
                "Diagnostics Test"), Is.False);
            Assert.That(Light2DDiagnostics.OwnedTextureBytes, Is.EqualTo(initialBytes));

            descriptor.width *= 2;
            Assert.That(resources.EnsureTexture(ref resources.ObstaclesTexture, descriptor, FilterMode.Point,
                "Diagnostics Test"), Is.True);
            Assert.That(Light2DDiagnostics.OwnedTextureBytes, Is.EqualTo(initialBytes * 2));

            resources.Dispose();
            resources.Dispose();

            Assert.That(Light2DDiagnostics.OwnedTextureBytes, Is.Zero);
            Assert.That(Light2DDiagnostics.ContextCount, Is.Zero);
        }

        [Test]
        public void ResetInvalidatesAccountingFromOwnersCreatedBeforeReload()
        {
            var resources = new LightingRenderResources();
            var descriptor = new RenderTextureDescriptor(16, 8, RenderTextureFormat.ARGB32, 0);
            resources.EnsureTexture(ref resources.ObstaclesTexture, descriptor, FilterMode.Point,
                "Diagnostics Reset Test");

            Light2DDiagnostics.Reset();
            resources.Dispose();

            Assert.That(Light2DDiagnostics.OwnedTextureBytes, Is.Zero);
            Assert.That(Light2DDiagnostics.ContextCount, Is.Zero);
        }

        [Test]
        public void ReportingAndCounterUpdatesAllocateNoManagedMemoryAfterWarmup()
        {
            var metrics = new Light2DFrameMetrics(320, 180, 1_843_200, 8, 1, 0, 1);
            ExerciseDiagnostics(in metrics);
            long before = GC.GetAllocatedBytesForCurrentThread();

            ExerciseDiagnostics(in metrics);

            Assert.That(GC.GetAllocatedBytesForCurrentThread(), Is.EqualTo(before));
        }

        private static void ExerciseDiagnostics(in Light2DFrameMetrics metrics)
        {
            // Exercises the same generation-aware paths production uses per frame and per context lifecycle.
            for (int i = 0; i < 128; i++)
            {
                Light2DProfiling.Validation.Begin();
                Light2DProfiling.Validation.End();
                Light2DDiagnostics.ReportFrame(in metrics);
                int generation = Light2DDiagnostics.RegisterContext();
                Light2DDiagnostics.AddOwnedTextureBytes(generation, 16);
                Light2DDiagnostics.RemoveOwnedTextureBytes(generation, 16);
                Light2DDiagnostics.UnregisterContext(generation);
            }
        }
    }
}
