using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Light2D
{
    public class LightingSystemCreationWindow : EditorWindow
    {
        private bool _installRendererFeature = true;
        private Camera _targetCamera;

        public static void CreateWindow()
        {
            var window = GetWindow<LightingSystemCreationWindow>("Lighting system creation window");
            window.position = new Rect(200, 200, 500, 260);
        }

        private void OnEnable()
        {
            if (_targetCamera == null)
                _targetCamera = Selection.activeGameObject != null
                    ? Selection.activeGameObject.GetComponent<Camera>()
                    : Camera.main;
        }

        void OnGUI()
        {
            _targetCamera = (Camera)EditorGUILayout.ObjectField("Source Camera", _targetCamera, typeof(Camera), true);
            var targetCamera = _targetCamera != null ? _targetCamera : Camera.main;
            if (targetCamera != null && targetCamera.GetComponent<LightingSystem>() != null)
                EditorGUILayout.HelpBox("The selected camera already contains a LightingSystem. Create will update its setup.", MessageType.Info);

            var pipelineAsset = GraphicsSettings.currentRenderPipeline;
            var isUrp = pipelineAsset is UniversalRenderPipelineAsset;
            if (pipelineAsset != null && !isUrp)
                EditorGUILayout.HelpBox("Light2D supports the Built-in Render Pipeline and URP. The active render pipeline is not supported.", MessageType.Error);
            if (isUrp)
            {
                _installRendererFeature = EditorGUILayout.Toggle("Install Renderer Feature", _installRendererFeature);
                var status = Light2DUniversalRendererFeatureInstaller.GetStatus(targetCamera, out var rendererData);
                var rendererName = rendererData == null ? "unresolved renderer" : rendererData.name;
                EditorGUILayout.HelpBox(GetRendererFeatureMessage(status, rendererName),
                    status == Light2DRendererFeatureStatus.Installed ? MessageType.Info : MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(targetCamera == null || pipelineAsset != null && !isUrp))
            {
                if (GUILayout.Button("Create"))
                {
                    CreateLightingSystem(targetCamera, isUrp);
                }
            }
        }

        private void CreateLightingSystem(Camera targetCamera, bool isUrp)
        {
            if (targetCamera == null)
            {
                Debug.LogError("Can't create a lighting system because no camera is selected and no MainCamera fallback exists.");
                return;
            }

            // The helper camera is retired; the default composition materials are carried by an
            // edit-time ScriptableObject that the creation flow reads to configure the LightingSystem.
            var defaults = AssetDatabase.LoadAssetAtPath<Light2DDefaultResources>(
                "Packages/com.sss13594.light2d/Runtime/Light2DDefaultResources.asset");
            if (defaults == null)
            {
                Debug.LogError("Can't create a lighting system because the default material configuration is missing.");
                return;
            }

            ScriptableRendererData urpRendererData = null;
            if (isUrp && !Light2DUniversalRendererFeatureInstaller.TryGetRendererData(targetCamera, out urpRendererData))
            {
                Debug.LogError("Can't create a URP lighting system because the selected camera does not use a Universal Renderer or 2D Renderer.");
                return;
            }

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Lighting System");
            // Assigned only when the installer runs so the catch block saves just the asset it touched.
            ScriptableRendererData rendererData = null;
            try
            {
                if (isUrp && _installRendererFeature)
                {
                    rendererData = urpRendererData;
                    Light2DUniversalRendererFeatureInstaller.Install(rendererData);
                }

                var lightingSystem = targetCamera.GetComponent<LightingSystem>() ??
                                     Undo.AddComponent<LightingSystem>(targetCamera.gameObject);
                Undo.RecordObjects(new Object[] { lightingSystem, targetCamera }, "Configure Lighting System");

                lightingSystem.AmbientLightComputeMaterial = defaults.AmbientLightComputeMaterial;
                lightingSystem.LightOverlayMaterial = defaults.LightOverlayMaterial;
                lightingSystem.AmbientLightBlurMaterial = lightingSystem.LightSourcesBlurMaterial = defaults.BlurMaterial;

                if (PrefabUtility.IsPartOfPrefabInstance(lightingSystem))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(lightingSystem);
                Undo.CollapseUndoOperations(undoGroup);
                Selection.activeGameObject = targetCamera.gameObject;
                Close();
            }
            catch (System.Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                if (rendererData != null)
                    AssetDatabase.SaveAssetIfDirty(rendererData);
                Debug.LogError($"Can't create a lighting system: {exception.Message}", targetCamera);
            }
        }

        private static string GetRendererFeatureMessage(Light2DRendererFeatureStatus status, string rendererName)
        {
            switch (status)
            {
                case Light2DRendererFeatureStatus.Installed:
                    return $"Light2D renderer feature is installed on '{rendererName}'.";
                case Light2DRendererFeatureStatus.InstalledOnDifferentRenderer:
                    return $"Light2D is installed on another renderer, not the selected camera renderer '{rendererName}'.";
                case Light2DRendererFeatureStatus.Duplicate:
                    return $"Renderer '{rendererName}' contains duplicate Light2D features. Installation will keep exactly one.";
                case Light2DRendererFeatureStatus.Inactive:
                    return $"The Light2D feature on '{rendererName}' is disabled. Installation will re-enable it.";
                case Light2DRendererFeatureStatus.UnsupportedRenderer:
                    return "The selected camera must use a Universal Renderer or 2D Renderer.";
                case Light2DRendererFeatureStatus.MissingCamera:
                    return "Select a camera or tag a fallback camera MainCamera.";
                default:
                    return $"Light2D renderer feature is not installed on '{rendererName}'.";
            }
        }
    }
}
