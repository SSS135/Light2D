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
            if (!sample.Import(Sample.ImportOptions.OverridePreviousImports | Sample.ImportOptions.HideImportWindow))
                throw new InvalidOperationException($"Failed to import the '{SampleName}' sample from {PackageName}.");

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            ValidateScenes(sample.importPath);
            Debug.Log($"Validated Light2D sample at '{sample.importPath}'.");
        }

        public static void ReserializeExamplesSource()
        {
            var sample = FindExamplesSample();
            var package = UnityEditor.PackageManager.PackageInfo.FindForPackageName(PackageName);
            var sourceRoot = Path.Combine(package.resolvedPath, "Samples~", "Examples");
            var importedPaths = Directory.EnumerateFiles(sample.importPath, "*", SearchOption.AllDirectories)
                .Select(path => path.Replace('\\', '/'))
                .Where(IsSerializedAsset)
                .ToArray();
            var backupRoot = Path.GetFullPath(Path.Combine("Library", "Light2DSampleReserializeBackup"));
            var textRoot = Path.GetFullPath(Path.Combine("Library", "Light2DSampleReserializeText"));

            try
            {
                foreach (var importedPath in importedPaths)
                    CopyWithDirectory(importedPath, GetTemporaryPath(backupRoot, sample.importPath, importedPath));

                AssetDatabase.ForceReserializeAssets(importedPaths.Select(ToAssetDatabasePath));

                foreach (var importedPath in importedPaths)
                    CopyWithDirectory(importedPath, GetTemporaryPath(textRoot, sample.importPath, importedPath));
            }
            finally
            {
                foreach (var importedPath in importedPaths)
                {
                    var backupPath = GetTemporaryPath(backupRoot, sample.importPath, importedPath);
                    if (File.Exists(backupPath))
                        File.Copy(backupPath, importedPath, true);
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            foreach (var importedPath in importedPaths)
            {
                var relativePath = GetRelativeSamplePath(sample.importPath, importedPath);
                var sourcePath = Path.Combine(sourceRoot, relativePath);
                CopyWithDirectory(Path.Combine(textRoot, relativePath), sourcePath);
                NormalizeSerializedText(sourcePath);
            }

            Directory.Delete(backupRoot, true);
            Directory.Delete(textRoot, true);
            Debug.Log($"Reserialized {importedPaths.Length} package sample assets as text without changing metas.");
        }

        public static void BackupHostAndImportExamplesSource()
        {
            var sample = FindExamplesSample();
            var backupRoot = Path.GetFullPath(Path.Combine("Library", "Light2DSampleImportBackup"));
            if (Directory.Exists(backupRoot))
                Directory.Delete(backupRoot, true);
            CopyDirectory(sample.importPath, backupRoot);

            if (!sample.Import(Sample.ImportOptions.OverridePreviousImports | Sample.ImportOptions.HideImportWindow))
                throw new InvalidOperationException("Failed to import the package sample for source validation.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        public static void ValidateImportedSourceAndRestoreHost()
        {
            var sample = FindExamplesSample();
            var backupRoot = Path.GetFullPath(Path.Combine("Library", "Light2DSampleImportBackup"));
            try
            {
                ValidateScenes(sample.importPath);
            }
            finally
            {
                Directory.Delete(sample.importPath, true);
                CopyDirectory(backupRoot, sample.importPath);
                Directory.Delete(backupRoot, true);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
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

        private static void ValidateScenes(string importPath)
        {
            foreach (var scenePath in GetScenePaths(importPath))
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

                    ValidateDependencyClosure(scenePath, importPath);
                    ValidateSerializedText(scenePath);
                }
                finally
                {
                    if (openedForValidation && scene.IsValid() && scene.isLoaded)
                        EditorSceneManager.CloseScene(scene, true);
                }
            }

            ValidatePrefabs(importPath);
            ValidateMaterials(importPath);
            ValidateSerializedAssets(importPath);
        }

        private static void ValidatePrefabs(string importPath)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { ToAssetDatabasePath(importPath) }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var missingScripts = new List<string>();
                CollectMissingScripts(prefab.transform, missingScripts);
                if (missingScripts.Count > 0)
                    throw new InvalidOperationException($"Prefab '{path}' has missing scripts on: {string.Join(", ", missingScripts)}");

                ValidateDependencyClosure(path, importPath);
            }
        }

        private static void ValidateMaterials(string importPath)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { ToAssetDatabasePath(importPath) }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == null ||
                    string.Equals(material.shader.name, "Hidden/InternalErrorShader", StringComparison.Ordinal))
                    throw new InvalidOperationException($"Sample material '{path}' has no valid shader.");

                ValidateDependencyClosure(path, importPath);
            }
        }

        private static void ValidateSerializedAssets(string importPath)
        {
            foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { ToAssetDatabasePath(importPath) }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (IsSerializedAsset(path))
                    ValidateSerializedText(path);
            }
        }

        private static void ValidateSerializedText(string path)
        {
            using var reader = new StreamReader(path);
            if (!string.Equals(reader.ReadLine(), "%YAML 1.1", StringComparison.Ordinal))
                throw new InvalidOperationException($"Sample asset '{path}' is not serialized as Unity text.");
        }

        private static void ValidateDependencyClosure(string path, string importPath)
        {
            var assetImportPath = ToAssetDatabasePath(importPath).TrimEnd('/') + "/";
            foreach (var dependency in AssetDatabase.GetDependencies(ToAssetDatabasePath(path), true))
            {
                if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(dependency)) &&
                    (dependency.StartsWith("Assets/", StringComparison.Ordinal) ||
                     dependency.StartsWith("Packages/", StringComparison.Ordinal)))
                    throw new InvalidOperationException($"Sample asset '{path}' has an unresolved dependency '{dependency}'.");

                if (dependency.StartsWith("Assets/", StringComparison.Ordinal) &&
                    !dependency.StartsWith(assetImportPath, StringComparison.Ordinal))
                    throw new InvalidOperationException($"Sample asset '{path}' depends on host-only asset '{dependency}'.");
            }
        }

        private static bool IsSerializedAsset(string path)
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            return extension == ".unity" || extension == ".prefab" || extension == ".mat" ||
                   extension == ".asset" || extension == ".lighting";
        }

        private static string GetTemporaryPath(string root, string importRoot, string importedPath)
        {
            return Path.Combine(root, GetRelativeSamplePath(importRoot, importedPath));
        }

        private static string GetRelativeSamplePath(string importRoot, string importedPath)
        {
            return importedPath.Substring(importRoot.Length).TrimStart('/', '\\');
        }

        private static void CopyWithDirectory(string source, string destination)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(source, destination, true);
        }

        private static string ToAssetDatabasePath(string path)
        {
            var normalizedPath = path.Replace('\\', '/');
            if (normalizedPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                normalizedPath.StartsWith("Packages/", StringComparison.Ordinal))
                return normalizedPath;

            var projectRoot = Path.GetDirectoryName(Application.dataPath).Replace('\\', '/').TrimEnd('/');
            var fullPath = Path.GetFullPath(path).Replace('\\', '/');
            return fullPath.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase)
                ? fullPath.Substring(projectRoot.Length + 1)
                : FileUtil.GetProjectRelativePath(fullPath).Replace('\\', '/');
        }

        private static void NormalizeSerializedText(string path)
        {
            var lines = File.ReadAllLines(path);
            for (var index = 0; index < lines.Length; index++)
                lines[index] = lines[index].TrimEnd(' ', '\t');
            // Unity text assets are canonically LF-only with a trailing newline; WriteAllLines would emit CRLF on Windows.
            File.WriteAllText(path, string.Join("\n", lines) + "\n");
        }

        private static void CopyDirectory(string sourceRoot, string destinationRoot)
        {
            foreach (var sourcePath in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
            {
                var relativePath = sourcePath.Substring(sourceRoot.Length).TrimStart('/', '\\');
                CopyWithDirectory(sourcePath, Path.Combine(destinationRoot, relativePath));
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
