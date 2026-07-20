#if LIGHT2D_TILEMAP
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Light2D
{
    /// <summary>
    /// Texture-aware Tilemap occluder. Emits one obstacle draw entry per source-texture group so the
    /// obstacle shader can sample each tile sprite's alpha (transparent tile pixels pass light). The
    /// sibling TilemapRenderer stays enabled and renders the visible tiles; this component owns no scene
    /// renderer, only in-memory group meshes drawn through the obstacle registry snapshot.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Tilemap))]
    [AddComponentMenu("Light2D/Light Obstacle Tilemap")]
    public class LightObstacleTilemap : MonoBehaviour, ILight2DObstacle, ILight2DMultiDrawable
    {
        // Default white: tiles are always textured, so the tile sprite alpha shapes the shadow and white
        // passes it through untinted (the textured-obstacle rule).
        [Tooltip("Per-vertex multiplicative color for every tile-group mesh; darkens light passing through " +
                 "(default white passes the tile sprite alpha through untinted).")]
        public Color32 MultiplicativeColor = new Color32(255, 255, 255, 255);

        [Tooltip("Additive obstacle color encoded into UV1; brightens light passing through the tiles " +
                 "(default transparent black adds nothing).")]
        public Color AdditiveColor;

        [Tooltip("Base obstacle material for the generated tile-group meshes. Leave empty to use the " +
                 "Light2D obstacle material.")]
        public Material Material;

        [Tooltip("LightingSystem that owns this occluder. Leave empty to draw for any system, or set it to " +
                 "scope the occluder to one system when several exist.")]
        public LightingSystem Owner;

        [Tooltip("Draw order within the obstacle pass; lower values draw first.")]
        public int SortingOrder;

        private Tilemap _tilemap;
        private bool _dirty;

        // Owned generated resources, one slot per source-texture group. Meshes are HideFlags.DontSave and
        // destroyed on release; materials are ref-counted through the shared CustomSprite cache by key.
        private readonly List<Mesh> _groupMeshes = new List<Mesh>();
        private readonly List<Material> _groupMaterials = new List<Material>();
        private readonly List<CustomSprite.MaterialKey> _groupKeys = new List<CustomSprite.MaterialKey>();

        private const string GeneratedMeshName = "Generated Light Obstacle Tilemap Mesh (DONT change it)";

        private void OnEnable()
        {
            _tilemap = GetComponent<Tilemap>();
#if UNITY_EDITOR
            if (Material == null)
                Material = LoadDefaultObstacleMaterial();
#endif
            Light2DRenderRegistry.RegisterObstacle(this);
            Tilemap.tilemapTileChanged += OnTilemapTileChanged;
            // Defer the initial build to the first GetDrawEntries so it runs on the snapshot thread.
            _dirty = true;
        }

        private void OnDisable()
        {
            Light2DRenderRegistry.DeregisterObstacle(this);
            Tilemap.tilemapTileChanged -= OnTilemapTileChanged;
            ReleaseGroups();
        }

        private void OnDestroy()
        {
            Light2DRenderRegistry.DeregisterObstacle(this);
            Tilemap.tilemapTileChanged -= OnTilemapTileChanged;
            ReleaseGroups();
        }

        private void OnValidate()
        {
            // Color/material edits rebuild on the next snapshot; no per-frame Update rebuild.
            if (isActiveAndEnabled)
                _dirty = true;
        }

#if UNITY_EDITOR
        // Seeds the texture-conditional obstacle defaults when the component is first added/reset in the
        // editor. Tiles are always textured, so the multiplicative default is the textured color.
        private void Reset()
        {
            var settings = Light2DObstacleSettings.GetOrCreate();
            MultiplicativeColor = settings.TexturedMultiplicativeColor;
            AdditiveColor = settings.AdditiveColor;
        }
#endif

        // Static event; coalesce the many callbacks a bulk tile edit fires into a single deferred rebuild.
        private void OnTilemapTileChanged(Tilemap changed, Tilemap.SyncTile[] _)
        {
            if (changed == _tilemap)
                _dirty = true;
        }

        // Multi producer: the registry drives GetDrawEntries instead of this single-entry path.
        bool ILight2DDrawable.TryGetDrawEntry(out Light2DDrawEntry entry)
        {
            entry = default;
            return false;
        }

        void ILight2DMultiDrawable.GetDrawEntries(List<Light2DDrawEntry> buffer)
        {
            if (!isActiveAndEnabled || _tilemap == null)
                return;
            if (Light2DObjectUtility.IsInPrefabStage(gameObject))
                return;

            if (_dirty)
            {
                Rebuild();
                _dirty = false;
            }

            var matrix = transform.localToWorldMatrix;
            for (var g = 0; g < _groupMeshes.Count; g++)
            {
                var mesh = _groupMeshes[g];
                var material = _groupMaterials[g];
                if (mesh == null || material == null)
                    continue;
                // Cell corners were built in tilemap-local space, so localToWorldMatrix places them once.
                buffer.Add(new Light2DDrawEntry(mesh, material, matrix, TransformBounds(mesh.bounds, matrix),
                    1, SortingOrder, Owner));
            }
        }

        private void Rebuild()
        {
            ReleaseGroups();
            if (_tilemap == null)
                return;

            _tilemap.CompressBounds();
            var bounds = _tilemap.cellBounds;
            var encodedAdditive = Light2DColorEncoding.EncodeAdditiveColorToUv(AdditiveColor);

            Dictionary<Texture2D, GroupBuilder> groups = null;
            foreach (var pos in bounds.allPositionsWithin)
            {
                if (!_tilemap.HasTile(pos))
                    continue;
                // Only collidable tiles occlude; background/decoration tiles pass light.
                if (_tilemap.GetColliderType(pos) == Tile.ColliderType.None)
                    continue;

                var sprite = _tilemap.GetSprite(pos);
                if (sprite == null || sprite.texture == null)
                    continue;

                var texture = sprite.texture;
                groups ??= new Dictionary<Texture2D, GroupBuilder>();
                if (!groups.TryGetValue(texture, out var group))
                {
                    group = new GroupBuilder();
                    groups.Add(texture, group);
                }

                var textureSize = new Vector2(texture.width, texture.height);
                var uvRect = sprite.textureRect;

                var start = group.Vertices.Count;
                group.Vertices.Add(_tilemap.CellToLocal(pos));                           // bottom-left
                group.Vertices.Add(_tilemap.CellToLocal(pos + new Vector3Int(1, 0, 0))); // bottom-right
                group.Vertices.Add(_tilemap.CellToLocal(pos + new Vector3Int(0, 1, 0))); // top-left
                group.Vertices.Add(_tilemap.CellToLocal(pos + new Vector3Int(1, 1, 0))); // top-right

                // uv0 maps the tile sprite's rect onto its texture so the obstacle shader samples the tile
                // alpha; this is what makes the occlusion texture-aware. Matches MapGenerator.CreatePoint.
                group.Uv0.Add(new Vector2(uvRect.xMin / textureSize.x, uvRect.yMin / textureSize.y));
                group.Uv0.Add(new Vector2(uvRect.xMax / textureSize.x, uvRect.yMin / textureSize.y));
                group.Uv0.Add(new Vector2(uvRect.xMin / textureSize.x, uvRect.yMax / textureSize.y));
                group.Uv0.Add(new Vector2(uvRect.xMax / textureSize.x, uvRect.yMax / textureSize.y));

                for (var i = 0; i < 4; i++)
                {
                    group.Colors.Add(MultiplicativeColor);
                    group.Uv2.Add(encodedAdditive);
                }

                // Winding matches MapGenerator.CreatePoint's obstacle quads.
                group.Triangles.Add(start + 2);
                group.Triangles.Add(start + 1);
                group.Triangles.Add(start);
                group.Triangles.Add(start + 1);
                group.Triangles.Add(start + 2);
                group.Triangles.Add(start + 3);
            }

            if (groups == null)
                return;

            var baseMaterial = ResolveBaseMaterial();
            foreach (var pair in groups)
            {
                var material = CustomSprite.AcquireMaterial(baseMaterial, pair.Key, false, out var key);
                if (material == null)
                    continue;

                var group = pair.Value;
                var mesh = new Mesh
                {
                    name = GeneratedMeshName,
                    hideFlags = HideFlags.DontSave
                };
                mesh.SetVertices(group.Vertices);
                mesh.SetUVs(0, group.Uv0);
                mesh.SetColors(group.Colors);
                mesh.SetUVs(1, group.Uv2);
                mesh.SetTriangles(group.Triangles, 0);
                mesh.RecalculateBounds();

                _groupMeshes.Add(mesh);
                _groupMaterials.Add(material);
                _groupKeys.Add(key);
            }
        }

        private void ReleaseGroups()
        {
            for (var i = 0; i < _groupMeshes.Count; i++)
                Light2DObjectUtility.Destroy(_groupMeshes[i]);
            _groupMeshes.Clear();

            for (var i = 0; i < _groupKeys.Count; i++)
                CustomSprite.ReleaseMaterial(_groupKeys[i]);
            _groupKeys.Clear();

            // Never destroy the base material or the group materials themselves; the CustomSprite cache
            // owns the generated clones and frees them when the key's ref count reaches zero.
            _groupMaterials.Clear();
        }

        private Material ResolveBaseMaterial()
        {
            if (Material != null)
                return Material;
#if UNITY_EDITOR
            return LoadDefaultObstacleMaterial();
#else
            // B.4: without this, a player build returned null here, AcquireMaterial(null, ...) yielded null,
            // every tile group was skipped, and the tilemap produced NO obstacle geometry in builds.
            return CustomSprite.GetRuntimeDefaultObstacleMaterial();
#endif
        }

        // Conservative world-space AABB for one group's local bounds; per-group bounds give per-group cull.
        private static Bounds TransformBounds(Bounds local, Matrix4x4 matrix)
        {
            var center = matrix.MultiplyPoint3x4(local.center);
            var extents = local.extents;
            var axisX = matrix.MultiplyVector(new Vector3(extents.x, 0f, 0f));
            var axisY = matrix.MultiplyVector(new Vector3(0f, extents.y, 0f));
            var axisZ = matrix.MultiplyVector(new Vector3(0f, 0f, extents.z));
            var worldExtents = new Vector3(
                Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
                Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
                Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));
            return new Bounds(center, worldExtents * 2f);
        }

#if UNITY_EDITOR
        private static Material LoadDefaultObstacleMaterial()
        {
            var path = AssetDatabase.GUIDToAssetPath("0a8c132f813f5514b9f5b48ff3956482");
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }
#endif

        // Per-source-texture scratch accumulated during a rebuild, then baked into one single-submesh mesh.
        private sealed class GroupBuilder
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector2> Uv0 = new List<Vector2>();
            public readonly List<Color32> Colors = new List<Color32>();
            public readonly List<Vector2> Uv2 = new List<Vector2>();
            public readonly List<int> Triangles = new List<int>();
        }
    }
}
#endif
