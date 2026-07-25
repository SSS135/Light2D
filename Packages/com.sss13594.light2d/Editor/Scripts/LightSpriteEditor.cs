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
    /// The <c>Shadows</c> popup drives the serialized <see cref="LightSprite.Shadows"/> value the light now
    /// auto-resolves its material from, so choosing a preset leaves the <see cref="CustomSprite.Material"/>
    /// slot empty (nothing to assign - it "just works"). When a Material IS assigned (a legacy scene, or a
    /// hand-set custom material) the popup reverse-maps that material's GUID back to a preset (via
    /// <see cref="Light2DLightPresets"/>) so it still reflects reality, and an explicit assignment always wins
    /// at draw time. <see cref="LightSprite.ShadowQuality.Custom"/> exposes the raw Material slot for a
    /// hand-assigned light material. All edits go through the serialized object (Undo automatic) except the
    /// Scene handles, which mutate the target directly with <c>Undo.RecordObject</c>.
    /// </summary>
    [CustomEditor(typeof(LightSprite))]
    [CanEditMultipleObjects]
    internal sealed class LightSpriteEditor : Editor
    {
        // Warm light-coloured handles so the Scene-view gizmos read as "this is the light".
        private static readonly Color HandleColor = new Color(1f, 0.85f, 0.4f, 0.9f);

        // Fainter warm tint for the secondary penumbra (hotspot) arc, Unity-spotlight style.
        private static readonly Color FaintHandleColor = new Color(1f, 0.85f, 0.4f, 0.35f);

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
        private const float RadiusSliderMax = 150f;
        private const float LengthSliderMax = 20f;
        private const float ThrowSliderMax = 20f;
        private const float SpotSoftnessSliderMax = 180f;

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
            "Shape of the generated falloff. 0..1 is edge hardness (0 soft, 1 hard); below 0 morphs toward a " +
            "longer, dimmer inverse-square-style tail. Ignored when a Sprite override is assigned.");
        private static readonly GUIContent SpotAngleLabel = new GUIContent("Spot Angle",
            "Cone angle in degrees for Spot. Up to 180 it is a cone; past 180 the origin migrates toward the centre and it opens into a full Point light at 360.");
        private static readonly GUIContent SpotSoftnessLabel = new GUIContent("Angle Softness",
            "Angular penumbra in degrees: how many degrees the cone edge takes to fade. 0 is a hard edge; 180 fades across the whole half-cone.");
        private static readonly GUIContent SpotSoftnessAutoLabel = new GUIContent("Auto Angle Softness",
            "Derive the cone-edge softness from Falloff, exactly as before. Turn off to set an explicit angular penumbra in degrees.");
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
        // Normal Map Height is the light's height above the plane - a placement value like Light Origin, so it
        // is drawn with the placement properties and stays visible for every light. Its effect is gated by the
        // normal-mapped pass, which the tooltip states; hiding the field only made the setting undiscoverable.
        private static readonly GUIContent NormalMapHeightLabel = new GUIContent("Normal Map Height",
            "Height of the light above the 2D plane, used as the light height by normal mapping. Takes effect " +
            "only when the owning LightingSystem has Normal Mapping enabled; ignored otherwise.");
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
        private SerializedProperty _spotSoftness;
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
            _spotSoftness = serializedObject.FindProperty("SpotSoftness");
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
            {
                EditorGUILayout.PropertyField(_spotAngle, SpotAngleLabel);
                DrawSpotSoftnessField();
            }

            DrawShadowsField();
            DrawSpriteOverride();

            EditorGUILayout.Space();
            // Light Origin is a correction offset for Point (centre) and Spot (apex); Line ignores it, so hide it.
            if (!isLine)
                EditorGUILayout.PropertyField(_lightOrigin, isSpot ? LightOriginApexLabel : LightOriginCenterLabel);
            // The height above the plane belongs next to the Light Origin XY correction: together they place the
            // light in 3D for normal mapping. Drawn for every Type, with or without an Override Sprite.
            EditorGUILayout.PropertyField(_normalMapHeight, NormalMapHeightLabel);
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

        // Angular penumbra control for Spot. SpotSoftness < 0 is the Auto sentinel (edge softness derived from
        // Falloff - the byte-identical legacy behaviour). An Auto toggle exposes it; with Auto off, a 0-180
        // degree slider sets an explicit penumbra. Toggling Auto off seeds the slider with the current
        // auto-equivalent width so the handoff does not visibly jump.
        private void DrawSpotSoftnessField()
        {
            var isAuto = !_spotSoftness.hasMultipleDifferentValues && _spotSoftness.floatValue < 0f;

            EditorGUI.showMixedValue = _spotSoftness.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var wantAuto = EditorGUILayout.Toggle(SpotSoftnessAutoLabel, isAuto);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
            {
                _spotSoftness.floatValue = wantAuto ? -1f : SuggestExplicitSoftness();
                isAuto = wantAuto;
            }

            if (!isAuto)
            {
                EditorGUI.indentLevel++;
                OverflowSlider(_spotSoftness, SpotSoftnessLabel, 0f, SpotSoftnessSliderMax);
                EditorGUI.indentLevel--;
            }
        }

        // The auto-equivalent angular penumbra (degrees) at the current Falloff/Spot Angle, so switching from
        // Auto to explicit does not visibly jump: the legacy softBand = lerp(0.5, 0.05, Falloff) fraction of
        // the half-aperture, expressed in degrees.
        private float SuggestExplicitSoftness()
        {
            var softBand = Mathf.Lerp(0.5f, 0.05f, Mathf.Clamp01(_falloff.floatValue));
            return Mathf.Clamp(_spotAngle.floatValue * 0.5f * softBand, 0f, SpotSoftnessSliderMax);
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
                // Presets auto-resolve from Shadows with an empty Material slot ("just works" - no asset to
                // assign), so clear any prior assignment when a preset is chosen. Custom keeps the slot so a
                // per-light custom material survives and can be assigned below.
                if (selected != LightSprite.ShadowQuality.Custom)
                    _material.objectReferenceValue = null;
                display = selected;
            }

            // Custom exposes the raw Material slot for a hand-assigned light material.
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
            if (_sprite.objectReferenceValue == null && !_sprite.hasMultipleDifferentValues)
            {
                EditorGUILayout.HelpBox(
                    "Empty: the falloff is generated from Type, Falloff, and Spot Angle. Assign a sprite to override generation.",
                    MessageType.None);
            }
        }

        private void DrawWarnings()
        {
            var warned = false;

            // Trap 1: only Custom shadow quality with no material still falls back to Sprites/Default (wrong
            // blend). Every other quality with a null Material now auto-resolves its preset, so it is no
            // longer a trap - the warning fires only for the remaining Custom-with-no-material case.
            if (ComputeDisplayQuality() == LightSprite.ShadowQuality.Custom &&
                !_material.hasMultipleDifferentValues && _material.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "Shadows is set to Custom but no light material is assigned, so the light falls back to " +
                    "Sprites/Default and blends incorrectly. Pick a shadow-quality preset or assign a custom light material.",
                    MessageType.Warning);
                if (GUILayout.Button("Use Balanced Preset"))
                    UseBalancedPreset();
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

        // Popup value: a serialized Custom is sticky (the user chose the raw-slot escape, so keep the slot
        // visible even if the assigned material happens to reverse-map to a preset). Otherwise an assigned
        // Material reverse-maps to its preset (a custom/unrecognised material reads as Custom); with no
        // Material assigned the light auto-resolves from the serialized Shadows value, so honor it directly
        // instead of reading a null material as Custom.
        private LightSprite.ShadowQuality ComputeDisplayQuality()
        {
            var serialized = (LightSprite.ShadowQuality)_shadows.enumValueIndex;
            if (serialized == LightSprite.ShadowQuality.Custom)
                return LightSprite.ShadowQuality.Custom;
            var material = _material.objectReferenceValue as Material;
            if (material != null)
                return ResolveQualityFromMaterial(material);
            return serialized;
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

        // Quick fix for the Custom-with-no-material trap: switch to the Balanced preset and clear the Material
        // slot so the light auto-resolves the Balanced preset material (no asset to hand-assign).
        private void UseBalancedPreset()
        {
            _shadows.enumValueIndex = (int)LightSprite.ShadowQuality.Balanced;
            _material.objectReferenceValue = null;
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

        // Spot cone handle. The cone opens from the emission origin along transform.up (its local +Y aim,
        // matching the generated cone texture). For SpotAngle <= 180 the origin is the LightOrigin-corrected
        // apex; past 180 it migrates toward the transform centre and the visible reach shrinks from Radius to
        // Radius*0.5, so at 360 the disc matches the Point handle (DrawRadiusHandle's Radius*0.5). Two edge
        // rays and an arc show the aperture (a "pac-man" mouth past 180, a full disc at 360); a fainter inner
        // arc marks the fully-lit hotspot inside the angular penumbra; a dot at one edge tip widens/narrows
        // SpotAngle smoothly across the whole 1..360 range.
        private static void DrawSpotHandle(LightSprite light)
        {
            var t = light.transform;
            var blend = LightSprite.SpotOriginBlend(light.SpotAngle);
            var origin = t.TransformPoint(new Vector3(light.LightOrigin.x * light.Radius * (1f - blend),
                light.LightOrigin.y * light.Radius * (1f - blend), 0f));
            var aim = t.up;
            var normal = t.forward;
            var reach = Mathf.Max(light.Radius / (1f + blend), 1e-3f);
            var half = light.SpotAngle * 0.5f;

            using (new Handles.DrawingScope(HandleColor))
            {
                var leftDir = Quaternion.AngleAxis(half, normal) * aim;
                var rightDir = Quaternion.AngleAxis(-half, normal) * aim;
                Handles.DrawWireArc(origin, normal, rightDir, light.SpotAngle, reach);
                // Below 360 the edge rays show the cone / pac-man mouth; at 360 the arc is already a full disc.
                if (light.SpotAngle < 360f)
                {
                    Handles.DrawLine(origin, origin + leftDir * reach);
                    Handles.DrawLine(origin, origin + rightDir * reach);
                }

                DrawSpotPenumbraArc(light, origin, aim, normal, reach, half, blend);

                var handlePos = origin + leftDir * reach;
                var dotSize = HandleUtility.GetHandleSize(handlePos) * 0.08f;

                EditorGUI.BeginChangeCheck();
                var moved = Handles.FreeMoveHandle(handlePos, dotSize, Vector3.zero, Handles.DotHandleCap);
                if (!EditorGUI.EndChangeCheck())
                    return;

                var newDir = Vector3.ProjectOnPlane(moved - origin, normal);
                if (newDir.sqrMagnitude <= 1e-6f)
                    return;

                // Vector3.Angle is the unsigned half-angle in [0,180], so 2x reaches the full 360 range and the
                // drag passes smoothly through 180 (straight down == 360).
                var angle = Vector3.Angle(aim, newDir.normalized);
                Undo.RecordObject(light, "Adjust Spot Angle");
                light.SpotAngle = Mathf.Clamp(angle * 2f, 1f, 360f);
                EditorUtility.SetDirty(light);
            }
        }

        // A fainter inner arc at the hotspot boundary (SpotAngle - 2*penumbra), Unity-spotlight style, so the
        // angular penumbra is visible. The inner half-angle matches the generated falloff: Auto derives it from
        // Falloff (legacy softBand of the half-aperture); an explicit SpotSoftness is a degrees inset from each
        // edge. It fades out as the Spot morphs to a Point (blend -> 1), where the angular mask fully opens and
        // no penumbra remains.
        private static void DrawSpotPenumbraArc(LightSprite light, Vector3 origin, Vector3 aim, Vector3 normal,
            float reach, float half, float blend)
        {
            var fade = 1f - blend;
            if (fade <= 0.01f)
                return;

            float innerHalf;
            if (light.SpotSoftness < 0f)
            {
                var softBand = Mathf.Lerp(0.5f, 0.05f, Mathf.Clamp01(light.Falloff));
                innerHalf = half * (1f - softBand);
            }
            else
            {
                innerHalf = Mathf.Max(0f, half - light.SpotSoftness);
            }

            // Draw only when the penumbra is visible: the hotspot cone strictly inside the aperture, non-degenerate.
            if (innerHalf <= 0.5f || innerHalf >= half - 0.5f)
                return;

            var color = FaintHandleColor;
            color.a *= fade;
            var innerFrom = Quaternion.AngleAxis(-innerHalf, normal) * aim;
            using (new Handles.DrawingScope(color))
                Handles.DrawWireArc(origin, normal, innerFrom, innerHalf * 2f, reach);
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
