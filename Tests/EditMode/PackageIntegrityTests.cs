using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Light2D.Tests
{
    public class PackageIntegrityTests
    {
        private const string PackageRoot = "Packages/com.sss13594.light2d";
        private const string RuntimeRoot = PackageRoot + "/Runtime/";
        private const string DefaultLightPath = RuntimeRoot + "Resources/DefaultLight.psd";
        private const string Light60PointsGuid = "5d8645bc2e5501a4ebd138db5aa609bd";

        private static readonly string[] RuntimeLightMaterialNames =
        {
            "Light1Point",
            "Light8Points",
            "Light20Points",
            "Light30Points",
            "Light40Points",
            "Light50Points",
            "Light60Points",
            "Light70Points",
            "Light80Points",
            "Light90Points",
            "LightAutoPoints",
            "LightIgnoreObstacles"
        };

        [Test]
        public void PackageMetadataDeclaresSupportedUnityAndUrp()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(PackageRoot + "/package.json");
            Assert.That(package, Is.Not.Null);
            Assert.That(package.name, Is.EqualTo("com.sss13594.light2d"));
            Assert.That(package.version, Is.EqualTo("2.0.0"));

            string json = File.ReadAllText(Path.Combine(package.resolvedPath, "package.json"));
            StringAssert.Contains("\"unity\": \"6000.4\"", json);
            StringAssert.Contains("\"com.unity.render-pipelines.universal\": \"17.4.0\"", json);
        }

        [Test]
        public void RequiredAssetsAndPrefabConfigurationAreValid()
        {
            string[] requiredPaths =
            {
                PackageRoot + "/Runtime/Resources/Lighting Camera.prefab",
                DefaultLightPath,
                PackageRoot + "/Runtime/Materials/LightOverlay.mat",
                PackageRoot + "/Runtime/Materials/Light1Point.mat",
                PackageRoot + "/Runtime/Materials/DualColor.mat",
                PackageRoot + "/Runtime/Materials/AmbientLightComputer.mat",
                PackageRoot + "/Runtime/Materials/FastBlur.mat"
            };

            foreach (string path in requiredPaths)
                Assert.That(AssetDatabase.LoadMainAssetAtPath(path), Is.Not.Null, path);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(requiredPaths[0]);
            var config = prefab.GetComponent<LightingSystemPrefabConfig>();
            Assert.That(prefab.GetComponent<Camera>(), Is.Not.Null);
            Assert.That(config, Is.Not.Null);
            Assert.That(config.AmbientLightComputeMaterial, Is.Not.Null);
            Assert.That(config.LightOverlayMaterial, Is.Not.Null);
            Assert.That(config.BlurMaterial, Is.Not.Null);

            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { PackageRoot + "/Runtime" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.That(material, Is.Not.Null, path);
                Assert.That(material.shader, Is.Not.Null, path);
            }
        }

        [Test]
        public void RuntimeLightMaterialsUseRuntimeDefaultTexture()
        {
            var defaultTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(DefaultLightPath);
            Assert.That(defaultTexture, Is.Not.Null);

            foreach (string materialName in RuntimeLightMaterialNames)
            {
                string materialPath = RuntimeRoot + "Materials/" + materialName + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                Assert.That(material, Is.Not.Null, materialPath);
                Assert.That(material.mainTexture, Is.SameAs(defaultTexture), materialPath);
            }
        }

        [Test]
        public void RuntimeMaterialReferencesStayWithinRuntime()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { PackageRoot + "/Runtime" }))
            {
                string materialPath = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                var property = new SerializedObject(material).GetIterator();

                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference ||
                        property.objectReferenceEntityIdValue == EntityId.None)
                        continue;

                    Object reference = property.objectReferenceValue;
                    Assert.That(reference, Is.Not.Null, materialPath + ":" + property.propertyPath);

                    string referencePath = AssetDatabase.GetAssetPath(reference);
                    Assert.That(referencePath.StartsWith(RuntimeRoot), Is.True,
                        materialPath + ":" + property.propertyPath + " -> " + referencePath);
                }
            }
        }

        [Test]
        public void RuntimePrefabMaterialReferencesStayWithinRuntime()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PackageRoot + "/Runtime" }))
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                foreach (string dependencyPath in AssetDatabase.GetDependencies(prefabPath, false))
                {
                    if (AssetDatabase.GetMainAssetTypeAtPath(dependencyPath) != typeof(Material))
                        continue;

                    Assert.That(dependencyPath.StartsWith(RuntimeRoot), Is.True,
                        prefabPath + " -> " + dependencyPath);
                }
            }
        }

        [Test]
        public void CreateLightSourceUsesLight60PointsGuidAndRuntimeAssets()
        {
            string materialPath = AssetDatabase.GUIDToAssetPath(Light60PointsGuid);
            Assert.That(materialPath, Is.EqualTo(RuntimeRoot + "Materials/Light60Points.mat"));
            var expectedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Assert.That(expectedMaterial, Is.Not.Null);

            Object[] previousSelection = Selection.objects;
            GameObject createdObject = null;
            try
            {
                Light2DMenu.CreateLightSource();
                createdObject = Selection.activeGameObject;
                Assert.That(createdObject, Is.Not.Null);

                var light = createdObject.GetComponent<LightSprite>();
                Assert.That(light, Is.Not.Null);
                Assert.That(light.Material, Is.SameAs(expectedMaterial));
                Assert.That(AssetDatabase.GetAssetPath(light.Sprite), Is.EqualTo(DefaultLightPath));
            }
            finally
            {
                if (createdObject != null)
                    Object.DestroyImmediate(createdObject);
                Selection.objects = previousSelection;
            }
        }

        [Test]
        public void PackagePrefabsContainNoMissingScripts()
        {
            var failures = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PackageRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Samples~/"))
                    continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (Transform transform in prefab.GetComponentsInChildren<Transform>(true))
                {
                    int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                    if (missing > 0)
                        failures.Add(path + ":" + GetHierarchyPath(transform) + " (" + missing + ")");
                }
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        private static string GetHierarchyPath(Transform transform)
        {
            return string.Join("/", transform.GetComponentsInParent<Transform>(true).Reverse().Select(item => item.name));
        }
    }
}
