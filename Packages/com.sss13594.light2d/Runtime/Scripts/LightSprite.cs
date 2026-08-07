using System;
using System.Collections.Generic;
using UnityEngine;

namespace Light2D
{
    /// <summary>
    /// Draws a Point, Spot, or Line light and stores its world-space origin in UV1. Radius sizes the
    /// mesh, Intensity scales the (HDR) color, and Falloff/SpotAngle drive a procedurally generated
    /// falloff texture when no Sprite override is assigned.
    /// </summary>
    [AddComponentMenu("Light2D/Light Sprite")]
    [HelpURL(Light2DDocs.HelpUrl)]
    public class LightSprite : CustomSprite
    {
        private static readonly HashSet<LightSprite> LightSprites = new HashSet<LightSprite>();
        private static readonly Comparison<LightSprite> CompareByEntityId = CompareLights;
        private static readonly Predicate<LightSprite> IsMissing = light => light == null;

        [Tooltip("Pure correction offset for the emission origin, in the sprite's local XY (default (0, 0)): " +
                 "for Point it nudges the light centre, for Spot it nudges the cone apex, and it is unused " +
                 "for Line. Switching Type never rewrites this value.")]
        public Vector2 LightOrigin = Vector2.zero;

        [Tooltip("Height of the light above the 2D plane, consumed only by the normal-mapped light pass " +
                 "(default 1). Requires normal mapping enabled on the owning LightingSystem; ignored " +
                 "otherwise.")]
        public float NormalMapHeight = 1f;

        [Tooltip("LightingSystem that draws this light. Leave empty to draw for any system, or set it to " +
                 "scope the light to one system when several exist.")]
        public LightingSystem Owner;

        [Tooltip("Light type. Point emits radially, Spot projects a soft cone whose apex sits at the " +
                 "transform, and Line emits along a bar. Drives the generated falloff texture and the mesh " +
                 "geometry (default Point).")]
        public LightType Type = LightType.Point;

        [Tooltip("Point/Spot light size in world units, baked into the mesh independently of the Transform " +
                 "scale (default 3). Unused for Line, which is sized by Length and Throw.")]
        public float Radius = 3f;

        [Tooltip("Line light extent along its local X axis, in world units (default 3). Used only when Type " +
                 "is Line.")]
        public float Length = 3f;

        [Tooltip("Line light perpendicular reach before it fades, in world units (default 1.5). Used only " +
                 "when Type is Line.")]
        public float Throw = 1.5f;

        [Tooltip("Which side(s) of a Line light emit: Both (two-sided), Front (+normal only), or Back " +
                 "(-normal only). Used only when Type is Line (default Both).")]
        public LightSides Sides = LightSides.Both;

        [Tooltip("Brightness multiplied into the light color. Values above 1 need the owning " +
                 "LightingSystem's HDR enabled to avoid clamping (default 1).")]
        public float Intensity = 1f;

        [Tooltip("Shape of the generated falloff texture (default 0.5). 0..1 is edge hardness as before " +
                 "(0 soft, 1 hard); below 0 morphs toward a progressively longer, dimmer inverse-square-style " +
                 "tail (softer than 0). Ignored when a Sprite override is assigned.")]
        [Range(-1f, 1f)]
        public float Falloff = 0.5f;

        [Tooltip("Cone angle in degrees for Spot (default 45). Up to 180 it is a cone whose apex sits at the " +
                 "origin; past 180 the emission origin migrates toward the centre and the shape opens up, " +
                 "becoming a full Point light at 360.")]
        [Range(1f, 360f)]
        public float SpotAngle = 45f;

        [Tooltip("Angular penumbra for Spot, in degrees: how many degrees the cone edge takes to fade, " +
                 "independent of the radial Falloff. Negative means Auto - derive the edge softness from " +
                 "Falloff exactly as before (default). 0 is a hard edge; 180 fades across the whole half-cone.")]
        public float SpotSoftness = -1f;

        [Tooltip("Ray-march shadow quality; the editor maps it to the light material. Custom hands the " +
                 "Material slot back to you (default Balanced).")]
        public ShadowQuality Shadows = ShadowQuality.Balanced;

        private Matrix4x4 _modelMatrix;
        private Vector2 _oldLightOrigin;
        private Sprite _positionSprite;
        private bool _xzPlane;
        private bool _oldXzPlane;
        private LightType _oldLightType;
        private float _oldRadius;
        private float _oldIntensity;
        private float _oldFalloff;
        private float _oldSpotAngle;
        private float _oldSpotSoftness;
        private float _oldLength;
        private float _oldThrow;
        private LightSides _oldSides;
        private ShadowQuality _oldShadows;

        public static LightSprite[] AllLightSprites { get { return CreateSnapshot(); } }

        // Radius sizes the light mesh directly (decoupled from the Transform scale).
        protected override float MeshScale { get { return Mathf.Max(Radius, 1e-4f); } }

        // Lights upload a Float32 vertex-color stream so Intensity/HDR overbright survives (see
        // CustomSprite.UpdateMesh; host validation required).
        protected override bool UseHdrVertexColors { get { return true; } }

        // No assigned Sprite → generate a falloff sprite from Type/Falloff/SpotAngle; otherwise the
        // assigned Sprite is the "custom" escape and is used verbatim.
        protected override Sprite ActiveSprite
        {
            get
            {
                return Sprite != null
                    ? Sprite
                    : Light2DLightTextureGenerator.GetSprite(Type, Falloff, SpotAngle, Sides, SpotSoftness);
            }
        }

        // Render-path view of the sprite this light actually draws (the generated falloff sprite, or the Sprite
        // override when one is assigned). Both backends' normal-mapped light passes need the drawn sprite's
        // bounds to place the emission origin, and Sprite alone is null for every generated-falloff light -
        // gating the normal path on it dropped those lights entirely. ActiveSprite stays protected so the
        // CustomSprite contract is unchanged.
        internal Sprite ActiveLightSprite { get { return ActiveSprite; } }

        // Lights auto-resolve their draw material from Shadows so a LightSprite "just works" with no Material
        // assigned (the register-and-draw core draws through DrawMaterial in both backends). An explicitly
        // assigned Material always wins - the custom-shader escape, or a serialized preset asset from a scene
        // authored before auto-resolution. Shadows == Custom has no preset, so a null Custom slot falls back
        // to the base Sprites/Default path (the one remaining wrong-blend case the inspector warns about);
        // every other quality resolves its packaged preset material (editor asset load / build-safe shader).
        protected override Material ResolveBaseMaterial()
        {
            if (Material != null)
                return Material;
            if (Shadows == ShadowQuality.Custom)
                return base.ResolveBaseMaterial();
            var preset = Light2DLightPresets.ResolvePresetMaterial(Shadows);
            return preset != null ? preset : base.ResolveBaseMaterial();
        }

        // Fold Intensity into rgb (the authored texture alpha stays intact). The Float32 vertex-color
        // stream preserves overbright rgb > 1 for the ARGBHalf light RT.
        protected override void UpdateColor()
        {
            for (var i = 0; i < _colors.Length; i++)
                _colors[i] = new Color(Color.r * Intensity, Color.g * Intensity, Color.b * Intensity, Color.a);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            LightSprites.Add(this);
        }

        protected override void OnDisable()
        {
            LightSprites.Remove(this);
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            LightSprites.Remove(this);
            base.OnDestroy();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()
        {
            LightSprites.Clear();
            // Explicit sort mode: the single-argument overload is 6000.4+, and the package floor is 6000.3.
            foreach (var lightSprite in FindObjectsByType<LightSprite>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                LightSprites.Add(lightSprite);
        }

        // Owner-filtered snapshot for the register-and-draw backends: no layer filter, an explicit Owner
        // scope, and caller-cached frustum planes (null skips frustum culling).
        internal static void PrepareSnapshot(List<LightSprite> snapshot, bool xzPlane, LightingSystem owner,
            Plane[] planes)
        {
            if (snapshot == null)
                throw new ArgumentNullException("snapshot");

            LightSprites.RemoveWhere(IsMissing);
            snapshot.Clear();
            foreach (var light in LightSprites)
            {
                if (!IsRenderable(light) ||
                    (light.Owner != null && light.Owner != owner) ||
                    (planes != null && !GeometryUtility.TestPlanesAABB(planes, light.WorldBounds)))
                    continue;
                snapshot.Add(light);
            }

            snapshot.Sort(CompareByEntityId);
            for (var i = 0; i < snapshot.Count; i++)
                snapshot[i].PrepareForLighting(xzPlane);
        }

        private static LightSprite[] CreateSnapshot()
        {
            LightSprites.RemoveWhere(IsMissing);
            var list = new List<LightSprite>(LightSprites.Count);
            foreach (var light in LightSprites)
            {
                if (IsRenderable(light))
                    list.Add(light);
            }
            list.Sort(CompareByEntityId);
            return list.ToArray();
        }

        private static bool IsRenderable(LightSprite light)
        {
            // Register-and-draw: there is no MeshRenderer. Renderability is gated on the component being
            // active/enabled and owning a built mesh + material (the draw-entry inputs).
            return light != null && light.isActiveAndEnabled && light.Mesh != null && light.DrawMaterial != null;
        }

        private static int CompareLights(LightSprite left, LightSprite right)
        {
            return left.GetEntityId().CompareTo(right.GetEntityId());
        }

        public void PrepareForLighting(bool xzPlane)
        {
            _xzPlane = xzPlane;
            UpdateMeshData();
        }

        private void UpdatePosition(bool xzPlane)
        {
            var sprite = ActiveSprite;
            if (sprite == null || _uv1.Length != _vertices.Length)
                return;

            if (Type == LightType.Line)
            {
                // Each column emits from the point on the line segment directly at its X. The segment lies
                // along local X at the transform origin and spans Length; Throw/Sides shape only the
                // perpendicular mesh, so the emitting line itself does not depend on LightOrigin.
                var half = Length * 0.5f;
                var left = _modelMatrix.MultiplyPoint(new Vector3(-half, 0f, 0f));
                var right = _modelMatrix.MultiplyPoint(new Vector3(half, 0f, 0f));
                var width = Mathf.Max(Length, Mathf.Epsilon);
                for (var i = 0; i < _uv1.Length; i++)
                {
                    var frac = Mathf.Clamp01((_vertices[i].x + half) / width);
                    var position = Vector3.LerpUnclamped(left, right, frac);
                    _uv1[i] = xzPlane ? new Vector2(position.x, position.z) : (Vector2)position;
                }
                return;
            }

            // Point and Spot emit from a single origin: the transform position nudged by the LightOrigin XY
            // correction, scaled into the sprite's local footprint. For Spot the mesh apex is baked to this
            // same point (see TransformVertex), so origin, apex, and the Scene gizmo coincide.
            var size = sprite.bounds.size * MeshScale;
            var origin = _modelMatrix.MultiplyPoint(Vector2.Scale(LightOrigin, size));
            var uv = xzPlane ? new Vector2(origin.x, origin.z) : (Vector2)origin;
            for (var i = 0; i < _uv1.Length; i++)
                _uv1[i] = uv;
        }

        // Spot->Point morph parameter shared by the falloff generator, the mesh lift, and the Scene gizmo so
        // they migrate in lockstep: 0 for SpotAngle <= 180 (a cone with its apex at the origin), rising
        // linearly to 1 at 360 (a Point-style radial light centred on the origin).
        internal static float SpotOriginBlend(float spotAngle)
        {
            return Mathf.Clamp01((spotAngle - 180f) / 180f);
        }

        // Reshapes the copied sprite quad per Type. Point keeps the centered quad; Spot lifts it so the
        // generated cone's apex row (the sprite's bottom edge) sits at the LightOrigin-corrected transform
        // origin; Line rebuilds it as a Length-by-Throw bar with the emitting segment placed by Sides.
        protected override Vector3 TransformVertex(Vector3 vertex, float scale, Sprite sprite)
        {
            switch (Type)
            {
                case LightType.Line:
                {
                    var yScale = Sides == LightSides.Both ? Throw * 2f : Throw;
                    var yOffset = Sides == LightSides.Front ? Throw * 0.5f
                        : Sides == LightSides.Back ? -Throw * 0.5f
                        : 0f;
                    return new Vector3(vertex.x * Length, vertex.y * yScale + yOffset, 0f);
                }
                case LightType.Spot:
                {
                    if (sprite == null)
                        return vertex * scale;
                    // The legacy lift baked the cone apex (the sprite's bottom edge, nudged by LightOrigin) to
                    // the transform origin. Origin migration fades that whole lift out as the Spot morphs to a
                    // Point: at blend == 0 (SpotAngle <= 180) the offsets are exactly the legacy values, so the
                    // apex stays at the origin; at blend == 1 (360) both offsets are zero, so the quad is
                    // centred exactly like Point (vertex * scale). Across the range the emission origin stays
                    // pinned to the LightOrigin-corrected transform (see the derivation in UpdatePosition).
                    var size = sprite.bounds.size * scale;
                    var legacyOffsetX = LightOrigin.x * size.x;
                    var legacyOffsetY = LightOrigin.y * size.y - sprite.bounds.min.y * scale;
                    var blend = 1f - SpotOriginBlend(SpotAngle);
                    return vertex * scale + new Vector3(legacyOffsetX * blend, legacyOffsetY * blend, 0f);
                }
                default:
                    return vertex * scale;
            }
        }

        protected override void UpdateMeshData(bool forceUpdate = false)
        {
            // Type is the single top-level chooser: it selects the generated falloff, the mesh geometry, and
            // how the emission origin is written into UV1. No field is mutated when Type switches -
            // LightOrigin is a pure correction offset the user owns.
            var lightTypeChanged = _oldLightType != Type;

            // Radius/Intensity/Falloff/SpotAngle, the Line dimensions (Length/Throw/Sides), and Type each
            // change alone and must rebuild the mesh or repaint. LightOrigin also rebuilds the mesh because
            // the Spot apex offset is baked into the vertices.
            // Shadows drives only the resolved draw material, not geometry, but it re-resolves through the
            // shared UpdateSprite path, so a change must force the base rebuild that re-runs material resolution.
            var lightDataDirty = lightTypeChanged || _oldRadius != Radius || _oldIntensity != Intensity ||
                _oldFalloff != Falloff || _oldSpotAngle != SpotAngle || _oldSpotSoftness != SpotSoftness ||
                _oldLength != Length || _oldThrow != Throw || _oldSides != Sides ||
                _oldLightOrigin != LightOrigin || _oldShadows != Shadows;

            var activeSprite = ActiveSprite;
            var matrix = transform.localToWorldMatrix;
            var positionDirty = !MatrixExactlyEquals(matrix, _modelMatrix) || _oldXzPlane != _xzPlane ||
                _positionSprite != activeSprite || lightDataDirty || forceUpdate;

            base.UpdateMeshData(forceUpdate || lightDataDirty);
            if (!positionDirty && _uv1.Length == _vertices.Length)
                return;

            _modelMatrix = matrix;
            _oldLightOrigin = LightOrigin;
            _oldXzPlane = _xzPlane;
            _positionSprite = activeSprite;
            _oldRadius = Radius;
            _oldIntensity = Intensity;
            _oldFalloff = Falloff;
            _oldSpotAngle = SpotAngle;
            _oldSpotSoftness = SpotSoftness;
            _oldLength = Length;
            _oldThrow = Throw;
            _oldSides = Sides;
            _oldShadows = Shadows;
            _oldLightType = Type;
            UpdatePosition(_xzPlane);
            _isMeshDirty = true;
            base.UpdateMeshData();
        }

        public enum LightType
        {
            Point,
            Spot,
            Line,
        }

        public enum LightSides
        {
            Both,
            Front,
            Back,
        }

        public enum ShadowQuality
        {
            Fast,
            Balanced,
            High,
            Auto,
            IgnoreObstacles,
            Custom,
        }

        private void OnDrawGizmosSelected()
        {
            var sprite = ActiveSprite;
            if (sprite == null)
                return;

            if (Type == LightType.Line)
            {
                // The emitting bar along local X, spanning Length at the transform origin.
                var half = Length * 0.5f;
                var left = transform.TransformPoint(new Vector3(-half, 0f, 0f));
                var right = transform.TransformPoint(new Vector3(half, 0f, 0f));
                Gizmos.DrawLine(left, right);
                return;
            }

            // Point and Spot: a small cross at the emission origin. For Point that is the light centre; for
            // Spot it is the cone apex - both are the transform position nudged by the LightOrigin XY.
            var size = sprite.bounds.size * MeshScale;
            var origin = transform.TransformPoint(
                new Vector3(LightOrigin.x * size.x, LightOrigin.y * size.y, 0f));
            Gizmos.DrawLine(origin + transform.TransformDirection(new Vector2(-0.1f, 0)),
                origin + transform.TransformDirection(new Vector2(0.1f, 0)));
            Gizmos.DrawLine(origin + transform.TransformDirection(new Vector2(0, -0.1f)),
                origin + transform.TransformDirection(new Vector2(0, 0.1f)));
        }

        // Exact component comparison; Matrix4x4.operator== is epsilon-based and would miss tiny transform changes.
        private static bool MatrixExactlyEquals(in Matrix4x4 m1, in Matrix4x4 m2)
        {
            return m1.m00 == m2.m00 && m1.m01 == m2.m01 && m1.m02 == m2.m02 && m1.m03 == m2.m03 &&
                   m1.m10 == m2.m10 && m1.m11 == m2.m11 && m1.m12 == m2.m12 && m1.m13 == m2.m13 &&
                   m1.m20 == m2.m20 && m1.m21 == m2.m21 && m1.m22 == m2.m22 && m1.m23 == m2.m23 &&
                   m1.m30 == m2.m30 && m1.m31 == m2.m31 && m1.m32 == m2.m32 && m1.m33 == m2.m33;
        }
    }
}
