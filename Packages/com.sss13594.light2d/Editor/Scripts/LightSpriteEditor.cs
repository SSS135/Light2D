using UnityEditor;
using UnityEngine;

namespace Light2D
{
    /// <summary>
    /// First-class inspector for <see cref="LightSprite"/>. It surfaces the light's real controls in a
    /// top-down order (Type, HDR Color, Intensity, then the Type-specific size controls - Radius for
    /// Point/Spot or Length/Throw/Sides for Line - Falloff, Spot Angle, Shadows, an optional Sprite
    /// override, then placement) instead of the raw serialized fields, adds one draggable Scene-view handle
    /// per Type (the Radius disc for Point, the cone for Spot, the bar for Line), and warns about the common
    /// setup traps (no light material, no LightingSystem, overbright without HDR) with one-click fixes.
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
        private const float LengthSliderMax = 20f;
        private const float ThrowSliderMax = 20f;

        private static readonly GUIContent TypeLabel = new GUIContent("Type",
            "Point emits radially, Spot projects a soft cone whose apex sits at the transform, and Line emits along a Length-by-Throw bar.");
        private static readonly GUIContent ColorLabel = new GUIContent("Color",
            "Light colour, multiplied by Intensity. HDR: channels above 1 need the owning LightingSystem's 64 Bit Color enabled.");
        private static readonly GUIContent IntensityLabel = new GUIContent("Intensity",
            "Brightness multiplied into the light colour. Type a value above the slider to overdrive it.");
        private static readonly GUIContent RadiusLabel = new GUIContent("Radius",
            "Point/Spot light size in world units, baked into the mesh independently of the Transform scale.");
        private static readonly GUIContent LengthLabel = new GUIContent("Length",
            "Line light extent along its local X axis, in world units.");
        private static readonly GUIContent ThrowLabel = new GUIContent("Throw",
            "Line light perpendicular reach before it fades, in world units.");
        private static readonly GUIContent SidesLabel = new GUIContent("Sides",
            "Which side(s) of a Line light emit: Both (two-sided), Front (+normal only), or Back (-normal only).");
        private static readonly GUIContent FalloffLabel = new GUIContent("Falloff",
            "Edge hardness of the generated falloff, 0 soft to 1 hard. Ignored when a Sprite override is assigned.");
        private static readonly GUIContent SpotAngleLabel = new GUIContent("Spot Angle",
            "Cone angle in degrees, used when Type is Spot.");
        private static readonly GUIContent ShadowsLabel = new GUIContent("Shadows",
            "Ray-march shadow quality. Maps to the light material; Custom hands the Material slot back to you.");
        private static readonly GUIContent MaterialLabel = new GUIContent("Custom Material",
            "Light material used when Shadows is Custom. Leave empty to fall back to Sprites/Default (wrong blend).");
        private static readonly GUIContent SpriteOverrideLabel = new GUIContent("Override Sprite",
            "Optional sprite that replaces the generated falloff. Leave empty to generate from Type/Falloff/Spot Angle.");
        // Light Origin is a pure XY correction offset; the label/tooltip changes with Type (it is hidden for Line).
        private static readonly GUIContent LightOriginCenterLabel = new GUIContent("Light Origin",
            "Correction offset for the light centre, in the sprite's local XY (leave at 0,0 for no correction). " +
            "Switching Type never rewrites this.");
        private static readonly GUIContent LightOriginApexLabel = new GUIContent("Light Origin",
            "Correction offset for the cone apex, in the sprite's local XY (leave at 0,0 to originate exactly at " +
            "the transform). Switching Type never rewrites this.");
        // Normal Map Height only feeds the normal-mapped light pass, so it is shown only when an Override Sprite is set.
        private static readonly GUIContent NormalMapHeightLabel = new GUIContent("Normal Map Height",
            "Height of the light above the 2D plane, used only by the normal-mapped light pass (needs normal " +
            "mapping enabled on the owning LightingSystem). Ignored otherwise.");
        private static readonly GUIContent SortingOrderLabel = new GUIContent("Sorting Order",
            "Draw order among lights; lower values draw first.");
        private static readonly GUIContent OwnerLabel = new GUIContent("Owner",
            "LightingSystem that draws this light. Leave empty to draw for any system.");

        private SerializedProperty _type;
        private SerializedProperty _color;
        private SerializedProperty _intensity;
        private SerializedProperty _radius;
        private SerializedProperty _length;
        private SerializedProperty _throw;
        private SerializedProperty _sides;
        private SerializedProperty _falloff;
        private SerializedProperty _spotAngle;
        private SerializedProperty _shadows;
        private SerializedProperty _material;
        private SerializedProperty _sprite;
        private SerializedProperty _lightOrigin;
        private SerializedProperty _normalMapHeight;
        private SerializedProperty _sortingOrder;
        private SerializedProperty _owner;

        private void OnEnable()
        {
            _type = serializedObject.FindProperty("Type");
            _color = serializedObject.FindProperty("Color");
            _intensity = serializedObject.FindProperty("Intensity");
            _radius = serializedObject.FindProperty("Radius");
            _length = serializedObject.FindProperty("Length");
            _throw = serializedObject.FindProperty("Throw");
            _sides = serializedObject.FindProperty("Sides");
            _falloff = serializedObject.FindProperty("Falloff");
            _spotAngle = serializedObject.FindProperty("SpotAngle");
            _shadows = serializedObject.FindProperty("Shadows");
            _material = serializedObject.FindProperty("Material");
            _sprite = serializedObject.FindProperty("Sprite");
            _lightOrigin = serializedObject.FindProperty("LightOrigin");
            _normalMapHeight = serializedObject.FindProperty("NormalMapHeight");
            _sortingOrder = serializedObject.FindProperty("SortingOrder");
            _owner = serializedObject.FindProperty("Owner");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawWarnings();

            EditorGUILayout.PropertyField(_type, TypeLabel);
            DrawHdrColor();
            OverflowSlider(_intensity, IntensityLabel, 0f, IntensitySliderMax);

            var type = (LightSprite.LightType)_type.enumValueIndex;
            var isLine = !_type.hasMultipleDifferentValues && type == LightSprite.LightType.Line;
            var isSpot = !_type.hasMultipleDifferentValues && type == LightSprite.LightType.Spot;

            // Line is sized by Length/Throw/Sides; Point and Spot are sized by Radius.
            if (isLine)
            {
                OverflowSlider(_length, LengthLabel, 0f, LengthSliderMax);
                OverflowSlider(_throw, ThrowLabel, 0f, ThrowSliderMax);
                EditorGUILayout.PropertyField(_sides, SidesLabel);
            }
            else
            {
                OverflowSlider(_radius, RadiusLabel, 0f, RadiusSliderMax);
            }

            EditorGUILayout.PropertyField(_falloff, FalloffLabel);
            if (isSpot)
                EditorGUILayout.PropertyField(_spotAngle, SpotAngleLabel);

            DrawShadowsField();
            DrawSpriteOverride();

            EditorGUILayout.Space();
            // Light Origin is a correction offset for Point (centre) and Spot (apex); Line ignores it, so hide it.
            if (!isLine)
                EditorGUILayout.PropertyField(_lightOrigin, isSpot ? LightOriginApexLabel : LightOriginCenterLabel);
            EditorGUILayout.PropertyField(_sortingOrder, SortingOrderLabel);
            EditorGUILayout.PropertyField(_owner, OwnerLabel);

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
            EditorGUILayout.PropertyField(_sprite, SpriteOverrideLabel);
            if (_sprite.objectReferenceValue != null)
            {
                // The normal-mapped light pass runs only for lights with an override sprite, so Normal Map
                // Height is meaningful only here (and further, only when the LightingSystem enables normal mapping).
                EditorGUILayout.PropertyField(_normalMapHeight, NormalMapHeightLabel);
            }
            else if (!_sprite.hasMultipleDifferentValues)
            {
                EditorGUILayout.HelpBox(
                    "Empty: the falloff is generated from Type, Falloff, and Spot Angle. Assign a sprite to override generation.",
                    MessageType.None);
            }
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

            // One handle per Type: the radius disc for Point, the cone for Spot, the bar for Line. Drawing
            // the disc for Spot/Line was the gizmo/render disagreement this redesign removes.
            switch (light.Type)
            {
                case LightSprite.LightType.Spot:
                    DrawSpotHandle(light);
                    break;
                case LightSprite.LightType.Line:
                    DrawLineHandle(light);
                    break;
                default:
                    DrawRadiusHandle(light);
                    break;
            }
        }

        private static void DrawRadiusHandle(LightSprite light)
        {
            var t = light.transform;
            using (new Handles.DrawingScope(HandleColor, Matrix4x4.TRS(t.position, t.rotation, Vector3.one)))
            {
                // Radius sizes the light quad edge-to-edge (half-extent = Radius * 0.5), and the radial
                // falloff fades to zero at that half-extent, so the visible light reaches Radius * 0.5.
                var visibleRadius = light.Radius * 0.5f;
                Handles.DrawWireDisc(Vector3.zero, Vector3.forward, visibleRadius);

                var dotSize = HandleUtility.GetHandleSize(t.position) * 0.08f;
                var newVisibleRadius = visibleRadius;

                EditorGUI.BeginChangeCheck();
                foreach (var dir in CircleHandleDirections)
                {
                    var moved = Handles.Slider(dir * visibleRadius, dir, dotSize, Handles.DotHandleCap, 0f);
                    var candidate = Vector3.Dot(moved, dir);
                    // Only the dragged dot deviates from the current radius; pick the largest deviation.
                    if (Mathf.Abs(candidate - visibleRadius) > Mathf.Abs(newVisibleRadius - visibleRadius))
                        newVisibleRadius = candidate;
                }

                if (!EditorGUI.EndChangeCheck())
                    return;

                Undo.RecordObject(light, "Resize Light Radius");
                light.Radius = Mathf.Max(1e-4f, newVisibleRadius * 2f);
                EditorUtility.SetDirty(light);
            }
        }

        // Spot cone handle: the cone opens from the apex along transform.up (its local +Y aim, matching the
        // apex-at-bottom generated cone texture). The apex is the transform origin nudged by the LightOrigin
        // XY correction - the exact world point the rendered cone now originates from. Two edge rays and an
        // arc show the span; a dot at one edge tip widens/narrows SpotAngle.
        private static void DrawSpotHandle(LightSprite light)
        {
            var t = light.transform;
            var apex = t.TransformPoint(new Vector3(light.LightOrigin.x * light.Radius,
                light.LightOrigin.y * light.Radius, 0f));
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

        // Line handle: the emitting bar along local X (Length), a rectangle showing the perpendicular reach
        // (Throw) on each emitting side per Sides, plus dots to drag Length (bar ends) and Throw (reach tip).
        // Everything is drawn in the light's unscaled local frame so it matches the rendered bar geometry.
        private static void DrawLineHandle(LightSprite light)
        {
            var t = light.transform;
            using (new Handles.DrawingScope(HandleColor, Matrix4x4.TRS(t.position, t.rotation, Vector3.one)))
            {
                var half = light.Length * 0.5f;
                var reach = Mathf.Max(light.Throw, 0f);
                var left = new Vector3(-half, 0f, 0f);
                var right = new Vector3(half, 0f, 0f);
                Handles.DrawLine(left, right);

                // Both emits +Y and -Y; Front only +Y (the +normal side); Back only -Y.
                var emitsFront = light.Sides != LightSprite.LightSides.Back;
                var emitsBack = light.Sides != LightSprite.LightSides.Front;
                if (emitsFront)
                    DrawThrowEdge(left, right, Vector3.up * reach);
                if (emitsBack)
                    DrawThrowEdge(left, right, Vector3.down * reach);

                var dotSize = HandleUtility.GetHandleSize(t.position) * 0.08f;

                // Length dots at the two bar ends; the dragged end sets the new half-length.
                EditorGUI.BeginChangeCheck();
                var newHalf = half;
                var movedRight = Handles.Slider(right, Vector3.right, dotSize, Handles.DotHandleCap, 0f);
                var rightCandidate = Vector3.Dot(movedRight, Vector3.right);
                if (Mathf.Abs(rightCandidate - half) > Mathf.Abs(newHalf - half))
                    newHalf = rightCandidate;
                var movedLeft = Handles.Slider(left, Vector3.left, dotSize, Handles.DotHandleCap, 0f);
                var leftCandidate = Vector3.Dot(movedLeft, Vector3.left);
                if (Mathf.Abs(leftCandidate - half) > Mathf.Abs(newHalf - half))
                    newHalf = leftCandidate;
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(light, "Resize Line Length");
                    light.Length = Mathf.Max(1e-4f, newHalf * 2f);
                    EditorUtility.SetDirty(light);
                }

                // Throw dot on an emitting side (prefer +Y; use -Y when only Back emits).
                var throwDir = emitsFront ? Vector3.up : Vector3.down;
                EditorGUI.BeginChangeCheck();
                var movedThrow = Handles.Slider(throwDir * reach, throwDir, dotSize, Handles.DotHandleCap, 0f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(light, "Adjust Line Throw");
                    light.Throw = Mathf.Max(1e-4f, Vector3.Dot(movedThrow, throwDir));
                    EditorUtility.SetDirty(light);
                }
            }
        }

        // Two perpendicular reach lines from the bar ends plus the far edge that closes them into a rectangle.
        private static void DrawThrowEdge(Vector3 left, Vector3 right, Vector3 offset)
        {
            Handles.DrawLine(left, left + offset);
            Handles.DrawLine(right, right + offset);
            Handles.DrawLine(left + offset, right + offset);
        }
    }
}
