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
        // The unified creation flow now uses Balanced shadow quality → Light40Points.
        private const string Light40PointsGuid = "ab37f3042fbc56e4b9288c3ba9cd32d8";

        // The five preset materials (8/40/90/Auto/Ignore) plus Light20/80Points, which Small Examples
        // binds as ShadowQuality.Custom raw-slot materials. The other per-count materials were deleted
        // with the per-count shader collapse (Light1Point/30/50/60/70).
        private static readonly string[] RuntimeLightMaterialNames =
        {
            "Light8Points",
            "Light20Points",
            "Light40Points",
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
            StringAssert.Contains("\"unity\": \"6000.3\"", json);
            StringAssert.Contains("\"com.unity.render-pipelines.universal\": \"17.3.0\"", json);
        }

        [Test]
        public void RequiredAssetsAndPrefabConfigurationAreValid()
        {
            string[] requiredPaths =
            {
                DefaultLightPath,
                PackageRoot + "/Runtime/Materials/LightOverlay.mat",
                PackageRoot + "/Runtime/Materials/DualColor.mat",
                PackageRoot + "/Runtime/Materials/AmbientLightComputer.mat",
                PackageRoot + "/Runtime/Materials/FastBlur.mat"
            };

            foreach (string path in requiredPaths)
                Assert.That(AssetDatabase.LoadMainAssetAtPath(path), Is.Not.Null, path);

            // v3 retires the Built-in helper camera and its "Lighting Camera.prefab"; the default
            // composition materials the creation flow reads now live on an edit-time ScriptableObject
            // under Runtime/. Load it directly and assert its three material fields resolve.
            var defaults = AssetDatabase.LoadAssetAtPath<Light2DDefaultResources>(
                RuntimeRoot + "Light2DDefaultResources.asset");
            Assert.That(defaults, Is.Not.Null,
                "The default resources asset was not found at " + RuntimeRoot + "Light2DDefaultResources.asset");
            Assert.That(defaults.AmbientLightComputeMaterial, Is.Not.Null);
            Assert.That(defaults.LightOverlayMaterial, Is.Not.Null);
            Assert.That(defaults.BlurMaterial, Is.Not.Null);
            // The obstacle components seed their hidden Material slot from this field on Reset.
            Assert.That(defaults.ObstacleMaterial, Is.Not.Null);

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
                    if (property.propertyType != SerializedPropertyType.ObjectReference)
                        continue;
                    // The serialized null-reference accessor differs across the supported editor range:
                    // objectReferenceEntityIdValue is the 6000.5+ API (and instanceID is obsolete-as-error
                    // there), while 6000.3/6000.4 have only objectReferenceInstanceIDValue.
#if UNITY_6000_5_OR_NEWER
                    if (property.objectReferenceEntityIdValue == EntityId.None)
                        continue;
#else
                    if (property.objectReferenceInstanceIDValue == 0)
                        continue;
#endif

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
        public void CreateLightSourceUsesLight40PointsGuidAndGeneratedFalloff()
        {
            string materialPath = AssetDatabase.GUIDToAssetPath(Light40PointsGuid);
            Assert.That(materialPath, Is.EqualTo(RuntimeRoot + "Materials/Light40Points.mat"));
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
                Assert.That(light.Shadows, Is.EqualTo(LightSprite.ShadowQuality.Balanced));
                // Post-refactor the falloff is procedural: no Sprite asset is assigned.
                Assert.That(light.Sprite, Is.Null);
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
