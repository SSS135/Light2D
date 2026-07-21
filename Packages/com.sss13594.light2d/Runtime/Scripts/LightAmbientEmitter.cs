using UnityEngine;

namespace Light2D
{
    /// <summary>
    /// Emits ambient light. It registers with the render core and owns a MeshFilter/MeshRenderer that
    /// carries the emission geometry drawn into the ambient emission texture. It covers both ambient
    /// producers: a sprite-mirroring mode that bakes <see cref="EmissionColor"/> into an owned mesh, and
    /// a self-mesh mode (both sprite sources empty) that draws the object's externally authored mesh
    /// untouched, preserving per-vertex emission colors such as MapGenerator's ambient chunk meshes.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [AddComponentMenu("Light2D/Light Ambient Emitter")]
    [HelpURL(Light2DDocs.HelpUrl)]
    public class LightAmbientEmitter : MonoBehaviour, ILight2DAmbientEmitter
    {
        [Tooltip("Emissive color drawn into the ambient emission texture. In sprite mode it is baked " +
                 "into the owned mesh's vertex colors and tints the mirrored source renderer's live color " +
                 "(default white passes the source color through unchanged); in self-mesh mode the " +
                 "external mesh keeps its own colors.")]
        public Color EmissionColor = Color.white;

        [Tooltip("Optional sprite whose geometry is mirrored into the owned emission mesh. Leave empty " +
                 "(with no Game Sprite Renderer) to run in self-mesh mode using this object's own mesh.")]
        public Sprite Sprite;

        [Tooltip("Optional renderer whose sprite is mirrored. Takes precedence over Sprite as the " +
                 "geometry source when set.")]
        public Renderer GameSpriteRenderer;

        [Tooltip("Material used to draw the emission mesh. Defaults to the Light2D obstacle/emissive " +
                 "material in the editor; assign an emissive material for builds.")]
        public Material Material;

        [Tooltip("LightingSystem that owns this emitter. Leave empty to draw for any system, or set it " +
                 "to scope the emitter to one system when several exist.")]
        public LightingSystem Owner;

        [Tooltip("Draw order within the ambient emission pass; lower values draw first.")]
        public int SortingOrder;

        /// <summary>
        /// Optional procedural shaped-fill mode. <see cref="FillShape.None"/> keeps the existing
        /// sprite-mirror / self-mesh behavior; <see cref="FillShape.Box"/> and <see cref="FillShape.Circle"/>
        /// build a feathered primitive with <see cref="EmissionColor"/> baked into the vertex colors instead.
        /// </summary>
        public enum FillShape
        {
            None,
            Box,
            Circle
        }

        [Tooltip("Optional procedural fill shape. None keeps the sprite-mirror / self-mesh behavior. Box and " +
                 "Circle instead draw a feathered primitive (EmissionColor baked into the vertex colors, alpha " +
                 "ramped to zero across the blend band) straight into the ambient emission texture, giving a " +
                 "shaped/blended regional fill or, at large size, a global 'never fully black' floor.")]
        public FillShape Fill = FillShape.None;

        [Tooltip("Box fill full size in local units (X,Y). Used when Fill is Box.")]
        public Vector2 FillSize = Vector2.one;

        [Tooltip("Circle fill radius in local units. Used when Fill is Circle.")]
        public float FillRadius = 0.5f;

        [Tooltip("Soft-edge width in local units. The fill fades from full EmissionColor to none across this " +
                 "band. Used when Fill is Box or Circle.")]
        public float FillBlendDistance = 0.5f;

        // GUID of the sample obstacle/emissive material, matching LightObstacleSprite's editor default.
        private const string DefaultMaterialGuid = "0a8c132f813f5514b9f5b48ff3956482";
        private const string GeneratedMeshName = "Generated Ambient Emission Mesh (DONT change it)";

        private static Material _runtimeDefaultMaterial;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;
        private CustomSprite.MaterialKey _materialKey;

        private Sprite _oldSprite;
        private Color _oldEffectiveColor;
        private Material _oldBaseMaterial;
        private Texture2D _oldTexture;
        private bool _meshDirty;

        private FillShape _oldFill;
        private Vector2 _oldFillSize;
        private float _oldFillRadius;
        private float _oldFillBlendDistance;
        private Color _oldFillColor;

        private Renderer _sourceRenderer;
        private CustomSprite _sourceCustomSprite;
        private SpriteRenderer _sourceSpriteRenderer;

        private void OnEnable()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
#if UNITY_EDITOR
            if (Material == null)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(DefaultMaterialGuid);
                Material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
            }
#endif
            ReleaseOrphanedResources();
            Light2DRenderRegistry.RegisterAmbient(this);
            Refresh();
        }

        private void OnDisable()
        {
            Light2DRenderRegistry.DeregisterAmbient(this);
            ReleaseOwnedResources();
        }

        private void OnDestroy()
        {
            Light2DRenderRegistry.DeregisterAmbient(this);
            ReleaseOwnedResources();
        }

        private void Update()
        {
            Refresh();
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled && _meshFilter != null && _meshRenderer != null)
                Refresh();
        }

        bool ILight2DDrawable.TryGetDrawEntry(out Light2DDrawEntry entry)
        {
            entry = default;
            if (!isActiveAndEnabled || _meshFilter == null || _meshRenderer == null)
                return false;
            if (Light2DObjectUtility.IsInPrefabStage(gameObject))
                return false;

            var mesh = _meshFilter.sharedMesh;
            var material = _meshRenderer.sharedMaterial;
            if (mesh == null || material == null)
                return false;

            entry = new Light2DDrawEntry(mesh, material, transform.localToWorldMatrix, _meshRenderer.bounds,
                Mathf.Max(1, mesh.subMeshCount), SortingOrder, Owner);
            return true;
        }

        private void Refresh()
        {
            if (_meshFilter == null || _meshRenderer == null)
                return;

            // Register-and-draw model (§0.3): the owned renderer stays disabled so no scene camera draws
            // the emission mesh; it is drawn only through the ambient registry snapshot.
            _meshRenderer.enabled = false;
            _meshRenderer.sortingOrder = SortingOrder;

            // Shaped-fill mode replaces the sprite-mirror / self-mesh path with an owned procedural primitive.
            if (Fill != FillShape.None)
            {
                RefreshFillMode();
                return;
            }

            var source = ResolveSourceSprite();
            if (source != null)
                RefreshSpriteMode(source);
            else
                RefreshSelfMeshMode();
        }

        private void RefreshSpriteMode(Sprite source)
        {
            EnsureOwnedMesh();
            var effectiveColor = ResolveEffectiveColor();
            if (source != _oldSprite || effectiveColor != _oldEffectiveColor || _meshDirty)
            {
                BuildSpriteMesh(source, effectiveColor);
                _oldSprite = source;
                _oldEffectiveColor = effectiveColor;
                _meshDirty = false;
            }

            var baseMaterial = Material != null ? Material : GetRuntimeDefaultMaterial();
            var texture = source.texture;
            if (baseMaterial == null || texture == null)
            {
                ReleaseMaterial();
                _meshRenderer.sharedMaterial = null;
                return;
            }

            if (_oldBaseMaterial != baseMaterial || _oldTexture != texture ||
                !CustomSprite.IsGeneratedMaterial(_meshRenderer.sharedMaterial))
            {
                ReleaseMaterial();
                _meshRenderer.sharedMaterial = CustomSprite.AcquireMaterial(baseMaterial, texture, false, out _materialKey);
                _oldBaseMaterial = baseMaterial;
                _oldTexture = texture;
            }
            else
            {
                CustomSprite.RefreshMaterial(_materialKey);
            }
        }

        private void RefreshSelfMeshMode()
        {
            // No sprite source: keep the object's own mesh and per-vertex colors. Drop any owned
            // sprite-mode mesh/material we created earlier so the external mesh is drawn as authored.
            ReleaseOwnedMesh();
            ReleaseMaterial();
            _oldSprite = null;
            _oldBaseMaterial = null;
            _oldTexture = null;

            if (_meshRenderer.sharedMaterial == null || CustomSprite.IsGeneratedMaterial(_meshRenderer.sharedMaterial))
                _meshRenderer.sharedMaterial = Material != null ? Material : GetRuntimeDefaultMaterial();
        }

        private void BuildSpriteMesh(Sprite sprite, Color color)
        {
            var spriteVertices = sprite.vertices;
            var spriteTriangles = sprite.triangles;

            var vertices = new Vector3[spriteVertices.Length];
            for (var i = 0; i < spriteVertices.Length; i++)
                vertices[i] = spriteVertices[i];

            var triangles = new int[spriteTriangles.Length];
            for (var i = 0; i < spriteTriangles.Length; i++)
                triangles[i] = spriteTriangles[i];

            var colors = new Color[vertices.Length];
            for (var i = 0; i < colors.Length; i++)
                colors[i] = color;

            _mesh.Clear();
            _mesh.vertices = vertices;
            _mesh.triangles = triangles;
            _mesh.uv = sprite.uv;
            _mesh.colors = colors;
        }

        // Procedural shaped-fill mode: instead of mirroring a sprite or drawing an external mesh, build a
        // feathered Box/Circle primitive with EmissionColor baked into the vertex colors and draw it through
        // the existing ambient path. The primitive carries no sprite texture, so the base material is bound
        // with a white main texture (col == white) and the vertex color alone drives the emission.
        private void RefreshFillMode()
        {
            EnsureOwnedMesh();

            if (Fill != _oldFill || FillSize != _oldFillSize ||
                !Mathf.Approximately(FillRadius, _oldFillRadius) ||
                !Mathf.Approximately(FillBlendDistance, _oldFillBlendDistance) ||
                EmissionColor != _oldFillColor || _meshDirty)
            {
                BuildFillMesh(EmissionColor);
                _oldFill = Fill;
                _oldFillSize = FillSize;
                _oldFillRadius = FillRadius;
                _oldFillBlendDistance = FillBlendDistance;
                _oldFillColor = EmissionColor;
                _meshDirty = false;
            }

            var baseMaterial = Material != null ? Material : GetRuntimeDefaultMaterial();
            var texture = Texture2D.whiteTexture;
            if (baseMaterial == null || texture == null)
            {
                ReleaseMaterial();
                _meshRenderer.sharedMaterial = null;
                return;
            }

            if (_oldBaseMaterial != baseMaterial || _oldTexture != texture ||
                !CustomSprite.IsGeneratedMaterial(_meshRenderer.sharedMaterial))
            {
                ReleaseMaterial();
                _meshRenderer.sharedMaterial = CustomSprite.AcquireMaterial(baseMaterial, texture, false, out _materialKey);
                _oldBaseMaterial = baseMaterial;
                _oldTexture = texture;
                // Invalidate the sprite-mode cache so a later switch back to sprite mode rebuilds the mesh.
                _oldSprite = null;
            }
            else
            {
                CustomSprite.RefreshMaterial(_materialKey);
            }
        }

        private void BuildFillMesh(Color color)
        {
            if (Fill == FillShape.Circle)
                BuildCircleFillMesh(color);
            else
                BuildBoxFillMesh(color);
        }

        // Feathered box: a full-emission inner quad expanded by FillBlendDistance to an outer ring whose
        // vertex alpha is zero, so the SrcAlpha blend fades the emission to nothing across the border band.
        // Corners fade diagonally (a minor cosmetic simplification vs a rounded feather).
        private void BuildBoxFillMesh(Color color)
        {
            var hx = Mathf.Max(0f, FillSize.x * 0.5f);
            var hy = Mathf.Max(0f, FillSize.y * 0.5f);
            var b = Mathf.Max(0f, FillBlendDistance);
            var edge = new Color(color.r, color.g, color.b, 0f);

            var vertices = new[]
            {
                new Vector3(-hx, -hy, 0f),                 // 0 inner BL
                new Vector3(hx, -hy, 0f),                  // 1 inner BR
                new Vector3(hx, hy, 0f),                   // 2 inner TR
                new Vector3(-hx, hy, 0f),                  // 3 inner TL
                new Vector3(-(hx + b), -(hy + b), 0f),     // 4 outer BL
                new Vector3(hx + b, -(hy + b), 0f),        // 5 outer BR
                new Vector3(hx + b, hy + b, 0f),           // 6 outer TR
                new Vector3(-(hx + b), hy + b, 0f)         // 7 outer TL
            };

            var colors = new[] { color, color, color, color, edge, edge, edge, edge };

            var uv = new Vector2[8];
            for (var i = 0; i < uv.Length; i++)
                uv[i] = new Vector2(0.5f, 0.5f);

            var triangles = new[]
            {
                0, 1, 2, 0, 2, 3,   // full-emission inner quad
                4, 5, 1, 4, 1, 0,   // bottom feather band
                5, 6, 2, 5, 2, 1,   // right feather band
                6, 7, 3, 6, 3, 2,   // top feather band
                7, 4, 0, 7, 0, 3    // left feather band
            };

            _mesh.Clear();
            _mesh.vertices = vertices;
            _mesh.triangles = triangles;
            _mesh.uv = uv;
            _mesh.colors = colors;
        }

        // Feathered circle: a full-emission disc (triangle fan) ringed by a feathered rim whose outer
        // vertices have zero alpha, giving a smooth radial falloff across FillBlendDistance.
        private void BuildCircleFillMesh(Color color)
        {
            const int segments = 48;
            var r = Mathf.Max(0f, FillRadius);
            var outer = r + Mathf.Max(0f, FillBlendDistance);
            var edge = new Color(color.r, color.g, color.b, 0f);

            var vertexCount = 1 + segments * 2;
            var vertices = new Vector3[vertexCount];
            var colors = new Color[vertexCount];
            var uv = new Vector2[vertexCount];

            vertices[0] = Vector3.zero;
            colors[0] = color;
            uv[0] = new Vector2(0.5f, 0.5f);

            for (var i = 0; i < segments; i++)
            {
                var angle = i / (float)segments * Mathf.PI * 2f;
                var cos = Mathf.Cos(angle);
                var sin = Mathf.Sin(angle);
                var innerIndex = 1 + i;
                var outerIndex = 1 + segments + i;
                vertices[innerIndex] = new Vector3(cos * r, sin * r, 0f);
                vertices[outerIndex] = new Vector3(cos * outer, sin * outer, 0f);
                colors[innerIndex] = color;
                colors[outerIndex] = edge;
                uv[innerIndex] = new Vector2(0.5f, 0.5f);
                uv[outerIndex] = new Vector2(0.5f, 0.5f);
            }

            var triangles = new int[segments * 9];
            var t = 0;
            for (var i = 0; i < segments; i++)
            {
                var next = (i + 1) % segments;
                var innerI = 1 + i;
                var innerNext = 1 + next;
                var outerI = 1 + segments + i;
                var outerNext = 1 + segments + next;

                // Inner disc fan triangle.
                triangles[t++] = 0;
                triangles[t++] = innerI;
                triangles[t++] = innerNext;

                // Feathered rim (two triangles bridging the inner ring to the zero-alpha outer ring).
                triangles[t++] = innerI;
                triangles[t++] = outerI;
                triangles[t++] = outerNext;

                triangles[t++] = innerI;
                triangles[t++] = outerNext;
                triangles[t++] = innerNext;
            }

            _mesh.Clear();
            _mesh.vertices = vertices;
            _mesh.triangles = triangles;
            _mesh.uv = uv;
            _mesh.colors = colors;
        }

        private Sprite ResolveSourceSprite()
        {
            if (GameSpriteRenderer != null)
            {
                // Component lookup runs only when the source renderer changes; the cached source stays live-read.
                if (GameSpriteRenderer != _sourceRenderer)
                {
                    _sourceRenderer = GameSpriteRenderer;
                    _sourceCustomSprite = GameSpriteRenderer.GetComponent<CustomSprite>();
                    _sourceSpriteRenderer = _sourceCustomSprite != null ? null : GameSpriteRenderer as SpriteRenderer;
                }

                if (_sourceCustomSprite != null)
                    return _sourceCustomSprite.Sprite;
                if (_sourceSpriteRenderer != null)
                    return _sourceSpriteRenderer.sprite;
                return Sprite;
            }

            _sourceRenderer = null;
            _sourceCustomSprite = null;
            _sourceSpriteRenderer = null;
            return Sprite;
        }

        // Effective emission color = the mirrored source renderer's current color tinted by EmissionColor,
        // so a script animating the source's .color drives the baked vertex colors live each frame. With
        // no live source (Sprite-field-only or self-mesh mode) it stays EmissionColor, preserving the old
        // behavior. Must run after ResolveSourceSprite so the cached source references are current.
        private Color ResolveEffectiveColor()
        {
            if (_sourceSpriteRenderer != null)
                return _sourceSpriteRenderer.color * EmissionColor;
            if (_sourceCustomSprite != null)
                return _sourceCustomSprite.Color * EmissionColor;
            return EmissionColor;
        }

        private void EnsureOwnedMesh()
        {
            if (_mesh != null)
            {
                if (_meshFilter.sharedMesh != _mesh)
                    _meshFilter.sharedMesh = _mesh;
                return;
            }

            _mesh = new Mesh { name = GeneratedMeshName, hideFlags = HideFlags.DontSave };
            _mesh.MarkDynamic();
            _meshFilter.sharedMesh = _mesh;
            _meshDirty = true;
        }

        private void ReleaseOrphanedResources()
        {
            if (_meshFilter.sharedMesh != null && _meshFilter.sharedMesh.name == GeneratedMeshName)
            {
                Light2DObjectUtility.Destroy(_meshFilter.sharedMesh);
                _meshFilter.sharedMesh = null;
            }
            if (CustomSprite.IsGeneratedMaterial(_meshRenderer.sharedMaterial))
            {
                Light2DObjectUtility.Destroy(_meshRenderer.sharedMaterial);
                _meshRenderer.sharedMaterial = null;
            }
        }

        private void ReleaseOwnedResources()
        {
            ReleaseOwnedMesh();
            ReleaseMaterial();
        }

        private void ReleaseOwnedMesh()
        {
            if (_meshFilter != null && _meshFilter.sharedMesh == _mesh)
                _meshFilter.sharedMesh = null;
            if (_mesh != null)
                Light2DObjectUtility.Destroy(_mesh);
            _mesh = null;
        }

        private void ReleaseMaterial()
        {
            if (_materialKey != null)
                CustomSprite.ReleaseMaterial(_materialKey);
            _materialKey = null;
            if (_meshRenderer != null && CustomSprite.IsGeneratedMaterial(_meshRenderer.sharedMaterial))
                _meshRenderer.sharedMaterial = null;
        }

        // Last-resort runtime material so builds never draw a null material. Scenes/prefabs should
        // assign an emissive Material; this Sprites/Default fallback only guarantees non-null state.
        private static Material GetRuntimeDefaultMaterial()
        {
            if (_runtimeDefaultMaterial != null)
                return _runtimeDefaultMaterial;

            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return null;

            _runtimeDefaultMaterial = new Material(shader)
            {
                name = "Generated Ambient Emitter Default Material",
                hideFlags = HideFlags.HideAndDontSave
            };
            return _runtimeDefaultMaterial;
        }
    }
}
