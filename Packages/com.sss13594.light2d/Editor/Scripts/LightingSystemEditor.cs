using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Light2D
{
    [CustomEditor(typeof(LightingSystem))]
    public class LightingSystemEditor : Editor
    {
        private SerializedProperty _lightPixelSize;
        private SerializedProperty _lightCameraSizeAdd;
        private SerializedProperty _lightCameraFovAdd;
        private SerializedProperty _enableAmbientLight;
        private SerializedProperty _blurLightSources;
        private SerializedProperty _blurAmbientLight ;
        private SerializedProperty  _hdr ;
        private SerializedProperty _lightObstaclesAntialiasing;
        private SerializedProperty _ambientLightComputeMaterial;
        private SerializedProperty _lightOverlayMaterial;
        private SerializedProperty _lightSourcesBlurMaterial;
        private SerializedProperty _ambientLightBlurMaterial;
        private SerializedProperty _lightCamera;
        private SerializedProperty _lightSourcesLayer;
        private SerializedProperty _ambientLightLayer;
        private SerializedProperty _lightObstaclesLayer;
        private SerializedProperty _lightObstaclesDistance;
        private SerializedProperty _lightTexturesFilterMode;
        private SerializedProperty _enableNormalMapping;
        private SerializedProperty _affectOnlyThisCamera;
        private SerializedProperty _xzPlane;

        void OnEnable()
        {
            _lightPixelSize = serializedObject.FindProperty("LightPixelSize");
            _lightCameraSizeAdd = serializedObject.FindProperty("LightCameraSizeAdd");
            _lightCameraFovAdd = serializedObject.FindProperty("LightCameraFovAdd");
            _enableAmbientLight = serializedObject.FindProperty("EnableAmbientLight");
            _blurLightSources = serializedObject.FindProperty("BlurLightSources");
            _blurAmbientLight = serializedObject.FindProperty("BlurAmbientLight");
            _hdr = serializedObject.FindProperty("HDR");
            _lightObstaclesAntialiasing = serializedObject.FindProperty("LightObstaclesAntialiasing");
            _ambientLightComputeMaterial = serializedObject.FindProperty("AmbientLightComputeMaterial");
            _lightOverlayMaterial = serializedObject.FindProperty("LightOverlayMaterial");
            _lightSourcesBlurMaterial = serializedObject.FindProperty("LightSourcesBlurMaterial");
            _ambientLightBlurMaterial = serializedObject.FindProperty("AmbientLightBlurMaterial");
            _lightCamera = serializedObject.FindProperty("LightCamera");
            _lightSourcesLayer = serializedObject.FindProperty("LightSourcesLayer");
            _ambientLightLayer = serializedObject.FindProperty("AmbientLightLayer");
            _lightObstaclesLayer = serializedObject.FindProperty("LightObstaclesLayer");
            _lightObstaclesDistance = serializedObject.FindProperty("LightObstaclesDistance");
            _lightTexturesFilterMode = serializedObject.FindProperty("LightTexturesFilterMode");
            _enableNormalMapping = serializedObject.FindProperty("EnableNormalMapping");
            _affectOnlyThisCamera = serializedObject.FindProperty("AffectOnlyThisCamera");
            _xzPlane = serializedObject.FindProperty("XZPlane");
        }

        public override void OnInspectorGUI()
        {
            // Update the serializedProperty - always do this in the beginning of OnInspectorGUI.
            serializedObject.Update();

            if (Application.isPlaying)
                GUI.enabled = false;

            var lightingSystem = (LightingSystem)target;
            var cam = lightingSystem.GetComponent<Camera>();
            if (cam == null)
            {
                EditorGUILayout.HelpBox("No attached camera found.", MessageType.Error);
            }

            DrawConfigurationWarnings(cam);

            EditorGUILayout.PropertyField(_lightPixelSize, new GUIContent("Light Pixel Size"));

            if (cam == null || cam.orthographic)
            {
                EditorGUILayout.PropertyField(_lightCameraSizeAdd, new GUIContent("Light Camera Size Add"));
            }
            else
            {
                EditorGUILayout.PropertyField(_lightCameraFovAdd, new GUIContent("Light Camera Fov Add"));
                EditorGUILayout.PropertyField(_lightObstaclesDistance, new GUIContent("Camera To Light Obstacles Distance"));
            }

            EditorGUILayout.PropertyField(_hdr, new GUIContent("64 Bit Color"));
            EditorGUILayout.PropertyField(_lightObstaclesAntialiasing, new GUIContent("Light Obstacles Antialiasing"));
            EditorGUILayout.PropertyField(_enableNormalMapping, new GUIContent("Normal Mapping"));
            EditorGUILayout.PropertyField(_xzPlane, new GUIContent("XZ Plane"));
            EditorGUILayout.PropertyField(_affectOnlyThisCamera, new GUIContent("Affect Only This Camera"));
            _lightTexturesFilterMode.enumValueIndex = (int)(FilterMode)EditorGUILayout.EnumPopup(
                "Texture Filtering", (FilterMode)_lightTexturesFilterMode.enumValueIndex);

            EditorGUILayout.PropertyField(_blurLightSources, new GUIContent("Blur Light Sources"));
            if (_blurLightSources.boolValue && _enableNormalMapping.boolValue)
            {
                EditorGUILayout.LabelField("    Blurring light sources with normal mapping enabled\n");
                EditorGUILayout.LabelField("     could significantly reduce lighting quality.");
            }

            bool normalGuiEnableState = GUI.enabled;
            if (!_blurLightSources.boolValue)
                GUI.enabled = false;
            EditorGUILayout.PropertyField(_lightSourcesBlurMaterial, new GUIContent("   Light Sources Blur Material"));
            GUI.enabled = normalGuiEnableState;

            EditorGUILayout.PropertyField(_enableAmbientLight, new GUIContent("Enable Ambient Light"));
            if (!_enableAmbientLight.boolValue)
                GUI.enabled = false;
            EditorGUILayout.PropertyField(_blurAmbientLight, new GUIContent("   Blur Ambient Light"));
            var oldEnabled = GUI.enabled;
            if (!_blurAmbientLight.boolValue)
                GUI.enabled = false;
            EditorGUILayout.PropertyField(_ambientLightBlurMaterial, new GUIContent("   Ambient Light Blur Material"));
            GUI.enabled = oldEnabled;
            EditorGUILayout.PropertyField(_ambientLightComputeMaterial, new GUIContent("   Ambient Light Compute Material"));
            GUI.enabled = normalGuiEnableState;

            EditorGUILayout.PropertyField(_lightOverlayMaterial, new GUIContent("Light Overlay Material"));
            if (GraphicsSettings.currentRenderPipeline == null)
                EditorGUILayout.PropertyField(_lightCamera, new GUIContent("Lighting Camera"));
            _lightSourcesLayer.intValue = EditorGUILayout.LayerField(new GUIContent("Light Sources Layer"), _lightSourcesLayer.intValue);
            _lightObstaclesLayer.intValue = EditorGUILayout.LayerField(new GUIContent("Light Obstacles Layer"), _lightObstaclesLayer.intValue);
            _ambientLightLayer.intValue = EditorGUILayout.LayerField(new GUIContent("Ambient Light Layer"), _ambientLightLayer.intValue);

            DrawSizeEstimate(cam);

            // Apply changes to the serializedProperty - always do this in the end of OnInspectorGUI.
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSizeEstimate(Camera camera)
        {
            if (!LightingSettingsValidator.TryCalculateSizes(camera, _lightPixelSize.floatValue,
                    _lightCameraSizeAdd.floatValue, _lightCameraFovAdd.floatValue,
                    _lightObstaclesDistance.floatValue, _hdr.boolValue, _enableAmbientLight.boolValue,
                    _lightObstaclesAntialiasing.boolValue, _enableNormalMapping.boolValue,
                    out var estimate, out var error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Estimated Light Textures", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Camera Coverage", $"{estimate.Small.x} x {estimate.Small.y}");
            EditorGUILayout.LabelField("Extended Coverage", $"{estimate.Extended.x} x {estimate.Extended.y}");
            EditorGUILayout.LabelField("Working Texture Memory", EditorUtility.FormatBytes(estimate.EstimatedTextureBytes));
            EditorGUILayout.HelpBox("Memory excludes camera-sized composition targets and driver overhead.", MessageType.None);
        }

        private void DrawConfigurationWarnings(Camera camera)
        {
            var pipelineAsset = GraphicsSettings.currentRenderPipeline;
            var urpAsset = pipelineAsset as UniversalRenderPipelineAsset;
            if (pipelineAsset != null && urpAsset == null)
            {
                EditorGUILayout.HelpBox("The active render pipeline is not supported. Use the Built-in Render Pipeline or URP.", MessageType.Error);
                return;
            }

            if (urpAsset != null)
                DrawRendererFeatureWarning(camera);
            else if (_lightCamera.objectReferenceValue == null)
                EditorGUILayout.HelpBox("Built-in rendering requires a Lighting Camera assignment.", MessageType.Error);

            var sourceLayer = _lightSourcesLayer.intValue;
            var ambientLayer = _ambientLightLayer.intValue;
            var obstacleLayer = _lightObstaclesLayer.intValue;
            var layerError = LightingSettingsValidator.ValidateLayers(sourceLayer, ambientLayer, obstacleLayer, false);
            if (layerError != null)
                EditorGUILayout.HelpBox(layerError, MessageType.Error);

            if (camera != null)
            {
                var lightingMask = (1 << sourceLayer) | (1 << ambientLayer) | (1 << obstacleLayer);
                if ((camera.cullingMask & lightingMask) != 0)
                    EditorGUILayout.HelpBox("The source camera culling mask includes one or more Light2D layers. Exclude all three to avoid drawing lighting inputs directly.", MessageType.Warning);

                if (_enableNormalMapping.boolValue && !camera.orthographic)
                    EditorGUILayout.HelpBox("Normal mapping requires an orthographic camera; perspective rendering is not supported.", MessageType.Error);
                if (_enableNormalMapping.boolValue && _xzPlane.boolValue)
                    EditorGUILayout.HelpBox("Normal mapping supports the XY plane only. Disable XZ Plane to use normal mapping.", MessageType.Error);

                if (_affectOnlyThisCamera.boolValue && urpAsset != null)
                    EditorGUILayout.HelpBox("Affect Only This Camera is supported only by the Built-in Render Pipeline. URP ignores this setting and composites Light2D only for the camera containing this LightingSystem.", MessageType.Warning);
                else if (_affectOnlyThisCamera.boolValue && camera.targetTexture != null)
                    EditorGUILayout.HelpBox("Affect Only This Camera is incompatible with a camera Target Texture in the Built-in Render Pipeline.", MessageType.Error);

                if (_hdr.boolValue && (!camera.allowHDR || urpAsset != null && !urpAsset.supportsHDR))
                    EditorGUILayout.HelpBox("64 Bit Color is enabled, but HDR is disabled on the camera or current URP asset.", MessageType.Warning);

                if (urpAsset != null && camera.TryGetComponent<UniversalAdditionalCameraData>(out var cameraData))
                {
                    if (cameraData.allowXRRendering)
                        EditorGUILayout.HelpBox("XR cameras are not supported by Light2D. Disable Allow XR Rendering on this camera.", MessageType.Error);
                    if (cameraData.renderType == CameraRenderType.Overlay)
                        EditorGUILayout.HelpBox("URP Overlay cameras are not supported. Attach Light2D to an independent Base camera.", MessageType.Error);
                    else if (cameraData.cameraStack.Count > 0)
                        EditorGUILayout.HelpBox("URP Base cameras with an Overlay stack are not supported. Use an independent Base camera.", MessageType.Error);
                }

                if (camera.targetTexture != null && urpAsset != null)
                    EditorGUILayout.HelpBox("URP target textures are supported for independent Base cameras when the selected renderer contains the Light2D feature.", MessageType.Info);
            }

            if (_hdr.boolValue && !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
                EditorGUILayout.HelpBox("This platform does not support the ARGBHalf render texture format required by 64 Bit Color.", MessageType.Warning);
            if (_hdr.boolValue && urpAsset != null &&
                urpAsset.hdrColorBufferPrecision != HDRColorBufferPrecision._64Bits)
                EditorGUILayout.HelpBox("The URP asset HDR Color Buffer Precision is 32 bits. Select 64 bits to preserve Light2D HDR precision through camera composition.", MessageType.Warning);
        }

        private static void DrawRendererFeatureWarning(Camera camera)
        {
            var status = Light2DUniversalRendererFeatureInstaller.GetStatus(camera, out var rendererData);
            if (status == Light2DRendererFeatureStatus.Installed)
                return;

            string message;
            switch (status)
            {
                case Light2DRendererFeatureStatus.InstalledOnDifferentRenderer:
                    message = "The Light2D renderer feature is installed, but not on the renderer selected by this camera.";
                    break;
                case Light2DRendererFeatureStatus.Duplicate:
                    message = "This camera's renderer contains duplicate Light2D renderer features.";
                    break;
                case Light2DRendererFeatureStatus.UnsupportedRenderer:
                    message = "This camera must use a Universal Renderer or 2D Renderer.";
                    break;
                default:
                    message = "The Light2D renderer feature is missing from this camera's renderer.";
                    break;
            }

            EditorGUILayout.HelpBox(message, MessageType.Error);
            var canInstall = status == Light2DRendererFeatureStatus.Missing ||
                             status == Light2DRendererFeatureStatus.InstalledOnDifferentRenderer ||
                             status == Light2DRendererFeatureStatus.Duplicate;
            if (rendererData != null && canInstall && GUILayout.Button("Install / Repair Renderer Feature"))
            {
                try
                {
                    Light2DUniversalRendererFeatureInstaller.Install(camera);
                }
                catch (System.Exception exception)
                {
                    Debug.LogError($"Could not install the Light2D renderer feature: {exception.Message}", camera);
                }
            }
        }
    }
}
