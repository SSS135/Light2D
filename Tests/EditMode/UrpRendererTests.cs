using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Light2D.Tests
{
    public class UrpRendererTests
    {
        [Test]
        public void UniversalRendererFeatureSetupIsDeterministic()
        {
            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            var feature = ScriptableObject.CreateInstance<Light2DUniversalRendererFeature>();
            try
            {
                rendererData.rendererFeatures.Add(feature);
                feature.Create();
                feature.Create();

                Assert.That(rendererData.rendererFeatures.Count(item => item is Light2DUniversalRendererFeature),
                    Is.EqualTo(1));
                Assert.That(feature.isActive, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(feature);
                Object.DestroyImmediate(rendererData);
            }
        }

        [Test]
        public void NormalAndFullscreenRenderGraphSetupMatchesShaderContracts()
        {
            const string packageRoot = "Packages/com.sss13594.light2d";
            PackageInfo package = PackageInfo.FindForAssetPath(packageRoot + "/package.json");
            Assert.That(package, Is.Not.Null);
            string normalShader = File.ReadAllText(Path.Combine(package.resolvedPath, "Runtime", "Resources",
                "Shaders", "NormalMap.shader"));
            string obstacleShader = File.ReadAllText(Path.Combine(package.resolvedPath, "Runtime", "Resources",
                "Shaders", "TransparentNormalMapped.shader"));
            string dualColorObstacleShader = File.ReadAllText(Path.Combine(package.resolvedPath, "Runtime", "Resources",
                "Shaders", "TransparentDualColorNormalMapped.shader"));
            string common = File.ReadAllText(Path.Combine(package.resolvedPath, "Runtime", "Resources",
                "Shaders", "Light2DURPCommon.hlsl"));
            string fullscreen = File.ReadAllText(Path.Combine(package.resolvedPath, "Runtime", "Resources",
                "Shaders", "Light2DURPFullscreen.hlsl"));
            string renderer = File.ReadAllText(Path.Combine(package.resolvedPath, "Runtime", "Resources",
                "Shaders", "Light2DURPRenderer.hlsl"));
            string overlayShader = File.ReadAllText(Path.Combine(package.resolvedPath, "Runtime", "Resources",
                "Shaders", "LightOverlay.shader"));
            string source = File.ReadAllText(Path.Combine(package.resolvedPath, "Runtime", "URP",
                "Light2DUniversalRendererFeature.cs"));
            StringAssert.Contains("Name \"NormalMap\"", normalShader);
            StringAssert.Contains("Shader \"Light2D/Internal/Normal Map Drawer\"", normalShader);
            StringAssert.Contains("_NormalTex (\"Normalmap\", 2D)", obstacleShader);
            StringAssert.Contains("_NormalTex (\"Normalmap\", 2D)", dualColorObstacleShader);
            StringAssert.Contains("Tags { \"LightMode\"=\"Light2DNormals\" }", obstacleShader);
            StringAssert.Contains("Tags { \"LightMode\"=\"Light2DNormals\" }", dualColorObstacleShader);
            StringAssert.Contains("new ShaderTagId(\"Light2DNormals\")", source);
            StringAssert.Contains("1 << _lighting.LightObstaclesLayer, NormalTags", source);
            StringAssert.DoesNotContain("overrideMaterial", source);
            StringAssert.DoesNotContain("NormalMapMaterial", source);

            string[] renderGraphTextures =
            {
                "MainTex", "ObstacleTex", "GameTex", "AmbientLightTex", "LightSourcesTex", "NormalsBuffer"
            };
            foreach (string texture in renderGraphTextures)
            {
                string property = "_Light2DURP" + texture;
                StringAssert.Contains("TEXTURE2D(" + property + ")", common);
                StringAssert.Contains("Shader.PropertyToID(\"" + property + "\")", source);
            }

            StringAssert.Contains("SetGlobalTexture(UrpAmbientLightTex, pass.Ambient)", source);
            StringAssert.Contains("SAMPLE_TEXTURE2D(_Light2DURPAmbientLightTex", fullscreen);
            StringAssert.Contains("SAMPLE_TEXTURE2D(_Light2DURPGameTex", fullscreen);
            StringAssert.Contains("SAMPLE_TEXTURE2D(_Light2DURPLightSourcesTex", fullscreen);
            StringAssert.Contains("uint vertexID : SV_VertexID", fullscreen);
            StringAssert.Contains("GetFullScreenTriangleVertexPosition(input.vertexID)", fullscreen);
            StringAssert.Contains("ShaderLibrary/DynamicScaling.hlsl", fullscreen);
            StringAssert.Contains("DYNAMIC_SCALING_APPLY_SCALEBIAS(GetFullScreenTriangleTexCoord(input.vertexID))",
                fullscreen);
            StringAssert.Contains("output.color = half4(1.0h, 1.0h, 1.0h, 1.0h)", fullscreen);
            StringAssert.Contains("Light2DClipSpaceVert(Light2DClipSpaceAttributes input)", fullscreen);
            StringAssert.Contains("output.color = input.color", fullscreen);
            StringAssert.DoesNotContain("TransformWorldToHClip", fullscreen);
            StringAssert.Contains("(output.screenPosition.xy - 0.5 * output.screenPosition.w)", renderer);
            StringAssert.Contains("0.5 * output.screenPosition.w;", renderer);
            StringAssert.DoesNotContain("(output.screenPosition.xy - 0.5) *", renderer);
            StringAssert.Contains("smallProjection * smallView, smallCamera.Position", source);
            StringAssert.Contains("normals, normalsList, smallView, smallProjection", source);
            StringAssert.IsMatch(@"AddNormalLightPass\([^;]*\bsources\b[^;]*\bsmallCamera\s*\);", source);
            StringAssert.Contains("smallCamera.Projection * smallCamera.View", source);
            StringAssert.DoesNotContain("WorldToViewportPoint", source);
            StringAssert.DoesNotContain("SystemInfo.graphicsUVStartsAtTop", source);
            StringAssert.Contains("SetGlobalVector(LightPosition, pass.LightPosition)", source);
            StringAssert.Contains("Shader.PropertyToID(\"_ViewportToWorldX\")", source);
            StringAssert.Contains("Shader.PropertyToID(\"_ViewportToWorldY\")", source);
            StringAssert.Contains("smallCamera.View.inverse", source);
            StringAssert.Contains("2f * smallCamera.OrthographicSize * smallCamera.Aspect", source);
            StringAssert.Contains("SetGlobalVector(ViewportToWorldX, pass.ViewportToWorldX)", source);
            StringAssert.Contains("SetGlobalVector(ViewportToWorldY, pass.ViewportToWorldY)", source);
            StringAssert.Contains("float2 _ViewportToWorldX", common);
            StringAssert.Contains("float2 _ViewportToWorldY", common);
            StringAssert.Contains("viewportDelta.x * _ViewportToWorldX + viewportDelta.y * _ViewportToWorldY",
                fullscreen);
            StringAssert.Contains("half3(worldXYDisplacement, input.lightPosition.z)", fullscreen);
            StringAssert.Contains("_GameTex (\"Game\", 2D)", overlayShader);
            StringAssert.Contains("_AmbientLightTex (\"Ambient Light\", 2D)", overlayShader);
            StringAssert.Contains("_LightSourcesTex (\"Light Sources\", 2D)", overlayShader);

            int fallback = source.IndexOf("TextureHandle ambient = renderGraph.defaultResources.blackTexture");
            Assert.That(fallback, Is.GreaterThanOrEqualTo(0));
            int guard = source.IndexOf("if (_lighting.EnableAmbientLight)", fallback);
            int historySetup = source.IndexOf("context.EnsureHistory", fallback);
            Assert.That(guard, Is.GreaterThan(fallback));
            Assert.That(historySetup, Is.GreaterThan(guard));
        }
    }
}
