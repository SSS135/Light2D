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
