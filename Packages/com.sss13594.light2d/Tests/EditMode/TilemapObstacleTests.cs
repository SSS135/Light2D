#if LIGHT2D_TILEMAP
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Light2D.Tests
{
    // Unit coverage for the texture-aware Tilemap occluder. Gated on LIGHT2D_TILEMAP: the component only
    // exists when com.unity.2d.tilemap is installed, so these tests compile and run only on that host.
    public class TilemapObstacleTests
    {
        private readonly List<Object> _disposables = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            // GameObjects are added before their assets, so destroying in insertion order runs the
            // occluder's OnDisable (releasing its generated group meshes/materials) before the tiles,
            // sprites and textures those groups referenced are torn down.
            for (var i = 0; i < _disposables.Count; i++)
                if (_disposables[i] != null)
                    Object.DestroyImmediate(_disposables[i]);
            _disposables.Clear();
        }

        [Test]
        public void EligibleTilesDetermineQuadCountAndPerTextureGrouping()
        {
            var (tilemap, _) = CreateTilemap();
            var textureA = CreateTexture();
            var textureB = CreateTexture();

            // textureA: two collidable tiles that share a texture, plus one non-collidable tile that must
            // be excluded from the occlusion geometry. textureB: a single collidable tile.
            var tileA1 = CreateTile(textureA, Tile.ColliderType.Sprite);
            var tileA2 = CreateTile(textureA, Tile.ColliderType.Sprite);
            var tileANonCollidable = CreateTile(textureA, Tile.ColliderType.None);
            var tileB = CreateTile(textureB, Tile.ColliderType.Sprite);

            tilemap.SetTile(new Vector3Int(0, 0, 0), tileA1);
            tilemap.SetTile(new Vector3Int(1, 0, 0), tileA2);
            tilemap.SetTile(new Vector3Int(2, 0, 0), tileB);
            tilemap.SetTile(new Vector3Int(3, 0, 0), tileANonCollidable);

            var mine = EntriesFor(Snapshot(), textureA, textureB);

            // Per-texture grouping: two distinct textures among the collidable tiles => two entries.
            Assert.That(mine.Count, Is.EqualTo(2));

            // Eligible-tile => quad count: three collidable tiles => three quads => 18 triangle indices
            // (6 per quad); the non-collidable tile contributes nothing.
            var totalIndices = mine.Sum(entry => entry.Mesh.triangles.Length);
            Assert.That(totalIndices / 6, Is.EqualTo(3));

            var entryA = mine.Single(entry => entry.Material.mainTexture == textureA);
            var entryB = mine.Single(entry => entry.Material.mainTexture == textureB);

            // Shared texture => one entry carrying two quads; the other texture => one entry, one quad.
            Assert.That(entryA.Mesh.triangles.Length / 6, Is.EqualTo(2));
            Assert.That(entryB.Mesh.triangles.Length / 6, Is.EqualTo(1));

            // Texture pass-through: each group's generated material samples its own group's tile texture,
            // which is how transparent tile pixels pass light through the obstacle shader.
            Assert.That(entryA.Material.mainTexture, Is.SameAs(textureA));
            Assert.That(entryB.Material.mainTexture, Is.SameAs(textureB));
        }

        [Test]
        public void OccluderRegistersWhileEnabledAndDeregistersWhenDisabled()
        {
            var (_, occluder) = CreateTilemap();

            Assert.That(Light2DRenderRegistry.ContainsObstacle(occluder), Is.True);
            occluder.enabled = false;
            Assert.That(Light2DRenderRegistry.ContainsObstacle(occluder), Is.False);
            occluder.enabled = true;
            Assert.That(Light2DRenderRegistry.ContainsObstacle(occluder), Is.True);
        }

        [Test]
        public void SnapshotRebuildsWhenTilesChange()
        {
            var (tilemap, _) = CreateTilemap();
            var texture = CreateTexture();
            var tile = CreateTile(texture, Tile.ColliderType.Sprite);

            tilemap.SetTile(new Vector3Int(0, 0, 0), tile);
            tilemap.SetTile(new Vector3Int(1, 0, 0), tile);

            var before = EntriesFor(Snapshot(), texture);
            Assert.That(before.Sum(entry => entry.Mesh.triangles.Length) / 6, Is.EqualTo(2));

            // Adding a tile fires Tilemap.tilemapTileChanged, which marks the occluder dirty; the next
            // snapshot must rebuild to the new eligible-cell count rather than return a stale cached mesh.
            tilemap.SetTile(new Vector3Int(2, 0, 0), tile);

            var after = EntriesFor(Snapshot(), texture);
            Assert.That(after.Sum(entry => entry.Mesh.triangles.Length) / 6, Is.EqualTo(3));
        }

        private (Tilemap tilemap, LightObstacleTilemap occluder) CreateTilemap()
        {
            var gridObject = new GameObject("Grid", typeof(Grid));
            _disposables.Add(gridObject);

            var tilemapObject = new GameObject("Tilemap", typeof(Tilemap), typeof(TilemapRenderer));
            tilemapObject.transform.SetParent(gridObject.transform, false);

            var occluder = tilemapObject.AddComponent<LightObstacleTilemap>();
            return (tilemapObject.GetComponent<Tilemap>(), occluder);
        }

        private Texture2D CreateTexture()
        {
            var texture = new Texture2D(4, 4);
            _disposables.Add(texture);
            return texture;
        }

        private Tile CreateTile(Texture2D texture, Tile.ColliderType colliderType)
        {
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * 0.5f);
            _disposables.Add(sprite);

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.colliderType = colliderType;
            _disposables.Add(tile);
            return tile;
        }

        private static List<Light2DDrawEntry> Snapshot()
        {
            var snapshot = new List<Light2DDrawEntry>();
            Light2DRenderRegistry.PrepareObstacleSnapshot(snapshot, default(Light2DCullContext));
            return snapshot;
        }

        // Isolates this test's entries from any other obstacle in the shared registry by the unique group
        // textures created here (each generated group material samples its group's tile texture).
        private static List<Light2DDrawEntry> EntriesFor(List<Light2DDrawEntry> snapshot, params Texture2D[] textures)
        {
            var set = new HashSet<Texture>(textures);
            return snapshot
                .Where(entry => entry.Material != null && set.Contains(entry.Material.mainTexture))
                .ToList();
        }
    }
}
#endif
