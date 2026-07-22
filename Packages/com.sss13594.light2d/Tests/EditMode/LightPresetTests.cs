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
    }
}
