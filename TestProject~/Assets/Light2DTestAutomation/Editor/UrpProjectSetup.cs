using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager;
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
        private const string UrpPackageName = "com.unity.render-pipelines.universal";
        private const string UrpPackageVersion = "17.4.0";
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
        private static readonly Regex ScriptGuidPattern = new Regex(@"m_Script:\s*\{[^}]*guid:\s*([0-9a-f]{32})", RegexOptions.IgnoreCase);

        public static void RegenerateGlobalSettings()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Wait for package compilation and asset importing to finish before regenerating URP global settings.");

            ReimportRequiredUrpShaderGraphs();

            GraphicsSettings.defaultRenderPipeline = null;
            EditorGraphicsSettings.SetRenderPipelineGlobalSettingsAsset<UniversalRenderPipeline>(null);

            DeleteCopiedGlobalSettings();
            DeleteAssetIfPresent(DefaultVolumeProfilePath);

            try
            {
                var settings = RenderPipelineGlobalSettingsUtils.Create(GetGlobalSettingsType(), GlobalSettingsPath);
                if (settings == null)
                    throw new InvalidOperationException($"URP failed to create global settings at '{GlobalSettingsPath}'.");

                RegisterAndValidate(settings);
                Debug.Log($"Generated and registered host URP global settings at '{GlobalSettingsPath}'.");
            }
            catch (Exception generationFailure)
            {
                Debug.LogWarning($"URP local global-settings generation failed; trying the validated {UrpPackageVersion} ColorWars resources.\n{generationFailure}");

                try
                {
                    CopyValidatedFallbackAssets();
                    var settings = (RenderPipelineGlobalSettings)RequireMainAsset(GlobalSettingsPath, GetGlobalSettingsType());

                    RegisterAndValidate(settings);
                    Debug.Log($"Imported and registered validated URP {UrpPackageVersion} host global settings from ColorWars.");
                }
                catch (Exception fallbackFailure)
                {
                    throw new AggregateException("URP local generation and the validated ColorWars fallback both failed.", generationFailure, fallbackFailure);
                }
            }
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

        private static void CopyValidatedFallbackAssets()
        {
            GraphicsSettings.defaultRenderPipeline = null;
            EditorGraphicsSettings.SetRenderPipelineGlobalSettingsAsset<UniversalRenderPipeline>(null);
            DeleteCopiedGlobalSettings();
            DeleteAssetIfPresent(DefaultVolumeProfilePath);

            var sourceAssetsDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "ColorWars", "Assets"));
            var sourceManifestPath = Path.GetFullPath(Path.Combine(sourceAssetsDirectory, "..", "Packages", "manifest.json"));
            var sourceGlobalSettingsPath = Path.Combine(sourceAssetsDirectory, Path.GetFileName(GlobalSettingsPath));
            var sourceVolumeProfilePath = Path.Combine(sourceAssetsDirectory, Path.GetFileName(DefaultVolumeProfilePath));

            ValidateFallbackVersion(sourceManifestPath);
            ValidateFallbackAssets(sourceGlobalSettingsPath, sourceVolumeProfilePath);

            CopyAssetAndMeta(sourceGlobalSettingsPath, GlobalSettingsPath);
            CopyAssetAndMeta(sourceVolumeProfilePath, DefaultVolumeProfilePath);
            AssetDatabase.Refresh(SynchronousForceImport);
            AssetDatabase.ImportAsset(GlobalSettingsPath, SynchronousForceImport);
            AssetDatabase.ImportAsset(DefaultVolumeProfilePath, SynchronousForceImport);
            RequireMainAsset(GlobalSettingsPath, GetGlobalSettingsType());
            RequireMainAsset(DefaultVolumeProfilePath, typeof(VolumeProfile));
        }

        private static void ValidateFallbackVersion(string sourceManifestPath)
        {
            var hostPackage = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(UniversalRenderPipeline).Assembly);
            if (hostPackage == null || hostPackage.name != UrpPackageName || hostPackage.version != UrpPackageVersion)
                throw new InvalidOperationException($"The host must resolve {UrpPackageName} {UrpPackageVersion} before using fallback resources.");

            if (!File.Exists(sourceManifestPath))
                throw new FileNotFoundException("ColorWars package manifest was not found.", sourceManifestPath);

            var manifest = File.ReadAllText(sourceManifestPath);
            var versionMatch = Regex.Match(manifest, $"\"{Regex.Escape(UrpPackageName)}\"\\s*:\\s*\"([^\"]+)\"");
            if (!versionMatch.Success || versionMatch.Groups[1].Value != UrpPackageVersion)
                throw new InvalidOperationException($"ColorWars does not declare {UrpPackageName} {UrpPackageVersion}.");
        }

        private static void ValidateFallbackAssets(string sourceGlobalSettingsPath, string sourceVolumeProfilePath)
        {
            RequireFile(sourceGlobalSettingsPath);
            RequireFile(sourceGlobalSettingsPath + ".meta");
            RequireFile(sourceVolumeProfilePath);
            RequireFile(sourceVolumeProfilePath + ".meta");

            var globalSettings = File.ReadAllText(sourceGlobalSettingsPath);
            var volumeProfile = File.ReadAllText(sourceVolumeProfilePath);
            var volumeMeta = File.ReadAllText(sourceVolumeProfilePath + ".meta");
            var volumeGuid = Regex.Match(volumeMeta, @"(?m)^guid:\s*([0-9a-f]{32})\s*$", RegexOptions.IgnoreCase);

            if (!Regex.IsMatch(globalSettings, $@"(?m)^\s*m_AssetVersion:\s*{Urp174AssetVersion}\s*$") ||
                !volumeGuid.Success ||
                !Regex.IsMatch(globalSettings, $@"guid:\s*{Regex.Escape(volumeGuid.Groups[1].Value)}\b", RegexOptions.IgnoreCase) ||
                !Regex.IsMatch(volumeProfile, @"(?m)^\s*m_Name:\s*DefaultVolumeProfile\s*$"))
                throw new InvalidOperationException("ColorWars fallback assets are not a matching URP 17.4 global-settings/default-volume pair.");

            var scriptGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddScriptGuids(globalSettings, scriptGuids);
            AddScriptGuids(volumeProfile, scriptGuids);
            if (scriptGuids.Count == 0)
                throw new InvalidOperationException("ColorWars fallback assets contain no script references to validate.");

            foreach (var guid in scriptGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || AssetDatabase.LoadAssetAtPath<MonoScript>(path) == null)
                    throw new InvalidOperationException($"ColorWars fallback script GUID '{guid}' does not resolve in this host.");
            }
        }

        private static void AddScriptGuids(string assetText, HashSet<string> scriptGuids)
        {
            foreach (Match match in ScriptGuidPattern.Matches(assetText))
                scriptGuids.Add(match.Groups[1].Value);
        }

        private static void RequireFile(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Required ColorWars fallback file was not found.", path);
        }

        private static void CopyAssetAndMeta(string sourcePath, string destinationAssetPath)
        {
            var destinationPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", destinationAssetPath));
            FileUtil.CopyFileOrDirectory(sourcePath, destinationPath);
            FileUtil.CopyFileOrDirectory(sourcePath + ".meta", destinationPath + ".meta");
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
