using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Light2D
{
    public class LightingSystemCreationWindow : EditorWindow
    {
        private int _lightObstaclesLayer;
        private int _lightSourcesLayer;
        private int _ambientLightLayer;
        private bool _installRendererFeature = true;

        public static void CreateWindow()
        {
            var window = GetWindow<LightingSystemCreationWindow>("Lighting system creation window");
            window.position = new Rect(200, 200, 500, 260);
        }

        void OnGUI()
        {
            if (FindAnyObjectByType<LightingSystem>())
            {
                GUILayout.Label("WARNING: existing lighting system is found.\nIt is recommended to remove it first, before adding new one.", EditorStyles.boldLabel);
            }

            GUILayout.Label("Select layers you wish to use. You could modify them later in created object.");
            _lightObstaclesLayer = EditorGUILayout.LayerField("Light Obstacles", _lightObstaclesLayer);
            _lightSourcesLayer = EditorGUILayout.LayerField("Light Sources", _lightSourcesLayer);
            _ambientLightLayer = EditorGUILayout.LayerField("Ambient Light", _ambientLightLayer);
            var layerValidationMessage = GetLayerValidationMessage();
            if (layerValidationMessage != null)
                EditorGUILayout.HelpBox(layerValidationMessage, MessageType.Error);

            var pipelineAsset = GraphicsSettings.currentRenderPipeline;
            var isUrp = pipelineAsset is UniversalRenderPipelineAsset;
            if (pipelineAsset != null && !isUrp)
                EditorGUILayout.HelpBox("Light2D supports the Built-in Render Pipeline and URP. The active render pipeline is not supported.", MessageType.Error);
            if (isUrp)
            {
                _installRendererFeature = EditorGUILayout.Toggle("Install Renderer Feature", _installRendererFeature);
                var status = Light2DUniversalRendererFeatureInstaller.GetStatus(Camera.main, out var rendererData);
                var rendererName = rendererData == null ? "unresolved renderer" : rendererData.name;
                EditorGUILayout.HelpBox(GetRendererFeatureMessage(status, rendererName),
                    status == Light2DRendererFeatureStatus.Installed ? MessageType.Info : MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(layerValidationMessage != null || pipelineAsset != null && !isUrp))
            {
                if (GUILayout.Button("Create"))
                {
                    CreateLightingSystem(isUrp);
                }
            }
        }

        private void CreateLightingSystem(bool isUrp)
        {
            var layerValidationMessage = GetLayerValidationMessage();
            if (layerValidationMessage != null)
            {
                EditorUtility.DisplayDialog("Invalid Light2D Layers", layerValidationMessage, "OK");
                return;
            }

            var mainCamera = Camera.main;
            var prefab = Resources.Load<GameObject>("Lighting Camera");
            if (mainCamera == null)
            {
                Debug.LogError("Can't create a lighting system because no camera is tagged MainCamera.");
                return;
            }

            if (prefab == null)
            {
                Debug.LogError("Can't create a lighting system because the Lighting Camera resource is missing.");
                return;
            }

            var prefabConfig = prefab.GetComponent<LightingSystemPrefabConfig>();
            var prefabCamera = prefab.GetComponent<Camera>();
            if (prefabConfig == null || (!isUrp && prefabCamera == null))
            {
                Debug.LogError("Can't create a lighting system because the Lighting Camera resource is invalid.");
                return;
            }

            if (isUrp && !Light2DUniversalRendererFeatureInstaller.TryGetRendererData(mainCamera, out _))
            {
                Debug.LogError("Can't create a URP lighting system because MainCamera does not use a Universal Renderer or 2D Renderer.");
                return;
            }

            var undoGroup = Undo.GetCurrentGroup();
            if (isUrp && _installRendererFeature)
            {
                try
                {
                    Light2DUniversalRendererFeatureInstaller.Install(mainCamera);
                }
                catch (System.Exception exception)
                {
                    Debug.LogError($"Can't create a URP lighting system because the renderer feature could not be installed: {exception.Message}");
                    return;
                }
            }

            Undo.SetCurrentGroupName("Create Lighting System");
            var lightingSystem = mainCamera.GetComponent<LightingSystem>() ??
                                 Undo.AddComponent<LightingSystem>(mainCamera.gameObject);
            Undo.RecordObjects(new Object[] { lightingSystem, mainCamera }, "Configure Lighting System");

            lightingSystem.AmbientLightComputeMaterial = prefabConfig.AmbientLightComputeMaterial;
            lightingSystem.LightOverlayMaterial = prefabConfig.LightOverlayMaterial;
            lightingSystem.AmbientLightBlurMaterial = lightingSystem.LightSourcesBlurMaterial = prefabConfig.BlurMaterial;
            lightingSystem.LightSourcesLayer = _lightSourcesLayer;
            lightingSystem.AmbientLightLayer = _ambientLightLayer;
            lightingSystem.LightObstaclesLayer = _lightObstaclesLayer;
            mainCamera.cullingMask &=
                ~((1 << _lightSourcesLayer) | (1 << _ambientLightLayer) | (1 << _lightObstaclesLayer));

            GameObject selectedObject;
            if (isUrp)
            {
                lightingSystem.LightCamera = null;
                selectedObject = mainCamera.gameObject;
            }
            else
            {
                var lightingSystemObj = (GameObject)Instantiate(prefab);
                Undo.RegisterCreatedObjectUndo(lightingSystemObj, "Create Lighting Camera");
                lightingSystemObj.name = lightingSystemObj.name.Replace("(Clone)", "");
                Undo.SetTransformParent(lightingSystemObj.transform, mainCamera.transform, "Parent Lighting Camera");
                lightingSystemObj.transform.localPosition = Vector3.zero;
                lightingSystemObj.transform.localScale = Vector3.one;
                lightingSystemObj.transform.localRotation = Quaternion.identity;

                var config = lightingSystemObj.GetComponent<LightingSystemPrefabConfig>();
                var lightCamera = lightingSystemObj.GetComponent<Camera>();
                lightingSystem.LightCamera = lightCamera;
                Undo.DestroyObjectImmediate(config);
                lightCamera.depth = mainCamera.depth - 1;
                lightCamera.cullingMask = 1 << _lightSourcesLayer;
                selectedObject = lightingSystemObj;
            }

            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = selectedObject;
            Close();
        }

        private string GetLayerValidationMessage()
        {
            if (!IsValidLayer(_lightObstaclesLayer) || !IsValidLayer(_lightSourcesLayer) ||
                !IsValidLayer(_ambientLightLayer))
                return "Select three configured layers other than Default (layer 0).";

            if (_lightObstaclesLayer == _lightSourcesLayer || _lightObstaclesLayer == _ambientLightLayer ||
                _lightSourcesLayer == _ambientLightLayer)
                return "Light Obstacles, Light Sources, and Ambient Light must use three different layers.";

            return null;
        }

        private static bool IsValidLayer(int layer)
        {
            return layer > 0 && layer < 32 && !string.IsNullOrEmpty(LayerMask.LayerToName(layer));
        }

        private static string GetRendererFeatureMessage(Light2DRendererFeatureStatus status, string rendererName)
        {
            switch (status)
            {
                case Light2DRendererFeatureStatus.Installed:
                    return $"Light2D renderer feature is installed on '{rendererName}'.";
                case Light2DRendererFeatureStatus.InstalledOnDifferentRenderer:
                    return $"Light2D is installed on another renderer, not the MainCamera renderer '{rendererName}'.";
                case Light2DRendererFeatureStatus.Duplicate:
                    return $"Renderer '{rendererName}' contains duplicate Light2D features. Installation will keep exactly one.";
                case Light2DRendererFeatureStatus.UnsupportedRenderer:
                    return "MainCamera must use a Universal Renderer or 2D Renderer.";
                case Light2DRendererFeatureStatus.MissingCamera:
                    return "No camera tagged MainCamera was found.";
                default:
                    return $"Light2D renderer feature is not installed on '{rendererName}'.";
            }
        }
    }
}
