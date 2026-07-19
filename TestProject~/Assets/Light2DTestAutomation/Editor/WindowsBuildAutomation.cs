using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Light2D.TestProject.Editor
{
    public static class WindowsBuildAutomation
    {
        private const string BuildOutputEnvironmentVariable = "LIGHT2D_BUILD_OUTPUT";
        private const string AutomationAssetsPath = "Assets/Light2DTestAutomation/Generated";
        private const string RendererDataPath = AutomationAssetsPath + "/Light2DUniversalRenderer.asset";
        private const string PipelineAssetPath = AutomationAssetsPath + "/Light2DUniversalRenderPipeline.asset";
        private const string UrpGlobalSettingsAssetName = "UniversalRenderPipelineGlobalSettings";
        private const string UrpGlobalSettingsScriptGuid = "2ec995e51a6e251468d2a3fd8a686257";
        private const string RendererFeaturesProperty = "m_RendererFeatures";
        private const string RendererFeatureMapProperty = "m_RendererFeatureMap";

        public static void BuildWindows64Builtin()
        {
            BuildWindows64("Builtin", null, true);
        }

        public static void BuildWindows64Urp()
        {
            BuildWindows64("Urp", GetOrCreateUrpAsset(), false);
        }

        private static void BuildWindows64(string configurationName, RenderPipelineAsset pipelineAsset, bool isBuiltin)
        {
            var previousGraphicsPipeline = GraphicsSettings.defaultRenderPipeline;
            var previousQualityPipeline = QualitySettings.renderPipeline;

            if (isBuiltin)
                PrepareBuiltinState();

            var scenes = SampleAutomation.EnsureExamplesImported();

            try
            {
                GraphicsSettings.defaultRenderPipeline = pipelineAsset;
                QualitySettings.renderPipeline = pipelineAsset;
                if (isBuiltin)
                    PrepareBuiltinState();

                var outputPath = GetOutputPath(configurationName);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? throw new InvalidOperationException("The build output must have a parent directory."));
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = isBuiltin ? BuildOptions.None : BuildOptions.StrictMode
                });

                var buildErrors = GetBuildErrors(report, isBuiltin, out var ignoredUrpErrors);
                Debug.Log(
                    $"Light2D {configurationName} BuildReport result: {report.summary.result}; " +
                    $"reported errors: {report.summary.totalErrors}; actionable step errors: {buildErrors.Count}; " +
                    $"ignored URP global-settings errors: {ignoredUrpErrors}.");

                if (report.summary.result != BuildResult.Succeeded || buildErrors.Count > 0)
                {
                    throw new InvalidOperationException(
                        $"Light2D {configurationName} build failed with result {report.summary.result} " +
                        $"and {buildErrors.Count} actionable error(s).\n{string.Join("\n", buildErrors)}");
                }

                Debug.Log($"Built Light2D {configurationName} sample player at '{outputPath}'.");
            }
            finally
            {
                if (isBuiltin)
                {
                    PrepareBuiltinState();
                }
                else
                {
                    GraphicsSettings.defaultRenderPipeline = previousGraphicsPipeline;
                    QualitySettings.renderPipeline = previousQualityPipeline;
                    AssetDatabase.SaveAssets();
                }
            }
        }

        private static List<string> GetBuildErrors(BuildReport report, bool ignoreUrpGlobalSettingsErrors, out int ignoredUrpErrors)
        {
            var errors = new List<string>();
            ignoredUrpErrors = 0;

            foreach (var step in report.steps)
            {
                foreach (var message in step.messages)
                {
                    if (message.type != LogType.Error && message.type != LogType.Exception && message.type != LogType.Assert)
                        continue;

                    if (ignoreUrpGlobalSettingsErrors && IsUrpGlobalSettingsPostprocessorError(message.content))
                    {
                        ignoredUrpErrors++;
                        continue;
                    }

                    errors.Add(message.content);
                }
            }

            return errors;
        }

        internal static bool IsUrpGlobalSettingsPostprocessorError(string message)
        {
            if (string.IsNullOrEmpty(message) ||
                message.IndexOf("error CS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("Shader error", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            return message.IndexOf("UniversalRenderPipelineGlobalSettingsPostprocessor", StringComparison.Ordinal) >= 0 ||
                   message.IndexOf("RenderPipelineGlobalSettingsUtils", StringComparison.Ordinal) >= 0 ||
                   message.IndexOf("No script asset for UniversalRenderPipelineGlobalSettings", StringComparison.Ordinal) >= 0 ||
                   (message.IndexOf("Host type is not matching any asset type", StringComparison.Ordinal) >= 0 &&
                    message.IndexOf("Packages/com.unity.render-pipelines.universal/", StringComparison.Ordinal) >= 0);
        }

        private static void PrepareBuiltinState()
        {
            GraphicsSettings.defaultRenderPipeline = null;
            QualitySettings.renderPipeline = null;

            ValidateBuiltinUrpSentinel();
            DeleteNumberedUrpGlobalSettingsCopies();
            AssetDatabase.SaveAssets();
        }

        private static void ValidateBuiltinUrpSentinel()
        {
            var projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var assetPath = Path.Combine(projectPath, UrpProjectSetup.GlobalSettingsPath);
            var metaPath = assetPath + ".meta";
            if (!File.Exists(assetPath) || !File.Exists(metaPath))
                throw new InvalidOperationException($"The URP global-settings sentinel asset and meta must exist at '{UrpProjectSetup.GlobalSettingsPath}'.");

            var assetText = File.ReadAllText(assetPath);
            if (!Regex.IsMatch(assetText, $@"(?m)^\s*m_Script:\s*\{{fileID:\s*11500000,\s*guid:\s*{UrpGlobalSettingsScriptGuid},\s*type:\s*3\}}\s*$", RegexOptions.IgnoreCase))
                throw new InvalidOperationException($"The URP global-settings sentinel at '{UrpProjectSetup.GlobalSettingsPath}' has an incorrect m_Script GUID.");

            var metaText = File.ReadAllText(metaPath);
            var assetGuid = Regex.Match(metaText, @"(?m)^guid:\s*([0-9a-f]{32})\s*$", RegexOptions.IgnoreCase);
            if (!assetGuid.Success)
                throw new InvalidOperationException($"The URP global-settings sentinel meta at '{UrpProjectSetup.GlobalSettingsPath}.meta' has no valid GUID.");

            var graphicsSettingsPath = Path.Combine(projectPath, "ProjectSettings", "GraphicsSettings.asset");
            var graphicsSettingsText = File.ReadAllText(graphicsSettingsPath);
            var registrationPattern = $@"(?m)^\s*UnityEngine\.Rendering\.Universal\.UniversalRenderPipeline:\s*\{{fileID:\s*11400000,\s*guid:\s*{Regex.Escape(assetGuid.Groups[1].Value)},\s*type:\s*2\}}\s*$";
            if (!Regex.IsMatch(graphicsSettingsText, registrationPattern, RegexOptions.IgnoreCase))
                throw new InvalidOperationException($"'{UrpProjectSetup.GlobalSettingsPath}' is not registered for URP in Graphics Settings.");
        }

        private static void DeleteNumberedUrpGlobalSettingsCopies()
        {
            foreach (var path in Directory.GetFiles(Application.dataPath, UrpGlobalSettingsAssetName + "*.asset", SearchOption.TopDirectoryOnly))
            {
                var fileName = Path.GetFileNameWithoutExtension(path);
                var suffix = fileName.Substring(UrpGlobalSettingsAssetName.Length);
                if (suffix.Length == 0 || suffix[0] != ' ' || !int.TryParse(suffix.Substring(1), out _))
                    continue;

                FileUtil.DeleteFileOrDirectory(path);
                FileUtil.DeleteFileOrDirectory(path + ".meta");
                Debug.Log($"Removed numbered URP global-settings copy generated during Built-in automation: '{path}'.");
            }
        }

        private static string GetOutputPath(string configurationName)
        {
            var configuredPath = Environment.GetEnvironmentVariable(BuildOutputEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(configuredPath))
                return Path.GetFullPath(configuredPath);

            return Path.GetFullPath(Path.Combine("Builds", "Windows64", configurationName, "Light2D.exe"));
        }

        private static UniversalRenderPipelineAsset GetOrCreateUrpAsset()
        {
            UrpProjectSetup.ValidateGlobalSettings();
            EnsureAssetFolder();
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererDataPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                rendererData.name = "Light2D Universal Renderer";
                AssetDatabase.CreateAsset(rendererData, RendererDataPath);
            }

            EnsureRendererFeature(rendererData);

            var pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
            if (pipelineAsset == null)
            {
                pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
                pipelineAsset.name = "Light2D Universal Render Pipeline";
                AssetDatabase.CreateAsset(pipelineAsset, PipelineAssetPath);
            }

            AssetDatabase.SaveAssets();
            return pipelineAsset;
        }

        private static void EnsureAssetFolder()
        {
            const string parent = "Assets/Light2DTestAutomation";
            if (!AssetDatabase.IsValidFolder(AutomationAssetsPath))
                AssetDatabase.CreateFolder(parent, "Generated");
        }

        private static void EnsureRendererFeature(UniversalRendererData rendererData)
        {
            var serializedRenderer = new SerializedObject(rendererData);
            serializedRenderer.Update();
            var features = serializedRenderer.FindProperty(RendererFeaturesProperty);
            var featureMap = serializedRenderer.FindProperty(RendererFeatureMapProperty);
            if (features == null || featureMap == null)
                throw new InvalidOperationException("The installed URP version does not expose serialized renderer feature lists.");

            for (var index = 0; index < features.arraySize; index++)
            {
                if (features.GetArrayElementAtIndex(index).objectReferenceValue is Light2DUniversalRendererFeature)
                    return;
            }

            var feature = ScriptableObject.CreateInstance<Light2DUniversalRendererFeature>();
            feature.name = nameof(Light2DUniversalRendererFeature);
            AssetDatabase.AddObjectToAsset(feature, rendererData);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            featureMap.arraySize++;
            featureMap.GetArrayElementAtIndex(featureMap.arraySize - 1).longValue = localId;
            serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
            rendererData.SetDirty();
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssetIfDirty(rendererData);
        }
    }
}
