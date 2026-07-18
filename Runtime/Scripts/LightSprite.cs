using System;
using System.Collections.Generic;
using UnityEngine;

namespace Light2D
{
    /// <summary>
    /// Used to draw lights. Puts LightOrigin world position to UV1.
    /// Supports Point and Line light types.
    /// </summary>
    [ExecuteInEditMode]
    public class LightSprite : CustomSprite
    {
        private static readonly HashSet<LightSprite> LightSprites = new HashSet<LightSprite>();
        public Vector3 LightOrigin = new Vector3(0, 0, 1);
        public LightShape Shape = LightShape.Point;
        private Matrix4x4 _modelMatrix;
        private Vector3 _oldLightOrigin;
        private LightShape _oldLightShape;
        private bool _xzPlane;
        private bool _oldXzPlane;

        public static LightSprite[] AllLightSprites
        {
            get { return CreateSnapshot(); }
        }

        public MeshRenderer Renderer
        {
            get { return _meshRenderer; }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            LightSprites.Add(this);
        }

        private void OnDisable()
        {
            LightSprites.Remove(this);
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
            foreach (var lightSprite in FindObjectsByType<LightSprite>(FindObjectsInactive.Exclude))
                LightSprites.Add(lightSprite);
        }

        public static void PrepareSnapshot(List<LightSprite> snapshot, bool xzPlane)
        {
            if (snapshot == null)
                throw new ArgumentNullException("snapshot");

            LightSprites.RemoveWhere(lightSprite => lightSprite == null);
            snapshot.Clear();
            foreach (var lightSprite in LightSprites)
                snapshot.Add(lightSprite);

            foreach (var lightSprite in snapshot)
                lightSprite.PrepareForLighting(xzPlane);
        }

        internal static void PrepareSnapshot(List<LightSprite> snapshot, bool xzPlane, int layer, Camera camera)
        {
            PrepareSnapshot(snapshot, xzPlane);
            if (camera == null)
                return;

            var planes = GeometryUtility.CalculateFrustumPlanes(camera);
            snapshot.RemoveAll(lightSprite => lightSprite.gameObject.layer != layer ||
                                               !lightSprite.isActiveAndEnabled ||
                                               lightSprite.Renderer == null || !lightSprite.Renderer.enabled ||
                                               !GeometryUtility.TestPlanesAABB(planes, lightSprite.Renderer.bounds));
        }

        public static LightSprite[] PrepareSnapshot(bool xzPlane)
        {
            var snapshot = CreateSnapshot();
            foreach (var lightSprite in snapshot)
                lightSprite.PrepareForLighting(xzPlane);
            return snapshot;
        }

        private static LightSprite[] CreateSnapshot()
        {
            LightSprites.RemoveWhere(lightSprite => lightSprite == null);
            var snapshot = new LightSprite[LightSprites.Count];
            LightSprites.CopyTo(snapshot);
            return snapshot;
        }

        public void PrepareForLighting(bool xzPlane)
        {
            _xzPlane = xzPlane;
            UpdateMeshData();
        }

        /// <summary>
        /// Update UV1 which is used for raytracking in shader. UV1 is set to world position of LightOrigin.
        /// </summary>
        private void UpdatePosition(bool xzPlane)
        {
            if (Sprite == null || !Application.isPlaying)
                return;

            var mat = _modelMatrix;
            Vector2 size = Sprite.bounds.size;

            if (Shape == LightShape.Point)
            {
                // LightOrigin needs to be send in world position instead of local because 
                // Unity non uniform scaling is breaking model matrix in shader.
                var pos = mat.MultiplyPoint(((Vector2)LightOrigin).Mul(size));
                if (!xzPlane)
                {
                    for (int i = 0; i < _uv1.Length; i++)
                        _uv1[i] = pos;
                }
                else
                {
                    var p = new Vector2(pos.x, pos.z);
                    for (int i = 0; i < _uv1.Length; i++)
                        _uv1[i] = p;
                }
            }
            else if (Shape == LightShape.Line)
            {
                var lpos = mat.MultiplyPoint(new Vector2(-0.5f, LightOrigin.y).Mul(size));
                var rpos = mat.MultiplyPoint(new Vector2(0.5f, LightOrigin.y).Mul(size));
                if (!xzPlane)
                {
                    _uv1[0] = lpos;
                    _uv1[1] = rpos;
                    _uv1[2] = lpos;
                    _uv1[3] = rpos;
                }
                else
                {
                    var lp = new Vector2(lpos.x, lpos.z);
                    var rp = new Vector2(rpos.x, rpos.z);
                    _uv1[0] = lp;
                    _uv1[1] = rp;
                    _uv1[2] = lp;
                    _uv1[3] = rp;
                }
            }
        }

        protected override void UpdateMeshData(bool forceUpdate = false)
        {
            if (IsPartOfStaticBatch)
                return;

            var objMat = transform.localToWorldMatrix;
            if (!objMat.FastEquals(_modelMatrix) ||
                _oldLightOrigin != LightOrigin || _oldLightShape != Shape || _oldXzPlane != _xzPlane || forceUpdate)
            {
                _modelMatrix = objMat;
                _oldLightOrigin = LightOrigin;
                _oldLightShape = Shape;
                _oldXzPlane = _xzPlane;
                UpdatePosition(_xzPlane);
                _isMeshDirty = true;
            }

            base.UpdateMeshData(forceUpdate);
        }

        public enum LightShape
        {
            Point,
            Line,
        }

        private void OnDrawGizmosSelected()
        {
            if (Sprite == null)
                return;

            var size = Sprite.bounds.size;
            if (Shape == LightShape.Point)
            {
                var center = transform.TransformPoint(LightOrigin);
                Gizmos.DrawLine(
                    center + transform.TransformDirection(new Vector2(-0.1f, 0)),
                    center + transform.TransformDirection(new Vector2(0.1f, 0)));
                Gizmos.DrawLine(
                    center + transform.TransformDirection(new Vector2(0, -0.1f)),
                    center + transform.TransformDirection(new Vector2(0, 0.1f)));
            }
            else if (Shape == LightShape.Line && Sprite != null)
            {
                var lpos = transform.TransformPoint(new Vector3(-0.5f, LightOrigin.y).Mul(size));
                var rpos = transform.TransformPoint(new Vector3(0.5f, LightOrigin.y).Mul(size));
                Gizmos.DrawLine(lpos, rpos);
            }
        }

        public void DrawLightingNow(Vector2 lightCamLocalPos)
        {
            var material = _meshRenderer.sharedMaterial;

            if (material == null || !material.SetPass(0))
                return;

            var v1 = _modelMatrix.MultiplyPoint(_vertices[0]) - (Vector3)lightCamLocalPos;
            var v2 = _modelMatrix.MultiplyPoint(_vertices[2]) - (Vector3)lightCamLocalPos;
            var v3 = _modelMatrix.MultiplyPoint(_vertices[3]) - (Vector3)lightCamLocalPos;
            var v4 = _modelMatrix.MultiplyPoint(_vertices[1]) - (Vector3)lightCamLocalPos;

            GL.Begin(GL.TRIANGLES);

            GL.Color(Color);

            DrawLightingVertex(v1, 0, lightCamLocalPos);
            DrawLightingVertex(v2, 2, lightCamLocalPos);
            DrawLightingVertex(v3, 3, lightCamLocalPos);
            DrawLightingVertex(v1, 0, lightCamLocalPos);
            DrawLightingVertex(v3, 3, lightCamLocalPos);
            DrawLightingVertex(v4, 1, lightCamLocalPos);

            GL.End();
        }

        private void DrawLightingVertex(Vector3 vertex, int spriteVertex, Vector2 lightCamLocalPos)
        {
            GL.MultiTexCoord(0, _uv0[spriteVertex]);
            GL.MultiTexCoord(1, _uv1[spriteVertex] - lightCamLocalPos);
            GL.Vertex(vertex);
        }

        public void DrawLightNormalsNow(Material material)
        {
            if (material == null || Sprite == null)
                return;

            Vector2 size = Sprite.bounds.size;
            Vector2 center = _modelMatrix.MultiplyPoint3x4(((Vector2) LightOrigin).Mul(size));
            var lightPos = new Vector4(center.x, center.y, LightOrigin.z);

            material.SetVector("_LightPos", lightPos);

            if (!material.SetPass(0))
                return;

            var v1 = _modelMatrix.MultiplyPoint3x4(_vertices[0]);
            var v2 = _modelMatrix.MultiplyPoint3x4(_vertices[2]);
            var v3 = _modelMatrix.MultiplyPoint3x4(_vertices[3]);
            var v4 = _modelMatrix.MultiplyPoint3x4(_vertices[1]);
            
            GL.Begin(GL.TRIANGLES);
            GL.Vertex(v1);
            GL.Vertex(v2);
            GL.Vertex(v3);
            GL.Vertex(v1);
            GL.Vertex(v3);
            GL.Vertex(v4);
            GL.End();
        }
    }
}

