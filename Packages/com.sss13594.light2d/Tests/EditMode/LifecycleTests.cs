using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Light2D.Tests
{
    public class LifecycleTests
    {
        [Test]
        public void LightingSettingsValidationRejectsInvalidAndOversizedDimensions()
        {
            var gameObject = new GameObject("Validation Camera");
            var camera = gameObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            try
            {
                Assert.That(LightingSettingsValidator.TryCalculateSizes(camera, float.NaN, 3f, 30f, 10f,
                    false, false, false, false, out _, out var invalidError), Is.False);
                Assert.That(invalidError, Does.Contain("finite"));
                Assert.That(LightingSettingsValidator.TryCalculateSizes(camera, 0.000001f, 3f, 30f, 10f,
                    false, false, true, false, out _, out var oversizedError), Is.False);
                Assert.That(oversizedError, Does.Contain("texture-size limit"));
                Assert.That(LightingSettingsValidator.ValidateLayers(8, 8, 9, false), Does.Contain("different"));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void LightSpriteRegistryTracksEnableDisableAndDestroy()
        {
            var gameObject = new GameObject("Registry Light");
            var light = gameObject.AddComponent<LightSprite>();
            try
            {
                Assert.That(LightSprite.AllLightSprites, Does.Contain(light));
                light.enabled = false;
                Assert.That(LightSprite.AllLightSprites.Any(item => object.ReferenceEquals(item, light)), Is.False);
                light.enabled = true;
                Assert.That(LightSprite.AllLightSprites, Does.Contain(light));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }

            Assert.That(LightSprite.AllLightSprites.Any(item => item == light), Is.False);
        }

        [Test]
        public void CustomSpriteGeneratedMeshAndSharedMaterialAreReleasedOnDestroy()
        {
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 1);
            var baseMaterial = new Material(Shader.Find("Sprites/Default"));
            var gameObject = new GameObject("Generated Sprite");
            var customSprite = gameObject.AddComponent<CustomSprite>();
            customSprite.Sprite = sprite;
            customSprite.Material = baseMaterial;
            customSprite.enabled = false;
            customSprite.enabled = true;

            var mesh = gameObject.GetComponent<MeshFilter>().sharedMesh;
            var generatedMaterial = gameObject.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.name, Does.StartWith("Generated Mesh"));
            Assert.That(generatedMaterial, Is.Not.Null);
            Assert.That(generatedMaterial, Is.Not.SameAs(baseMaterial));

            Object.DestroyImmediate(gameObject);
            Assert.That(mesh == null, Is.True, "Generated mesh survived its owner");
            Assert.That(generatedMaterial == null, Is.True, "Generated material survived its final owner");

            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(baseMaterial);
        }

        [Test]
        public void LightingSystemPublicValidationAndLifecycleContractsAreStable()
        {
            var gameObject = new GameObject("Lighting System");
            var camera = gameObject.AddComponent<Camera>();
            var lighting = gameObject.AddComponent<LightingSystem>();
            var overlay = new Material(Shader.Find("Light2D/Light Overlay"));
            try
            {
                Assert.That(lighting.SourceCamera, Is.SameAs(camera));
                Assert.That(LightingSystem.Instances, Does.Contain(lighting));
                Assert.That(lighting.IsRenderingReady, Is.False);
                lighting.LightOverlayMaterial = overlay;
                Assert.That(lighting.IsRenderingReady, Is.EqualTo(lighting.IsScriptableRenderPipelineActive));
                var lightCameraObject = new GameObject("Assigned Lighting Camera");
                lightCameraObject.transform.SetParent(gameObject.transform);
                lighting.LightCamera = lightCameraObject.AddComponent<Camera>();
                Assert.That(lighting.IsRenderingReady, Is.True);
                lighting.LightPixelSize = 0;
                Assert.That(lighting.LightPixelsPerUnityMeter, Is.EqualTo(100000).Within(1));

                lighting.LoopAmbientLight(3);
                lighting.LoopAmbientLight(2);
                Assert.That(lighting.ConsumePendingAmbientLightCycles(), Is.EqualTo(5));
                Assert.That(lighting.ConsumePendingAmbientLightCycles(), Is.Zero);
                lighting.LoopAmbientLight(-1);
                Assert.That(lighting.ConsumePendingAmbientLightCycles(), Is.Zero);

                lighting.LoopAmbientLight(int.MaxValue);
                Assert.That(lighting.ConsumePendingAmbientLightCycles(), Is.EqualTo(8));
                Assert.That(lighting.ConsumePendingAmbientLightCycles(), Is.EqualTo(8));
                lighting.EnableAmbientLight = false;
                lighting.LoopAmbientLight(5);
                Assert.That(lighting.ConsumePendingAmbientLightCycles(), Is.Zero);

                lighting.enabled = false;
                Assert.That(LightingSystem.Instances.Any(item => object.ReferenceEquals(item, lighting)), Is.False);
                Assert.That(lighting.IsRenderingReady, Is.False);
                Assert.That(lighting.ObstaclesTexture, Is.Null);
                Assert.That(lighting.LightSourcesTexture, Is.Null);
                Assert.That(lighting.AmbientTexture, Is.Null);
                lighting.enabled = true;
                Assert.That(LightingSystem.Instances, Does.Contain(lighting));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(overlay);
            }

            Assert.That(LightingSystem.Instances.Any(item => item == lighting), Is.False);
        }
    }
}
