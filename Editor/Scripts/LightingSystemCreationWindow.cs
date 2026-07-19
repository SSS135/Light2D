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
                var status = Light2DUniversalRendererFeatureInstaller.GetStatus(targetCamera, out var rendererData);
                var rendererName = rendererData == null ? "unresolved renderer" : rendererData.name;
                EditorGUILayout.HelpBox(GetRendererFeatureMessage(status, rendererName),
                    status == Light2DRendererFeatureStatus.Installed ? MessageType.Info : MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(targetCamera == null || layerValidationMessage != null ||
                                               pipelineAsset != null && !isUrp))
            {
                if (GUILayout.Button("Create"))
                {
                    CreateLightingSystem(targetCamera, isUrp);
                }
            }
        }

        private void CreateLightingSystem(Camera targetCamera, bool isUrp)
        {
            var layerValidationMessage = GetLayerValidationMessage();
            if (layerValidationMessage != null)
            {
                EditorUtility.DisplayDialog("Invalid Light2D Layers", layerValidationMessage, "OK");
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Packages/com.sss13594.light2d/Runtime/Resources/Lighting Camera.prefab");
            if (targetCamera == null)
            {
                Debug.LogError("Can't create a lighting system because no camera is selected and no MainCamera fallback exists.");
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

                lightingSystem.AmbientLightComputeMaterial = prefabConfig.AmbientLightComputeMaterial;
                lightingSystem.LightOverlayMaterial = prefabConfig.LightOverlayMaterial;
                lightingSystem.AmbientLightBlurMaterial = lightingSystem.LightSourcesBlurMaterial = prefabConfig.BlurMaterial;
                lightingSystem.LightSourcesLayer = _lightSourcesLayer;
                lightingSystem.AmbientLightLayer = _ambientLightLayer;
                lightingSystem.LightObstaclesLayer = _lightObstaclesLayer;
                targetCamera.cullingMask &=
                    ~((1 << _lightSourcesLayer) | (1 << _ambientLightLayer) | (1 << _lightObstaclesLayer));

                GameObject selectedObject;
                if (isUrp)
                {
                    lightingSystem.LightCamera = null;
                    selectedObject = targetCamera.gameObject;
                }
                else
                {
                    var lightingSystemObj = PrefabUtility.InstantiatePrefab(prefab, targetCamera.gameObject.scene) as GameObject;
                    if (lightingSystemObj == null)
                        throw new System.InvalidOperationException("The Lighting Camera prefab could not be instantiated.");
                    Undo.RegisterCreatedObjectUndo(lightingSystemObj, "Create Lighting Camera");
                    Undo.SetTransformParent(lightingSystemObj.transform, targetCamera.transform, "Parent Lighting Camera");
                    lightingSystemObj.transform.localPosition = Vector3.zero;
                    lightingSystemObj.transform.localScale = Vector3.one;
                    lightingSystemObj.transform.localRotation = Quaternion.identity;

                    var config = lightingSystemObj.GetComponent<LightingSystemPrefabConfig>();
                    var lightCamera = lightingSystemObj.GetComponent<Camera>();
                    lightingSystem.LightCamera = lightCamera;
                    Undo.DestroyObjectImmediate(config);
                    lightCamera.depth = targetCamera.depth - 1;
                    lightCamera.cullingMask = 1 << _lightSourcesLayer;
                    selectedObject = lightingSystemObj;
                }

                if (PrefabUtility.IsPartOfPrefabInstance(targetCamera))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(targetCamera);
                if (PrefabUtility.IsPartOfPrefabInstance(lightingSystem))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(lightingSystem);
                Undo.CollapseUndoOperations(undoGroup);
                Selection.activeGameObject = selectedObject;
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

        private string GetLayerValidationMessage()
        {
            return LightingSettingsValidator.ValidateLayers(_lightSourcesLayer, _ambientLightLayer,
                _lightObstaclesLayer, true);
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
