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
        private SerializedProperty _lightObstaclesDistance;
        private SerializedProperty _obstacleColor;
        private SerializedProperty _lightTexturesFilterMode;
        private SerializedProperty _enableNormalMapping;
        private SerializedProperty _affectOnlyThisCamera;
        private SerializedProperty _xzPlane;
        private SerializedProperty _ambientColoredBounce;
        private SerializedProperty _ambientIterations;
        private SerializedProperty _defaultCoverage;
        private SerializedProperty _maxAmbientCyclesPerFrame;
        private SerializedProperty _ambientWarmupCycles;
        private SerializedProperty _maxQueuedAmbientCycles;
        private SerializedProperty _maxLightTexturePixels;
        private SerializedProperty _lightBrightness;
        private SerializedProperty _lightSourcesBrightness;
        private SerializedProperty _bloomPower;
        private SerializedProperty _bloomAdd;
        private SerializedProperty _ambientObstacleMul;
        private SerializedProperty _ambientObstacleAdd;
        private SerializedProperty _ambientEmissionMul;
        private SerializedProperty _ambientSamplingDistance;
        private SerializedProperty _ambientAdaptSpeed;
        private SerializedProperty _blurDistance;

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
            _lightObstaclesDistance = serializedObject.FindProperty("LightObstaclesDistance");
            _obstacleColor = serializedObject.FindProperty("ObstacleColor");
            _lightTexturesFilterMode = serializedObject.FindProperty("LightTexturesFilterMode");
            _enableNormalMapping = serializedObject.FindProperty("EnableNormalMapping");
            _affectOnlyThisCamera = serializedObject.FindProperty("AffectOnlyThisCamera");
            _xzPlane = serializedObject.FindProperty("XZPlane");
            _ambientColoredBounce = serializedObject.FindProperty("AmbientColoredBounce");
            _ambientIterations = serializedObject.FindProperty("AmbientIterations");
            _defaultCoverage = serializedObject.FindProperty("DefaultCoverage");
            _maxAmbientCyclesPerFrame = serializedObject.FindProperty("MaxAmbientCyclesPerFrame");
            _ambientWarmupCycles = serializedObject.FindProperty("AmbientWarmupCycles");
            _maxQueuedAmbientCycles = serializedObject.FindProperty("MaxQueuedAmbientCycles");
            _maxLightTexturePixels = serializedObject.FindProperty("MaxLightTexturePixels");
            _lightBrightness = serializedObject.FindProperty("LightBrightness");
            _lightSourcesBrightness = serializedObject.FindProperty("LightSourcesBrightness");
            _bloomPower = serializedObject.FindProperty("BloomPower");
            _bloomAdd = serializedObject.FindProperty("BloomAdd");
            _ambientObstacleMul = serializedObject.FindProperty("AmbientObstacleMul");
            _ambientObstacleAdd = serializedObject.FindProperty("AmbientObstacleAdd");
            _ambientEmissionMul = serializedObject.FindProperty("AmbientEmissionMul");
            _ambientSamplingDistance = serializedObject.FindProperty("AmbientSamplingDistance");
            _ambientAdaptSpeed = serializedObject.FindProperty("AmbientAdaptSpeed");
            _blurDistance = serializedObject.FindProperty("BlurDistance");
        }

        public override void OnInspectorGUI()
        {
            // Update the serializedProperty - always do this in the beginning of OnInspectorGUI.
            serializedObject.Update();

            // Play-mode gating: the value fields (Obstacles / Overlay Look / Ambient Feel / Blur / Advanced
            // Limits, plus Colored Bounce and Ambient Iterations) are pushed to their materials or read as
            // loop counts every frame, so they live-update the running scene and stay editable in Play - this
            // is the intended in-Play tuning loop. Structural setup fields (texture size, camera geometry,
            // HDR, antialiasing, normal mapping, plane, single-camera mode, filter mode, the ambient/blur
            // resource toggles, and the coverage baseline) allocate or reconfigure render targets, so editing
            // them mid-play would force a reallocation and an ambient-history reset; they are locked in Play
            // instead of the old blanket disable that greyed the value fields too.
            bool playMode = Application.isPlaying;

            var lightingSystem = (LightingSystem)target;
            var cam = lightingSystem.GetComponent<Camera>();
            if (cam == null)
            {
                EditorGUILayout.HelpBox("No attached camera found.", MessageType.Error);
            }

            DrawConfigurationWarnings(cam);

            // Rendered enabled so it stays legible: Unity discards Play-mode inspector edits on exit, so warn
            // that live value tweaks made now are not persisted.
            if (playMode)
                EditorGUILayout.HelpBox("Play mode: the value fields below (Obstacles, Overlay Look, Ambient " +
                    "Feel, Blur, Advanced Limits) are editable and update the lighting live. Setup fields are " +
                    "locked because they reallocate render targets. Note: value tweaks made in Play mode are " +
                    "reverted by Unity when you exit Play mode.", MessageType.Info);

            // --- Structural / setup fields: locked in Play mode (allocate/reconfigure render targets). ---
            using (new EditorGUI.DisabledScope(playMode))
            {
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

                EditorGUILayout.PropertyField(_hdr, new GUIContent("16-bit HDR Color"));
                EditorGUILayout.PropertyField(_lightObstaclesAntialiasing, new GUIContent("Light Obstacles Antialiasing"));
                EditorGUILayout.PropertyField(_enableNormalMapping, new GUIContent("Normal Mapping"));
                EditorGUILayout.PropertyField(_xzPlane, new GUIContent("XZ Plane"));
                EditorGUILayout.PropertyField(_affectOnlyThisCamera, new GUIContent("Affect Only This Camera"));
                _lightTexturesFilterMode.enumValueIndex = (int)(FilterMode)EditorGUILayout.EnumPopup(
                    "Texture Filtering", (FilterMode)_lightTexturesFilterMode.enumValueIndex);

                // Composition materials (overlay, ambient compute, blur) are intentionally hidden from this
                // inspector: the value fields below are the single source of truth and are applied every frame,
                // so only the materials' shaders matter. A freshly added LightingSystem auto-populates the slots
                // from the packaged defaults (LightingSystem.Reset); a power user can still assign a custom-shader
                // material through Unity's Debug inspector.
                EditorGUILayout.PropertyField(_blurLightSources, new GUIContent("Blur Light Sources"));
                if (_blurLightSources.boolValue && _enableNormalMapping.boolValue)
                {
                    EditorGUILayout.LabelField("    Blurring light sources with normal mapping enabled\n");
                    EditorGUILayout.LabelField("     could significantly reduce lighting quality.");
                }

                EditorGUILayout.PropertyField(_enableAmbientLight, new GUIContent("Enable Ambient Light"));
            }

            // Ambient sub-fields are additionally greyed when ambient is disabled. Blur Ambient Light is a
            // structural toggle (also locked in Play), while Colored Bounce and Ambient Iterations are
            // always-applied per-frame values and stay editable in Play whenever ambient is enabled.
            bool ambientEnabled = _enableAmbientLight.boolValue;
            using (new EditorGUI.DisabledScope(!ambientEnabled || playMode))
                EditorGUILayout.PropertyField(_blurAmbientLight, new GUIContent("   Blur Ambient Light"));
            using (new EditorGUI.DisabledScope(!ambientEnabled))
            {
                EditorGUILayout.PropertyField(_ambientColoredBounce, new GUIContent("   Colored Bounce"));
                EditorGUILayout.PropertyField(_ambientIterations, new GUIContent("   Ambient Iterations"));
            }

            // Default Coverage is the world-coverage baseline; crossing 1.0 activates coverage compositing and
            // allocates the coverage render targets, so it is a structural field and stays locked in Play.
            using (new EditorGUI.DisabledScope(playMode))
                EditorGUILayout.PropertyField(_defaultCoverage, new GUIContent("Default Coverage"));

            // --- Always-applied value groups and limits: editable in Play (the live tuning loop). ---
            DrawValueGroups();
            DrawAdvancedLimits();

            DrawSizeEstimate(cam);

            // Apply changes to the serializedProperty - always do this in the end of OnInspectorGUI.
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawValueGroups()
        {
            // Plain, always-expanded section headers (no collapsible foldouts). Each value is a plain,
            // always-editable, always-applied field - the values-only source of truth.
            // Obstacles: the global obstacle multiplier is a write-side value (it scales every obstacle's
            // transmission), so it lives in its own header here rather than under the ambient-only "Feel" group.
            DrawSectionHeader("Obstacles");
            EditorGUILayout.PropertyField(_obstacleColor, Content(_obstacleColor, "Obstacle Color"));

            DrawSectionHeader("Overlay Look");
            EditorGUILayout.PropertyField(_lightBrightness, new GUIContent("Light Brightness"));
            EditorGUILayout.PropertyField(_lightSourcesBrightness, new GUIContent("Light Sources Brightness"));
            EditorGUILayout.PropertyField(_bloomPower, new GUIContent("Bloom Power"));
            EditorGUILayout.PropertyField(_bloomAdd, new GUIContent("Bloom Add"));

            DrawSectionHeader("Ambient Feel");
            EditorGUILayout.PropertyField(_ambientObstacleMul, new GUIContent("Obstacle Mul"));
            EditorGUILayout.PropertyField(_ambientObstacleAdd, new GUIContent("Obstacle Add"));
            EditorGUILayout.PropertyField(_ambientEmissionMul, new GUIContent("Emission Mul"));
            EditorGUILayout.PropertyField(_ambientSamplingDistance, new GUIContent("Sampling Distance"));
            EditorGUILayout.PropertyField(_ambientAdaptSpeed, new GUIContent("Adapt Speed"));

            DrawSectionHeader("Blur");
            EditorGUILayout.PropertyField(_blurDistance, new GUIContent("Blur Distance"));
        }

        private void DrawAdvancedLimits()
        {
            // Always-applied limits: a plain header plus normal fields.
            DrawSectionHeader("Advanced ▸ Limits");
            EditorGUILayout.PropertyField(_maxAmbientCyclesPerFrame, new GUIContent("Max Cycles / Frame"));
            EditorGUILayout.PropertyField(_ambientWarmupCycles, new GUIContent("Warm-up Cycles"));
            EditorGUILayout.PropertyField(_maxQueuedAmbientCycles, new GUIContent("Max Queued Cycles"));
            EditorGUILayout.PropertyField(_maxLightTexturePixels, new GUIContent("Max Light Texture Pixels"));
        }

        private static void DrawSectionHeader(string label)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        }

        // Builds a GUIContent that keeps the custom inspector label but restores the field's [Tooltip] hover
        // text. Passing a bare new GUIContent(label) to PropertyField drops the tooltip that Unity's default
        // inspector would otherwise show from the [Tooltip] attribute; SerializedProperty.tooltip re-reads it.
        private static GUIContent Content(SerializedProperty property, string label)
        {
            return new GUIContent(label, property.tooltip);
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

            if (camera != null)
            {
                if (_enableNormalMapping.boolValue && !camera.orthographic)
                    EditorGUILayout.HelpBox("Normal mapping requires an orthographic camera; perspective rendering is not supported.", MessageType.Error);
                if (_enableNormalMapping.boolValue && _xzPlane.boolValue)
                    EditorGUILayout.HelpBox("Normal mapping supports the XY plane only. Disable XZ Plane to use normal mapping.", MessageType.Error);

                if (_affectOnlyThisCamera.boolValue && urpAsset != null)
                    EditorGUILayout.HelpBox("Affect Only This Camera is supported only by the Built-in Render Pipeline. URP ignores this setting and composites Light2D only for the camera containing this LightingSystem.", MessageType.Warning);
                else if (_affectOnlyThisCamera.boolValue && camera.targetTexture != null)
                    EditorGUILayout.HelpBox("Affect Only This Camera is incompatible with a camera Target Texture in the Built-in Render Pipeline.", MessageType.Error);

                if (_hdr.boolValue && (!camera.allowHDR || urpAsset != null && !urpAsset.supportsHDR))
                    DrawHdrDisabledWarning(camera, urpAsset);

                if (urpAsset != null && camera.TryGetComponent<UniversalAdditionalCameraData>(out var cameraData))
                {
                    if (ShouldWarnAboutXrCamera(cameraData.allowXRRendering))
                        DrawXrCameraWarning(cameraData);
                    if (cameraData.renderType == CameraRenderType.Overlay)
                        EditorGUILayout.HelpBox("URP Overlay cameras are not supported. Attach Light2D to an independent Base camera.", MessageType.Error);
                    else if (cameraData.cameraStack.Count > 0)
                        EditorGUILayout.HelpBox("URP Base cameras with an Overlay stack are not supported. Use an independent Base camera.", MessageType.Error);
                }

                if (camera.targetTexture != null && urpAsset != null)
                    EditorGUILayout.HelpBox("URP target textures are supported for independent Base cameras when the selected renderer contains the Light2D feature.", MessageType.Info);
            }

            if (_hdr.boolValue && !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
                EditorGUILayout.HelpBox("This platform does not support the ARGBHalf render texture format required by 16-bit HDR Color.", MessageType.Warning);
            if (_hdr.boolValue && urpAsset != null &&
                urpAsset.hdrColorBufferPrecision != HDRColorBufferPrecision._64Bits)
                DrawHdrPrecisionWarning(urpAsset);
        }

        // Quick fix for the HDR mismatch warning. It repairs whichever half is actually off - the camera flag,
        // the active URP asset's HDR support, or both - under one collapsed undo step. URP 17 exposes public
        // setters for both serialized values, so no SerializedObject access is needed; the asset is the same
        // one the warning tested (GraphicsSettings.currentRenderPipeline), and it is saved because a pipeline
        // asset is a project asset rather than scene state.
        private static void DrawHdrDisabledWarning(Camera camera, UniversalRenderPipelineAsset urpAsset)
        {
            EditorGUILayout.HelpBox("16-bit HDR Color is enabled, but HDR is disabled on the camera or current URP asset.", MessageType.Warning);
            if (!GUILayout.Button("Enable HDR"))
                return;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Enable HDR");
            var undoGroup = Undo.GetCurrentGroup();

            if (!camera.allowHDR)
            {
                Undo.RecordObject(camera, "Enable HDR");
                camera.allowHDR = true;
                EditorUtility.SetDirty(camera);
            }

            if (urpAsset != null && !urpAsset.supportsHDR)
            {
                Undo.RecordObject(urpAsset, "Enable HDR");
                urpAsset.supportsHDR = true;
                EditorUtility.SetDirty(urpAsset);
                AssetDatabase.SaveAssetIfDirty(urpAsset);
            }

            Undo.CollapseUndoOperations(undoGroup);
        }

        private static void DrawHdrPrecisionWarning(UniversalRenderPipelineAsset urpAsset)
        {
            EditorGUILayout.HelpBox("The URP asset HDR Color Buffer Precision is 32 bits. Select 64 bits to preserve Light2D HDR precision through camera composition.", MessageType.Warning);
            if (!GUILayout.Button("Use 64-bit Precision"))
                return;

            Undo.RecordObject(urpAsset, "Use 64-bit HDR Precision");
            urpAsset.hdrColorBufferPrecision = HDRColorBufferPrecision._64Bits;
            EditorUtility.SetDirty(urpAsset);
            AssetDatabase.SaveAssetIfDirty(urpAsset);
        }

        // UniversalAdditionalCameraData.allowXRRendering defaults to true on every camera, and URP's camera
        // inspector hides the Allow XR Rendering toggle unless the project can render XR - so in a project
        // with no XR packages the flag is always true and cannot even be turned off. URP itself honours the
        // flag only when an XR display is active (it computes cameraData.xrRendering as
        // allowXRRendering && XRSystem.displayActive, which is what Light2DUniversalRendererFeature rejects),
        // so this warning mirrors that and fires only for a project that can actually initialize XR.
        internal static bool ShouldWarnAboutXrCamera(bool allowXRRendering)
        {
            return allowXRRendering && IsProjectXrCapable();
        }

        internal static bool IsProjectXrCapable()
        {
            // XRSRPSettings is SRP core's XR-module-safe accessor (it is compiled against the engine's own
            // ENABLE_VR/ENABLE_XR_MODULE guards and returns false when the XR modules are absent), so this
            // call compiles and answers correctly whether or not any XR package is installed. It reports an
            // XR display that is already initialized - Play mode, or a provider loaded in the Editor.
            if (XRSRPSettings.enabled)
                return true;

#if LIGHT2D_XR
            // XR Plug-in Management is installed (LIGHT2D_XR comes from the asmdef versionDefines entry for
            // com.unity.xr.management, which every current XR provider package depends on), so a provider can
            // initialize at runtime even though none is loaded while this inspector draws. Warn.
            return true;
#else
            // No XR package and no active XR display: this project has no way to run XR, so the default-true
            // allowXRRendering flag is not evidence of anything and must not warn.
            return false;
#endif
        }

        private static void DrawXrCameraWarning(UniversalAdditionalCameraData cameraData)
        {
            EditorGUILayout.HelpBox("XR cameras are not supported by Light2D. Disable Allow XR Rendering on this camera.", MessageType.Error);
            if (!GUILayout.Button("Disable XR Rendering"))
                return;

            Undo.RecordObject(cameraData, "Disable XR Rendering");
            cameraData.allowXRRendering = false;
            EditorUtility.SetDirty(cameraData);
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
                case Light2DRendererFeatureStatus.Inactive:
                    message = "The Light2D renderer feature on this camera's renderer is disabled. URP skips " +
                        "inactive renderer features, so no lighting is composited.";
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
                             status == Light2DRendererFeatureStatus.Duplicate ||
                             status == Light2DRendererFeatureStatus.Inactive;
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
