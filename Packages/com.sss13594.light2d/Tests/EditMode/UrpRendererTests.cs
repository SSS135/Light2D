using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

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
                UnityEngine.Object.DestroyImmediate(feature);
                UnityEngine.Object.DestroyImmediate(rendererData);
            }
        }

        [Test]
        public void RendererFeatureStatusTreatsAnInactiveFeatureAsNotInstalled()
        {
            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            var feature = ScriptableObject.CreateInstance<Light2DUniversalRendererFeature>();
            var duplicate = ScriptableObject.CreateInstance<Light2DUniversalRendererFeature>();
            try
            {
                Assert.That(Light2DUniversalRendererFeatureInstaller.EvaluateRendererFeatures(rendererData),
                    Is.EqualTo(Light2DRendererFeatureStatus.Missing));

                rendererData.rendererFeatures.Add(feature);
                Assert.That(Light2DUniversalRendererFeatureInstaller.EvaluateRendererFeatures(rendererData),
                    Is.EqualTo(Light2DRendererFeatureStatus.Installed));

                feature.SetActive(false);
                Assert.That(Light2DUniversalRendererFeatureInstaller.EvaluateRendererFeatures(rendererData),
                    Is.EqualTo(Light2DRendererFeatureStatus.Inactive),
                    "URP skips inactive renderer features, so a disabled feature must warn instead of reporting Installed.");

                rendererData.rendererFeatures.Add(duplicate);
                Assert.That(Light2DUniversalRendererFeatureInstaller.EvaluateRendererFeatures(rendererData),
                    Is.EqualTo(Light2DRendererFeatureStatus.Duplicate),
                    "Duplicate detection stays ahead of the inactive check.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(duplicate);
                UnityEngine.Object.DestroyImmediate(feature);
                UnityEngine.Object.DestroyImmediate(rendererData);
            }
        }

        [Test]
        public void RendererFeatureOwnsPassAndContextPerLightingSystem()
        {
            Type featureType = typeof(Light2DUniversalRendererFeature);
            FieldInfo contexts = featureType.GetField("_contexts", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo passes = featureType.GetField("_passes", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(contexts, Is.Not.Null);
            Assert.That(passes, Is.Not.Null);
            Assert.That(typeof(IDictionary).IsAssignableFrom(contexts.FieldType), Is.True);
            Assert.That(typeof(IDictionary).IsAssignableFrom(passes.FieldType), Is.True);

            Type passType = featureType.GetNestedType("Light2DPass", BindingFlags.NonPublic);
            FieldInfo lighting = passType.GetField("_lighting", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(lighting, Is.Not.Null);
            Assert.That(lighting.IsInitOnly, Is.True, "Each enqueued pass must retain immutable camera input");
            Assert.That(passType.GetField("_temporarilyCulledLights", BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Null, "URP rendering must not mutate Renderer.forceRenderingOff");
        }

        [Test]
        public void CameraPolicyRejectsXrOverlayAndStackedBaseCameras()
        {
            string reason;
            Assert.That(Light2DUniversalRendererFeature.IsCameraSupported(true, CameraRenderType.Base, false,
                out reason), Is.False);
            StringAssert.Contains("XR", reason);

            Assert.That(Light2DUniversalRendererFeature.IsCameraSupported(false, CameraRenderType.Overlay, false,
                out reason), Is.False);
            StringAssert.Contains("Overlay", reason);

            Assert.That(Light2DUniversalRendererFeature.IsCameraSupported(false, CameraRenderType.Base, true,
                out reason), Is.False);
            StringAssert.Contains("camera stack", reason);

            // XR rejection wins over every other condition, matching the production check order.
            Assert.That(Light2DUniversalRendererFeature.IsCameraSupported(true, CameraRenderType.Overlay, true,
                out reason), Is.False);
            StringAssert.Contains("XR", reason);
        }

        [Test]
        public void XrCameraInspectorWarningRequiresAnXrCapableProject()
        {
            Assert.That(LightingSystemEditor.ShouldWarnAboutXrCamera(false), Is.False,
                "A camera with Allow XR Rendering off is never an XR camera.");

#if LIGHT2D_XR
            // XR Plug-in Management is installed, so a provider can initialize and the warning must stand.
            Assert.That(LightingSystemEditor.IsProjectXrCapable(), Is.True);
            Assert.That(LightingSystemEditor.ShouldWarnAboutXrCamera(true), Is.True);
#else
            // No XR package: only an already-initialized XR display may report capability, and the
            // default-true allowXRRendering flag must not warn on its own.
            Assert.That(LightingSystemEditor.IsProjectXrCapable(), Is.EqualTo(XRSRPSettings.enabled),
                "Without an XR package, XR capability must come from an active XR display only.");
            Assert.That(LightingSystemEditor.ShouldWarnAboutXrCamera(true), Is.EqualTo(XRSRPSettings.enabled),
                "UniversalAdditionalCameraData.allowXRRendering defaults to true on every URP camera, so the " +
                "flag alone must not raise the XR warning in a project without XR.");
#endif
        }

        [Test]
        public void CameraPolicyAcceptsIndependentBaseCameras()
        {
            string reason;
            Assert.That(Light2DUniversalRendererFeature.IsCameraSupported(false, CameraRenderType.Base, false,
                out reason), Is.True);
            Assert.That(reason, Is.Null);

            // A plain Base camera without URP additional data reports no active stack.
            var cameraObject = new GameObject("Independent Base Camera", typeof(Camera));
            try
            {
                var camera = cameraObject.GetComponent<Camera>();
                Assert.That(Light2DUniversalRendererFeature.HasActiveCameraStack(camera), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void CameraWarningsAreIssuedOncePerCamera()
        {
            var feature = ScriptableObject.CreateInstance<Light2DUniversalRendererFeature>();
            var cameraObjectA = new GameObject("Unsupported Camera A", typeof(Camera));
            var cameraObjectB = new GameObject("Unsupported Camera B", typeof(Camera));
            try
            {
                var cameraA = cameraObjectA.GetComponent<Camera>();
                var cameraB = cameraObjectB.GetComponent<Camera>();
                const string reason = "Light2D camera policy test.";
                int warnings = 0;
                Application.LogCallback counter = (message, stackTrace, type) =>
                {
                    if (type == LogType.Warning && message.Contains("Rendering was skipped"))
                        warnings++;
                };
                Application.logMessageReceived += counter;
                try
                {
                    LogAssert.Expect(LogType.Warning,
                        reason + " Rendering was skipped for camera 'Unsupported Camera A'.");
                    LogAssert.Expect(LogType.Warning,
                        reason + " Rendering was skipped for camera 'Unsupported Camera B'.");
                    feature.WarnOnce(cameraA, null, reason);
                    feature.WarnOnce(cameraA, null, reason);
                    feature.WarnOnce(cameraB, null, reason);
                    feature.WarnOnce(cameraB, null, reason);
                }
                finally
                {
                    Application.logMessageReceived -= counter;
                }

                // Each camera warns exactly once; a second camera is not suppressed by the first.
                Assert.That(warnings, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObjectA);
                UnityEngine.Object.DestroyImmediate(cameraObjectB);
                UnityEngine.Object.DestroyImmediate(feature);
            }
        }

        [Test]
        public void ImportedShadersSatisfyStructuralContracts()
        {
            Shader normalDrawer = Shader.Find("Light2D/Internal/Normal Map Drawer");
            Shader obstacle = Shader.Find("Light2D/Transparent Normal Mapped");
            Shader dualColorObstacle = Shader.Find("Light2D/Transparent Dual Color Normal Mapped");
            Shader overlay = Shader.Find("Light2D/Light Overlay");
            Shader obstaclePostProcessor = Shader.Find("Light2D/Obstacle Texture Post Porcessor");
            Assert.That(normalDrawer, Is.Not.Null);
            Assert.That(obstacle, Is.Not.Null);
            Assert.That(dualColorObstacle, Is.Not.Null);
            Assert.That(overlay, Is.Not.Null);
            Assert.That(obstaclePostProcessor, Is.Not.Null);

            // Normal buffers bind through the Light2DNormals LightMode in both backends.
            Assert.That(HasPassWithLightMode(normalDrawer, "Light2DNormals"), Is.True);
            Assert.That(HasPassWithLightMode(obstacle, "Light2DNormals"), Is.True);
            Assert.That(HasPassWithLightMode(dualColorObstacle, "Light2DNormals"), Is.True);

            // URP renderer lists draw obstacle geometry through SRPDefaultUnlit.
            Assert.That(HasPassWithLightMode(obstacle, "SRPDefaultUnlit"), Is.True);
            Assert.That(HasPassWithLightMode(dualColorObstacle, "SRPDefaultUnlit"), Is.True);

            // Fullscreen composition passes carry the Light2DFullscreen LightMode.
            Assert.That(HasPassWithLightMode(overlay, "Light2DFullscreen"), Is.True);
            Assert.That(HasPassWithLightMode(obstaclePostProcessor, "Light2DFullscreen"), Is.True);

            // Built-in replacement rendering selects obstacles by the LightObstacle=True tag.
            Assert.That(HasSubshaderTag(normalDrawer, "LightObstacle", "True"), Is.True);
            Assert.That(HasSubshaderTag(obstacle, "LightObstacle", "True"), Is.True);
            Assert.That(HasSubshaderTag(dualColorObstacle, "LightObstacle", "True"), Is.True);

            // Serialized-material compatibility property names.
            Assert.That(obstacle.FindPropertyIndex("_NormalTex"), Is.GreaterThanOrEqualTo(0));
            Assert.That(dualColorObstacle.FindPropertyIndex("_NormalTex"), Is.GreaterThanOrEqualTo(0));
            Assert.That(overlay.FindPropertyIndex("_GameTex"), Is.GreaterThanOrEqualTo(0));
            Assert.That(overlay.FindPropertyIndex("_AmbientLightTex"), Is.GreaterThanOrEqualTo(0));
            Assert.That(overlay.FindPropertyIndex("_LightSourcesTex"), Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void CollapsedLightPointsShaderCarriesBothPipelines()
        {
            Shader lightPoints = Shader.Find("Light2D/Light Points");
            Assert.That(lightPoints, Is.Not.Null,
                "The collapsed Light2D/Light Points shader replaces the retired per-count light shaders.");

            // URP draws the light geometry through the SRPDefaultUnlit LightMode; a Built-in CGPROGRAM
            // SubShader (no RenderPipeline tag) coexists in the same shader, so both pipelines render it.
            Assert.That(HasPassWithLightMode(lightPoints, "SRPDefaultUnlit"), Is.True);
            Assert.That(lightPoints.subshaderCount, Is.GreaterThanOrEqualTo(2));

            // The sample count is a per-material property that drives the uniform-bound ray-march [loop].
            Assert.That(lightPoints.FindPropertyIndex("_PathTrackingSamples"), Is.GreaterThanOrEqualTo(0));
        }

        // A material value always overrides Shader/CommandBuffer.SetGlobal*, so a uniform declared in a
        // Properties block can only be driven per-material. The two fullscreen composition shaders sit on
        // opposite sides of that rule for the same three sampling-transform uniforms, and each backend's
        // delivery mechanism must match its shader: Light2D/Coverage Blend declares them (both backends set
        // them on the coverage-blend material) while Light2D/Light Overlay does not (the URP overlay pass
        // delivers them as globals). Moving either shader across the line without moving its C# delivery
        // silently reverts to the Properties defaults asserted below - which is exactly how URP lost the
        // extended->small coverage remap and rendered the coverage region extended/small too small.
        [Test]
        public void FullscreenSamplingTransformDeliveryMatchesShaderProperties()
        {
            Shader coverageBlend = Shader.Find("Light2D/Coverage Blend");
            Shader overlay = Shader.Find("Light2D/Light Overlay");
            Assert.That(coverageBlend, Is.Not.Null);
            Assert.That(overlay, Is.Not.Null);

            foreach (string uniform in new[] { "_Scale", "_Offset", "_ExtendedToSmallTextureScale" })
            {
                Assert.That(coverageBlend.FindPropertyIndex(uniform), Is.GreaterThanOrEqualTo(0),
                    uniform + " is a Light2D/Coverage Blend material property, so both backends must push it " +
                    "onto the coverage-blend material; a global can never reach it.");
                Assert.That(overlay.FindPropertyIndex(uniform), Is.LessThan(0),
                    uniform + " must stay out of Light2D/Light Overlay's Properties block: the URP overlay pass " +
                    "delivers it as a global, which a material property would override.");
            }

            var material = new Material(coverageBlend);
            try
            {
                // The identity mapping a global-only coverage blend renders with: no extended->small remap.
                Vector4 extendedToSmall = material.GetVector("_ExtendedToSmallTextureScale");
                Vector4 scale = material.GetVector("_Scale");
                Vector4 offset = material.GetVector("_Offset");
                Assert.That(extendedToSmall.x, Is.EqualTo(1f));
                Assert.That(extendedToSmall.y, Is.EqualTo(1f));
                Assert.That(scale.x, Is.EqualTo(1f));
                Assert.That(scale.y, Is.EqualTo(1f));
                Assert.That(offset.x, Is.EqualTo(0f));
                Assert.That(offset.y, Is.EqualTo(0f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        private static bool HasPassWithLightMode(Shader shader, string lightMode)
        {
            var lightModeTag = new ShaderTagId("LightMode");
            var expected = new ShaderTagId(lightMode);
            for (int subshader = 0; subshader < shader.subshaderCount; subshader++)
            {
                for (int pass = 0; pass < shader.GetPassCountInSubshader(subshader); pass++)
                {
                    if (shader.FindPassTagValue(subshader, pass, lightModeTag) == expected)
                        return true;
                }
            }
            return false;
        }

        private static bool HasSubshaderTag(Shader shader, string tagName, string value)
        {
            var tag = new ShaderTagId(tagName);
            var expected = new ShaderTagId(value);
            for (int subshader = 0; subshader < shader.subshaderCount; subshader++)
            {
                if (shader.FindSubshaderTagValue(subshader, tag) == expected)
                    return true;
            }
            return false;
        }
    }
}
