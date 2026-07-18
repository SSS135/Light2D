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
        }

        public override void OnInspectorGUI()
        {
            // Update the serializedProperty - always do this in the beginning of OnInspectorGUI.
            serializedObject.Update();

            if (Application.isPlaying)
                GUI.enabled = false;

            var lightingSystem = (LightingSystem)target;
            var cam = lightingSystem.GetComponent<Camera>();
            bool isMobileTarget = EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS ||
                                EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android;

            if (cam == null)
            {
                EditorGUILayout.HelpBox("No attached camera found.", MessageType.Error);
            }

            DrawConfigurationWarnings(cam);

            EditorGUILayout.PropertyField(_lightPixelSize, new GUIContent("Light Pixel Size"));

            if (cam != null)
            {
                float size;
                if (cam.orthographic)
                {
                    size = (cam.orthographicSize + _lightCameraSizeAdd.floatValue)*2f;
                }
                else
                {
                    var halfFov = (cam.fieldOfView + _lightCameraFovAdd.floatValue)*Mathf.Deg2Rad/2f;
                    size = Mathf.Tan(halfFov)*_lightObstaclesDistance.floatValue*2;
                }
                if (!Application.isPlaying)
                {

                    int lightTextureHeight = Mathf.RoundToInt(size/_lightPixelSize.floatValue);
                    var oldSize = lightTextureHeight;
                    lightTextureHeight = EditorGUILayout.IntField("Light Texture Height", lightTextureHeight);
                    if (lightTextureHeight%2 != 0)
                        lightTextureHeight++;
                    if (lightTextureHeight < 16)
                    {
                        if (lightTextureHeight < 8)
                            lightTextureHeight = 8;
                        EditorGUILayout.LabelField("WARNING: Light Texture Height is too small.");
                        EditorGUILayout.LabelField(" 50-200 (mobile) and 200-1000 (pc) is recommended.");
                    }
                    if (lightTextureHeight > (isMobileTarget ? 200 : 1000))
                    {
                        if (lightTextureHeight > 2048)
                            lightTextureHeight = 2048;
                        EditorGUILayout.LabelField("WARNING: Light Texture Height is too big.");
                        EditorGUILayout.LabelField(" 50-200 (mobile) and 200-1000 (pc) is recommended.");
                    }
                    if (oldSize != lightTextureHeight)
                    {
                        _lightPixelSize.floatValue = size/lightTextureHeight;
                    }
                }
            }

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
            if (_enableNormalMapping.boolValue && isMobileTarget)
            {
                EditorGUILayout.HelpBox("Normal mapping is not supported on mobile targets.", MessageType.Warning);
            }
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

            // Apply changes to the serializedProperty - always do this in the end of OnInspectorGUI.
            serializedObject.ApplyModifiedProperties();
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
            if (sourceLayer == ambientLayer || sourceLayer == obstacleLayer || ambientLayer == obstacleLayer)
                EditorGUILayout.HelpBox("Light Sources, Ambient Light, and Light Obstacles must use three different layers.", MessageType.Error);

            if (camera != null)
            {
                var lightingMask = (1 << sourceLayer) | (1 << ambientLayer) | (1 << obstacleLayer);
                if ((camera.cullingMask & lightingMask) != 0)
                    EditorGUILayout.HelpBox("The source camera culling mask includes one or more Light2D layers. Exclude all three to avoid drawing lighting inputs directly.", MessageType.Warning);

                if (_enableNormalMapping.boolValue && !camera.orthographic)
                    EditorGUILayout.HelpBox("Normal mapping requires an orthographic camera; perspective rendering is not supported.", MessageType.Error);

                if (_affectOnlyThisCamera.boolValue && urpAsset != null)
                    EditorGUILayout.HelpBox("Affect Only This Camera is supported only by the Built-in Render Pipeline. URP ignores this setting and composites Light2D only for the camera containing this LightingSystem.", MessageType.Warning);
                else if (_affectOnlyThisCamera.boolValue && camera.targetTexture != null)
                    EditorGUILayout.HelpBox("Affect Only This Camera is incompatible with a camera Target Texture in the Built-in Render Pipeline.", MessageType.Error);

                if (_hdr.boolValue && (!camera.allowHDR || urpAsset != null && !urpAsset.supportsHDR))
                    EditorGUILayout.HelpBox("64 Bit Color is enabled, but HDR is disabled on the camera or current URP asset.", MessageType.Warning);
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
