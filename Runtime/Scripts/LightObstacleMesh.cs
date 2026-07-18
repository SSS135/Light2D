using UnityEngine;

namespace Light2D
{
    /// <summary>
    /// Automatically updating mesh, material and main texture of light obstacle. 
    /// Class is copying all data used for rendering from parent.
    /// </summary>
    public class LightObstacleMesh : MonoBehaviour
    {
        public Color32 MultiplicativeColor;
        public Color AdditiveColor;
        public Material Material;
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
        private CustomSprite.MaterialKey _materialKey;
        private const string GeneratedMeshName = "Generated Light Obstacle Mesh (DONT change it)";

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
            Refresh();
        }

        private void Initialize()
        {
            _parentMeshRenderer = transform.parent != null ? transform.parent.GetComponent<MeshRenderer>() : null;
            _parentMeshFilter = transform.parent != null ? transform.parent.GetComponent<MeshFilter>() : null;
            _meshRenderer = GetComponent<MeshRenderer>();
            if (_meshRenderer == null) _meshRenderer = gameObject.AddComponent<MeshRenderer>();
            _meshFilter = GetComponent<MeshFilter>();
            if (_meshFilter == null) _meshFilter = gameObject.AddComponent<MeshFilter>();

            if (_mesh == null && _meshFilter.sharedMesh != null && _meshFilter.sharedMesh.name == GeneratedMeshName)
            {
                Util.Destroy(_meshFilter.sharedMesh);
                _meshFilter.sharedMesh = null;
            }

            if (_materialKey == null && CustomSprite.IsGeneratedMaterial(_meshRenderer.sharedMaterial))
            {
                Util.Destroy(_meshRenderer.sharedMaterial);
                _meshRenderer.sharedMaterial = null;
            }
        }

        private void Update()
        {
            Refresh();
        }

        void Refresh()
        {
            if (_parentMeshFilter == null || _parentMeshRenderer == null || _meshRenderer == null || _meshFilter == null ||
                _parentMeshFilter.sharedMesh == null || _parentMeshRenderer.sharedMaterial == null)
            {
                if (_meshRenderer != null)
                    _meshRenderer.enabled = false;
                return;
            }

            _meshRenderer.enabled = true;

            var dirty = false;
            if (_parentMeshFilter.sharedMesh != _oldParentMesh || _mesh == null)
            {
                ReleaseMesh();
                _mesh = Instantiate(_parentMeshFilter.sharedMesh);
                _mesh.name = GeneratedMeshName;
                _mesh.hideFlags = HideFlags.DontSave;
                _mesh.MarkDynamic();
                _meshFilter.sharedMesh = _mesh;

                if (_mesh.tangents == null || _mesh.tangents.Length != _mesh.vertexCount)
                {
                    var tangents = new Vector4[_mesh.vertexCount];
                    for (int i = 0; i < tangents.Length; i++)
                        tangents[i] = new Vector4(1, 0);
                    _mesh.tangents = tangents;
                }

                _oldParentMesh = _parentMeshFilter.sharedMesh;
                dirty = true;
            }

            var parentMaterial = _parentMeshRenderer.sharedMaterial;
            var baseMaterial = Material == null ? parentMaterial : Material;
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

            if (!MultiplicativeColor.Equals(_oldMulColor) || AdditiveColor != _oldAddColor || dirty)
            {
                var colors = _mesh.colors32;
                if (colors == null || colors.Length != _mesh.vertexCount)
                    colors = new Color32[_mesh.vertexCount];

                for (int i = 0; i < colors.Length; i++)
                    colors[i] = MultiplicativeColor;
                _mesh.colors32 = colors;

                var uv1 = new Vector2(
                    Util.DecodeFloatRGBA((Vector4) AdditiveColor),
                    Util.DecodeFloatRGBA(new Vector4(AdditiveColor.a, 0, 0)));
                var uv1Arr = _mesh.uv2;
                if (uv1Arr == null || uv1Arr.Length != colors.Length)
                    uv1Arr = new Vector2[colors.Length];
                for (int i = 0; i < uv1Arr.Length; i++)
                {
                    uv1Arr[i] = uv1;
                }
                _mesh.uv2 = uv1Arr;

                _oldMulColor = MultiplicativeColor;
                _oldAddColor = AdditiveColor;
            }
        }

        private void ReleaseMesh()
        {
            if (_meshFilter != null && _meshFilter.sharedMesh == _mesh)
                _meshFilter.sharedMesh = null;
            if (_mesh != null)
                Util.Destroy(_mesh);
            _mesh = null;
        }

        private void ReleaseMaterial()
        {
            if (_materialKey != null)
                CustomSprite.ReleaseMaterial(_materialKey);
            _materialKey = null;
            if (_meshRenderer != null)
                _meshRenderer.sharedMaterial = null;
        }

        private void OnDestroy()
        {
            ReleaseMesh();
            ReleaseMaterial();
        }
    }
}
