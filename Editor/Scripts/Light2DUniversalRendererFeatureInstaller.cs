using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Light2D
{
    public enum Light2DRendererFeatureStatus
    {
        Installed,
        Missing,
        InstalledOnDifferentRenderer,
        Duplicate,
        UnsupportedPipeline,
        UnsupportedRenderer,
        MissingCamera
    }

    public static class Light2DUniversalRendererFeatureInstaller
    {
        private const string FeatureListProperty = "m_RendererFeatures";
        private const string FeatureMapProperty = "m_RendererFeatureMap";
        private const string InstallMenuPath = "Tools/Light2D/Universal Render Pipeline/Install Renderer Feature";
        private const string RemoveMenuPath = "Tools/Light2D/Universal Render Pipeline/Remove Renderer Feature";

        public static UniversalRenderPipelineAsset CurrentPipelineAsset
        {
            get { return GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset; }
        }

        public static bool TryGetRendererData(Camera camera, out ScriptableRendererData rendererData)
        {
            rendererData = null;
            var pipelineAsset = CurrentPipelineAsset;
            if (pipelineAsset == null || camera == null)
                return false;

            ScriptableRenderer selectedRenderer;
            if (camera.TryGetComponent<UniversalAdditionalCameraData>(out var cameraData))
                selectedRenderer = cameraData.scriptableRenderer;
            else
                selectedRenderer = pipelineAsset.scriptableRenderer;

            var rendererDataList = pipelineAsset.rendererDataList;
            for (var index = 0; index < rendererDataList.Length; index++)
            {
                if (rendererDataList[index] != null && pipelineAsset.GetRenderer(index) == selectedRenderer)
                {
                    rendererData = rendererDataList[index];
                    return IsSupportedRenderer(rendererData);
                }
            }

            return false;
        }

        public static Light2DRendererFeatureStatus GetStatus(Camera camera, out ScriptableRendererData rendererData)
        {
            rendererData = null;
            var pipelineAsset = CurrentPipelineAsset;
            if (pipelineAsset == null)
                return Light2DRendererFeatureStatus.UnsupportedPipeline;
            if (camera == null)
                return Light2DRendererFeatureStatus.MissingCamera;
            if (!TryGetRendererData(camera, out rendererData))
                return Light2DRendererFeatureStatus.UnsupportedRenderer;

            var count = CountFeatures(rendererData);
            if (count > 1)
                return Light2DRendererFeatureStatus.Duplicate;
            if (count == 1)
                return Light2DRendererFeatureStatus.Installed;

            foreach (var candidate in pipelineAsset.rendererDataList)
            {
                if (candidate != rendererData && IsSupportedRenderer(candidate) && CountFeatures(candidate) > 0)
                    return Light2DRendererFeatureStatus.InstalledOnDifferentRenderer;
            }

            return Light2DRendererFeatureStatus.Missing;
        }

        public static Light2DUniversalRendererFeature Install(Camera camera)
        {
            if (!TryGetRendererData(camera, out var rendererData))
                throw new InvalidOperationException("The camera must use a Universal Renderer or 2D Renderer from the current URP asset.");

            return Install(rendererData);
        }

        internal static Light2DUniversalRendererFeature Install(ScriptableRendererData rendererData)
        {
            ValidateEditableRendererData(rendererData, "installed");

            var serializedRenderer = new SerializedObject(rendererData);
            serializedRenderer.Update();
            GetFeatureProperties(serializedRenderer, out var features, out var featureMap);
            RemoveNullEntries(features);
            var matches = FindFeatureIndices(features);
            var removedFeatures = new List<Light2DUniversalRendererFeature>();

            Undo.SetCurrentGroupName("Install Light2D Renderer Feature");
            Undo.RecordObject(rendererData, "Install Light2D Renderer Feature");
            Light2DUniversalRendererFeature installedFeature;
            if (matches.Count == 0)
            {
                installedFeature = ScriptableObject.CreateInstance<Light2DUniversalRendererFeature>();
                installedFeature.name = nameof(Light2DUniversalRendererFeature);
                Undo.RegisterCreatedObjectUndo(installedFeature, "Install Light2D Renderer Feature");
                AssetDatabase.AddObjectToAsset(installedFeature, rendererData);

                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = installedFeature;
            }
            else
            {
                installedFeature = (Light2DUniversalRendererFeature)features
                    .GetArrayElementAtIndex(matches[0]).objectReferenceValue;
                for (var index = matches.Count - 1; index > 0; index--)
                {
                    var featureIndex = matches[index];
                    removedFeatures.Add((Light2DUniversalRendererFeature)features
                        .GetArrayElementAtIndex(featureIndex).objectReferenceValue);
                    DeleteFeatureAt(features, featureIndex);
                }
            }

            RebuildFeatureMap(features, featureMap);
            serializedRenderer.ApplyModifiedProperties();
            foreach (var removedFeature in removedFeatures)
                Undo.DestroyObjectImmediate(removedFeature);

            SaveRendererData(rendererData);
            return installedFeature;
        }

        public static int Remove(Camera camera)
        {
            if (!TryGetRendererData(camera, out var rendererData))
                return 0;

            return Remove(rendererData);
        }

        internal static int Remove(ScriptableRendererData rendererData)
        {
            ValidateEditableRendererData(rendererData, "removed");
            var serializedRenderer = new SerializedObject(rendererData);
            serializedRenderer.Update();
            GetFeatureProperties(serializedRenderer, out var features, out var featureMap);
            RemoveNullEntries(features);
            var matches = FindFeatureIndices(features);
            var removedFeatures = new List<Light2DUniversalRendererFeature>();

            Undo.SetCurrentGroupName("Remove Light2D Renderer Feature");
            Undo.RecordObject(rendererData, "Remove Light2D Renderer Feature");
            for (var index = matches.Count - 1; index >= 0; index--)
            {
                var featureIndex = matches[index];
                removedFeatures.Add((Light2DUniversalRendererFeature)features
                    .GetArrayElementAtIndex(featureIndex).objectReferenceValue);
                DeleteFeatureAt(features, featureIndex);
            }

            RebuildFeatureMap(features, featureMap);
            serializedRenderer.ApplyModifiedProperties();
            foreach (var removedFeature in removedFeatures)
                Undo.DestroyObjectImmediate(removedFeature);

            SaveRendererData(rendererData);
            return removedFeatures.Count;
        }

        [MenuItem(InstallMenuPath)]
        private static void InstallFromMenu()
        {
            var camera = GetSelectedCamera();
            try
            {
                var feature = Install(camera);
                Selection.activeObject = feature;
                Debug.Log($"Installed {nameof(Light2DUniversalRendererFeature)} for camera '{camera.name}'.", feature);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Could not install the Light2D renderer feature: {exception.Message}");
            }
        }

        [MenuItem(InstallMenuPath, true)]
        private static bool CanInstallFromMenu()
        {
            var status = GetStatus(GetSelectedCamera(), out _);
            return status == Light2DRendererFeatureStatus.Missing ||
                   status == Light2DRendererFeatureStatus.InstalledOnDifferentRenderer ||
                   status == Light2DRendererFeatureStatus.Duplicate;
        }

        [MenuItem(RemoveMenuPath)]
        private static void RemoveFromMenu()
        {
            var camera = GetSelectedCamera();
            var count = Remove(camera);
            Debug.Log($"Removed {count} Light2D renderer feature instance(s) for camera '{camera.name}'.", camera);
        }

        [MenuItem(RemoveMenuPath, true)]
        private static bool CanRemoveFromMenu()
        {
            var status = GetStatus(GetSelectedCamera(), out _);
            return status == Light2DRendererFeatureStatus.Installed ||
                   status == Light2DRendererFeatureStatus.Duplicate;
        }

        private static bool IsSupportedRenderer(ScriptableRendererData rendererData)
        {
            return rendererData is UniversalRendererData || rendererData is Renderer2DData;
        }

        private static int CountFeatures(ScriptableRendererData rendererData)
        {
            var count = 0;
            foreach (var feature in rendererData.rendererFeatures)
            {
                if (feature is Light2DUniversalRendererFeature)
                    count++;
            }
            return count;
        }

        private static List<int> FindFeatureIndices(SerializedProperty features)
        {
            var indices = new List<int>();
            for (var index = 0; index < features.arraySize; index++)
            {
                if (features.GetArrayElementAtIndex(index).objectReferenceValue is Light2DUniversalRendererFeature)
                    indices.Add(index);
            }
            return indices;
        }

        private static void DeleteFeatureAt(SerializedProperty features, int index)
        {
            features.GetArrayElementAtIndex(index).objectReferenceValue = null;
            features.DeleteArrayElementAtIndex(index);
        }

        private static void RemoveNullEntries(SerializedProperty features)
        {
            for (var index = features.arraySize - 1; index >= 0; index--)
            {
                if (features.GetArrayElementAtIndex(index).objectReferenceValue == null)
                    features.DeleteArrayElementAtIndex(index);
            }
        }

        private static void RebuildFeatureMap(SerializedProperty features, SerializedProperty featureMap)
        {
            featureMap.arraySize = features.arraySize;
            for (var index = 0; index < features.arraySize; index++)
            {
                var feature = features.GetArrayElementAtIndex(index).objectReferenceValue;
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId))
                    throw new InvalidOperationException($"Renderer feature '{feature.name}' is not a persistent sub-asset.");
                featureMap.GetArrayElementAtIndex(index).longValue = localId;
            }
        }

        private static void GetFeatureProperties(SerializedObject serializedRenderer,
            out SerializedProperty features, out SerializedProperty featureMap)
        {
            features = serializedRenderer.FindProperty(FeatureListProperty);
            featureMap = serializedRenderer.FindProperty(FeatureMapProperty);
            if (features == null || !features.isArray || featureMap == null || !featureMap.isArray)
                throw new InvalidOperationException("This URP version does not expose compatible renderer-feature serialization properties.");
        }

        private static void ValidateEditableRendererData(ScriptableRendererData rendererData, string operation)
        {
            if (!IsSupportedRenderer(rendererData))
                throw new InvalidOperationException("Renderer data must be a Universal Renderer or 2D Renderer.");
            var assetPath = AssetDatabase.GetAssetPath(rendererData);
            if (string.IsNullOrEmpty(assetPath) || !EditorUtility.IsPersistent(rendererData))
                throw new InvalidOperationException($"Renderer data must be a persistent asset before a feature can be {operation}.");
            if (!AssetDatabase.IsOpenForEdit(rendererData, StatusQueryOptions.UseCachedIfPossible))
                throw new InvalidOperationException($"Renderer data '{assetPath}' is read-only. Make it editable before changing Light2D.");
        }

        private static void SaveRendererData(ScriptableRendererData rendererData)
        {
            rendererData.SetDirty();
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssetIfDirty(rendererData);
        }

        private static Camera GetSelectedCamera()
        {
            if (Selection.activeGameObject != null && Selection.activeGameObject.TryGetComponent<Camera>(out var selected))
                return selected;
            return Camera.main;
        }
    }
}
