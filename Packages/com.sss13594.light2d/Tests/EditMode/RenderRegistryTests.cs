using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Light2D.Tests
{
    public class RenderRegistryTests
    {
        [Test]
        public void AmbientEmitterRegistryTracksEnableDisableAndDestroy()
        {
            var gameObject = new GameObject("Registry ambient emitter");
            var emitter = gameObject.AddComponent<LightAmbientEmitter>();
            try
            {
                Assert.That(Light2DRenderRegistry.ContainsAmbient(emitter), Is.True);
                emitter.enabled = false;
                Assert.That(Light2DRenderRegistry.ContainsAmbient(emitter), Is.False);
                emitter.enabled = true;
                Assert.That(Light2DRenderRegistry.ContainsAmbient(emitter), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }

            Assert.That(Light2DRenderRegistry.ContainsAmbient(emitter), Is.False);
        }

        [Test]
        public void ObstacleRegistryTracksEnableDisableAndDestroy()
        {
            var gameObject = new GameObject("Registry obstacle");
            var obstacle = gameObject.AddComponent<TestObstacle>();
            try
            {
                Assert.That(Light2DRenderRegistry.ContainsObstacle(obstacle), Is.True);
                obstacle.enabled = false;
                Assert.That(Light2DRenderRegistry.ContainsObstacle(obstacle), Is.False);
                obstacle.enabled = true;
                Assert.That(Light2DRenderRegistry.ContainsObstacle(obstacle), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }

            Assert.That(Light2DRenderRegistry.ContainsObstacle(obstacle), Is.False);
        }

        [Test]
        public void SubsystemResetReclearsAndRefindsActiveRegisteredInstances()
        {
            var obstacleObject = new GameObject("Reset obstacle");
            var ambientObject = new GameObject("Reset ambient");
            var obstacle = obstacleObject.AddComponent<TestObstacle>();
            var emitter = ambientObject.AddComponent<LightAmbientEmitter>();
            try
            {
                Assert.That(Light2DRenderRegistry.ContainsObstacle(obstacle), Is.True);
                Assert.That(Light2DRenderRegistry.ContainsAmbient(emitter), Is.True);

                // Clear membership, then prove the SubsystemRegistration reset re-finds active instances.
                Light2DRenderRegistry.DeregisterObstacle(obstacle);
                Light2DRenderRegistry.DeregisterAmbient(emitter);
                Assert.That(Light2DRenderRegistry.ContainsObstacle(obstacle), Is.False);
                Assert.That(Light2DRenderRegistry.ContainsAmbient(emitter), Is.False);

                InvokeReset();

                Assert.That(Light2DRenderRegistry.ContainsObstacle(obstacle), Is.True);
                Assert.That(Light2DRenderRegistry.ContainsAmbient(emitter), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(obstacleObject);
                Object.DestroyImmediate(ambientObject);
            }
        }

        [Test]
        public void ObstacleSnapshotOrdersBySortKeyThenEntityId()
        {
            var meshHigh = new Mesh();
            var meshLowFirst = new Mesh();
            var meshLowSecond = new Mesh();
            var material = new Material(Shader.Find("Sprites/Default"));
            var highObject = new GameObject("High order");
            var lowFirstObject = new GameObject("Low order first");
            var lowSecondObject = new GameObject("Low order second");
            try
            {
                var high = highObject.AddComponent<TestObstacle>();
                high.Mesh = meshHigh;
                high.Material = material;
                high.Order = 10;

                var lowFirst = lowFirstObject.AddComponent<TestObstacle>();
                lowFirst.Mesh = meshLowFirst;
                lowFirst.Material = material;
                lowFirst.Order = -5;

                var lowSecond = lowSecondObject.AddComponent<TestObstacle>();
                lowSecond.Mesh = meshLowSecond;
                lowSecond.Material = material;
                lowSecond.Order = -5;

                var snapshot = new List<Light2DDrawEntry>();
                var context = default(Light2DCullContext);
                Light2DRenderRegistry.PrepareObstacleSnapshot(snapshot, context);

                var mine = snapshot.Where(entry => entry.Material == material).ToList();
                Assert.That(mine.Count, Is.EqualTo(3));
                // Primary key: SortKey ascending.
                Assert.That(mine.Select(entry => entry.SortKey), Is.Ordered);

                // Tie-break: equal SortKey resolves deterministically by ascending entity id.
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
        public void AmbientSnapshotIncludesValidEmitterAndSkipsDisabled()
        {
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 1);
            var material = new Material(Shader.Find("Sprites/Default"));
            var gameObject = new GameObject("Snapshot ambient emitter");
            try
            {
                var emitter = gameObject.AddComponent<LightAmbientEmitter>();
                emitter.Sprite = sprite;
                emitter.Material = material;
                emitter.SendMessage("Update");

                var ownedMesh = gameObject.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(ownedMesh, Is.Not.Null);

                var snapshot = new List<Light2DDrawEntry>();
                var context = default(Light2DCullContext);
                Light2DRenderRegistry.PrepareAmbientSnapshot(snapshot, context);
                Assert.That(snapshot.Any(entry => entry.Mesh == ownedMesh), Is.True);

                emitter.enabled = false;
                Light2DRenderRegistry.PrepareAmbientSnapshot(snapshot, context);
                Assert.That(snapshot.Any(entry => entry.Mesh == ownedMesh), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(material);
            }
        }

        private static void InvokeReset()
        {
            typeof(Light2DRenderRegistry)
                .GetMethod("ResetRegistry", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, null);
        }

        // Minimal obstacle producer for exercising the registry in Wave 1, before the shipping obstacle
        // components implement ILight2DObstacle in a later chunk. ExecuteAlways so its enable/disable
        // lifecycle fires in the edit-mode test runner, matching the shipping components.
        [ExecuteAlways]
        private sealed class TestObstacle : MonoBehaviour, ILight2DObstacle
        {
            public Mesh Mesh;
            public Material Material;
            public int Order;

            private void OnEnable()
            {
                Light2DRenderRegistry.RegisterObstacle(this);
            }

            private void OnDisable()
            {
                Light2DRenderRegistry.DeregisterObstacle(this);
            }

            private void OnDestroy()
            {
                Light2DRenderRegistry.DeregisterObstacle(this);
            }

            bool ILight2DDrawable.TryGetDrawEntry(out Light2DDrawEntry entry)
            {
                if (!isActiveAndEnabled || Mesh == null || Material == null)
                {
                    entry = default;
                    return false;
                }

                entry = new Light2DDrawEntry(Mesh, Material, transform.localToWorldMatrix,
                    new Bounds(transform.position, Vector3.one), 1, Order, null);
                return true;
            }
        }
    }
}
