using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Light2D.Tests
{
    public class EditorWorkflowTests
    {
        private const string UniversalPath = "Assets/Light2D-UniversalRendererData-Test.asset";
        private const string Renderer2DPath = "Assets/Light2D-Renderer2DData-Test.asset";
        private const string KnownDefectUnityVersion = "6000.4.7f1";
        private const string KnownDefectUrpVersion = "17.4.0";
        private const string KnownDefectShaderGraphPath =
            "Packages/com.unity.render-pipelines.universal/Shaders/AutodeskInteractive/AutodeskInteractive.shadergraph";

        [TearDown]
        public void TearDown()
        {
            Selection.objects = new Object[0];
            AssetDatabase.DeleteAsset(UniversalPath);
            AssetDatabase.DeleteAsset(Renderer2DPath);
            Undo.ClearAll();
        }

        [Test]
        public void SizeValidationRejectsUnsafeValuesWithoutThrowing()
        {
            var cameraObject = new GameObject("Size Validation Camera", typeof(Camera));
            try
            {
                var camera = cameraObject.GetComponent<Camera>();
                Assert.That(LightingSettingsValidator.TryCalculateSizes(camera, 0f, 3f, 30f, 10f,
                    true, true, true, false, out _, out var zeroError), Is.False);
                StringAssert.Contains("greater than zero", zeroError);

                Assert.That(LightingSettingsValidator.TryCalculateSizes(camera, float.NaN, 3f, 30f, 10f,
                    true, true, true, false, out _, out var nanError), Is.False);
                StringAssert.Contains("finite", nanError);
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void ObstacleMenuCreatesRendererSpecificComponents()
        {
            var spriteObject = new GameObject("Sprite", typeof(SpriteRenderer));
            var meshObject = new GameObject("Mesh", typeof(MeshRenderer), typeof(MeshFilter));
            var sourceMesh = CreateQuadMesh();
            var sourceTexture = new Texture2D(2, 2);
            var sourceMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = sourceTexture };
            try
            {
                meshObject.GetComponent<MeshFilter>().sharedMesh = sourceMesh;
                meshObject.GetComponent<MeshRenderer>().sharedMaterial = sourceMaterial;
                Selection.objects = new Object[] { spriteObject, meshObject };
                Light2DMenu.CreateLightObstacle();

                var spriteObstacle = spriteObject.transform.Find("Sprite Light Obstacle");
                var meshObstacle = meshObject.transform.Find("Mesh Light Obstacle");
                Assert.That(spriteObstacle, Is.Not.Null);
                var spriteComponent = spriteObstacle.GetComponent<LightObstacleSprite>();
                Assert.That(spriteComponent, Is.Not.Null);
                Assert.That(spriteComponent.GameSpriteRenderer,
                    Is.SameAs(spriteObject.GetComponent<SpriteRenderer>()));
                Assert.That(spriteObstacle.GetComponent<LightObstacleMesh>(), Is.Null);
                Assert.That(meshObstacle, Is.Not.Null);
                var meshComponent = meshObstacle.GetComponent<LightObstacleMesh>();
                Assert.That(meshComponent, Is.Not.Null);
                Assert.That(meshObstacle.GetComponent<LightObstacleSprite>(), Is.Null);
                // Out-of-box visual parity with the sprite path (CustomSprite.Color defaults to white).
                Assert.That(meshComponent.MultiplicativeColor, Is.EqualTo(new Color32(255, 255, 255, 255)));

                // A functional mesh obstacle mirrors the parent source at creation, not merely exists.
                var obstacleFilter = meshObstacle.GetComponent<MeshFilter>();
                var obstacleRenderer = meshObstacle.GetComponent<MeshRenderer>();
                Assert.That(obstacleFilter, Is.Not.Null);
                Assert.That(obstacleRenderer, Is.Not.Null);
                Assert.That(obstacleFilter.sharedMesh, Is.Not.Null);
                Assert.That(obstacleFilter.sharedMesh, Is.Not.SameAs(sourceMesh));
                Assert.That(obstacleFilter.sharedMesh.vertexCount, Is.EqualTo(sourceMesh.vertexCount));
                Assert.That(obstacleRenderer.enabled, Is.True);
                Assert.That(obstacleRenderer.sharedMaterial, Is.Not.Null);
                Assert.That(obstacleRenderer.sharedMaterial.mainTexture, Is.SameAs(sourceTexture));
            }
            finally
            {
                Object.DestroyImmediate(spriteObject);
                Object.DestroyImmediate(meshObject);
                Object.DestroyImmediate(sourceMaterial);
                Object.DestroyImmediate(sourceTexture);
                Object.DestroyImmediate(sourceMesh);
            }
        }

        private static Mesh CreateQuadMesh()
        {
            return new Mesh
            {
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f),
                    new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f)
                },
                triangles = new[] { 0, 2, 1, 2, 3, 1 }
            };
        }

        [Test]
        public void InstallerRepairsUniversalRendererFeatureListAndMap()
        {
            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            IgnoreKnownUrpHostFixtureDefect(rendererData);
            AssetDatabase.CreateAsset(rendererData, UniversalPath);
            // URP's ScriptableRendererData.OnValidate logs this error exactly once when the fixture
            // plants a null feature entry (features[0]=null) to simulate corruption. Observed on
            // healthy-guard hosts where the test actually executes; this expectation is unreachable
            // when the skip guard above fires.
            LogAssert.Expect(LogType.Error,
                "Light2D-UniversalRendererData-Test is missing RendererFeatures\n" +
                "This could be due to missing scripts or compile error.");
            VerifyPersistentInstaller(rendererData);
        }

        [Test]
        public void InstallerSupportsPersistentRenderer2DData()
        {
            var rendererData = ScriptableObject.CreateInstance<Renderer2DData>();
            IgnoreKnownUrpHostFixtureDefect(rendererData);
            AssetDatabase.CreateAsset(rendererData, Renderer2DPath);
            // As in InstallerRepairsUniversalRendererFeatureListAndMap, OnValidate logs this error
            // exactly once for the planted null feature entry. Observed on healthy-guard hosts where
            // the test actually executes; this expectation is unreachable when the skip guard above
            // fires.
            LogAssert.Expect(LogType.Error,
                "Light2D-Renderer2DData-Test is missing RendererFeatures\n" +
                "This could be due to missing scripts or compile error.");
            VerifyPersistentInstaller(rendererData);
        }

        [Test]
        public void InstallerRejectsNonPersistentRendererWithoutMutation()
        {
            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            try
            {
                Assert.Throws<System.InvalidOperationException>(() =>
                    Light2DUniversalRendererFeatureInstaller.Install(rendererData));
                Assert.That(rendererData.rendererFeatures, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(rendererData);
            }
        }

        private static void VerifyPersistentInstaller(ScriptableRendererData rendererData)
        {
            var serializedRenderer = new SerializedObject(rendererData);
            var features = serializedRenderer.FindProperty("m_RendererFeatures");
            var featureMap = serializedRenderer.FindProperty("m_RendererFeatureMap");
            features.arraySize = 1;
            features.GetArrayElementAtIndex(0).objectReferenceValue = null;
            featureMap.arraySize = 3;
            serializedRenderer.ApplyModifiedPropertiesWithoutUndo();

            var first = Light2DUniversalRendererFeatureInstaller.Install(rendererData);
            var second = Light2DUniversalRendererFeatureInstaller.Install(rendererData);
            Assert.That(second, Is.SameAs(first));
            Assert.That(rendererData.rendererFeatures, Has.Exactly(1).SameAs(first));

            serializedRenderer.Update();
            Assert.That(features.arraySize, Is.EqualTo(1));
            Assert.That(featureMap.arraySize, Is.EqualTo(1));
            Assert.That(featureMap.GetArrayElementAtIndex(0).longValue, Is.Not.Zero);

            Assert.That(Light2DUniversalRendererFeatureInstaller.Remove(rendererData), Is.EqualTo(1));
            serializedRenderer.Update();
            Assert.That(features.arraySize, Is.Zero);
            Assert.That(featureMap.arraySize, Is.Zero);
        }

        // Skips the persistent-renderer fixtures only when three robust conditions positively identify
        // the known-defect host: the editor version is exactly 6000.4.7f1, the URP package version is
        // exactly 17.4.0, and AutodeskInteractive.shadergraph loads as a non-Shader main asset. That
        // DefaultAsset import IS the load-bearing defect. The former fourth condition
        // (MonoScript.FromScriptableObject(rendererData) resolving to null) was dropped because
        // MonoScript resolution is Library-state-dependent: it was proven to resolve on a fresh Library
        // while the defect persists, so it is a Library artifact rather than a reliable defect signal.
        private static void IgnoreKnownUrpHostFixtureDefect(ScriptableRendererData rendererData)
        {
            if (Application.unityVersion != KnownDefectUnityVersion)
                return;

            var urpPackage = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(UniversalRenderPipeline).Assembly);
            var shaderGraphAsset = AssetDatabase.LoadMainAssetAtPath(KnownDefectShaderGraphPath);
            if (urpPackage?.version != KnownDefectUrpVersion || shaderGraphAsset == null ||
                shaderGraphAsset is Shader)
                return;

            Object.DestroyImmediate(rendererData);
            Assert.Ignore($"Unity {KnownDefectUnityVersion} with URP {KnownDefectUrpVersion} imported " +
                $"'{KnownDefectShaderGraphPath}' as '{shaderGraphAsset.GetType().FullName}' instead of Shader and " +
                "cannot resolve the renderer-data MonoScript; persistent renderer fixtures are unavailable.");
        }
    }
}
