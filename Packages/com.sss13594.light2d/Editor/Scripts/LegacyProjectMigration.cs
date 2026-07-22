using System;
using System.Collections.Generic;
using System.IO;
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

        // GUID of the collapsed "Light2D/Light Points" shader that replaces the eight per-count
        // Light20/30/40/50/60/70/80/90 shaders and the retired Light 8/9 stub (Workstream C2/C3).
        private const string NewLightShaderGuid = "52429f18779e42d09ce2a72bef04eac9";

        // Retired ray-march light-shader GUIDs mapped to the path-tracking sample count encoded in
        // their old name. Any material (standalone or embedded in a scene/prefab) referencing one of
        // these is repointed to NewLightShaderGuid with _PathTrackingSamples set to the mapped count.
        private static readonly Dictionary<string, int> RetiredLightShaderGuids = new(StringComparer.Ordinal)
        {
            { "855974e4ec7380c468d9ef075b0c1134", 8 },  // Light 8/9 Points (retired stub)
            { "9f4a2ad6220aedf47a83280c2e1932e7", 20 }, // Light 20 Points
            { "f90422fc704c9ac47b22ad527e0151eb", 30 }, // Light 30 Points
            { "47f04ff6c4cd6184e82b0d0902c007db", 40 }, // Light 40 Points
            { "815936569415cfd459ace4f9e2026627", 50 }, // Light 50 Points
            { "9423e206b9b6f5f45b0eeb0790bba270", 60 }, // Light 60 Points
            { "c248f902c930201439651266882a2150", 70 }, // Light 70 Points
            { "9a981bc67d3aff14ea6d31cdfcc7e321", 80 }, // Light 80 Points
            { "3a65b5d7f8a65c346bb3f168c0c4a914", 90 }  // Light 90 Points
        };

        private static readonly string[] ScanFolders = { "Assets", RootPath };

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
                // Repoint retired ray-march light shaders first, so the subsequent save/reserialize
                // steps operate on materials that already reference the valid collapsed shader.
                RepointRetiredLightShaders(failures);

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

        // Rewrites every material — standalone .mat assets plus materials embedded in scenes and
        // prefabs — whose m_Shader GUID is one of the retired ray-march light shaders, repointing it
        // to the collapsed "Light2D/Light Points" shader and setting _PathTrackingSamples to the count
        // from the old name. Operates at the YAML-text level so it still resolves the original count
        // after the retired shaders and their metas have been deleted. Currently-loaded scenes are
        // skipped to avoid rewriting an open scene from under the editor; rerun after closing them.
        private static void RepointRetiredLightShaders(ICollection<string> failures)
        {
            var loadedScenePaths = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (scene.isLoaded && !string.IsNullOrEmpty(scene.path))
                    loadedScenePaths.Add(scene.path);
            }

            var candidatePaths = FindAssetPathsIn("t:Material", ScanFolders)
                .Concat(FindAssetPathsIn("t:Prefab", ScanFolders))
                .Concat(FindAssetPathsIn("t:SceneAsset", ScanFolders))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            var rewrittenCount = 0;
            foreach (var path in candidatePaths)
            {
                if (loadedScenePaths.Contains(path))
                    continue;

                try
                {
                    var fullPath = Path.GetFullPath(path);
                    if (!File.Exists(fullPath))
                        continue;

                    var text = File.ReadAllText(fullPath);
                    if (!ContainsRetiredShaderGuid(text))
                        continue;

                    if (!TryRepointShaderReferences(text, out var rewritten))
                        continue;

                    File.WriteAllText(fullPath, rewritten);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    rewrittenCount++;
                }
                catch (Exception exception)
                {
                    AddFailure(failures, $"Could not repoint retired light shaders in '{path}'. Ensure the file is writable and imports without errors, then rerun the migration.", exception);
                }
            }

            Debug.Log($"[LegacyProjectMigration] Repointed retired ray-march light shaders in {rewrittenCount} asset(s) to '{NewLightShaderGuid}'.");
        }

        private static bool ContainsRetiredShaderGuid(string text)
        {
            foreach (var guid in RetiredLightShaderGuids.Keys)
            {
                if (text.Contains(guid, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        // Pure text transform (unit-testable): repoints retired-shader m_Shader references and injects
        // _PathTrackingSamples per material block. Returns false when nothing changed.
        internal static bool TryRepointShaderReferences(string text, out string result)
        {
            result = text;
            if (!ContainsRetiredShaderGuid(text))
                return false;

            var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var lines = text.Split(new[] { newline }, StringSplitOptions.None).ToList();
            var changed = false;

            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (!line.TrimStart().StartsWith("m_Shader:", StringComparison.Ordinal))
                    continue;

                var retiredGuid = RetiredLightShaderGuids.Keys
                    .FirstOrDefault(guid => line.Contains(guid, StringComparison.Ordinal));
                if (retiredGuid == null)
                    continue;

                var sampleCount = RetiredLightShaderGuids[retiredGuid];
                lines[i] = line.Replace(retiredGuid, NewLightShaderGuid);
                changed = true;

                var blockEnd = lines.Count;
                for (var j = i + 1; j < lines.Count; j++)
                {
                    if (lines[j].StartsWith("--- !u!", StringComparison.Ordinal))
                    {
                        blockEnd = j;
                        break;
                    }
                }

                SetPathTrackingSamples(lines, i + 1, blockEnd, sampleCount);
            }

            if (changed)
                result = string.Join(newline, lines);

            return changed;
        }

        // Within one material's YAML block, ensures the m_Floats list carries _PathTrackingSamples with
        // the given value: updates it in place if present, converts an inline empty list, or inserts it.
        private static void SetPathTrackingSamples(List<string> lines, int blockStart, int blockEnd, int sampleCount)
        {
            var floatsLine = -1;
            for (var j = blockStart; j < blockEnd; j++)
            {
                if (lines[j].TrimStart().StartsWith("m_Floats:", StringComparison.Ordinal))
                {
                    floatsLine = j;
                    break;
                }
            }

            if (floatsLine < 0)
                return; // No m_Floats block; the shader's default sample count applies.

            var indent = lines[floatsLine].Substring(0, lines[floatsLine].Length - lines[floatsLine].TrimStart().Length);
            var entry = $"{indent}- _PathTrackingSamples: {sampleCount}";

            // Inline empty list ("m_Floats: []"): expand to a block form before inserting.
            if (lines[floatsLine].Contains("[]", StringComparison.Ordinal))
            {
                lines[floatsLine] = $"{indent}m_Floats:";
                lines.Insert(floatsLine + 1, entry);
                return;
            }

            for (var j = floatsLine + 1; j < blockEnd; j++)
            {
                var trimmed = lines[j].TrimStart();
                if (trimmed.StartsWith("- _PathTrackingSamples:", StringComparison.Ordinal))
                {
                    lines[j] = entry; // Already present: normalize its value.
                    return;
                }

                if (!trimmed.StartsWith("- ", StringComparison.Ordinal))
                    break; // Left the m_Floats list without finding the key.
            }

            lines.Insert(floatsLine + 1, entry);
        }

        private static string[] FindAssetPaths(string filter)
        {
            return FindAssetPathsIn(filter, new[] { RootPath });
        }

        private static string[] FindAssetPathsIn(string filter, string[] folders)
        {
            return AssetDatabase.FindAssets(filter, folders)
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
