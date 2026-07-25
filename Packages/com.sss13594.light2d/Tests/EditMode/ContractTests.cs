using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

        [Test]
        public void ObstacleColorFlowsToObstacleGlobalMulShaderProperty()
        {
            // The Built-in obstacle pass pushes LightingSystem.ObstacleColor onto every obstacle draw through
            // a MaterialPropertyBlock keyed on _Light2DObstacleGlobalMul (RenderObstacles). That SetColor runs
            // only inside OnRenderImage, so without a render loop EditMode asserts the static plumbing instead:
            // the serialized default, the private property-ID constant the pass writes to, and that the
            // obstacle shader exposes it.
            var cameraObject = new GameObject("Obstacle color plumbing");
            try
            {
                var system = cameraObject.AddComponent<LightingSystem>();
                // The serialized default is the byte-identical no-op multiplier (white, full alpha).
                Assert.That(system.ObstacleColor, Is.EqualTo(Color.white));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }

            // The pass writes ObstacleColor to exactly this property ID.
            var idField = typeof(LightingSystem).GetField("ObstacleGlobalMulId",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(idField, Is.Not.Null, "LightingSystem.ObstacleGlobalMulId plumbing constant is missing");
            Assert.That((int)idField.GetValue(null),
                Is.EqualTo(Shader.PropertyToID("_Light2DObstacleGlobalMul")));

            // The Built-in obstacle shader declares the property the pass drives.
            var shader = Shader.Find("Light2D/Transparent Dual Color Normal Mapped");
            Assert.That(shader, Is.Not.Null, "The Built-in obstacle shader must be present");
            var material = new Material(shader);
            try
            {
                Assert.That(material.HasProperty("_Light2DObstacleGlobalMul"), Is.True,
                    "The obstacle shader must expose the _Light2DObstacleGlobalMul multiplier the pass writes");
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void NormalMapHeightFlowsToLightPosZShaderProperty()
        {
            // The Built-in normal-mapped source pass packs LightSprite.NormalMapHeight into the z component of
            // the _LightPos vector it sets per light (RenderNormalMappedLightSources:
            // new Vector4(center.x, center.y, light.NormalMapHeight, 0f)), and the Normal Mapped Light shader
            // reads _LightPos.z. That SetVector runs only inside OnRenderImage, so without a render loop
            // EditMode asserts the static plumbing: the serialized default height and the private _LightPos
            // property-ID constant the pass writes to.
            var lightObject = new GameObject("Normal map height plumbing");
            try
            {
                var light = lightObject.AddComponent<LightSprite>();
                // A fresh light sits one unit above the plane.
                Assert.That(light.NormalMapHeight, Is.EqualTo(1f));
                // Both backends' normal-mapped passes gate on the sprite the light actually draws
                // (LightSprite.ActiveLightSprite), never on the override-only Sprite: a fresh light has no
                // override yet still draws its generated falloff sprite, so it must reach the normal-mapped
                // path and receive its own _LightPos.z. Gating on Sprite dropped every such light.
                Assert.That(light.Sprite, Is.Null, "A fresh light must carry no Sprite override");
                Assert.That(light.ActiveLightSprite, Is.Not.Null,
                    "A light with no Sprite override must still expose its generated falloff sprite to the " +
                    "normal-mapped light passes");
            }
            finally
            {
                Object.DestroyImmediate(lightObject);
            }

            var idField = typeof(LightingSystem).GetField("LightPosId",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(idField, Is.Not.Null, "LightingSystem.LightPosId plumbing constant is missing");
            Assert.That((int)idField.GetValue(null), Is.EqualTo(Shader.PropertyToID("_LightPos")));

            // The Built-in Normal Mapped Light shader that consumes _LightPos.z must be present.
            Assert.That(Shader.Find("Light2D/Internal/Normal Mapped Light"), Is.Not.Null,
                "The Built-in Normal Mapped Light shader that reads _LightPos.z must be present");
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
