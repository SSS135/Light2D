using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = NUnit.Framework.Is;

namespace Light2D.Tests
{
    public class ComponentResourceTests
    {
        [Test]
        public void CustomSpriteUsesSpriteGeometryAndReleasesResourcesWhileDisabled()
        {
            var texture = new Texture2D(8, 8);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), new Vector2(0.25f, 0.75f), 8, 0,
                SpriteMeshType.Tight);
            var material = new Material(Shader.Find("Sprites/Default"));
            var gameObject = new GameObject("Custom sprite geometry");

            try
            {
                var customSprite = gameObject.AddComponent<TestCustomSprite>();
                customSprite.Sprite = sprite;
                customSprite.Material = material;
                customSprite.SendMessage("Update");

                // The owned mesh and generated material are read through the internal draw accessors;
                // the component owns no MeshFilter/MeshRenderer to inspect.
                var mesh = customSprite.Mesh;
                var generatedMaterial = customSprite.DrawMaterial;
                Assert.That(mesh, Is.Not.Null);
                Assert.That(mesh.vertices.Select(vertex => (Vector2)vertex), Is.EqualTo(sprite.vertices));
                Assert.That(mesh.triangles, Is.EqualTo(sprite.triangles.Select(index => (int)index)));
                Assert.That(mesh.uv, Is.EqualTo(sprite.uv));

                customSprite.enabled = false;
                Assert.That(customSprite.Mesh, Is.Null);
                Assert.That(customSprite.DrawMaterial, Is.Null);
                Assert.That(mesh == null, Is.True);
                Assert.That(generatedMaterial == null, Is.True);

                customSprite.enabled = true;
                Assert.That(customSprite.Mesh, Is.Not.Null);
                Assert.That(customSprite.DrawMaterial, Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void MaterialCacheResetRebindsEnabledSprites()
        {
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 1);
            var material = new Material(Shader.Find("Sprites/Default"));
            var gameObject = new GameObject("Cache reset sprite");
            try
            {
                var customSprite = gameObject.AddComponent<TestCustomSprite>();
                customSprite.Sprite = sprite;
                customSprite.Material = material;
                customSprite.SendMessage("Update");
                var oldGeneratedMaterial = customSprite.DrawMaterial;

                typeof(CustomSprite).GetMethod("ResetMaterialCache", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, null);

                var reboundMaterial = customSprite.DrawMaterial;
                Assert.That(oldGeneratedMaterial == null, Is.True);
                Assert.That(reboundMaterial, Is.Not.Null);
                Assert.That(reboundMaterial, Is.Not.SameAs(oldGeneratedMaterial));
                Assert.That(reboundMaterial.mainTexture, Is.SameAs(texture));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void ObstacleOwnerScopesWhichLightingSystemSnapshotIncludesIt()
        {
            // v3 removes the obstacle layer; the obstacle's Owner field now selects which system's
            // registry snapshot includes it. An unowned obstacle draws for every system; an owned one
            // draws only for its owner. This replaces the retired "gameObject.layer mirrors the owner's
            // LightObstaclesLayer" behavior.
            var firstObject = new GameObject("First lighting", typeof(Camera), typeof(LightingSystem));
            var secondObject = new GameObject("Second lighting", typeof(Camera), typeof(LightingSystem));
            var sourceObject = new GameObject("Source", typeof(SpriteRenderer));
            var obstacleObject = new GameObject("Obstacle");
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 1);
            try
            {
                var first = firstObject.GetComponent<LightingSystem>();
                var second = secondObject.GetComponent<LightingSystem>();
                sourceObject.GetComponent<SpriteRenderer>().sprite = sprite;
                obstacleObject.transform.SetParent(sourceObject.transform);
                var obstacle = obstacleObject.AddComponent<LightObstacleSprite>();
                obstacle.GameSpriteRenderer = sourceObject.GetComponent<SpriteRenderer>();
                obstacle.SendMessage("Update");

                var ownedMesh = obstacle.Mesh;
                Assert.That(ownedMesh, Is.Not.Null, "The obstacle did not build an owned mesh to register");

                var snapshot = new List<Light2DDrawEntry>();

                // Unowned: the obstacle appears in every system's snapshot.
                Light2DRenderRegistry.PrepareObstacleSnapshot(snapshot, new Light2DCullContext(null, first));
                Assert.That(snapshot.Any(entry => entry.Mesh == ownedMesh), Is.True,
                    "An unowned obstacle must draw for the first system");
                Light2DRenderRegistry.PrepareObstacleSnapshot(snapshot, new Light2DCullContext(null, second));
                Assert.That(snapshot.Any(entry => entry.Mesh == ownedMesh), Is.True,
                    "An unowned obstacle must draw for the second system");

                // Owner set: the obstacle is scoped to exactly that system's snapshot.
                obstacle.Owner = second;
                obstacle.SendMessage("Update");
                Light2DRenderRegistry.PrepareObstacleSnapshot(snapshot, new Light2DCullContext(null, second));
                Assert.That(snapshot.Any(entry => entry.Mesh == ownedMesh), Is.True,
                    "An owner-scoped obstacle must draw for its own system");
                Light2DRenderRegistry.PrepareObstacleSnapshot(snapshot, new Light2DCullContext(null, first));
                Assert.That(snapshot.Any(entry => entry.Mesh == ownedMesh), Is.False,
                    "An owner-scoped obstacle must not draw for another system");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(obstacleObject);
                UnityEngine.Object.DestroyImmediate(sourceObject);
                UnityEngine.Object.DestroyImmediate(firstObject);
                UnityEngine.Object.DestroyImmediate(secondObject);
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void RegisteredLightingInputComponentsOwnNoMeshRendererSoGeometryCannotLeak()
        {
            // Register-and-draw guard: sources, obstacles and ambient emitters draw only through the
            // registry via CommandBuffer.DrawMesh, so none of them owns a MeshRenderer the source camera
            // could draw into the game image — the exact defect the retired LightSources/AmbientLight/
            // LightObstacles layers used to prevent. With no renderer at all there is nothing to leak.
            // None of them owns a MeshFilter either: a sprite-mode ambient emitter keeps its generated mesh
            // in an internal field, and its MeshFilter is now only an optional self-mesh input that
            // AddComponent no longer forces.
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 1);
            var material = new Material(Shader.Find("Sprites/Default"));
            var sourceObject = new GameObject("No-leak obstacle source", typeof(SpriteRenderer));
            var lightObject = new GameObject("No-leak light source");
            var obstacleObject = new GameObject("No-leak obstacle");
            var ambientObject = new GameObject("No-leak ambient emitter");
            try
            {
                sourceObject.GetComponent<SpriteRenderer>().sprite = sprite;

                var light = lightObject.AddComponent<LightSprite>();
                light.Sprite = sprite;
                light.Material = material;
                light.SendMessage("Update");

                var obstacle = obstacleObject.AddComponent<LightObstacleSprite>();
                obstacle.GameSpriteRenderer = sourceObject.GetComponent<SpriteRenderer>();
                obstacle.Material = material;
                obstacle.SendMessage("Update");

                var ambient = ambientObject.AddComponent<LightAmbientEmitter>();
                ambient.Sprite = sprite;
                ambient.Material = material;
                ambient.SendMessage("Update");

                // Lights and obstacles own neither a MeshRenderer nor a MeshFilter, so no lighting-input
                // geometry can reach the game image.
                Assert.That(lightObject.GetComponent<MeshRenderer>(), Is.Null,
                    "The light source must not own a MeshRenderer");
                Assert.That(lightObject.GetComponent<MeshFilter>(), Is.Null,
                    "The light source must not own a MeshFilter");
                Assert.That(obstacleObject.GetComponent<MeshRenderer>(), Is.Null,
                    "The obstacle must not own a MeshRenderer");
                Assert.That(obstacleObject.GetComponent<MeshFilter>(), Is.Null,
                    "The obstacle must not own a MeshFilter");

                // A sprite-mode ambient emitter draws an internal generated mesh through the registry, so it
                // owns neither a MeshRenderer nor a MeshFilter; AddComponent no longer forces a MeshFilter
                // now that it is only an optional self-mesh input.
                Assert.That(ambientObject.GetComponent<MeshRenderer>(), Is.Null,
                    "The ambient emitter must not own a MeshRenderer");
                Assert.That(ambientObject.GetComponent<MeshFilter>(), Is.Null,
                    "AddComponent must not force a MeshFilter on the ambient emitter (optional self-mesh input)");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(lightObject);
                UnityEngine.Object.DestroyImmediate(obstacleObject);
                UnityEngine.Object.DestroyImmediate(ambientObject);
                UnityEngine.Object.DestroyImmediate(sourceObject);
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void LightSnapshotsFilterDisabledLightsAndHaveDeterministicOrder()
        {
            var firstObject = new GameObject("First light");
            var secondObject = new GameObject("Second light");
            try
            {
                var first = firstObject.AddComponent<LightSprite>();
                var second = secondObject.AddComponent<LightSprite>();
                var snapshot = new List<LightSprite>();
                LightSprite.PrepareSnapshot(snapshot, false, null, null);
                Assert.That(snapshot, Does.Contain(first));
                Assert.That(snapshot, Does.Contain(second));
                Assert.That(snapshot.Select(light => light.GetEntityId()), Is.Ordered);

                first.enabled = false;
                LightSprite.PrepareSnapshot(snapshot, false, null, null);
                Assert.That(snapshot.Any(light => ReferenceEquals(light, first)), Is.False);
                Assert.That(snapshot, Does.Contain(second));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstObject);
                UnityEngine.Object.DestroyImmediate(secondObject);
            }
        }

        [Test]
        public void CustomSpriteSteadyStateUpdateDoesNotAllocate()
        {
            var texture = new Texture2D(4, 4);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f, 4);
            var material = new Material(Shader.Find("Sprites/Default"));
            var gameObject = new GameObject("Steady state sprite");
            try
            {
                var customSprite = gameObject.AddComponent<TestCustomSprite>();
                customSprite.Sprite = sprite;
                customSprite.Material = material;

                // Bind the frame path once so the measured calls invoke no reflection and box no arguments.
                var update = (Action<bool>)typeof(CustomSprite)
                    .GetMethod("UpdateMeshData", BindingFlags.Instance | BindingFlags.NonPublic)
                    .CreateDelegate(typeof(Action<bool>), customSprite);
                for (var i = 0; i < 3; i++)
                    update(false);

                // Run the constraint once unasserted first: the first execution of the measuring delegate
                // and of the constraint pipeline JIT-compiles inside the recorder window and reports
                // allocations that are not part of the steady-state production path.
                var steadyStateUpdate = (TestDelegate)(() => update(false));
                new AllocatingGCMemoryConstraint().ApplyTo(steadyStateUpdate);

                Assert.That(steadyStateUpdate, Is.Not.AllocatingGCMemory());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void GeneratedMaterialMirrorsSourceOnlyWhenSourceChanges()
        {
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 1);
            var source = new Material(Shader.Find("Sprites/Default"));
            var gameObject = new GameObject("Mirroring sprite");
            try
            {
                var customSprite = gameObject.AddComponent<TestCustomSprite>();
                customSprite.Sprite = sprite;
                customSprite.Material = source;
                customSprite.SendMessage("Update");
                var generated = customSprite.DrawMaterial;

                // While the source is unchanged, steady-state updates must not re-copy source properties.
                var localColor = new Color(0.1f, 0.2f, 0.3f, 0.4f);
                generated.color = localColor;
                customSprite.SendMessage("Update");
                Assert.That(generated.color, Is.EqualTo(localColor));

                // Mutating the serialized source material must still live-mirror into the generated clone.
                var sourceColor = new Color(0.9f, 0.8f, 0.7f, 0.6f);
                source.color = sourceColor;
                customSprite.SendMessage("Update");
                Assert.That(generated.color, Is.EqualTo(sourceColor));
                Assert.That(generated.mainTexture, Is.SameAs(texture));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void ObstacleSpriteReflectsCachedCustomSpriteSourceChanges()
        {
            var texture = new Texture2D(4, 4);
            var firstSprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f, 4);
            var secondSprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 2);
            var material = new Material(Shader.Find("Sprites/Default"));
            // The CustomSprite source owns no renderer of its own; a bare MeshRenderer is added only to
            // wire GameSpriteRenderer, and ResolveSourceSprite prefers the sibling CustomSprite's Sprite.
            var sourceObject = new GameObject("Source custom sprite", typeof(MeshRenderer));
            var obstacleObject = new GameObject("Obstacle sprite");
            try
            {
                var sourceSprite = sourceObject.AddComponent<TestCustomSprite>();
                sourceSprite.Sprite = firstSprite;
                sourceSprite.Material = material;

                var obstacle = obstacleObject.AddComponent<LightObstacleSprite>();
                obstacle.GameSpriteRenderer = sourceObject.GetComponent<MeshRenderer>();
                obstacle.SendMessage("Update");
                Assert.That(obstacle.Sprite, Is.SameAs(firstSprite));

                // The cached source component must stay live: sprite changes propagate without re-resolution.
                sourceSprite.Sprite = secondSprite;
                obstacle.SendMessage("Update");
                Assert.That(obstacle.Sprite, Is.SameAs(secondSprite));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(obstacleObject);
                UnityEngine.Object.DestroyImmediate(sourceObject);
                UnityEngine.Object.DestroyImmediate(firstSprite);
                UnityEngine.Object.DestroyImmediate(secondSprite);
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void ObstacleMeshSelfMeshWithNullMaterialStillYieldsDrawEntryWithDefaultObstacleMaterial()
        {
            // Regression guard (Rocket shadows): a self-mesh LightObstacleMesh with no serialized Material
            // must still resolve the build-safe default obstacle material so TryGetDrawEntry keeps it in the
            // snapshot, instead of silently dropping every self-mesh obstacle when its material is null.
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
                triangles = new[] { 0, 1, 2 }
            };
            var gameObject = new GameObject("Self-mesh obstacle", typeof(MeshFilter));
            try
            {
                gameObject.GetComponent<MeshFilter>().sharedMesh = mesh;
                var obstacle = gameObject.AddComponent<LightObstacleMesh>();
                obstacle.SelfMesh = true;
                obstacle.Material = null;
                obstacle.SendMessage("Update");

                Assert.That(((ILight2DDrawable)obstacle).TryGetDrawEntry(out var entry), Is.True,
                    "A self-mesh obstacle with a valid mesh and a null Material must still draw");
                Assert.That(entry.Material, Is.Not.Null,
                    "The self-mesh branch must resolve the default obstacle material when Material is null");
                Assert.That(entry.Mesh, Is.SameAs(mesh),
                    "The draw entry must register the object's own self-mesh");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void ResourceComponentsDeclareRequiredLifecycleAttributes()
        {
            Assert.That(typeof(CustomSprite).GetCustomAttribute<ExecuteAlways>(), Is.Not.Null);
            Assert.That(typeof(CustomSprite).GetCustomAttribute<DisallowMultipleComponent>(), Is.Not.Null);
            // CustomSprite and LightAmbientEmitter both draw a mesh through the registry (a bare owned mesh
            // and an internal generated mesh respectively), so neither requires a MeshFilter/MeshRenderer;
            // the emitter's MeshFilter is now only an optional self-mesh input, not a RequireComponent.
            Assert.That(typeof(CustomSprite).GetCustomAttributes<RequireComponent>(), Is.Empty);
            Assert.That(typeof(LightAmbientEmitter).GetCustomAttributes<RequireComponent>(), Is.Empty);
            Assert.That(typeof(LightObstacleMesh).GetCustomAttribute<ExecuteAlways>(), Is.Not.Null);
            Assert.That(typeof(LightObstacleGenerator).GetCustomAttribute<DisallowMultipleComponent>(), Is.Not.Null);
        }

        // Concrete stand-in for the retired standalone CustomSprite: an empty subclass behaves exactly
        // like the old bare component, so these base-class tests stay valid before and after CustomSprite
        // is made abstract. AddComponent needs a concrete type; the abstract base cannot be added directly.
        private sealed class TestCustomSprite : CustomSprite
        {
        }
    }
}
