using UnityEngine;

namespace Light2D
{
    /// <summary>
    /// Maintains an owned obstacle mesh that live-mirrors a parent MeshRenderer.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [AddComponentMenu("Light2D/Light Obstacle Mesh")]
    public class LightObstacleMesh : MonoBehaviour, ILight2DObstacle
    {
        [Tooltip("Per-vertex multiplicative obstacle color; darkens light passing through (opaque black " +
                 "casts a full shadow, white passes light untinted).")]
        public Color32 MultiplicativeColor;

        [Tooltip("Additive obstacle color encoded into UV1; brightens light passing through the obstacle " +
                 "(default transparent black adds nothing).")]
        public Color AdditiveColor;

        [Tooltip("Optional base obstacle material for the mirrored mesh. Leave empty to reuse the mirrored " +
                 "parent renderer's material.")]
        public Material Material;

        [Tooltip("LightingSystem that owns this obstacle. Leave empty to draw for any system, or set it to " +
                 "scope the obstacle to one system when several exist.")]
        public LightingSystem Owner;

        [Tooltip("Draw order within the obstacle pass; lower values draw first.")]
        public int SortingOrder;

        [Tooltip("Draw this object's own MeshFilter/MeshRenderer directly instead of mirroring a parent " +
                 "renderer. Required when the obstacle object is parented to unrelated geometry whose mesh " +
                 "must NOT be mirrored (MapGenerator chunks parent the obstacle mesh under the block-visual " +
                 "mesh, and the Phase-2 Tilemap occluder owns its own mesh).")]
        public bool SelfMesh;

        private MeshRenderer _parentMeshRenderer;
        private MeshFilter _parentMeshFilter;
        // D.2: parent-component lookup cache. Refresh() runs every frame, so the parent MeshRenderer/
        // MeshFilter are resolved only when transform.parent or SelfMesh actually changes; their
        // sharedMesh/sharedMaterial are still read live (LightObstacleSprite.ResolveSourceSprite pattern).
        private Transform _cachedParent;
        private bool _cachedSelfMesh;
        private MeshRenderer _meshRenderer;
        private MeshFilter _meshFilter;
        private Mesh _mesh;
        private Mesh _oldParentMesh;
        private Color32 _oldMulColor;
        private Color _oldAddColor;
        private Material _oldParentMaterial;
        private Material _oldBaseMaterial;
        private Texture2D _oldTexture;
        private CustomSprite.MaterialKey _materialKey;

        private const string GeneratedMeshName = "Generated Light Obstacle Mesh (DONT change it)";

        private void OnEnable()
        {
            _meshRenderer = GetComponent<MeshRenderer>();
            _meshFilter = GetComponent<MeshFilter>();
            ReleaseOrphanedResources();
            Light2DRenderRegistry.RegisterObstacle(this);
            Refresh();
        }

        private void OnDisable()
        {
            Light2DRenderRegistry.DeregisterObstacle(this);
            ReleaseOwnedResources();
        }

        private void OnDestroy()
        {
            Light2DRenderRegistry.DeregisterObstacle(this);
            ReleaseOwnedResources();
        }

        bool ILight2DDrawable.TryGetDrawEntry(out Light2DDrawEntry entry)
        {
            entry = default;
            if (!isActiveAndEnabled || _meshFilter == null || _meshRenderer == null)
                return false;
            if (Light2DObjectUtility.IsInPrefabStage(gameObject))
                return false;

            // Read the shared mesh/material live so a self-mesh assigned after OnEnable (MapGenerator,
            // Phase-2 Tilemap) is picked up the next frame without caching a stale entry (Risk R4).
            var mesh = _meshFilter.sharedMesh;
            var material = _meshRenderer.sharedMaterial;
            if (mesh == null || material == null)
                return false;

            entry = new Light2DDrawEntry(mesh, material, transform.localToWorldMatrix, _meshRenderer.bounds,
                Mathf.Max(1, mesh.subMeshCount), SortingOrder, Owner);
            return true;
        }

        private void Update()
        {
            Refresh();
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled)
                Refresh();
        }

#if UNITY_EDITOR
        // Seeds the texture-conditional obstacle defaults when the component is first added/reset in the
        // editor. Textured-ness comes from the mirrored source: the parent MeshRenderer normally, or this
        // object's own MeshRenderer when SelfMesh is set. A textured source keeps white; a solid source
        // gets opaque black so it casts a full shadow instead of a silent no-op.
        private void Reset()
        {
            var settings = Light2DObstacleSettings.GetOrCreate();
            var source = SelfMesh
                ? GetComponent<MeshRenderer>()
                : (transform.parent != null ? transform.parent.GetComponent<MeshRenderer>() : null);
            var textured = source != null && source.sharedMaterial != null &&
                           source.sharedMaterial.mainTexture != null;
            MultiplicativeColor = settings.MultiplicativeFor(textured);
            AdditiveColor = settings.AdditiveColor;
        }
#endif

        private void Refresh()
        {
            // Register-and-draw model (§0.3): obstacles draw only through the registry, never a scene
            // camera, so the owned renderer stays disabled in both mirror and self-mesh modes.
            if (_meshRenderer != null)
                _meshRenderer.enabled = false;

            // SelfMesh forces the self-mesh path even when a mirror-able parent exists: MapGenerator parents
            // the obstacle mesh under the block-visual mesh, whose geometry must never be mirrored.
            // D.2: only re-GetComponent when the parent transform changes or SelfMesh toggles; otherwise the
            // cached components are reused and their sharedMesh/sharedMaterial read live below.
            var rawParent = transform.parent;
            if (rawParent != _cachedParent || SelfMesh != _cachedSelfMesh)
            {
                _cachedParent = rawParent;
                _cachedSelfMesh = SelfMesh;
                var parent = SelfMesh ? null : rawParent;
                _parentMeshRenderer = parent != null ? parent.GetComponent<MeshRenderer>() : null;
                _parentMeshFilter = parent != null ? parent.GetComponent<MeshFilter>() : null;
            }

            var hasParentMirror = _parentMeshFilter != null && _parentMeshRenderer != null &&
                _parentMeshFilter.sharedMesh != null && _parentMeshRenderer.sharedMaterial != null;
            if (!hasParentMirror)
            {
                // Self-mesh mode (OQ1 option A): no parent geometry to mirror. Drop any owned generated
                // mesh/material left from a prior mirror pass and register this object's own MeshFilter/
                // MeshRenderer directly. Serves MapGenerator's self-mesh obstacle prefab and the Phase-2
                // Tilemap occluder, whose meshes/materials are assigned externally after OnEnable.
                ReleaseMesh();
                ReleaseMaterial();
                _oldParentMesh = null;
                _oldParentMaterial = null;
                return;
            }

            var geometryChanged = false;
            if (_parentMeshFilter.sharedMesh != _oldParentMesh || _mesh == null)
            {
                ReleaseMesh();
                _mesh = Instantiate(_parentMeshFilter.sharedMesh);
                _mesh.name = GeneratedMeshName;
                _mesh.hideFlags = HideFlags.DontSave;
                _mesh.MarkDynamic();
                _meshFilter.sharedMesh = _mesh;

                var tangents = _mesh.tangents;
                if (tangents == null || tangents.Length != _mesh.vertexCount)
                {
                    tangents = new Vector4[_mesh.vertexCount];
                    for (var i = 0; i < tangents.Length; i++)
                        tangents[i] = new Vector4(1, 0, 0, 1);
                    _mesh.tangents = tangents;
                }

                _oldParentMesh = _parentMeshFilter.sharedMesh;
                geometryChanged = true;
            }

            var parentMaterial = _parentMeshRenderer.sharedMaterial;
            var baseMaterial = Material != null ? Material : parentMaterial;
            var texture = parentMaterial.mainTexture as Texture2D;
            if (_oldParentMaterial != parentMaterial || _oldBaseMaterial != baseMaterial || _oldTexture != texture ||
                _meshRenderer.sharedMaterial == null)
            {
                ReleaseMaterial();
                _meshRenderer.sharedMaterial = CustomSprite.AcquireMaterial(baseMaterial, texture, false, out _materialKey);
                _oldParentMaterial = parentMaterial;
                _oldBaseMaterial = baseMaterial;
                _oldTexture = texture;
            }
            else
            {
                CustomSprite.RefreshMaterial(_materialKey);
            }

            if (!MultiplicativeColor.Equals(_oldMulColor) || AdditiveColor != _oldAddColor || geometryChanged)
                UpdateVertexData();
        }

        private void UpdateVertexData()
        {
            var colors = _mesh.colors32;
            if (colors == null || colors.Length != _mesh.vertexCount)
                colors = new Color32[_mesh.vertexCount];
            for (var i = 0; i < colors.Length; i++)
                colors[i] = MultiplicativeColor;
            _mesh.colors32 = colors;

            var encodedAdditive = Light2DColorEncoding.EncodeAdditiveColorToUv(AdditiveColor);
            var uv1 = _mesh.uv2;
            if (uv1 == null || uv1.Length != colors.Length)
                uv1 = new Vector2[colors.Length];
            for (var i = 0; i < uv1.Length; i++)
                uv1[i] = encodedAdditive;
            _mesh.uv2 = uv1;

            _oldMulColor = MultiplicativeColor;
            _oldAddColor = AdditiveColor;
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
            ReleaseMesh();
            ReleaseMaterial();
            if (_meshRenderer != null)
                _meshRenderer.enabled = false;
        }

        private void ReleaseMesh()
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
    }
}
