using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Light2D.Tests
{
    public class ContractTests
    {
        [Test]
        public void PipelineClassificationDistinguishesBuiltInUrpAndUnsupportedPipelines()
        {
            Assert.That(LightingSystem.ClassifyPipeline(null), Is.EqualTo(Light2DPipeline.BuiltIn));

            var urpAsset = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
            var unsupportedAsset = ScriptableObject.CreateInstance<UnsupportedPipelineAsset>();
            try
            {
                Assert.That(LightingSystem.ClassifyPipeline(urpAsset),
                    Is.EqualTo(Light2DPipeline.UniversalRenderPipeline));
                Assert.That(LightingSystem.ClassifyPipeline(unsupportedAsset),
                    Is.EqualTo(Light2DPipeline.UnsupportedScriptableRenderPipeline));
                Assert.That(LightingSystem.ActivePipeline,
                    Is.EqualTo(LightingSystem.ClassifyPipeline(GraphicsSettings.currentRenderPipeline)));
            }
            finally
            {
                Object.DestroyImmediate(urpAsset);
                Object.DestroyImmediate(unsupportedAsset);
            }
        }

        [Test]
        public void LegacyUtilityTypesAreRemovedFromTheRuntimeAssembly()
        {
            var runtimeAssembly = typeof(LightingSystem).Assembly;
            Assert.That(runtimeAssembly.GetType("Light2D.Util"), Is.Null);
            Assert.That(runtimeAssembly.GetType("Light2D.Point2"), Is.Null);
            Assert.That(runtimeAssembly.GetType("Light2D.ReadOnlyAttribute"), Is.Null);
        }

        [Test]
        public void RuntimeAssemblyExposesOnlyApprovedPublicTypes()
        {
            var approvedPublicTypes = new List<string>
            {
                "Light2D.CustomSprite",
                "Light2D.Light2DDiagnostics",
                "Light2D.Light2DFrameMetrics",
                "Light2D.Light2DPipeline",
                "Light2D.Light2DProfiling",
                "Light2D.Light2DUniversalRendererFeature",
                "Light2D.Light2DVolume",
                "Light2D.LightAmbientEmitter",
                "Light2D.LightObstacleGenerator",
                "Light2D.LightObstacleMesh",
                "Light2D.LightObstacleSprite",
                "Light2D.LightSprite",
                "Light2D.LightingSystem"
            };
#if LIGHT2D_TILEMAP
            // The Tilemap occluder is compiled into the runtime assembly only when com.unity.2d.tilemap is
            // present; the expected set follows the same define so it matches with and without the package.
            approvedPublicTypes.Add("Light2D.LightObstacleTilemap");
#endif

            var exportedTypes = typeof(LightingSystem).Assembly.GetExportedTypes()
                .Where(type => !type.IsNested)
                .Select(type => type.FullName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.That(exportedTypes, Is.EquivalentTo(approvedPublicTypes));
        }

        [Test]
        public void RuntimeAssemblyDefinitionHasNoEditorDependency()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(
                "Packages/com.sss13594.light2d/package.json");
            Assert.That(package, Is.Not.Null);

            string json = System.IO.File.ReadAllText(
                System.IO.Path.Combine(package.resolvedPath, "Runtime", "Light2D.Runtime.asmdef"));
            var asmdef = JsonUtility.FromJson<AsmdefData>(json);

            Assert.That(asmdef.name, Is.EqualTo("Light2D.Runtime"));
            Assert.That(asmdef.includePlatforms ?? Array.Empty<string>(), Is.Empty,
                "The runtime assembly must not be platform-restricted to the editor.");
            Assert.That(asmdef.references ?? Array.Empty<string>(),
                Has.None.Contains("Editor"), "The runtime assembly must not reference editor assemblies.");
        }

        [Test]
        public void AdditiveColorUvEncodingPreservesLegacyPacking()
        {
            var opaqueWhite = Light2DColorEncoding.EncodeAdditiveColorToUv(Color.white);
            Assert.That(opaqueWhite.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(opaqueWhite.y, Is.EqualTo(254f / 255f).Within(0.0001f));

            var transparentBlack = Light2DColorEncoding.EncodeAdditiveColorToUv(Color.clear);
            Assert.That(transparentBlack, Is.EqualTo(Vector2.zero));

            var halfRed = Light2DColorEncoding.EncodeAdditiveColorToUv(new Color(0.5f, 0f, 0f, 0f));
            Assert.That(halfRed.x, Is.EqualTo(127f / 255f).Within(0.0001f));
            Assert.That(halfRed.y, Is.Zero);
        }

        private sealed class UnsupportedPipelineAsset : RenderPipelineAsset
        {
            protected override RenderPipeline CreatePipeline()
            {
                return null;
            }
        }

        [Serializable]
        private sealed class AsmdefData
        {
            public string name;
            public string[] references;
            public string[] includePlatforms;
        }
    }
}
