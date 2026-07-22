using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace Light2D.Tests
{
    public class ShaderImportTests
    {
        [Test]
        public void EveryPackageShaderImportsWithoutCompilerErrors()
        {
            string[] paths = AssetDatabase.FindAssets("t:Shader", new[] { "Packages/com.sss13594.light2d/Runtime" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path)
                .ToArray();
            Assert.That(paths, Is.Not.Empty);

            foreach (string path in paths)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                Assert.That(shader, Is.Not.Null, path);
                Assert.That(shader.isSupported, Is.True, path + " is unsupported by the active editor graphics API");
                var errors = ShaderUtil.GetShaderMessages(shader)
                    .Where(message => message.severity == ShaderCompilerMessageSeverity.Error)
                    .Select(message => message.platform + ": " + message.message)
                    .ToArray();
                Assert.That(errors, Is.Empty, path + "\n" + string.Join("\n", errors));
            }
        }

        [Test]
        public void CollapsedLightPointsShaderImportsWithoutErrors()
        {
            var shader = Shader.Find("Light2D/Light Points");
            Assert.That(shader, Is.Not.Null,
                "The collapsed Light2D/Light Points shader must exist after the 12->1 shader collapse.");
            Assert.That(shader.isSupported, Is.True);

            var errors = ShaderUtil.GetShaderMessages(shader)
                .Where(message => message.severity == ShaderCompilerMessageSeverity.Error)
                .Select(message => message.platform + ": " + message.message)
                .ToArray();
            Assert.That(errors, Is.Empty, string.Join("\n", errors));

            // Built-in CGPROGRAM SubShader plus the URP UniversalPipeline SubShader.
            Assert.That(shader.subshaderCount, Is.GreaterThanOrEqualTo(2));
            // The uniform sample count is exposed as a per-material property driving the bounded [loop].
            Assert.That(shader.FindPropertyIndex("_PathTrackingSamples"), Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void RetiredPerCountLightShadersAreRemoved()
        {
            string[] retiredNames =
            {
                "Light2D/Light 8 Points", "Light2D/Light 20 Points", "Light2D/Light 30 Points",
                "Light2D/Light 40 Points", "Light2D/Light 50 Points", "Light2D/Light 60 Points",
                "Light2D/Light 70 Points", "Light2D/Light 80 Points", "Light2D/Light 90 Points"
            };
            foreach (string name in retiredNames)
                Assert.That(Shader.Find(name), Is.Null, name + " should be retired by the 12->1 collapse.");

            // The structurally distinct light shaders are retained.
            Assert.That(Shader.Find("Light2D/Light 1 Point"), Is.Not.Null);
            Assert.That(Shader.Find("Light2D/Light Auto Points"), Is.Not.Null);
            Assert.That(Shader.Find("Light2D/Light Ignoring Obstacles"), Is.Not.Null);
        }
    }
}
