using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Light2D
{
    /// <summary>
    /// Custom sprite wich uses MeshFilter and MeshRenderer to render.
    /// Main improvement from Unity SpriteRenderer is that you can access and modify mesh.
    /// Also multiple CustomSprites could be merged to single mesh with MeshCombiner,
    /// which gives much better performance for small meshes than StaticBatchingUtility.Combine.
    /// </summary>
    [ExecuteInEditMode]
    public class CustomSprite : MonoBehaviour
    {
        /// <summary>
        /// Vertex color of mesh.
        /// </summary>
        public Color Color = Color.white;

        /// <summary>
        /// Sprite from which mesh will be generated.
        /// </summary>
        public Sprite Sprite;

        /// <summary>
        /// Sorting order of MeshRenderer.
        /// </summary>
        public int SortingOrder;

        /// <summary>
        /// Material to be used.
        /// </summary>
        public Material Material;

        // mesh data
        protected Color[] _colors;
        protected Vector2[] _uv0;
        protected Vector2[] _uv1;
        protected Vector3[] _vertices;
        protected int[] _triangles;
        protected Vector4[] _tangents;

        protected bool _isMeshDirty = false;
        protected MeshRenderer _meshRenderer;
        protected MeshFilter _meshFilter;
        protected Mesh _mesh;
        private Color _oldColor;
        private Sprite _oldSprite;
        private Material _oldMaterial;
        private MaterialKey _oldMaterialKey;
        private Camera _normalMappingCamera;
        private static readonly Dictionary<MaterialKey, MaterialValue> MaterialMap = new Dictionary<MaterialKey, MaterialValue>();
        private static Material _defaultSpriteMaterial;
        private const string DefaultSpriteMaterialName = "Generated Default Sprite Material (DONT change it)";
        private const string GeneratedMaterialName = "Generated Material (DONT change it)";
        private const string GeneratedMeshName = "Generated Mesh (DONT change it)";

        public bool RendererEnabled { get; private set; }

        /// <summary>
        /// Is that sprite is staticaly batched?
        /// </summary>
        public bool IsPartOfStaticBatch
        {
            get { return _meshRenderer.isPartOfStaticBatch; }
        }

        protected virtual void OnEnable()
        {
            _colors = new Color[4];
            _uv1 = new Vector2[4];
            _uv0 = new Vector2[4];
            _vertices = new Vector3[4];
            _triangles = new[] {2, 1, 0, 1, 2, 3};
            _meshRenderer = GetComponent<MeshRenderer>();
            _meshFilter = GetComponent<MeshFilter>();

            if (_meshRenderer == null)
                _meshRenderer = gameObject.AddComponent<MeshRenderer>();

            if (_meshFilter == null)
                _meshFilter = gameObject.AddComponent<MeshFilter>();

            if (_oldMaterialKey == null && _meshRenderer.sharedMaterial != null &&
                _meshRenderer.sharedMaterial.name == GeneratedMaterialName)
            {
                Util.Destroy(_meshRenderer.sharedMaterial);
                _meshRenderer.sharedMaterial = null;
            }

            TryReleaseMesh();
            _meshFilter.sharedMesh = _mesh = new Mesh();
            _mesh.MarkDynamic();
            _mesh.name = GeneratedMeshName;
            _mesh.hideFlags = HideFlags.DontSave;

            _tangents = new Vector4[4];
            for (int i = 0; i < _tangents.Length; i++)
                _tangents[i] = new Vector4(1, 0, 0);

            UpdateMeshData(true);

            RendererEnabled = _meshRenderer.enabled;
        }

        protected virtual void Start()
        {
            UpdateMeshData(true);
        }

        private void OnWillRenderObject()
        {
            UpdateMeshData();
            if (_normalMappingCamera == null)
                RendererEnabled = _meshRenderer.enabled;
            if (Application.isPlaying && GraphicsSettings.currentRenderPipeline == null &&
                LightingSystem.IsNormalMappingLightCamera(Camera.current))
            {
                RendererEnabled = _meshRenderer.enabled;
                _meshRenderer.enabled = false;
                _normalMappingCamera = Camera.current;
            }
        }

        private void OnRenderObject()
        {
            if (Application.isPlaying && GraphicsSettings.currentRenderPipeline == null &&
                _normalMappingCamera != null && Camera.current == _normalMappingCamera)
            {
                _meshRenderer.enabled = RendererEnabled;
                _normalMappingCamera = null;
            }
        }

        protected virtual bool UseNormalTexcoordMaterial
        {
            get { return false; }
        }

        /// <summary>
        /// Getting material from cache or instantiating new one.
        /// </summary>
        /// <returns></returns>
        private Material GetOrCreateMaterial()
        {
            TryReleaseMaterial();

            if (Sprite == null)
                return null;

            var baseMaterial = Material != null ? Material : GetOrCreateDefaultSpriteMaterial();
            return AcquireMaterial(baseMaterial, Sprite.texture, UseNormalTexcoordMaterial, out _oldMaterialKey);
        }

        private static Material GetOrCreateDefaultSpriteMaterial()
        {
            if (_defaultSpriteMaterial != null)
                return _defaultSpriteMaterial;

            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return null;

            _defaultSpriteMaterial = new Material(shader)
            {
                name = DefaultSpriteMaterialName,
                hideFlags = HideFlags.HideAndDontSave
            };
            return _defaultSpriteMaterial;
        }
        
        /// <summary>
        /// Getting material from cache or instantiating new one.
        /// </summary>
        /// <returns></returns>
        internal static Material AcquireMaterial(Material baseMaterial, Texture2D texture, bool normalTexcoord,
            out MaterialKey materialKey)
        {
            if (baseMaterial == null || texture == null)
            {
                materialKey = null;
                return null;
            }

            MaterialValue matValue;
            var key = materialKey = new MaterialKey(baseMaterial, texture, normalTexcoord);

            if (!MaterialMap.TryGetValue(key, out matValue) || matValue.Material == null)
            {
                MaterialMap.Remove(key);
                var mat = (Material)Instantiate(baseMaterial);
                mat.name = GeneratedMaterialName;
                mat.hideFlags = HideFlags.DontSave;
                mat.mainTexture = texture;
                if (normalTexcoord)
                    mat.EnableKeyword("NORMAL_TEXCOORD");
                else
                    mat.DisableKeyword("NORMAL_TEXCOORD");
                MaterialMap[key] = matValue = new MaterialValue(mat, 1);
            }
            else
            {
                matValue.UsageCount++;
            }
            
            return matValue.Material;
        }

        /// <summary>
        /// Deleting material from cache with reference counting.
        /// </summary>
        /// <param name="key"></param>
        internal static void ReleaseMaterial(MaterialKey key)
        {
            MaterialValue matValue;

            if (!MaterialMap.TryGetValue(key, out matValue))
                return;

            matValue.UsageCount--;

            if (matValue.UsageCount <= 0)
            {
                Util.Destroy(matValue.Material);
                MaterialMap.Remove(key);
                TryReleaseDefaultSpriteMaterial();
            }
        }

        private static void TryReleaseDefaultSpriteMaterial()
        {
            if (_defaultSpriteMaterial == null)
                return;

            foreach (var key in MaterialMap.Keys)
            {
                if (key.Material == _defaultSpriteMaterial)
                    return;
            }

            Util.Destroy(_defaultSpriteMaterial);
            _defaultSpriteMaterial = null;
        }

        internal static bool IsGeneratedMaterial(Material material)
        {
            return material != null && material.name == GeneratedMaterialName;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetMaterialCache()
        {
            foreach (var value in MaterialMap.Values)
                Util.Destroy(value.Material);
            MaterialMap.Clear();
            Util.Destroy(_defaultSpriteMaterial);
            _defaultSpriteMaterial = null;
        }

        private void TryReleaseMesh()
        {
            if (_meshFilter != null && _meshFilter.sharedMesh != null &&
                (_meshFilter.sharedMesh == _mesh || _meshFilter.sharedMesh.name == GeneratedMeshName))
            {
                Util.Destroy(_meshFilter.sharedMesh);
                _meshFilter.sharedMesh = null;
            }
            else if (_mesh != null)
            {
                Util.Destroy(_mesh);
            }

            _mesh = null;
        }

        private void TryReleaseMaterial()
        {
            if (_oldMaterialKey != default(MaterialKey))
            {
                ReleaseMaterial(_oldMaterialKey);
                _oldMaterialKey = default(MaterialKey);
            }
        }

        protected virtual void OnDestroy()
        {
            TryReleaseMesh();
            TryReleaseMaterial();
        }

        protected virtual void UpdateColor()
        {
            for (int i = 0; i < _colors.Length; i++)
                _colors[i] = Color;
        }

        /// <summary>
        /// Recreating mesh data for Sprite based on it's bounds.
        /// </summary>
        protected virtual void UpdateSprite()
        {
            if (Sprite == null)
            {
                TryReleaseMaterial();
                if (_meshRenderer != null)
                    _meshRenderer.sharedMaterial = null;
                return;
            }
            
            var rect = Sprite.textureRect;
            var bounds = Sprite.bounds;
            var tex = Sprite.texture;
            var textureSize = new Point2(tex.width, tex.height);
            
            // HACK: mipmap could cause texture padding sometimes so padded size of texture needs to be computed.
            var realSize =
#if UNITY_EDITOR || UNITY_STANDALONE
                tex.mipmapCount <= 1
#else
                true
#endif
                    ? textureSize
                    : new Point2(Mathf.NextPowerOfTwo(textureSize.x), Mathf.NextPowerOfTwo(textureSize.y));

            var unitSize2 = rect.size/Sprite.pixelsPerUnit/2f;
            var offest = (Vector2) bounds.center;

            _vertices[0] = new Vector3(-unitSize2.x + offest.x, -unitSize2.y + offest.y, 0);
            _vertices[1] = new Vector3(unitSize2.x + offest.x, -unitSize2.y + offest.y, 0);
            _vertices[2] = new Vector3(-unitSize2.x + offest.x, unitSize2.y + offest.y, 0);
            _vertices[3] = new Vector3(unitSize2.x + offest.x, unitSize2.y + offest.y, 0);

            _uv0[0] = new Vector2(rect.xMin/realSize.x, rect.yMin/realSize.y); // 0, 0
            _uv0[1] = new Vector2(rect.xMax/realSize.x, rect.yMin/realSize.y); // 1, 0
            _uv0[2] = new Vector2(rect.xMin/realSize.x, rect.yMax/realSize.y); // 0, 1
            _uv0[3] = new Vector2(rect.xMax/realSize.x, rect.yMax/realSize.y); // 1, 1

            for (int i = 0; i < 4; i++)
            {
                _colors[i] = Color;
            }

            _meshRenderer.sharedMaterial = GetOrCreateMaterial();
        }

        /// <summary>
        /// Clearing and rebuilding mesh.
        /// </summary>
        protected virtual void UpdateMesh()
        {
            _mesh.Clear();
            _mesh.vertices = _vertices;
            _mesh.triangles = _triangles;
            _mesh.uv = _uv0;
            _mesh.uv2 = _uv1;
            _mesh.colors = _colors;
            _mesh.tangents = _tangents;
        }

        /// <summary>
        /// Checking public fields and mesh data, then rebuilding internal state if changes found.
        /// </summary>
        /// <param name="forceUpdate">Force update even if no changes found.</param>
        protected virtual void UpdateMeshData(bool forceUpdate = false)
        {
            if (_meshRenderer == null || _meshFilter == null || IsPartOfStaticBatch)
                return;

            _meshRenderer.sortingOrder = SortingOrder;

            if (Color != _oldColor || forceUpdate)
            {
                UpdateColor();
                _isMeshDirty = true;
                _oldColor = Color;
            }
            if (Sprite != _oldSprite || Material != _oldMaterial ||
                (Sprite != null && Material != null && _meshRenderer.sharedMaterial == null) || forceUpdate)
            {
                UpdateSprite();
                _isMeshDirty = true;
                _oldSprite = Sprite;
                _oldMaterial = Material;
            }
            if (_isMeshDirty)
            {
                UpdateMesh();
                _isMeshDirty = false;
            }
        }

        /// <summary>
        /// Used as a value to material map to support reference counting.
        /// </summary>
        private sealed class MaterialValue
        {
            /// <summary>
            /// Instantiated material from MaterialKey.Material with texture from MaterialKey.Texture.
            /// </summary>
            public Material Material;

            /// <summary>
            /// Count of CustomSprites using that material.
            /// </summary>
            public int UsageCount;

            public MaterialValue(Material material, int usageCount)
            {
                Material = material;
                UsageCount = usageCount;
            }
        }

        /// <summary>
        /// Used as a key to material map.
        /// </summary>
        internal sealed class MaterialKey : IEquatable<MaterialKey>
        {
            /// <summary>
            /// Sprite's texture.
            /// </summary>
            public Texture2D Texture;

            /// <summary>
            /// Non instantiated material.
            /// </summary>
            public Material Material;

            public bool NormalTexcoord;
#if UNITY_6000_0_OR_NEWER
            private readonly EntityId _textureId;
            private readonly EntityId _materialId;
#else
            private readonly int _textureId;
            private readonly int _materialId;
#endif

            public MaterialKey(Material material, Texture2D texture, bool normalTexcoord)
            {
                Material = material;
                Texture = texture;
                NormalTexcoord = normalTexcoord;
#if UNITY_6000_0_OR_NEWER
                _materialId = material.GetEntityId();
                _textureId = texture.GetEntityId();
#else
                _materialId = material.GetInstanceID();
                _textureId = texture.GetInstanceID();
#endif
            }

            public bool Equals(MaterialKey other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return _textureId == other._textureId && _materialId == other._materialId &&
                    NormalTexcoord == other.NormalTexcoord;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((MaterialKey) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = _textureId.GetHashCode();
                    hashCode = (hashCode*397) ^ _materialId.GetHashCode();
                    return (hashCode*397) ^ NormalTexcoord.GetHashCode();
                }
            }

            public static bool operator ==(MaterialKey left, MaterialKey right)
            {
                return Equals(left, right);
            }

            public static bool operator !=(MaterialKey left, MaterialKey right)
            {
                return !Equals(left, right);
            }
        }
    }
}
