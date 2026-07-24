using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Light2D.Tests
{
    public class LightPresetTests
    {
        private const string RuntimeMaterialsRoot = "Packages/com.sss13594.light2d/Runtime/Materials/";

        [Test]
        public void FastMapsToLight8PointsGuid()
        {
            Assert.That(Light2DLightPresets.GetMaterialGuid(LightSprite.ShadowQuality.Fast),
                Is.EqualTo("9a4db11e5d304784685eaee65120aff9"));
        }

        [Test]
        public void BalancedMapsToLight40PointsGuid()
        {
            Assert.That(Light2DLightPresets.GetMaterialGuid(LightSprite.ShadowQuality.Balanced),
                Is.EqualTo("ab37f3042fbc56e4b9288c3ba9cd32d8"));
        }

        [Test]
        public void HighMapsToLight90PointsGuid()
        {
            Assert.That(Light2DLightPresets.GetMaterialGuid(LightSprite.ShadowQuality.High),
                Is.EqualTo("eba84647c4f5b25448790591a30ca17e"));
        }

        [Test]
        public void AutoMapsToLightAutoPointsGuid()
        {
            Assert.That(Light2DLightPresets.GetMaterialGuid(LightSprite.ShadowQuality.Auto),
                Is.EqualTo("04a39e1d85156e34bb45d05db1a5eb00"));
        }

        [Test]
        public void IgnoreObstaclesMapsToLightIgnoreObstaclesGuid()
        {
            Assert.That(Light2DLightPresets.GetMaterialGuid(LightSprite.ShadowQuality.IgnoreObstacles),
                Is.EqualTo("dd2043e5cd5311e4fbe905ebe0644ed1"));
        }

        [Test]
        public void CustomHasNoPresetMaterial()
        {
            Assert.That(Light2DLightPresets.GetMaterialGuid(LightSprite.ShadowQuality.Custom), Is.Null);
        }

        // Guards against GUID drift: every non-Custom preset must resolve to an existing runtime material
        // whose name matches the intended Light{N}Points/… asset.
        [TestCase(LightSprite.ShadowQuality.Fast, "Light8Points")]
        [TestCase(LightSprite.ShadowQuality.Balanced, "Light40Points")]
        [TestCase(LightSprite.ShadowQuality.High, "Light90Points")]
        [TestCase(LightSprite.ShadowQuality.Auto, "LightAutoPoints")]
        [TestCase(LightSprite.ShadowQuality.IgnoreObstacles, "LightIgnoreObstacles")]
        public void PresetGuidResolvesToExpectedRuntimeMaterial(LightSprite.ShadowQuality quality, string materialName)
        {
            var guid = Light2DLightPresets.GetMaterialGuid(quality);
            var path = AssetDatabase.GUIDToAssetPath(guid);
            Assert.That(path, Is.EqualTo(RuntimeMaterialsRoot + materialName + ".mat"));

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Assert.That(material, Is.Not.Null, path);
        }

        // Auto-resolution surface: every non-Custom quality resolves a light material whose shader is the
        // ray-march light shader for that preset (never Sprites/Default), so a null Material "just works".
        [TestCase(LightSprite.ShadowQuality.Fast, "Light2D/Light Points")]
        [TestCase(LightSprite.ShadowQuality.Balanced, "Light2D/Light Points")]
        [TestCase(LightSprite.ShadowQuality.High, "Light2D/Light Points")]
        [TestCase(LightSprite.ShadowQuality.Auto, "Light2D/Light Auto Points")]
        [TestCase(LightSprite.ShadowQuality.IgnoreObstacles, "Light2D/Light Ignoring Obstacles")]
        public void ResolvePresetMaterialUsesTheLightShaderForEachPreset(LightSprite.ShadowQuality quality,
            string shaderName)
        {
            var material = Light2DLightPresets.ResolvePresetMaterial(quality);
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo(shaderName));
        }

        [Test]
        public void ResolvePresetMaterialForCustomIsNull()
        {
            Assert.That(Light2DLightPresets.ResolvePresetMaterial(LightSprite.ShadowQuality.Custom), Is.Null);
        }

        // The defect this fix closes: a programmatically added light with default Shadows (Balanced) and no
        // Material must resolve a real ray-march light draw material, NOT the alpha-blending Sprites/Default
        // that stamped darkness into the light-sources texture.
        [Test]
        public void NullMaterialWithDefaultShadowsResolvesLightShaderNotSpritesDefault()
        {
            var lightObject = new GameObject("Auto-resolving light");
            try
            {
                var light = lightObject.AddComponent<LightSprite>();
                Assert.That(light.Shadows, Is.EqualTo(LightSprite.ShadowQuality.Balanced));
                Assert.That(light.Material, Is.Null, "The light must resolve with no Material assigned");
                light.SendMessage("Update");

                var drawMaterial = light.DrawMaterial;
                Assert.That(drawMaterial, Is.Not.Null,
                    "A null-Material light with a preset quality must still resolve a draw material");
                Assert.That(drawMaterial.shader.name, Is.EqualTo("Light2D/Light Points"));
                Assert.That(drawMaterial.shader.name, Is.Not.EqualTo("Sprites/Default"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(lightObject);
            }
        }

        // Switching Shadows re-resolves the draw material at runtime (dirty-tracked), so IgnoreObstacles
        // picks up its own shader even with the Material slot left empty the whole time.
        [Test]
        public void ChangingShadowsReResolvesTheDrawMaterial()
        {
            var lightObject = new GameObject("Shadows-switching light");
            try
            {
                var light = lightObject.AddComponent<LightSprite>();
                light.SendMessage("Update");
                Assert.That(light.DrawMaterial.shader.name, Is.EqualTo("Light2D/Light Points"));

                light.Shadows = LightSprite.ShadowQuality.IgnoreObstacles;
                light.SendMessage("Update");
                Assert.That(light.Material, Is.Null, "Auto-resolution must not populate the Material slot");
                Assert.That(light.DrawMaterial.shader.name, Is.EqualTo("Light2D/Light Ignoring Obstacles"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(lightObject);
            }
        }

        // Precedence: an explicitly assigned Material always wins over Shadows auto-resolution.
        [Test]
        public void ExplicitMaterialTakesPrecedenceOverShadows()
        {
            var lightObject = new GameObject("Explicit-material light");
            var explicitMaterial = new Material(Shader.Find("Sprites/Default"));
            try
            {
                var light = lightObject.AddComponent<LightSprite>();
                light.Shadows = LightSprite.ShadowQuality.Balanced;
                light.Material = explicitMaterial;
                light.SendMessage("Update");

                // The generated draw material clones the explicit base material's shader, not the preset.
                Assert.That(light.DrawMaterial.shader.name, Is.EqualTo("Sprites/Default"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(lightObject);
                UnityEngine.Object.DestroyImmediate(explicitMaterial);
            }
        }

        // The one remaining fallback: Custom with no Material still uses Sprites/Default (the case the
        // inspector warns about), so the Custom raw-slot escape keeps working.
        [Test]
        public void CustomShadowsWithNullMaterialFallsBackToSpritesDefault()
        {
            var lightObject = new GameObject("Custom-no-material light");
            try
            {
                var light = lightObject.AddComponent<LightSprite>();
                light.Shadows = LightSprite.ShadowQuality.Custom;
                light.Material = null;
                light.SendMessage("Update");

                Assert.That(light.DrawMaterial, Is.Not.Null);
                Assert.That(light.DrawMaterial.shader.name, Is.EqualTo("Sprites/Default"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(lightObject);
            }
        }
    }
}
