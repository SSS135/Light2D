using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Light2D
{
    /// <summary>
    /// Renders a Sprite through an owned Mesh and a cached Material instance.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public abstract class CustomSprite : MonoBehaviour
    {
        [Tooltip("Tint multiplied into the sprite's vertex colors (default white draws the sprite " +
                 "unmodified).")]
        public Color Color = Color.white;

        [Tooltip("Sprite whose geometry and texture are drawn through the owned mesh. Leave empty to " +
                 "draw nothing.")]
        public Sprite Sprite;

        [Tooltip("Draw order for the owned renderer; lower values draw first (default 0).")]
        public int SortingOrder;

        [Tooltip("Material used to draw the sprite. Leave empty to fall back to a generated " +
                 "Sprites/Default material.")]
        public Material Material;

        protected Color[] _colors = Array.Empty<Color>();
        protected Vector2[] _uv0 = Array.Empty<Vector2>();
        protected Vector2[] _uv1 = Array.Empty<Vector2>();
        protected Vector3[] _vertices = Array.Empty<Vector3>();
        protected int[] _triangles = Array.Empty<int>();
        protected Vector4[] _tangents = Array.Empty<Vector4>();
        protected bool _isMeshDirty;
        protected Mesh _mesh;
        private Material _generatedMaterial;
        private HdrVertex[] _hdrVertices = Array.Empty<HdrVertex>();

        private Color _oldColor;
        private Sprite _oldSprite;
        private Material _oldMaterial;
        private MaterialKey _oldMaterialKey;

        private static readonly Dictionary<MaterialKey, MaterialValue> MaterialMap =
            new Dictionary<MaterialKey, MaterialValue>();

        // Interleaved layout for the HDR vertex-color upload. Color is Float32 so overbright survives.
        private static readonly VertexAttributeDescriptor[] HdrVertexLayout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 2),
        };
        private static readonly HashSet<CustomSprite> LiveSprites = new HashSet<CustomSprite>();
        private static Material _defaultSpriteMaterial;
        private static Material _runtimeDefaultObstacleMaterial;

        private const string DefaultSpriteMaterialName = "Generated Default Sprite Material (DONT change it)";
        private const string GeneratedMaterialName = "Generated Material (DONT change it)";
        private const string GeneratedMeshName = "Generated Mesh (DONT change it)";

        // The owned mesh + generated material + world bounds the register-and-draw registry consumes. There is
        // no MeshRenderer/MeshFilter: geometry is drawn only through the draw-entry snapshot (CommandBuffer.DrawMesh),
        // so nothing renders into the game image and there is no renderer bounds to read for culling.
        internal Mesh Mesh { get { return _mesh; } }
        internal Material DrawMaterial { get { return _generatedMaterial; } }
        internal Bounds WorldBounds
        {
            get
            {
                return _mesh != null
                    ? Light2DGeometry.TransformBounds(_mesh.bounds, transform.localToWorldMatrix)
                    : new Bounds(transform.position, Vector3.zero);
            }
        }

        // Uniform scale baked into the copied sprite vertices so a component can size its mesh
        // independently of the Transform (used by LightSprite.Radius). Base sprites draw at 1:1.
        protected virtual float MeshScale { get { return 1f; } }

        // Maps a raw sprite vertex into the owned mesh's local space. Base sprites scale uniformly by
        // MeshScale; a light overrides this to reshape its quad per Type (anchor a Spot cone's apex at the
        // transform origin, or size a Line bar by Length/Throw) without touching this shared build path.
        protected virtual Vector3 TransformVertex(Vector3 vertex, float scale, Sprite sprite)
        {
            return vertex * scale;
        }

        // When true, the vertex color stream is uploaded as Float32 so overbright (> 1) HDR colors
        // survive to the light RT instead of clamping through the default Color32 path. Base sprites
        // (obstacles/ambient) stay on the cheap Color32 path.
        protected virtual bool UseHdrVertexColors { get { return false; } }

        // The sprite actually drawn through the owned mesh. Base sprites draw the assigned Sprite; a
        // light can substitute a procedurally generated falloff sprite when no Sprite is assigned.
        protected virtual Sprite ActiveSprite { get { return Sprite; } }

        protected virtual void OnEnable()
        {
            CreateMesh();
            LiveSprites.Add(this);
            UpdateMeshData(true);
        }

        protected virtual void OnDisable()
        {
            LiveSprites.Remove(this);
            ReleaseOwnedResources();
        }

        protected virtual void OnDestroy()
        {
            LiveSprites.Remove(this);
            ReleaseOwnedResources();
        }

        private void Update()
        {
            UpdateMeshData();
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled)
                UpdateMeshData(true);
        }

        private void CreateMesh()
        {
            _mesh = new Mesh
            {
                name = GeneratedMeshName,
                hideFlags = HideFlags.DontSave
            };
            _mesh.MarkDynamic();
        }

        private void ReleaseOwnedResources()
        {
            if (_mesh != null)
                Light2DObjectUtility.Destroy(_mesh);
            _mesh = null;

            ReleaseMaterialReference();
            _generatedMaterial = null;
        }

        private Material GetOrCreateMaterial()
        {
            ReleaseMaterialReference();
            var sprite = ActiveSprite;
            if (sprite == null)
                return null;

            var baseMaterial = Material != null ? Material : GetOrCreateDefaultSpriteMaterial();
            return AcquireMaterial(baseMaterial, sprite.texture, out _oldMaterialKey);
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

        // Build-safe last-resort obstacle material. Modeled on LightAmbientEmitter.GetRuntimeDefaultMaterial
        // but with the obstacle shader (not Sprites/Default), so obstacle components draw with correct light
        // absorption in player builds where the editor-only DualColor.mat asset load is unavailable. The
        // shader lives under Runtime/Resources/Shaders, so it is build-included and Shader.Find-able.
        internal static Material GetRuntimeDefaultObstacleMaterial()
        {
            if (_runtimeDefaultObstacleMaterial != null)
                return _runtimeDefaultObstacleMaterial;

            var shader = Shader.Find("Light2D/Transparent Dual Color Normal Mapped");
            if (shader == null)
                return null;

            _runtimeDefaultObstacleMaterial = new Material(shader)
            {
                name = "Generated Obstacle Default Material",
                hideFlags = HideFlags.HideAndDontSave
            };
            return _runtimeDefaultObstacleMaterial;
        }

        internal static Material AcquireMaterial(Material baseMaterial, Texture2D texture,
            out MaterialKey materialKey)
        {
            if (baseMaterial == null || texture == null)
            {
                materialKey = null;
                return null;
            }

            var key = materialKey = new MaterialKey(baseMaterial, texture);
            MaterialValue value;
            if (!MaterialMap.TryGetValue(key, out value) || value.Material == null)
            {
                MaterialMap.Remove(key);
                var material = new Material(baseMaterial)
                {
                    name = GeneratedMaterialName,
                    hideFlags = HideFlags.DontSave
                };
                ApplyMaterialState(material, baseMaterial, texture);
                value = new MaterialValue(material, baseMaterial.ComputeCRC());
                MaterialMap[key] = value;
            }
            else
            {
                value.UsageCount++;
                ApplyMaterialState(value.Material, baseMaterial, texture);
                value.SourceCrc = baseMaterial.ComputeCRC();
            }

            return value.Material;
        }

        internal static void RefreshMaterial(MaterialKey key)
        {
            MaterialValue value;
            if (key == null || !MaterialMap.TryGetValue(key, out value) || value.Material == null ||
                key.Material == null || key.Texture == null)
                return;

            // Change-detected live mirroring: per-frame callers skip the property copy (and the allocating
            // shaderKeywords getter) unless the serialized source material actually changed.
            var sourceCrc = key.Material.ComputeCRC();
            if (sourceCrc == value.SourceCrc)
                return;

            ApplyMaterialState(value.Material, key.Material, key.Texture);
            value.SourceCrc = sourceCrc;
        }

        private static void ApplyMaterialState(Material target, Material source, Texture2D texture)
        {
            target.CopyPropertiesFromMaterial(source);
            target.shaderKeywords = source.shaderKeywords;
            target.renderQueue = source.renderQueue;
            target.mainTexture = texture;
        }

        internal static void ReleaseMaterial(MaterialKey key)
        {
            MaterialValue value;
            if (key == null || !MaterialMap.TryGetValue(key, out value))
                return;

            value.UsageCount--;
            if (value.UsageCount > 0)
                return;

            Light2DObjectUtility.Destroy(value.Material);
            MaterialMap.Remove(key);
            TryReleaseDefaultSpriteMaterial();
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

            Light2DObjectUtility.Destroy(_defaultSpriteMaterial);
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
                Light2DObjectUtility.Destroy(value.Material);
            MaterialMap.Clear();
            Light2DObjectUtility.Destroy(_defaultSpriteMaterial);
            _defaultSpriteMaterial = null;

            foreach (var sprite in LiveSprites)
            {
                if (sprite == null)
                    continue;
                sprite._oldMaterialKey = null;
                sprite._oldMaterial = null;
                sprite._generatedMaterial = null;
                sprite.UpdateMeshData(true);
            }
        }

        private void ReleaseMaterialReference()
        {
            if (_oldMaterialKey != null)
                ReleaseMaterial(_oldMaterialKey);
            _oldMaterialKey = null;
        }

        protected virtual void UpdateColor()
        {
            for (var i = 0; i < _colors.Length; i++)
                _colors[i] = Color;
        }

        protected virtual void UpdateSprite()
        {
            ReleaseMaterialReference();
            var sprite = ActiveSprite;
            if (sprite == null)
            {
                _vertices = Array.Empty<Vector3>();
                _triangles = Array.Empty<int>();
                _uv0 = Array.Empty<Vector2>();
                ResizeVertexData(0);
                _generatedMaterial = null;
                return;
            }

            var spriteVertices = sprite.vertices;
            var spriteTriangles = sprite.triangles;
            var scale = MeshScale;
            _vertices = new Vector3[spriteVertices.Length];
            for (var i = 0; i < spriteVertices.Length; i++)
                _vertices[i] = TransformVertex(spriteVertices[i], scale, sprite);

            _triangles = new int[spriteTriangles.Length];
            for (var i = 0; i < spriteTriangles.Length; i++)
                _triangles[i] = spriteTriangles[i];

            _uv0 = sprite.uv;
            ResizeVertexData(_vertices.Length);
            _generatedMaterial = GetOrCreateMaterial();
        }

        private void ResizeVertexData(int vertexCount)
        {
            if (_colors.Length == vertexCount)
                return;

            _colors = new Color[vertexCount];
            _uv1 = new Vector2[vertexCount];
            _tangents = new Vector4[vertexCount];
            for (var i = 0; i < vertexCount; i++)
                _tangents[i] = new Vector4(1, 0, 0, 1);
        }

        protected virtual void UpdateMesh()
        {
            if (_mesh == null)
                return;

            if (UseHdrVertexColors)
            {
                UpdateMeshHdr();
                return;
            }

            _mesh.Clear();
            _mesh.vertices = _vertices;
            _mesh.triangles = _triangles;
            _mesh.uv = _uv0;
            _mesh.uv2 = _uv1;
            _mesh.colors = _colors;
            _mesh.tangents = _tangents;
        }

        // HDR path: the default Mesh.colors upload stores vertex colors as Color32 (UNorm8) and clamps
        // any channel > 1, silently discarding overbright HDR before it reaches the ARGBHalf light RT.
        // This declares an explicit interleaved vertex layout with a Float32 Color attribute and uploads
        // it manually so values > 1 survive. The light shader input is float4 COLOR0, so the stream is
        // carried unclamped through the vertex fetch.
        //
        // HOST-VALIDATION REQUIRED (top residual risk): confirm on the real host that this Float32 color
        // stream actually uploads unclamped AND that the half4 i.color interpolator carries > 1 through to
        // the ARGBHalf RT. If not, fall back to per-light <= 1 with cross-light accumulation.
        private void UpdateMeshHdr()
        {
            var vertexCount = _vertices.Length;
            _mesh.Clear();
            if (vertexCount == 0)
                return;

            if (_hdrVertices.Length != vertexCount)
                _hdrVertices = new HdrVertex[vertexCount];
            for (var i = 0; i < vertexCount; i++)
            {
                _hdrVertices[i].Position = _vertices[i];
                _hdrVertices[i].Tangent = _tangents[i];
                _hdrVertices[i].Color = _colors[i];
                _hdrVertices[i].Uv0 = _uv0[i];
                _hdrVertices[i].Uv1 = _uv1[i];
            }

            _mesh.SetVertexBufferParams(vertexCount, HdrVertexLayout);
            _mesh.SetVertexBufferData(_hdrVertices, 0, 0, vertexCount);
            _mesh.SetIndexBufferParams(_triangles.Length, IndexFormat.UInt32);
            _mesh.SetIndexBufferData(_triangles, 0, 0, _triangles.Length);
            _mesh.subMeshCount = 1;
            _mesh.SetSubMesh(0, new SubMeshDescriptor(0, _triangles.Length));
            _mesh.RecalculateBounds();
        }

        protected virtual void UpdateMeshData(bool forceUpdate = false)
        {
            if (_mesh == null)
                return;

            var activeSprite = ActiveSprite;
            var spriteChanged = activeSprite != _oldSprite;
            if (spriteChanged || Material != _oldMaterial || _generatedMaterial == null || forceUpdate)
            {
                UpdateSprite();
                _oldSprite = activeSprite;
                _oldMaterial = Material;
                _isMeshDirty = true;
            }

            if (spriteChanged || Color != _oldColor || forceUpdate)
            {
                UpdateColor();
                _oldColor = Color;
                _isMeshDirty = true;
            }

            RefreshMaterial(_oldMaterialKey);
            if (_isMeshDirty)
            {
                UpdateMesh();
                _isMeshDirty = false;
            }
        }

        // Sequential CPU layout matching HdrVertexLayout for the Float32 vertex-color upload path.
        [StructLayout(LayoutKind.Sequential)]
        private struct HdrVertex
        {
            public Vector3 Position;
            public Vector4 Tangent;
            public Color Color;
            public Vector2 Uv0;
            public Vector2 Uv1;
        }

        private sealed class MaterialValue
        {
            public readonly Material Material;
            public int UsageCount;
            public int SourceCrc;

            public MaterialValue(Material material, int sourceCrc)
            {
                Material = material;
                UsageCount = 1;
                SourceCrc = sourceCrc;
            }
        }

        internal sealed class MaterialKey : IEquatable<MaterialKey>
        {
            public readonly Texture2D Texture;
            public readonly Material Material;
            private readonly EntityId _textureId;
            private readonly EntityId _materialId;

            public MaterialKey(Material material, Texture2D texture)
            {
                Material = material;
                Texture = texture;
                _materialId = material.GetEntityId();
                _textureId = texture.GetEntityId();
            }

            public bool Equals(MaterialKey other)
            {
                return !ReferenceEquals(other, null) && _textureId == other._textureId &&
                    _materialId == other._materialId;
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as MaterialKey);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = _textureId.GetHashCode();
                    return (hashCode * 397) ^ _materialId.GetHashCode();
                }
            }
        }
    }
}
