using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Light2D
{
    public static class LegacyProjectMigration
    {
        private const string RootPath = "Packages/com.sss13594.light2d";

        private static readonly string[] ExampleSceneGuids =
        {
            "8c60e4126ab1d02439b0990f802b7de0",
            "d2934fa085d9ef945991f958c310237d"
        };

        public static void Run()
        {
            var failures = new List<string>();
            var originalSceneSetup = EditorSceneManager.GetSceneManagerSetup()
                .Where(setup => !string.IsNullOrEmpty(setup.path))
                .ToArray();

            EnsureNoUnsavedScenes();

            var exampleScenePaths = ExampleSceneGuids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .ToArray();
            var materialPaths = FindAssetPaths("t:Material");
            var prefabPaths = FindAssetPaths("t:Prefab");
            var scriptableObjectPaths = FindAssetPaths("t:ScriptableObject");
            var nonScenePaths = materialPaths
                .Concat(prefabPaths)
                .Concat(scriptableObjectPaths)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            try
            {
                foreach (var path in materialPaths.Concat(scriptableObjectPaths).Distinct(StringComparer.Ordinal))
                    SaveAsset(path, failures);

                foreach (var path in prefabPaths)
                    SaveAndScanPrefab(path, failures);

                foreach (var path in exampleScenePaths)
                    SaveAndScanScene(path, failures);

                try
                {
                    AssetDatabase.SaveAssets();
                }
                catch (Exception exception)
                {
                    AddFailure(failures, "Unity could not flush all pending asset changes. Resolve read-only files or import errors, then rerun the migration.", exception);
                }

                foreach (var path in nonScenePaths.Concat(exampleScenePaths))
                    ForceReserialize(path, failures);
            }
            finally
            {
                if (originalSceneSetup.Length > 0)
                {
                    try
                    {
                        EditorSceneManager.RestoreSceneManagerSetup(originalSceneSetup);
                    }
                    catch (Exception exception)
                    {
                        AddFailure(failures, "Could not restore the original scene setup. Reopen the previously active scene manually.", exception);
                    }
                }
            }

            if (failures.Count > 0)
                throw new InvalidOperationException($"Light2D legacy migration failed with {failures.Count} error(s). Review the preceding console messages for asset paths and corrective actions.");

            Debug.Log($"[LegacyProjectMigration] Completed successfully. Reserialized {nonScenePaths.Length} assets and {exampleScenePaths.Length} imported example scenes under {RootPath}.");
        }

        private static string[] FindAssetPaths(string filter)
        {
            return AssetDatabase.FindAssets(filter, new[] { RootPath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        private static void EnsureNoUnsavedScenes()
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (scene.isLoaded && scene.isDirty)
                {
                    var path = string.IsNullOrEmpty(scene.path) ? "<unsaved scene>" : scene.path;
                    throw new InvalidOperationException($"Light2D legacy migration cannot start while '{path}' has unsaved changes. Save or discard those changes, then run the migration again.");
                }
            }
        }

        private static void SaveAsset(string path, ICollection<string> failures)
        {
            try
            {
                var assets = AssetDatabase.LoadAllAssetsAtPath(path)
                    .Where(asset => asset is Material || asset is ScriptableObject)
                    .ToArray();

                if (assets.Length == 0)
                    throw new InvalidOperationException("No Material or ScriptableObject could be loaded from this path.");

                foreach (var asset in assets)
                {
                    EditorUtility.SetDirty(asset);
                    AssetDatabase.SaveAssetIfDirty(asset);
                }
            }
            catch (Exception exception)
            {
                AddFailure(failures, $"Could not save asset '{path}'. Resolve import or serialization errors for this asset, then rerun the migration.", exception);
            }
        }

        private static void SaveAndScanPrefab(string path, ICollection<string> failures)
        {
            GameObject root = null;
            var missingScriptsReported = false;
            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                missingScriptsReported = ReportMissingScripts(root, path, failures);

                var savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (savedPrefab == null)
                    throw new InvalidOperationException("PrefabUtility.SaveAsPrefabAsset returned null.");
            }
            catch (Exception exception)
            {
                var message = $"Could not load or save prefab '{path}'. Open the prefab and resolve its reported errors, then rerun the migration.";
                if (missingScriptsReported)
                    Debug.LogError($"[LegacyProjectMigration] {message}\n{exception}");
                else
                    AddFailure(failures, message, exception);
            }
            finally
            {
                if (root != null)
                {
                    try
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                    catch (Exception exception)
                    {
                        AddFailure(failures, $"Could not unload prefab contents for '{path}'. Close its isolated prefab scene before rerunning the migration.", exception);
                    }
                }
            }
        }

        private static void SaveAndScanScene(string path, ICollection<string> failures)
        {
            try
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                    throw new InvalidOperationException("The expected example scene does not exist or could not be imported.");

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (var root in scene.GetRootGameObjects())
                    ReportMissingScripts(root, path, failures);

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, path))
                    throw new InvalidOperationException("EditorSceneManager.SaveScene returned false.");
            }
            catch (Exception exception)
            {
                AddFailure(failures, $"Could not load or save scene '{path}'. Open the scene and resolve its reported errors, then rerun the migration.", exception);
            }
        }

        private static bool ReportMissingScripts(GameObject root, string assetPath, ICollection<string> failures)
        {
            var foundMissingScripts = false;
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                var missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                if (missingCount == 0)
                    continue;

                foundMissingScripts = true;
                var objectPath = GetObjectPath(transform);
                var message = $"Missing MonoBehaviour script reference(s): asset='{assetPath}', object='{objectPath}', count={missingCount}. Restore the referenced script or inspect and remove the component manually if it is obsolete.";
                AddFailure(failures, message, null);
            }

            return foundMissingScripts;
        }

        private static string GetObjectPath(Transform transform)
        {
            var names = new Stack<string>();
            while (transform != null)
            {
                names.Push(transform.name);
                transform = transform.parent;
            }

            return string.Join("/", names);
        }

        private static void ForceReserialize(string path, ICollection<string> failures)
        {
            try
            {
                AssetDatabase.ForceReserializeAssets(new[] { path }, ForceReserializeAssetsOptions.ReserializeAssets);
            }
            catch (Exception exception)
            {
                AddFailure(failures, $"Could not force-reserialize '{path}'. Check that the asset is writable and imports without errors, then rerun the migration.", exception);
            }
        }

        private static void AddFailure(ICollection<string> failures, string message, Exception exception)
        {
            var detail = exception == null ? message : $"{message}\n{exception}";
            failures.Add(detail);
            Debug.LogError($"[LegacyProjectMigration] {detail}");
        }
    }
}
