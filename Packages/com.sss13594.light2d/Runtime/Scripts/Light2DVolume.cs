using UnityEngine;

namespace Light2D
{
    /// <summary>
    /// A world-space lighting volume that scopes how much Light2D applies inside a region (Coverage).
    /// It owns a procedural shape mesh whose vertex-color RGB carries the coverage value and whose vertex
    /// alpha carries the blend-distance falloff, and registers with the render core through the coverage
    /// role. Like the other lighting-input components it owns no scene renderer: the shape is drawn only
    /// into the coverage mask render target through the registry snapshot, never into the game image.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Light2D/Light Volume")]
    [HelpURL(Light2DDocs.HelpUrl)]
    public class Light2DVolume : MonoBehaviour, ILight2DCoverage
    {
        // Nested so it stays invisible to the ContractTests non-nested exported-type check.
        public enum VolumeShape { Box, Circle, Sprite, Global }

        [Tooltip("Region shape. Box/Circle are procedural primitives; Sprite uses a sprite outline (its texture " +
                 "alpha shapes the region, blend distance approximated); Global is an unbounded fallback that " +
                 "fills the whole view at its priority.")]
        public VolumeShape Shape = VolumeShape.Box;

        [Tooltip("Box full size in world units (X,Y). Used when Shape is Box.")]
        public Vector2 Size = Vector2.one;

        [Tooltip("Circle radius in world units. Used when Shape is Circle.")]
        public float Radius = 0.5f;

        [Tooltip("Sprite whose outline defines the region. Used when Shape is Sprite.")]
        public Sprite Sprite;

        [Tooltip("Higher priority wins where volumes overlap; within Blend Distance the transition is smooth.")]
        public int Priority;

        [Tooltip("Soft-edge width in world units. The region fades from full effect to none across this band.")]
        public float BlendDistance = 0.5f;

        [Range(0f, 1f)]
        [Tooltip("Coverage: how much Light2D applies inside this volume. 1 = lighting applies (dark + torches); " +
                 "0 = bypassed (the raw game image shows through, full brightness). Outside all volumes the world " +
                 "default is 'lighting applies' unless a Global coverage-off volume flips it.")]
        public float Coverage = 1f;

        [Tooltip("LightingSystem that owns this volume. Leave empty to affect any system, or set it to scope " +
                 "the volume to one system when several exist.")]
        public LightingSystem Owner;

        private const string GeneratedMeshName = "Generated Coverage Shape Mesh (DONT change it)";
        private const int CircleSegments = 48;
        private const float GlobalExtent = 1e6f;

        // Box/Circle/Global share this one stateless mask material (the coverage value lives in vertex
        // color); Sprite shapes clone a per-texture variant from it. Mirrors LightAmbientEmitter's static
        // runtime default material.
        private static Material _sharedCoverageMaterial;

        private Mesh _shapeMesh;
        private Material _coverageMaterial;
        private CustomSprite.MaterialKey _materialKey;
        private Texture2D _oldSpriteTexture;
        private Bounds _localBounds;
        private bool _meshDirty = true;

        private void OnEnable()
        {
            Light2DRenderRegistry.RegisterCoverage(this);
            _meshDirty = true;
        }

        private void OnDisable()
        {
            Light2DRenderRegistry.DeregisterCoverage(this);
            ReleaseOwnedResources();
        }

        private void OnDestroy()
        {
            Light2DRenderRegistry.DeregisterCoverage(this);
            ReleaseOwnedResources();
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled)
                _meshDirty = true;
        }

        bool ILight2DDrawable.TryGetDrawEntry(out Light2DDrawEntry entry)
        {
            entry = default;
            if (!isActiveAndEnabled)
                return false;
            if (Light2DObjectUtility.IsInPrefabStage(gameObject))
                return false;

            EnsureBuilt();
            if (_shapeMesh == null || _coverageMaterial == null || _shapeMesh.vertexCount == 0)
                return false;

            // The matrix is read live each frame so the volume follows its transform without caching.
            var matrix = transform.localToWorldMatrix;
            var worldBounds = Shape == VolumeShape.Global
                ? new Bounds(Vector3.zero, new Vector3(GlobalExtent * 2f, GlobalExtent * 2f, GlobalExtent * 2f))
                : TransformBounds(_localBounds, matrix);

            entry = new Light2DDrawEntry(_shapeMesh, _coverageMaterial, matrix, worldBounds, 1, Priority, Owner);
            return true;
        }

        private void EnsureBuilt()
        {
            if (_meshDirty)
            {
                BuildMesh();
                _meshDirty = false;
            }
            ResolveMaterial();
        }

        private void BuildMesh()
        {
            EnsureMesh();
            switch (Shape)
            {
                case VolumeShape.Box:
                    BuildBoxMesh();
                    break;
                case VolumeShape.Circle:
                    BuildCircleMesh();
                    break;
                case VolumeShape.Sprite:
                    BuildSpriteMesh();
                    break;
                case VolumeShape.Global:
                    BuildGlobalMesh();
                    break;
            }
        }

        private void EnsureMesh()
        {
            if (_shapeMesh != null)
                return;
            _shapeMesh = new Mesh { name = GeneratedMeshName, hideFlags = HideFlags.DontSave };
            _shapeMesh.MarkDynamic();
        }

        // Feathered quad: an inner rectangle at alpha 1 expanded by BlendDistance to an outer rectangle at
        // alpha 0, joined by a border ring. Corners fade diagonally (a minor cosmetic simplification).
        private void BuildBoxMesh()
        {
            var hx = Mathf.Max(0f, Size.x * 0.5f);
            var hy = Mathf.Max(0f, Size.y * 0.5f);
            var b = Mathf.Max(0f, BlendDistance);
            var inner = InteriorColor();
            var outer = EdgeColor();

            var vertices = new Vector3[8];
            vertices[0] = new Vector3(-hx, -hy, 0f);
            vertices[1] = new Vector3(-hx, hy, 0f);
            vertices[2] = new Vector3(hx, hy, 0f);
            vertices[3] = new Vector3(hx, -hy, 0f);
            vertices[4] = new Vector3(-hx - b, -hy - b, 0f);
            vertices[5] = new Vector3(-hx - b, hy + b, 0f);
            vertices[6] = new Vector3(hx + b, hy + b, 0f);
            vertices[7] = new Vector3(hx + b, -hy - b, 0f);

            var colors = new Color[8];
            colors[0] = colors[1] = colors[2] = colors[3] = inner;
            colors[4] = colors[5] = colors[6] = colors[7] = outer;

            var triangles = new[]
            {
                0, 1, 2, 0, 2, 3, // inner quad
                0, 1, 5, 0, 5, 4, // left border
                1, 2, 6, 1, 6, 5, // top border
                2, 3, 7, 2, 7, 6, // right border
                3, 0, 4, 3, 4, 7  // bottom border
            };

            _shapeMesh.Clear();
            _shapeMesh.vertices = vertices;
            _shapeMesh.colors = colors;
            _shapeMesh.uv = new Vector2[8];
            _shapeMesh.triangles = triangles;
            _localBounds = new Bounds(Vector3.zero, new Vector3((hx + b) * 2f, (hy + b) * 2f, 0f));
        }

        // Triangle-fan disc at alpha 1 out to Radius, plus a feathered rim ring fading to alpha 0 at
        // Radius + BlendDistance.
        private void BuildCircleMesh()
        {
            var r = Mathf.Max(0f, Radius);
            var b = Mathf.Max(0f, BlendDistance);
            var inner = InteriorColor();
            var outer = EdgeColor();

            var vertexCount = 1 + CircleSegments * 2;
            var vertices = new Vector3[vertexCount];
            var colors = new Color[vertexCount];

            vertices[0] = Vector3.zero;
            colors[0] = inner;
            for (var i = 0; i < CircleSegments; i++)
            {
                var angle = (i / (float)CircleSegments) * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                vertices[1 + i] = dir * r;
                colors[1 + i] = inner;
                vertices[1 + CircleSegments + i] = dir * (r + b);
                colors[1 + CircleSegments + i] = outer;
            }

            var triangles = new int[CircleSegments * 3 + CircleSegments * 6];
            var t = 0;
            for (var i = 0; i < CircleSegments; i++)
            {
                var next = (i + 1) % CircleSegments;
                var innerCur = 1 + i;
                var innerNext = 1 + next;
                var outerCur = 1 + CircleSegments + i;
                var outerNext = 1 + CircleSegments + next;

                triangles[t++] = 0;
                triangles[t++] = innerCur;
                triangles[t++] = innerNext;

                triangles[t++] = innerCur;
                triangles[t++] = outerCur;
                triangles[t++] = outerNext;
                triangles[t++] = innerCur;
                triangles[t++] = outerNext;
                triangles[t++] = innerNext;
            }

            _shapeMesh.Clear();
            _shapeMesh.vertices = vertices;
            _shapeMesh.colors = colors;
            _shapeMesh.uv = new Vector2[vertexCount];
            _shapeMesh.triangles = triangles;
            _localBounds = new Bounds(Vector3.zero, new Vector3((r + b) * 2f, (r + b) * 2f, 0f));
        }

        // The sprite's own vertices/triangles/uv at alpha 1; the sprite texture alpha shapes the mask via
        // _MainTex in the coverage material. BlendDistance is not mesh-feathered for arbitrary outlines.
        private void BuildSpriteMesh()
        {
            if (Sprite == null)
            {
                _shapeMesh.Clear();
                _localBounds = new Bounds(Vector3.zero, Vector3.zero);
                return;
            }

            var spriteVertices = Sprite.vertices;
            var spriteTriangles = Sprite.triangles;

            var vertices = new Vector3[spriteVertices.Length];
            for (var i = 0; i < spriteVertices.Length; i++)
                vertices[i] = spriteVertices[i];

            var triangles = new int[spriteTriangles.Length];
            for (var i = 0; i < spriteTriangles.Length; i++)
                triangles[i] = spriteTriangles[i];

            var interior = InteriorColor();
            var colors = new Color[vertices.Length];
            for (var i = 0; i < colors.Length; i++)
                colors[i] = interior;

            _shapeMesh.Clear();
            _shapeMesh.vertices = vertices;
            _shapeMesh.triangles = triangles;
            _shapeMesh.uv = Sprite.uv;
            _shapeMesh.colors = colors;
            _localBounds = Sprite.bounds;
        }

        // A huge quad at alpha 1 that fills the whole extended view at any sane zoom; paired with a huge
        // world bounds so it always passes frustum culling.
        private void BuildGlobalMesh()
        {
            var color = InteriorColor();
            var vertices = new[]
            {
                new Vector3(-GlobalExtent, -GlobalExtent, 0f),
                new Vector3(-GlobalExtent, GlobalExtent, 0f),
                new Vector3(GlobalExtent, GlobalExtent, 0f),
                new Vector3(GlobalExtent, -GlobalExtent, 0f)
            };
            var colors = new[] { color, color, color, color };
            var triangles = new[] { 0, 1, 2, 0, 2, 3 };

            _shapeMesh.Clear();
            _shapeMesh.vertices = vertices;
            _shapeMesh.colors = colors;
            _shapeMesh.uv = new Vector2[4];
            _shapeMesh.triangles = triangles;
            _localBounds = new Bounds(Vector3.zero, new Vector3(GlobalExtent * 2f, GlobalExtent * 2f, 0f));
        }

        private Color InteriorColor()
        {
            return new Color(Coverage, Coverage, Coverage, 1f);
        }

        private Color EdgeColor()
        {
            return new Color(Coverage, Coverage, Coverage, 0f);
        }

        private void ResolveMaterial()
        {
            var shared = GetSharedCoverageMaterial();

            if (Shape == VolumeShape.Sprite && Sprite != null && Sprite.texture != null && shared != null)
            {
                // Per-texture variant so the sprite alpha shapes the mask; reacquired only when the texture changes.
                if (_materialKey == null || _oldSpriteTexture != Sprite.texture)
                {
                    ReleaseMaterial();
                    _coverageMaterial = CustomSprite.AcquireMaterial(shared, Sprite.texture, false, out _materialKey);
                    _oldSpriteTexture = Sprite.texture;
                }
                return;
            }

            // Box/Circle/Global (or a Sprite shape with no usable sprite) draw with the shared stateless material.
            ReleaseMaterial();
            _coverageMaterial = shared;
        }

        private static Material GetSharedCoverageMaterial()
        {
            if (_sharedCoverageMaterial != null)
                return _sharedCoverageMaterial;

            var shader = Shader.Find("Light2D/Internal/Coverage Mask");
            if (shader == null)
                return null;

            _sharedCoverageMaterial = new Material(shader)
            {
                name = "Generated Coverage Mask Material",
                hideFlags = HideFlags.HideAndDontSave
            };
            return _sharedCoverageMaterial;
        }

        private void ReleaseMaterial()
        {
            if (_materialKey != null)
                CustomSprite.ReleaseMaterial(_materialKey);
            _materialKey = null;
            _oldSpriteTexture = null;
            _coverageMaterial = null;
        }

        private void ReleaseOwnedResources()
        {
            ReleaseMaterial();
            if (_shapeMesh != null)
                Light2DObjectUtility.Destroy(_shapeMesh);
            _shapeMesh = null;
            _meshDirty = true;
        }

        // Rotation-aware AABB transform: transform the center and sum the absolute projected extents of
        // each local axis. Mirrors the Tilemap occluder's world-bounds idiom.
        private static Bounds TransformBounds(Bounds local, Matrix4x4 matrix)
        {
            var center = matrix.MultiplyPoint3x4(local.center);
            var ext = local.extents;
            var axisX = matrix.MultiplyVector(new Vector3(ext.x, 0f, 0f));
            var axisY = matrix.MultiplyVector(new Vector3(0f, ext.y, 0f));
            var axisZ = matrix.MultiplyVector(new Vector3(0f, 0f, ext.z));
            var worldExtents = new Vector3(
                Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
                Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
                Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));
            return new Bounds(center, worldExtents * 2f);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var previousMatrix = Gizmos.matrix;
            var previousColor = Gizmos.color;
            Gizmos.matrix = transform.localToWorldMatrix;
            var fill = new Color(1f, 0.85f, 0.4f, 0.9f);
            var band = new Color(1f, 0.85f, 0.4f, 0.35f);

            switch (Shape)
            {
                case VolumeShape.Box:
                    Gizmos.color = fill;
                    Gizmos.DrawWireCube(Vector3.zero, new Vector3(Size.x, Size.y, 0f));
                    if (BlendDistance > 0f)
                    {
                        Gizmos.color = band;
                        Gizmos.DrawWireCube(Vector3.zero,
                            new Vector3(Size.x + BlendDistance * 2f, Size.y + BlendDistance * 2f, 0f));
                    }
                    break;
                case VolumeShape.Circle:
                    Gizmos.color = fill;
                    DrawWireCircleGizmo(Radius);
                    if (BlendDistance > 0f)
                    {
                        Gizmos.color = band;
                        DrawWireCircleGizmo(Radius + BlendDistance);
                    }
                    break;
                case VolumeShape.Sprite:
                    if (Sprite != null)
                    {
                        Gizmos.color = fill;
                        var size = Sprite.bounds.size;
                        Gizmos.DrawWireCube(Sprite.bounds.center, new Vector3(size.x, size.y, 0f));
                    }
                    break;
            }

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }

        private static void DrawWireCircleGizmo(float radius)
        {
            var previous = new Vector3(radius, 0f, 0f);
            for (var i = 1; i <= CircleSegments; i++)
            {
                var angle = (i / (float)CircleSegments) * Mathf.PI * 2f;
                var next = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
#endif
    }
}
