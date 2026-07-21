using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Light2D.Tests
{
    public class CoverageRegistryTests
    {
        [Test]
        public void CoverageRegistryTracksEnableDisableAndDestroy()
        {
            var gameObject = new GameObject("Registry coverage");
            var coverage = gameObject.AddComponent<TestCoverage>();
            try
            {
                Assert.That(Light2DRenderRegistry.ContainsCoverage(coverage), Is.True);
                coverage.enabled = false;
                Assert.That(Light2DRenderRegistry.ContainsCoverage(coverage), Is.False);
                coverage.enabled = true;
                Assert.That(Light2DRenderRegistry.ContainsCoverage(coverage), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }

            Assert.That(Light2DRenderRegistry.ContainsCoverage(coverage), Is.False);
        }

        [Test]
        public void CoverageSnapshotOrdersByPriorityThenEntityId()
        {
            var meshHigh = new Mesh();
            var meshLowFirst = new Mesh();
            var meshLowSecond = new Mesh();
            var material = new Material(Shader.Find("Sprites/Default"));
            var highObject = new GameObject("High priority");
            var lowFirstObject = new GameObject("Low priority first");
            var lowSecondObject = new GameObject("Low priority second");
            try
            {
                var high = highObject.AddComponent<TestCoverage>();
                high.Mesh = meshHigh;
                high.Material = material;
                high.Priority = 10;

                var lowFirst = lowFirstObject.AddComponent<TestCoverage>();
                lowFirst.Mesh = meshLowFirst;
                lowFirst.Material = material;
                lowFirst.Priority = -5;

                var lowSecond = lowSecondObject.AddComponent<TestCoverage>();
                lowSecond.Mesh = meshLowSecond;
                lowSecond.Material = material;
                lowSecond.Priority = -5;

                var snapshot = new List<Light2DDrawEntry>();
                var context = default(Light2DCullContext);
                Light2DRenderRegistry.PrepareCoverageSnapshot(snapshot, context);

                var mine = snapshot.Where(entry => entry.Material == material).ToList();
                Assert.That(mine.Count, Is.EqualTo(3));
                // Primary key: Priority (SortKey) ascending.
                Assert.That(mine.Select(entry => entry.SortKey), Is.Ordered);

                // Tie-break: equal priority resolves deterministically by ascending entity id.
                var indexFirst = snapshot.FindIndex(entry => entry.Mesh == meshLowFirst);
                var indexSecond = snapshot.FindIndex(entry => entry.Mesh == meshLowSecond);
                var firstBeforeSecond = lowFirst.GetEntityId().CompareTo(lowSecond.GetEntityId()) < 0;
                Assert.That(indexFirst < indexSecond, Is.EqualTo(firstBeforeSecond));
            }
            finally
            {
                Object.DestroyImmediate(highObject);
                Object.DestroyImmediate(lowFirstObject);
                Object.DestroyImmediate(lowSecondObject);
                Object.DestroyImmediate(meshHigh);
                Object.DestroyImmediate(meshLowFirst);
                Object.DestroyImmediate(meshLowSecond);
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void SubsystemResetReclearsCoverage()
        {
            var coverageObject = new GameObject("Reset coverage");
            var coverage = coverageObject.AddComponent<TestCoverage>();
            try
            {
                Assert.That(Light2DRenderRegistry.ContainsCoverage(coverage), Is.True);

                // Clear membership, then prove the SubsystemRegistration reset re-finds active instances.
                Light2DRenderRegistry.DeregisterCoverage(coverage);
                Assert.That(Light2DRenderRegistry.ContainsCoverage(coverage), Is.False);

                InvokeReset();

                Assert.That(Light2DRenderRegistry.ContainsCoverage(coverage), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(coverageObject);
            }
        }

        private static void InvokeReset()
        {
            typeof(Light2DRenderRegistry)
                .GetMethod("ResetRegistry", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, null);
        }

        // Minimal coverage producer for exercising the registry directly, mirroring
        // RenderRegistryTests.TestObstacle. ExecuteAlways so its enable/disable lifecycle fires in the
        // edit-mode test runner, matching the shipping Light2DVolume component.
        [ExecuteAlways]
        private sealed class TestCoverage : MonoBehaviour, ILight2DCoverage
        {
            public Mesh Mesh;
            public Material Material;
            public int Priority;

            private void OnEnable()
            {
                Light2DRenderRegistry.RegisterCoverage(this);
            }

            private void OnDisable()
            {
                Light2DRenderRegistry.DeregisterCoverage(this);
            }

            private void OnDestroy()
            {
                Light2DRenderRegistry.DeregisterCoverage(this);
            }

            bool ILight2DDrawable.TryGetDrawEntry(out Light2DDrawEntry entry)
            {
                if (!isActiveAndEnabled || Mesh == null || Material == null)
                {
                    entry = default;
                    return false;
                }

                entry = new Light2DDrawEntry(Mesh, Material, transform.localToWorldMatrix,
                    new Bounds(transform.position, Vector3.one), 1, Priority, null);
                return true;
            }
        }
    }
}
