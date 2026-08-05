using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Light2D.TestProject.Editor
{
    public static class UrpProjectSetup
    {
        internal const string GlobalSettingsPath = "Assets/UniversalRenderPipelineGlobalSettings.asset";
        private const string DefaultVolumeProfilePath = "Assets/DefaultVolumeProfile.asset";
        private const string GlobalSettingsTypeName = "UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings, Unity.RenderPipelines.Universal.Runtime";
        private const string GlobalSettingsAssetName = "UniversalRenderPipelineGlobalSettings";
        private const string ShaderGraphTemplatesPath = "Packages/com.unity.shadergraph/GraphTemplates";
        private const int Urp174AssetVersion = 10;
        private const ImportAssetOptions SynchronousForceImport = ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate;
        private static readonly string[] UrpShaderGraphResourcePaths =
        {
            "Packages/com.unity.render-pipelines.universal/Shaders/AutodeskInteractive/AutodeskInteractive.shadergraph",
            "Packages/com.unity.render-pipelines.universal/Shaders/AutodeskInteractive/AutodeskInteractiveTransparent.shadergraph",
            "Packages/com.unity.render-pipelines.universal/Shaders/AutodeskInteractive/AutodeskInteractiveMasked.shadergraph",
            "Packages/com.unity.render-pipelines.universal/Shaders/Nature/SpeedTree8_PBRLit.shadergraph",
            "Packages/com.unity.render-pipelines.universal/Shaders/Nature/SpeedTree9_URP.shadergraph"
        };

        public static void RegenerateGlobalSettings()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Wait for package compilation and asset importing to finish before regenerating URP global settings.");

            ReimportRequiredUrpShaderGraphs();

            GraphicsSettings.defaultRenderPipeline = null;
            EditorGraphicsSettings.SetRenderPipelineGlobalSettingsAsset<UniversalRenderPipeline>(null);

            DeleteCopiedGlobalSettings();
            DeleteAssetIfPresent(DefaultVolumeProfilePath);

            var settings = RenderPipelineGlobalSettingsUtils.Create(GetGlobalSettingsType(), GlobalSettingsPath);
            if (settings == null)
                throw new InvalidOperationException(
                    $"URP failed to create global settings at '{GlobalSettingsPath}'. " +
                    "This host has no external fallback; resolve the Unity/ShaderGraph import defect described in docs/TESTING.md and rerun.");

            RegisterAndValidate(settings);
            Debug.Log($"Generated and registered host URP global settings at '{GlobalSettingsPath}'.");
        }

        public static void ValidateGlobalSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<RenderPipelineGlobalSettings>(GlobalSettingsPath);
            if (settings == null || settings.GetType() != GetGlobalSettingsType())
                throw new InvalidOperationException($"Run {nameof(RegenerateGlobalSettings)} before URP builds or tests; '{GlobalSettingsPath}' is missing or invalid.");

            var registeredSettings = EditorGraphicsSettings.GetRenderPipelineGlobalSettingsAsset<UniversalRenderPipeline>();
            if (registeredSettings != settings)
                throw new InvalidOperationException($"'{GlobalSettingsPath}' is not the URP global settings asset registered in Graphics Settings.");

            if (GraphicsSettings.defaultRenderPipeline != null)
                throw new InvalidOperationException("GraphicsSettings.defaultRenderPipeline must remain null in the shared Built-in/URP test host.");

            var serializedSettings = new SerializedObject(settings);
            var assetVersion = serializedSettings.FindProperty("m_AssetVersion");
            var populatedSettings = serializedSettings.FindProperty("m_Settings.m_SettingsList.m_List");
            if (assetVersion == null || assetVersion.intValue != Urp174AssetVersion)
                throw new InvalidOperationException($"'{GlobalSettingsPath}' is not a current URP 17.4 global settings asset.");

            if (populatedSettings == null || populatedSettings.arraySize == 0)
                throw new InvalidOperationException($"'{GlobalSettingsPath}' has no populated render pipeline graphics settings.");

            var volumeSettings = GraphicsSettings.GetRenderPipelineSettings<URPDefaultVolumeProfileSettings>();
            if (volumeSettings?.volumeProfile == null || AssetDatabase.GetAssetPath(volumeSettings.volumeProfile) != DefaultVolumeProfilePath)
                throw new InvalidOperationException($"URP did not generate the host default volume profile at '{DefaultVolumeProfilePath}'.");

            foreach (var path in FindCopiedGlobalSettingsPaths())
            {
                if (!string.Equals(path, GlobalSettingsPath, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Duplicate URP global settings asset remains at '{path}'.");
            }
        }

        private static void DeleteCopiedGlobalSettings()
        {
            var paths = new HashSet<string>(FindCopiedGlobalSettingsPaths(), StringComparer.OrdinalIgnoreCase)
            {
                GlobalSettingsPath
            };

            foreach (var path in paths)
                DeleteAssetIfPresent(path);
        }

        private static void RegisterAndValidate(RenderPipelineGlobalSettings settings)
        {
            EditorGraphicsSettings.SetRenderPipelineGlobalSettingsAsset<UniversalRenderPipeline>(settings);
            GraphicsSettings.defaultRenderPipeline = null;
            AssetDatabase.SaveAssets();
            ValidateGlobalSettings();
        }

        private static void ReimportRequiredUrpShaderGraphs()
        {
            var graphTemplatesNeedReimport = false;
            foreach (var path in UrpShaderGraphResourcePaths)
            {
                AssetDatabase.ImportAsset(path, SynchronousForceImport);
                graphTemplatesNeedReimport |= !(AssetDatabase.LoadMainAssetAtPath(path) is Shader);
            }

            if (graphTemplatesNeedReimport)
            {
                AssetDatabase.ImportAsset(ShaderGraphTemplatesPath, SynchronousForceImport | ImportAssetOptions.ImportRecursive);
                foreach (var path in UrpShaderGraphResourcePaths)
                    AssetDatabase.ImportAsset(path, SynchronousForceImport);
            }

            foreach (var path in UrpShaderGraphResourcePaths)
                RequireMainAsset(path, typeof(Shader));
        }

        private static UnityEngine.Object RequireMainAsset(string path, Type expectedType)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset != null && expectedType.IsInstanceOfType(asset))
                return asset;

            var actualType = asset == null ? "null" : asset.GetType().AssemblyQualifiedName;
            var importer = AssetImporter.GetAtPath(path);
            var importerType = importer == null ? "null" : importer.GetType().AssemblyQualifiedName;
            throw new InvalidOperationException(
                $"Resource import failed for '{path}': expected main asset type '{expectedType.AssemblyQualifiedName}', " +
                $"but AssetDatabase.LoadMainAssetAtPath returned '{actualType}' (importer: '{importerType}').");
        }

        private static string[] FindCopiedGlobalSettingsPaths()
        {
            var paths = AssetDatabase.FindAssets(GlobalSettingsAssetName, new[] { "Assets" });
            var matches = new System.Collections.Generic.List<string>();

            foreach (var guid in paths)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.Equals(Path.GetDirectoryName(path)?.Replace('\\', '/'), "Assets", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(Path.GetExtension(path), ".asset", StringComparison.OrdinalIgnoreCase))
                    continue;

                var fileName = Path.GetFileNameWithoutExtension(path);
                if (string.Equals(fileName, GlobalSettingsAssetName, StringComparison.OrdinalIgnoreCase) || IsNumberedCopy(fileName, GlobalSettingsAssetName))
                    matches.Add(path);
            }

            return matches.ToArray();
        }

        private static bool IsNumberedCopy(string fileName, string assetName)
        {
            var prefix = assetName + " ";
            return fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                   int.TryParse(fileName.Substring(prefix.Length), out _);
        }

        private static Type GetGlobalSettingsType()
        {
            return Type.GetType(GlobalSettingsTypeName, true);
        }

        private static void DeleteAssetIfPresent(string path)
        {
            if (AssetDatabase.DeleteAsset(path))
                return;

            FileUtil.DeleteFileOrDirectory(path);
            FileUtil.DeleteFileOrDirectory(path + ".meta");
            AssetDatabase.Refresh(SynchronousForceImport);
        }
    }
}
