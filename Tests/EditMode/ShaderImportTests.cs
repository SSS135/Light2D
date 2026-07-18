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
    }
}
