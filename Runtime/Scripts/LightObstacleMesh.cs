using UnityEngine;

namespace Light2D
{
    /// <summary>
    /// Maintains an owned obstacle mesh that live-mirrors a parent MeshRenderer.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class LightObstacleMesh : MonoBehaviour
    {
        public Color32 MultiplicativeColor;
        public Color AdditiveColor;
        public Material Material;
        public LightingSystem Owner;

        private MeshRenderer _parentMeshRenderer;
        private MeshFilter _parentMeshFilter;
        private MeshRenderer _meshRenderer;
        private MeshFilter _meshFilter;
        private Mesh _mesh;
        private Mesh _oldParentMesh;
        private Color32 _oldMulColor;
        private Color _oldAddColor;
        private Material _oldParentMaterial;
        private Material _oldBaseMaterial;
        private Texture2D _oldTexture;
        private LightingSystem _oldResolvedOwner;
        private int _oldObstacleLayer = -1;
        private CustomSprite.MaterialKey _materialKey;

        private const string GeneratedMeshName = "Generated Light Obstacle Mesh (DONT change it)";

        private void OnEnable()
        {
            _meshRenderer = GetComponent<MeshRenderer>();
            _meshFilter = GetComponent<MeshFilter>();
            ReleaseOrphanedResources();
            Refresh();
        }

        private void OnDisable()
        {
            ReleaseOwnedResources();
        }

        private void OnDestroy()
        {
            ReleaseOwnedResources();
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

        private void Refresh()
        {
            var parent = transform.parent;
            _parentMeshRenderer = parent != null ? parent.GetComponent<MeshRenderer>() : null;
            _parentMeshFilter = parent != null ? parent.GetComponent<MeshFilter>() : null;

            var resolvedOwner = Light2DObjectUtility.ResolveOwner(Owner);
            var obstacleLayer = resolvedOwner != null ? resolvedOwner.LightObstaclesLayer : -1;
            if (!Light2DObjectUtility.IsInPrefabStage(gameObject) && obstacleLayer >= 0 &&
                (resolvedOwner != _oldResolvedOwner || obstacleLayer != _oldObstacleLayer))
                gameObject.layer = obstacleLayer;
            _oldResolvedOwner = resolvedOwner;
            _oldObstacleLayer = obstacleLayer;

            if (_parentMeshFilter == null || _parentMeshRenderer == null ||
                _parentMeshFilter.sharedMesh == null || _parentMeshRenderer.sharedMaterial == null)
            {
                ReleaseMesh();
                ReleaseMaterial();
                _oldParentMesh = null;
                _oldParentMaterial = null;
                _meshRenderer.enabled = false;
                return;
            }

            _meshRenderer.enabled = true;
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
