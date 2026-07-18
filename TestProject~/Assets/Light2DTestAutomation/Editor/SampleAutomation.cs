using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Light2D.TestProject.Editor
{
    public static class SampleAutomation
    {
        internal const string PackageName = "com.sss13594.light2d";
        internal const string SampleName = "Examples";
        internal const string SmallExamplesSceneName = "Small Examples.unity";
        internal const string RocketSceneName = "Rocket.unity";

        public static void ImportExamples()
        {
            var sample = FindExamplesSample();
            var expectedScenes = GetScenePaths(sample.importPath);
            if (!sample.isImported || expectedScenes.Any(path => !File.Exists(path)))
            {
                if (!sample.Import(Sample.ImportOptions.OverridePreviousImports | Sample.ImportOptions.HideImportWindow))
                    throw new InvalidOperationException($"Failed to import the '{SampleName}' sample from {PackageName}.");

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            ValidateScenes(expectedScenes);
            Debug.Log($"Validated Light2D sample at '{sample.importPath}'.");
        }

        internal static string[] EnsureExamplesImported()
        {
            ImportExamples();
            return GetScenePaths(FindExamplesSample().importPath);
        }

        private static Sample FindExamplesSample()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForPackageName(PackageName);
            if (package == null)
                throw new InvalidOperationException($"Package '{PackageName}' is not registered. Refresh the package manifest first.");

            var sample = Sample.FindByPackage(package.name, package.version)
                .FirstOrDefault(candidate => string.Equals(candidate.displayName, SampleName, StringComparison.Ordinal));
            if (string.IsNullOrEmpty(sample.displayName))
                throw new InvalidOperationException($"Package '{PackageName}' version '{package.version}' does not expose the '{SampleName}' sample.");

            return sample;
        }

        private static string[] GetScenePaths(string importPath)
        {
            return new[]
            {
                Path.Combine(importPath, SmallExamplesSceneName).Replace('\\', '/'),
                Path.Combine(importPath, RocketSceneName).Replace('\\', '/')
            };
        }

        private static void ValidateScenes(IEnumerable<string> scenePaths)
        {
            foreach (var scenePath in scenePaths)
            {
                if (!File.Exists(scenePath))
                    throw new FileNotFoundException($"Imported sample scene is missing: {scenePath}", scenePath);

                var existingScene = SceneManager.GetSceneByPath(scenePath);
                var openedForValidation = !existingScene.IsValid() || !existingScene.isLoaded;
                var scene = openedForValidation
                    ? EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive)
                    : existingScene;

                try
                {
                    var missingScripts = new List<string>();
                    foreach (var root in scene.GetRootGameObjects())
                        CollectMissingScripts(root.transform, missingScripts);

                    if (missingScripts.Count > 0)
                        throw new InvalidOperationException($"Scene '{scenePath}' has missing scripts on: {string.Join(", ", missingScripts)}");
                }
                finally
                {
                    if (openedForValidation && scene.IsValid() && scene.isLoaded)
                        EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void CollectMissingScripts(Transform transform, ICollection<string> missingScripts)
        {
            if (transform.gameObject.GetComponents<Component>().Any(component => component == null))
                missingScripts.Add(GetHierarchyPath(transform));

            for (var index = 0; index < transform.childCount; index++)
                CollectMissingScripts(transform.GetChild(index), missingScripts);
        }

        private static string GetHierarchyPath(Transform transform)
        {
            var path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = $"{transform.name}/{path}";
            }

            return path;
        }
    }
}
