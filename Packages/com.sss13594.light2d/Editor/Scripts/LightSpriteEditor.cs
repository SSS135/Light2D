using UnityEditor;
using UnityEngine;

namespace Light2D
{
    /// <summary>
    /// First-class inspector for <see cref="LightSprite"/>. It surfaces the light's real controls in a
    /// top-down order (Type, HDR Color, Intensity, Radius, Falloff, Spot Angle, Shadows, an optional
    /// Sprite override, then placement) instead of the raw serialized fields, adds draggable Scene-view
    /// handles for Radius and the Spot cone, and warns about the common setup traps (no light material,
    /// no LightingSystem, overbright without HDR) with one-click fixes.
    ///
    /// The <c>Shadows</c> popup is a view over the real <see cref="CustomSprite.Material"/> slot: it
    /// reverse-maps the assigned material's GUID back to a <see cref="LightSprite.ShadowQuality"/> preset
    /// (via <see cref="Light2DLightPresets"/>) so the popup always reflects reality, and writes the resolved
    /// preset material back into the slot when a level is chosen. <see cref="LightSprite.ShadowQuality.Custom"/>
    /// stops syncing and hands the raw material slot back. All edits go through the serialized object (Undo
    /// automatic) except the Scene handles, which mutate the target directly with <c>Undo.RecordObject</c>.
    /// </summary>
    [CustomEditor(typeof(LightSprite))]
    [CanEditMultipleObjects]
    internal sealed class LightSpriteEditor : Editor
    {
        // Warm light-coloured handles so the Scene-view gizmos read as "this is the light".
        private static readonly Color HandleColor = new Color(1f, 0.85f, 0.4f, 0.9f);

        // Cardinal directions for the Radius dots, in the light's local XY plane (mirrors Light2DVolumeEditor).
        private static readonly Vector3[] CircleHandleDirections =
        {
            Vector3.right, Vector3.up, Vector3.left, Vector3.down
        };

        // Non-Custom quality levels, used to reverse-map an assigned material GUID back to a preset.
        private static readonly LightSprite.ShadowQuality[] PresetQualities =
        {
            LightSprite.ShadowQuality.Fast,
            LightSprite.ShadowQuality.Balanced,
            LightSprite.ShadowQuality.High,
            LightSprite.ShadowQuality.Auto,
            LightSprite.ShadowQuality.IgnoreObstacles,
        };

        private const float IntensitySliderMax = 8f;
        private const float RadiusSliderMax = 20f;

        private static readonly GUIContent TypeLabel = new GUIContent("Type",
            "Point emits radially, Spot projects a soft cone from the origin apex, and Line emits along the sprite's horizontal extent.");
        private static readonly GUIContent ColorLabel = new GUIContent("Color",
            "Light colour, multiplied by Intensity. HDR: channels above 1 need the owning LightingSystem's 64 Bit Color enabled.");
        private static readonly GUIContent IntensityLabel = new GUIContent("Intensity",
            "Brightness multiplied into the light colour. Type a value above the slider to overdrive it.");
        private static readonly GUIContent RadiusLabel = new GUIContent("Radius",
            "Light size in world units, baked into the mesh independently of the Transform scale.");
        private static readonly GUIContent FalloffLabel = new GUIContent("Falloff",
            "Edge hardness of the generated falloff, 0 soft to 1 hard. Ignored when a Sprite override is assigned.");
        private static readonly GUIContent SpotAngleLabel = new GUIContent("Spot Angle",
            "Cone angle in degrees, used when Type is Spot.");
        private static readonly GUIContent ShadowsLabel = new GUIContent("Shadows",
            "Ray-march shadow quality. Maps to the light material; Custom hands the Material slot back to you.");
        private static readonly GUIContent MaterialLabel = new GUIContent("Custom Material",
            "Light material used when Shadows is Custom. Leave empty to fall back to Sprites/Default (wrong blend).");
        private static readonly GUIContent SpriteOverrideLabel = new GUIContent("Override Sprite");
        private static readonly GUIContent SpriteLabel = new GUIContent("Sprite",
            "Optional sprite that replaces the generated falloff. Leave empty to generate from Type/Falloff/Spot Angle.");
        private static readonly GUIContent LightOriginLabel = new GUIContent("Light Origin",
            "Emission origin in sprite-local space; Z is the light's height above the plane for normal mapping.");
        private static readonly GUIContent SortingOrderLabel = new GUIContent("Sorting Order",
            "Draw order among lights; lower values draw first.");
        private static readonly GUIContent OwnerLabel = new GUIContent("Owner",
            "LightingSystem that draws this light. Leave empty to draw for any system.");

        private SerializedProperty _type;
        private SerializedProperty _color;
        private SerializedProperty _intensity;
        private SerializedProperty _radius;
        private SerializedProperty _falloff;
        private SerializedProperty _spotAngle;
        private SerializedProperty _shadows;
        private SerializedProperty _material;
        private SerializedProperty _sprite;
        private SerializedProperty _lightOrigin;
        private SerializedProperty _sortingOrder;
        private SerializedProperty _owner;

        private bool _showSpriteOverride;

        private void OnEnable()
        {
            _type = serializedObject.FindProperty("Type");
            _color = serializedObject.FindProperty("Color");
            _intensity = serializedObject.FindProperty("Intensity");
            _radius = serializedObject.FindProperty("Radius");
            _falloff = serializedObject.FindProperty("Falloff");
            _spotAngle = serializedObject.FindProperty("SpotAngle");
            _shadows = serializedObject.FindProperty("Shadows");
            _material = serializedObject.FindProperty("Material");
            _sprite = serializedObject.FindProperty("Sprite");
            _lightOrigin = serializedObject.FindProperty("LightOrigin");
            _sortingOrder = serializedObject.FindProperty("SortingOrder");
            _owner = serializedObject.FindProperty("Owner");

            // Reveal the override foldout when a sprite is already assigned so the user sees their override.
            _showSpriteOverride = _sprite.objectReferenceValue != null;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawWarnings();

            EditorGUILayout.PropertyField(_type, TypeLabel);
            DrawHdrColor();
            OverflowSlider(_intensity, IntensityLabel, 0f, IntensitySliderMax);
            OverflowSlider(_radius, RadiusLabel, 0f, RadiusSliderMax);
            EditorGUILayout.PropertyField(_falloff, FalloffLabel);
            if (_type.enumValueIndex == (int)LightSprite.LightType.Spot)
                EditorGUILayout.PropertyField(_spotAngle, SpotAngleLabel);

            DrawShadowsField();
            DrawSpriteOverride();

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_lightOrigin, LightOriginLabel);
            EditorGUILayout.PropertyField(_sortingOrder, SortingOrderLabel);
            EditorGUILayout.PropertyField(_owner, OwnerLabel);

            // Shape is intentionally not drawn: it is derived from Type by LightSprite.ApplyLightTypeShape.
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawHdrColor()
        {
            EditorGUI.showMixedValue = _color.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var newColor = EditorGUILayout.ColorField(ColorLabel, _color.colorValue, true, true, true);
            if (EditorGUI.EndChangeCheck())
                _color.colorValue = newColor;
            EditorGUI.showMixedValue = false;
        }

        // Slider whose accompanying field allows values above the slider maximum (Intensity/Radius overdrive).
        private static void OverflowSlider(SerializedProperty prop, GUIContent label, float min, float max)
        {
            var rect = EditorGUILayout.GetControlRect();
            EditorGUI.BeginProperty(rect, label, prop);

            var content = EditorGUI.PrefixLabel(rect, label);
            const float fieldWidth = 52f;
            const float gap = 4f;
            var sliderRect = new Rect(content.x, content.y, Mathf.Max(0f, content.width - fieldWidth - gap), content.height);
            var fieldRect = new Rect(content.xMax - fieldWidth, content.y, fieldWidth, content.height);

            EditorGUI.BeginChangeCheck();
            var sliderValue = GUI.HorizontalSlider(sliderRect, Mathf.Clamp(prop.floatValue, min, max), min, max);
            if (EditorGUI.EndChangeCheck())
                prop.floatValue = sliderValue;

            EditorGUI.BeginChangeCheck();
            var fieldValue = EditorGUI.FloatField(fieldRect, prop.floatValue);
            if (EditorGUI.EndChangeCheck())
                prop.floatValue = Mathf.Max(min, fieldValue);

            EditorGUI.EndProperty();
        }

        private void DrawShadowsField()
        {
            var display = ComputeDisplayQuality();

            EditorGUI.showMixedValue = _shadows.hasMultipleDifferentValues || _material.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var selected = (LightSprite.ShadowQuality)EditorGUILayout.EnumPopup(ShadowsLabel, display);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
            {
                _shadows.enumValueIndex = (int)selected;
                if (selected != LightSprite.ShadowQuality.Custom)
                    _material.objectReferenceValue = LoadPresetMaterial(selected);
                // Custom: leave Material untouched so per-light custom assignments survive.
                display = selected;
            }

            // Custom (or an unrecognised/null material) exposes the raw Material slot for manual assignment.
            if (display == LightSprite.ShadowQuality.Custom)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_material, MaterialLabel);
                EditorGUI.indentLevel--;
            }
        }

        private void DrawSpriteOverride()
        {
            _showSpriteOverride = EditorGUILayout.Foldout(_showSpriteOverride, SpriteOverrideLabel, true);
            if (!_showSpriteOverride)
                return;

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_sprite, SpriteLabel);
            if (_sprite.objectReferenceValue == null && !_sprite.hasMultipleDifferentValues)
                EditorGUILayout.HelpBox(
                    "Empty: the falloff is generated from Type, Falloff, and Spot Angle. Assign a sprite to override generation.",
                    MessageType.None);
            EditorGUI.indentLevel--;
        }

        private void DrawWarnings()
        {
            var warned = false;

            // Trap 1: Custom shadow quality with no material falls back to Sprites/Default (wrong blend).
            if (ComputeDisplayQuality() == LightSprite.ShadowQuality.Custom &&
                !_material.hasMultipleDifferentValues && _material.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "No light material is assigned, so the light falls back to Sprites/Default and blends incorrectly. Assign a shadow-quality material.",
                    MessageType.Warning);
                if (GUILayout.Button("Assign Balanced Material"))
                    AssignPresetMaterial(LightSprite.ShadowQuality.Balanced);
                warned = true;
            }

            // Trap 2: no LightingSystem to draw this light, so it never appears.
            if (!HasOwningSystem())
            {
                EditorGUILayout.HelpBox(
                    "No LightingSystem was found for this light, so it never draws. Add a LightingSystem to your camera.",
                    MessageType.Warning);
                if (GUILayout.Button("Create Lighting System"))
                    LightingSystemCreationWindow.CreateWindow();
                warned = true;
            }

            // Trap 3 (soft): overbright Intensity/Color while the owning system has 64 Bit Color off, so it clamps.
            var system = ResolveOwningSystem();
            if (system != null && !system.HDR)
            {
                var color = _color.colorValue;
                if (_intensity.floatValue > 1f || color.r > 1f || color.g > 1f || color.b > 1f)
                {
                    EditorGUILayout.HelpBox(
                        "Intensity or the HDR colour exceeds 1, but the owning LightingSystem has 64 Bit Color disabled, so the extra brightness is clamped.",
                        MessageType.Info);
                    warned = true;
                }
            }

            if (warned)
                EditorGUILayout.Space();
        }

        // Popup value: Custom stays sticky once chosen; otherwise the assigned material's GUID is reverse-mapped
        // back to its preset (a null/unrecognised material reads as Custom), so the popup mirrors reality.
        private LightSprite.ShadowQuality ComputeDisplayQuality()
        {
            var serialized = (LightSprite.ShadowQuality)_shadows.enumValueIndex;
            if (serialized == LightSprite.ShadowQuality.Custom)
                return LightSprite.ShadowQuality.Custom;
            return ResolveQualityFromMaterial(_material.objectReferenceValue as Material);
        }

        private static LightSprite.ShadowQuality ResolveQualityFromMaterial(Material material)
        {
            if (material == null)
                return LightSprite.ShadowQuality.Custom;
            var path = AssetDatabase.GetAssetPath(material);
            if (string.IsNullOrEmpty(path))
                return LightSprite.ShadowQuality.Custom;
            var guid = AssetDatabase.AssetPathToGUID(path);
            foreach (var quality in PresetQualities)
            {
                if (Light2DLightPresets.GetMaterialGuid(quality) == guid)
                    return quality;
            }
            return LightSprite.ShadowQuality.Custom;
        }

        private static Material LoadPresetMaterial(LightSprite.ShadowQuality quality)
        {
            var guid = Light2DLightPresets.GetMaterialGuid(quality);
            if (string.IsNullOrEmpty(guid))
                return null;
            var path = AssetDatabase.GUIDToAssetPath(guid);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private void AssignPresetMaterial(LightSprite.ShadowQuality quality)
        {
            _shadows.enumValueIndex = (int)quality;
            _material.objectReferenceValue = LoadPresetMaterial(quality);
        }

        private bool HasOwningSystem()
        {
            // An explicit, resolved Owner is enough; otherwise any LightingSystem in the scene can serve the light.
            if (!_owner.hasMultipleDifferentValues && _owner.objectReferenceValue is LightingSystem)
                return true;
            return Object.FindAnyObjectByType<LightingSystem>() != null;
        }

        private LightingSystem ResolveOwningSystem()
        {
            if (!_owner.hasMultipleDifferentValues && _owner.objectReferenceValue is LightingSystem owner)
                return owner;
            return Object.FindAnyObjectByType<LightingSystem>();
        }

        private void OnSceneGUI()
        {
            var light = target as LightSprite;
            if (light == null)
                return;

            DrawRadiusHandle(light);
            if (light.Type == LightSprite.LightType.Spot)
                DrawSpotHandle(light);
        }

        private static void DrawRadiusHandle(LightSprite light)
        {
            var t = light.transform;
            using (new Handles.DrawingScope(HandleColor, Matrix4x4.TRS(t.position, t.rotation, Vector3.one)))
            {
                Handles.DrawWireDisc(Vector3.zero, Vector3.forward, light.Radius);

                var dotSize = HandleUtility.GetHandleSize(t.position) * 0.08f;
                var newRadius = light.Radius;

                EditorGUI.BeginChangeCheck();
                foreach (var dir in CircleHandleDirections)
                {
                    var moved = Handles.Slider(dir * light.Radius, dir, dotSize, Handles.DotHandleCap, 0f);
                    var candidate = Vector3.Dot(moved, dir);
                    // Only the dragged dot deviates from the current radius; pick the largest deviation.
                    if (Mathf.Abs(candidate - light.Radius) > Mathf.Abs(newRadius - light.Radius))
                        newRadius = candidate;
                }

                if (!EditorGUI.EndChangeCheck())
                    return;

                Undo.RecordObject(light, "Resize Light Radius");
                light.Radius = Mathf.Max(1e-4f, newRadius);
                EditorUtility.SetDirty(light);
            }
        }

        // Spot cone handle: the cone opens from the transform origin along transform.up (its local +Y aim,
        // matching the apex-at-bottom generated cone texture). Two edge rays and an arc show the span; a dot
        // at one edge tip widens/narrows SpotAngle.
        private static void DrawSpotHandle(LightSprite light)
        {
            var t = light.transform;
            var apex = t.position;
            var aim = t.up;
            var normal = t.forward;
            var length = Mathf.Max(light.Radius, 1e-3f);
            var half = light.SpotAngle * 0.5f;

            using (new Handles.DrawingScope(HandleColor))
            {
                var leftDir = Quaternion.AngleAxis(half, normal) * aim;
                var rightDir = Quaternion.AngleAxis(-half, normal) * aim;
                Handles.DrawLine(apex, apex + leftDir * length);
                Handles.DrawLine(apex, apex + rightDir * length);
                Handles.DrawWireArc(apex, normal, rightDir, light.SpotAngle, length);

                var handlePos = apex + leftDir * length;
                var dotSize = HandleUtility.GetHandleSize(handlePos) * 0.08f;

                EditorGUI.BeginChangeCheck();
                var moved = Handles.FreeMoveHandle(handlePos, dotSize, Vector3.zero, Handles.DotHandleCap);
                if (!EditorGUI.EndChangeCheck())
                    return;

                var newDir = Vector3.ProjectOnPlane(moved - apex, normal);
                if (newDir.sqrMagnitude <= 1e-6f)
                    return;

                var angle = Vector3.Angle(aim, newDir.normalized);
                Undo.RecordObject(light, "Adjust Spot Angle");
                light.SpotAngle = Mathf.Clamp(angle * 2f, 1f, 179f);
                EditorUtility.SetDirty(light);
            }
        }
    }
}
