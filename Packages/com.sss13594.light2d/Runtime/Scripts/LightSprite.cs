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
        private static readonly Plane[] FrustumPlanes = new Plane[6];
        private static readonly Comparison<LightSprite> CompareByEntityId = CompareLights;
        private static readonly Predicate<LightSprite> IsMissing = light => light == null;

        [Tooltip("Light emission origin in sprite-local space; Z is the light's height above the plane " +
                 "used for normal mapping (default (0, 0, 1)).")]
        public Vector3 LightOrigin = new Vector3(0, 0, 1);

        [Tooltip("Point emits radially from LightOrigin; Line emits along the sprite's horizontal extent " +
                 "(default Point).")]
        public LightShape Shape = LightShape.Point;

        [Tooltip("LightingSystem that draws this light. Leave empty to draw for any system, or set it to " +
                 "scope the light to one system when several exist.")]
        public LightingSystem Owner;

        [Tooltip("Light type. Point emits radially, Spot projects a soft cone from the origin apex, and " +
                 "Line emits along the sprite's horizontal extent. Drives Shape, the generated falloff " +
                 "texture, and (for Spot) the origin (default Point).")]
        public LightType Type = LightType.Point;

        [Tooltip("Light size in world units, baked into the mesh independently of the Transform scale " +
                 "(default 3).")]
        public float Radius = 3f;

        [Tooltip("Brightness multiplied into the light color. Values above 1 need the owning " +
                 "LightingSystem's HDR enabled to avoid clamping (default 1).")]
        public float Intensity = 1f;

        [Tooltip("Edge hardness of the generated falloff texture, 0 soft to 1 hard (default 0.5). " +
                 "Ignored when a Sprite override is assigned.")]
        [Range(0f, 1f)]
        public float Falloff = 0.5f;

        [Tooltip("Cone angle in degrees, used when Type is Spot (default 45).")]
        [Range(1f, 179f)]
        public float SpotAngle = 45f;

        [Tooltip("Ray-march shadow quality; the editor maps it to the light material. Custom hands the " +
                 "Material slot back to you (default Balanced).")]
        public ShadowQuality Shadows = ShadowQuality.Balanced;

        private Matrix4x4 _modelMatrix;
        private Vector3 _oldLightOrigin;
        private LightShape _oldLightShape;
        private Sprite _positionSprite;
        private bool _xzPlane;
        private bool _oldXzPlane;
        private LightType _oldLightType;
        private float _oldRadius;
        private float _oldIntensity;
        private float _oldFalloff;
        private float _oldSpotAngle;

        public static LightSprite[] AllLightSprites { get { return CreateSnapshot(); } }
        public MeshRenderer Renderer { get { return _meshRenderer; } }

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
                    : Light2DLightTextureGenerator.GetSprite(Type, Falloff, SpotAngle);
            }
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
            // Register-and-draw model (§0.3): the owned renderer is permanently disabled so no scene
            // camera ever draws the light mesh; it is drawn only through the LightSprites snapshot.
            if (_meshRenderer != null)
                _meshRenderer.enabled = false;
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
            foreach (var lightSprite in FindObjectsByType<LightSprite>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                LightSprites.Add(lightSprite);
        }

        public static void PrepareSnapshot(List<LightSprite> snapshot, bool xzPlane)
        {
            if (snapshot == null)
                throw new ArgumentNullException("snapshot");

            LightSprites.RemoveWhere(IsMissing);
            snapshot.Clear();
            foreach (var light in LightSprites)
            {
                if (IsRenderable(light))
                    snapshot.Add(light);
            }
            snapshot.Sort(CompareByEntityId);
            for (var i = 0; i < snapshot.Count; i++)
                snapshot[i].PrepareForLighting(xzPlane);
        }

        internal static void PrepareSnapshot(List<LightSprite> snapshot, bool xzPlane, int layer, Camera camera)
        {
            if (snapshot == null)
                throw new ArgumentNullException("snapshot");

            LightSprites.RemoveWhere(IsMissing);
            snapshot.Clear();
            var owner = ResolveCameraOwner(camera);
            if (camera != null)
                GeometryUtility.CalculateFrustumPlanes(camera, FrustumPlanes);

            foreach (var light in LightSprites)
            {
                if (!IsRenderable(light) || light.gameObject.layer != layer ||
                    (light.Owner != null && light.Owner != owner) ||
                    (camera != null && !GeometryUtility.TestPlanesAABB(FrustumPlanes, light.Renderer.bounds)))
                    continue;
                snapshot.Add(light);
            }

            snapshot.Sort(CompareByEntityId);
            for (var i = 0; i < snapshot.Count; i++)
                snapshot[i].PrepareForLighting(xzPlane);
        }

        // Additive owner-filtered overload for the v3 register-and-draw backends: no layer filter, an
        // explicit Owner scope, and caller-cached frustum planes (null skips frustum culling). The
        // layer-based overload above stays for the current backends until they switch over.
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
                    (planes != null && !GeometryUtility.TestPlanesAABB(planes, light.Renderer.bounds)))
                    continue;
                snapshot.Add(light);
            }

            snapshot.Sort(CompareByEntityId);
            for (var i = 0; i < snapshot.Count; i++)
                snapshot[i].PrepareForLighting(xzPlane);
        }

        public static LightSprite[] PrepareSnapshot(bool xzPlane)
        {
            var snapshot = CreateSnapshot();
            for (var i = 0; i < snapshot.Length; i++)
                snapshot[i].PrepareForLighting(xzPlane);
            return snapshot;
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
            // The owned MeshRenderer is permanently disabled in the register-and-draw model, so
            // renderability is gated on the component being active/enabled, not the renderer's flag.
            // The renderer is still required as the mesh/material/bounds container for the snapshot.
            return light != null && light.isActiveAndEnabled && light.Renderer != null;
        }

        private static int CompareLights(LightSprite left, LightSprite right)
        {
            return left.GetEntityId().CompareTo(right.GetEntityId());
        }

        private static LightingSystem ResolveCameraOwner(Camera camera)
        {
            if (camera == null)
                return null;
            var instances = LightingSystem.Instances;
            for (var i = 0; i < instances.Count; i++)
            {
                if (instances[i] != null && instances[i].SourceCamera == camera)
                    return instances[i];
            }
            return null;
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

            // The mesh vertices are already scaled by MeshScale (radius), so the origin/segment must be
            // scaled by the same size*MeshScale to stay aligned with them.
            var size = sprite.bounds.size * MeshScale;
            if (Shape == LightShape.Point)
            {
                var position = _modelMatrix.MultiplyPoint(Vector2.Scale((Vector2)LightOrigin, size));
                var uv = xzPlane ? new Vector2(position.x, position.z) : (Vector2)position;
                for (var i = 0; i < _uv1.Length; i++)
                    _uv1[i] = uv;
                return;
            }

            var left = _modelMatrix.MultiplyPoint(Vector2.Scale(new Vector2(-0.5f, LightOrigin.y), size));
            var right = _modelMatrix.MultiplyPoint(Vector2.Scale(new Vector2(0.5f, LightOrigin.y), size));
            var minX = sprite.bounds.min.x * MeshScale;
            var width = Mathf.Max(size.x, Mathf.Epsilon);
            for (var i = 0; i < _uv1.Length; i++)
            {
                var position = Vector3.LerpUnclamped(left, right, (_vertices[i].x - minX) / width);
                _uv1[i] = xzPlane ? new Vector2(position.x, position.z) : (Vector2)position;
            }
        }

        protected override void UpdateMeshData(bool forceUpdate = false)
        {
            if (IsPartOfStaticBatch)
                return;

            // Type is the top-level chooser: switching it (re)configures Shape and the emission origin.
            var lightTypeChanged = _oldLightType != Type;
            if (lightTypeChanged)
            {
                ApplyLightTypeShape();
                _oldLightType = Type;
            }

            // Radius (mesh scale), Intensity (vertex color), and the generated-texture params all change
            // alone and must repaint. Radius/Intensity force a vertex/color rebuild; Falloff/SpotAngle/Type
            // also swap the generated ActiveSprite, which the base sprite-change path already catches.
            var lightDataDirty = lightTypeChanged || _oldRadius != Radius || _oldIntensity != Intensity ||
                _oldFalloff != Falloff || _oldSpotAngle != SpotAngle;

            var activeSprite = ActiveSprite;
            var matrix = transform.localToWorldMatrix;
            var positionDirty = !MatrixExactlyEquals(matrix, _modelMatrix) || _oldLightOrigin != LightOrigin ||
                _oldLightShape != Shape || _oldXzPlane != _xzPlane || _positionSprite != activeSprite ||
                lightDataDirty || forceUpdate;

            base.UpdateMeshData(forceUpdate || lightDataDirty);
            if (!positionDirty && _uv1.Length == _vertices.Length)
                return;

            _modelMatrix = matrix;
            _oldLightOrigin = LightOrigin;
            _oldLightShape = Shape;
            _oldXzPlane = _xzPlane;
            _positionSprite = activeSprite;
            _oldRadius = Radius;
            _oldIntensity = Intensity;
            _oldFalloff = Falloff;
            _oldSpotAngle = SpotAngle;
            UpdatePosition(_xzPlane);
            _isMeshDirty = true;
            base.UpdateMeshData();
        }

        // Applies the Shape and emission origin implied by the chosen Type. Runs only when Type changes so
        // it configures the light once without fighting later manual tweaks.
        private void ApplyLightTypeShape()
        {
            switch (Type)
            {
                case LightType.Line:
                    Shape = LightShape.Line;
                    break;
                case LightType.Spot:
                    Shape = LightShape.Point;
                    // Spot emits from the cone apex at the bottom-center of the generated cone texture.
                    LightOrigin = new Vector3(0f, -0.5f, LightOrigin.z);
                    break;
                default:
                    Shape = LightShape.Point;
                    LightOrigin = new Vector3(0f, 0f, LightOrigin.z);
                    break;
            }
        }

        public enum LightShape
        {
            Point,
            Line,
        }

        public enum LightType
        {
            Point,
            Spot,
            Line,
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

            var size = sprite.bounds.size * MeshScale;
            if (Shape == LightShape.Point)
            {
                var center = transform.TransformPoint(
                    new Vector3(LightOrigin.x * size.x, LightOrigin.y * size.y, LightOrigin.z));
                Gizmos.DrawLine(center + transform.TransformDirection(new Vector2(-0.1f, 0)),
                    center + transform.TransformDirection(new Vector2(0.1f, 0)));
                Gizmos.DrawLine(center + transform.TransformDirection(new Vector2(0, -0.1f)),
                    center + transform.TransformDirection(new Vector2(0, 0.1f)));
            }
            else
            {
                var left = transform.TransformPoint(Vector3.Scale(new Vector3(-0.5f, LightOrigin.y), size));
                var right = transform.TransformPoint(Vector3.Scale(new Vector3(0.5f, LightOrigin.y), size));
                Gizmos.DrawLine(left, right);
            }
        }

        public void DrawLightingNow(Vector2 lightCamLocalPos)
        {
            var material = _meshRenderer != null ? _meshRenderer.sharedMaterial : null;
            if (material == null || !material.SetPass(0))
                return;

            GL.Begin(GL.TRIANGLES);
            GL.Color(Color * Intensity);
            for (var i = 0; i < _triangles.Length; i++)
            {
                var index = _triangles[i];
                var vertex = _modelMatrix.MultiplyPoint(_vertices[index]) - (Vector3)lightCamLocalPos;
                GL.MultiTexCoord(0, _uv0[index]);
                GL.MultiTexCoord(1, _uv1[index] - lightCamLocalPos);
                GL.Vertex(vertex);
            }
            GL.End();
        }

        public void DrawLightNormalsNow(Material material)
        {
            var sprite = ActiveSprite;
            if (material == null || sprite == null)
                return;

            var size = sprite.bounds.size * MeshScale;
            var center = _modelMatrix.MultiplyPoint3x4(Vector2.Scale((Vector2)LightOrigin, size));
            material.SetVector("_LightPos", new Vector4(center.x, center.y, LightOrigin.z));
            if (!material.SetPass(0))
                return;

            GL.Begin(GL.TRIANGLES);
            for (var i = 0; i < _triangles.Length; i++)
                GL.Vertex(_modelMatrix.MultiplyPoint3x4(_vertices[_triangles[i]]));
            GL.End();
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
