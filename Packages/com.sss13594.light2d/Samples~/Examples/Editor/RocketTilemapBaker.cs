#if LIGHT2D_TILEMAP
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Light2D.Examples
{
    /// <summary>
    /// Editor bake that authors the PLAYABLE "Rocket Tilemap" sample scene. It starts from a copy of the
    /// procedural Rocket scene (keeping the flyable Spacecraft, its controls, camera-follow, touch UI and
    /// the ship-carried light), removes MapGenerator's runtime terrain, and drops a fixed Tilemap cave in
    /// its place. The cave REUSES the four existing Tile assets under Examples/Tiles (matched by sprite +
    /// collider type, never recreated, so their .meta GUIDs are preserved) and reproduces MapGenerator's
    /// deterministic block field for a fixed seed over a fixed window (mirroring SampleTestSupport.MapProbe's
    /// exact selection). The visible TilemapRenderer stays enabled (OQ-T1): it draws the dark cave while
    /// LightObstacleTilemap emits the texture-aware occluder as draw entries only. A TilemapCollider2D +
    /// static Rigidbody2D + CompositeCollider2D give the ship solid cave walls (Tile.ColliderType.Sprite
    /// walls occlude AND collide; Background tiles pass light and do not collide). The ship spawns in an
    /// open pocket (never inside a wall), and the Main Camera (with its LightingSystem + CameraFollower) is
    /// reframed on that pocket, lit by a bright warm central light plus two distinctly coloured crystal
    /// lights. The camera keeps Rocket's nebula skybox background (inherited from the source scene), so
    /// empty cave/void cells show the nebula through the cave exactly as the Rocket reference does.
    ///
    /// The output scene (+ meta) is written into the imported sample tree and mirrored byte-identically into
    /// Samples~/Examples so both trees match. The Tiles assets are untouched by the bake and stay identical
    /// in both trees.
    ///
    /// Run headlessly with:
    ///   -executeMethod Light2D.Examples.RocketTilemapBaker.Bake
    /// This whole file is gated on LIGHT2D_TILEMAP, which the sample Editor asmdef defines only when
    /// com.unity.2d.tilemap is installed.
    /// </summary>
    internal static class RocketTilemapBaker
    {
        // Fixed inputs. The window is ~5x5 screens of cells (at orthographic size 15, 16:9, one screen
        // is ~53x30 cells). Only the block SELECTION is reproduced faithfully; the per-cell sprite pick
        // is a deterministic hash (equivalent look, not RNG-lockstep) per R-T3.
        private const int Seed = 20240720;
        private const int SpriteSalt = 0x2f6d1c3b;
        private const int WindowHalfWidth = 132;
        private const int WindowHalfHeight = 75;

        // Capture/framing geometry matches the smoke test target (640x360 -> 16:9) at the scene-default
        // orthographic size 15, so a rock-dense framing chosen here lines up with what the test reads.
        private const float OrthographicSize = 15f;
        private const float ViewHalfHeight = OrthographicSize;
        private const float ViewHalfWidth = OrthographicSize * (640f / 360f);

        // Target framed wall fraction: dense enough for a dark cave, open enough that the lights carve
        // lit pockets (a pure-max-density frame would bury the lights and undershoot the mean-luminance
        // floor). Balanced value; a verify-time fix pass may retune it against the captured statistics.
        private const float TargetWallFraction = 0.6f;

        // The ship spawn pocket must be a clear disk of open (non-colliding) cells so the articulated craft
        // (hull + deployed legs, scaled 0.75) never overlaps a wall. Larger disks are preferred; the search
        // relaxes only if the dense cave has no room for one.
        private static readonly int[] SpawnPocketRadii = { 4, 3, 2 };

        // Package default obstacle material (Runtime/Materials/DualColor.mat) — the material
        // LightObstacleTilemap loads by default; assigned explicitly so the serialized scene is
        // deterministic and works outside the editor default-load path.
        private const string ObstacleMaterialGuid = "0a8c132f813f5514b9f5b48ff3956482";
        private const string BlockSetProfileGuid = "99b2aa50cb51ad9409451b6056775bd6";
        private const string LightingCrystalsPrefabGuid = "9f10243117669f046af3db5fc661dc8d";
        private const string PointLightPrefabGuid = "f1f87948c09d89b48acf75b3f771eece";

        private const string PackageName = "com.sss13594.light2d";
        private const string SceneFileName = "Rocket Tilemap.unity";
        private const string SourceSceneFileName = "Rocket.unity";
        private const string TilesFolderName = "Tiles";

        public static void Bake()
        {
            try
            {
                BakeInternal();
            }
            catch (Exception exception)
            {
                Debug.LogError("Rocket Tilemap bake failed: " + exception);
                throw;
            }
        }

        private static void BakeInternal()
        {
            var blockSetPath = AssetDatabase.GUIDToAssetPath(BlockSetProfileGuid);
            var separatorIndex = string.IsNullOrEmpty(blockSetPath)
                ? -1
                : blockSetPath.IndexOf("/Resources/", StringComparison.Ordinal);
            if (separatorIndex < 0)
                throw new InvalidOperationException(
                    "Could not locate the imported sample Examples root from the BlockSetProfile asset. " +
                    "Import the Light2D 'Examples' sample before baking.");

            var examplesRoot = blockSetPath.Substring(0, separatorIndex);
            var blockSet = AssetDatabase.LoadAssetAtPath<BlockSetProfile>(blockSetPath);
            if (blockSet == null)
                throw new InvalidOperationException($"Failed to load BlockSetProfile at '{blockSetPath}'.");

            var obstacleMaterial = LoadRequired<Material>(ObstacleMaterialGuid, "obstacle material");
            var crystalPrefab = LoadRequired<GameObject>(LightingCrystalsPrefabGuid, "LightingCrystals prefab");
            var pointLightPrefab = LoadRequired<GameObject>(PointLightPrefabGuid, "PointLight prefab");

            var sourceScenePath = examplesRoot + "/" + SourceSceneFileName;
            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(sourceScenePath)))
                throw new InvalidOperationException(
                    $"Source scene '{sourceScenePath}' is missing; import the Light2D 'Examples' sample first.");

            // 0. Load the four existing Tile assets (Wall_*/Background_*). They are REUSED by reference so
            //    their .meta GUIDs never churn; the bake never creates or deletes tile assets.
            var tilesFolderAsset = examplesRoot + "/" + TilesFolderName;
            var wallTiles = new Dictionary<Sprite, Tile>();
            var backgroundTiles = new Dictionary<Sprite, Tile>();
            LoadExistingTiles(tilesFolderAsset, wallTiles, backgroundTiles);

            // 1. Reproduce the deterministic block field for the fixed seed over the window.
            var seedRandom = new SimpleRNG(Seed);
            var firstAddX = (seedRandom.value - 0.5f) * 1000f;
            var firstAddY = (seedRandom.value - 0.5f) * 10000f;
            var secondAddX = (seedRandom.value - 0.5f) * 1000f;
            var secondAddY = (seedRandom.value - 0.5f) * 1000f;

            var width = 2 * WindowHalfWidth + 1;
            var height = 2 * WindowHalfHeight + 1;
            var isWall = new bool[width, height];
            var positions = new List<Vector3Int>(width * height);
            var placedTiles = new List<TileBase>(width * height);

            for (var ix = 0; ix < width; ix++)
            {
                var x = ix - WindowHalfWidth;
                for (var iy = 0; iy < height; iy++)
                {
                    var y = iy - WindowHalfHeight;
                    var noise = GetNoise(blockSet, firstAddX, firstAddY, secondAddX, secondAddY, x, y);
                    var info = SelectBlockInfo(blockSet, noise, x, y);
                    if (info == null || info.BlockType == BlockSetProfile.BlockType.Empty)
                        continue; // Empty cells are transparent sky pockets; no tile.

                    var colliding = info.BlockType == BlockSetProfile.BlockType.CollidingWall;
                    isWall[ix, iy] = colliding;

                    var sprite = PickSprite(info, x, y);
                    if (sprite == null)
                        continue;

                    var tile = ResolveTile(colliding ? wallTiles : backgroundTiles, sprite, colliding);
                    positions.Add(new Vector3Int(x, y, 0));
                    placedTiles.Add(tile);
                }
            }

            if (placedTiles.Count == 0)
                throw new InvalidOperationException("The block field produced no tiles; nothing to bake.");

            // 2. Pick the ship's spawn: an open pocket (clear disk of non-wall cells) whose framed view is a
            //    dark, rock-dense cave. The camera and the central light both frame this pocket.
            FindSpawnPocket(isWall, out var spawnX, out var spawnY, out var spawnRadius);
            Debug.Log($"Rocket Tilemap spawn pocket: cell ({spawnX}, {spawnY}) with clear radius {spawnRadius}.");

            // 3. Start from the procedural Rocket scene, then swap terrain for the fixed cave.
            var scene = EditorSceneManager.OpenScene(sourceScenePath, OpenSceneMode.Single);

            var lightingSystem = UnityEngine.Object.FindFirstObjectByType<LightingSystem>();
            if (lightingSystem == null)
                throw new InvalidOperationException("The Rocket scene has no LightingSystem to reuse.");
            var mainCamera = lightingSystem.GetComponent<Camera>();
            if (mainCamera == null)
                throw new InvalidOperationException("The Rocket LightingSystem has no Camera on its GameObject.");
            var spacecraft = UnityEngine.Object.FindFirstObjectByType<Spacecraft>();
            if (spacecraft == null || spacecraft.MainRigidbody == null)
                throw new InvalidOperationException("The Rocket scene has no Spacecraft with a MainRigidbody.");

            // Remove only the procedural terrain generator: MapGenerator is a component on "Globals"
            // (shared with InputManager, which must stay), and it fills the "StaticObjects" Container at
            // runtime. Delete the component and the empty Container root; keep everything else.
            RemoveProceduralTerrain();

            var spawnWorld = new Vector2(spawnX + 0.5f, spawnY + 0.5f);
            ReframeCamera(mainCamera, spawnWorld);
            RepositionSpacecraft(spacecraft, spawnWorld);

            BuildTilemap(scene, positions, placedTiles, obstacleMaterial, lightingSystem);
            SpawnLights(scene, crystalPrefab, pointLightPrefab, isWall, spawnX, spawnY, lightingSystem);

            EditorSceneManager.MarkSceneDirty(scene);
            var sceneAsset = examplesRoot + "/" + SceneFileName;
            // Save-As to the tilemap path: writes the tilemap scene and leaves the source Rocket.unity
            // untouched. The existing tilemap scene asset (and its .meta GUID) is overwritten in place.
            if (!EditorSceneManager.SaveScene(scene, sceneAsset))
                throw new InvalidOperationException($"Failed to save the baked scene to '{sceneAsset}'.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // 4. Normalize the serialized scene to the repo convention (trailing whitespace stripped,
            //    LF-only, trailing newline) so this machine-generated scene matches the hand-maintained
            //    sample scenes and passes git's whitespace checks.
            NormalizeSerializedScene(Path.GetFullPath(examplesRoot + "/" + SceneFileName));

            // 5. Mirror the scene (+meta) into the package Samples~ source, byte-identical. The Tiles assets
            //    are unchanged by the bake, so they are not touched and stay identical in both trees.
            MirrorSceneToPackage(examplesRoot);

            Debug.Log($"Baked playable '{SceneFileName}' with {placedTiles.Count} tiles, ship spawned at " +
                      $"({spawnWorld.x}, {spawnWorld.y}); mirrored to Samples~/Examples.");
        }

        // Loads every Tile asset already present under Examples/Tiles and indexes it by its sprite, split by
        // collider type. The bake reuses these (never recreates them) so the four Tile .meta GUIDs persist.
        private static void LoadExistingTiles(string tilesFolderAsset, Dictionary<Sprite, Tile> wallTiles,
            Dictionary<Sprite, Tile> backgroundTiles)
        {
            if (!AssetDatabase.IsValidFolder(tilesFolderAsset))
                throw new InvalidOperationException(
                    $"Tiles folder '{tilesFolderAsset}' is missing; the baked Tile assets are required.");

            foreach (var guid in AssetDatabase.FindAssets("t:Tile", new[] { tilesFolderAsset }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
                if (tile == null || tile.sprite == null)
                    continue;

                if (tile.colliderType == Tile.ColliderType.Sprite)
                    wallTiles[tile.sprite] = tile;
                else
                    backgroundTiles[tile.sprite] = tile;
            }

            if (wallTiles.Count == 0 || backgroundTiles.Count == 0)
                throw new InvalidOperationException(
                    $"Tiles folder '{tilesFolderAsset}' did not contain both wall and background Tile assets.");
        }

        private static Tile ResolveTile(Dictionary<Sprite, Tile> tiles, Sprite sprite, bool colliding)
        {
            if (tiles.TryGetValue(sprite, out var tile))
                return tile;

            throw new InvalidOperationException(
                $"No existing {(colliding ? "wall" : "background")} Tile asset reuses sprite '{sprite.name}'. " +
                "The Tiles folder is out of sync with the block field; re-bake the Tile assets first.");
        }

        // Reproduces MapGenerator.GetNoise for the live seed.
        private static float GetNoise(BlockSetProfile blockSet, float firstAddX, float firstAddY,
            float secondAddX, float secondAddY, int x, int y)
        {
            var noise1 = (Noise.Generate(
                x * blockSet.FirstNoiseScale + firstAddX,
                y * blockSet.FirstNoiseScale + firstAddY) - 0.5f) * 2f;
            var noise2 = (Noise.Generate(
                x * blockSet.SecondNoiseScale + secondAddX,
                y * blockSet.SecondNoiseScale + secondAddY) - 0.5f) * 2f;
            return Mathf.Clamp01((noise1 + noise2 * blockSet.SecondNoiseMul) / 2f + 0.5f);
        }

        // Exact replica of MapGenerator.SelectBlockInfo's weighted per-cell selection (mirrors MapProbe).
        private static BlockSetProfile.BlockInfo SelectBlockInfo(BlockSetProfile blockSet, float noise, int x, int y)
        {
            var rand = new SimpleRNG(Hash(Seed, x, y));
            BlockSetProfile.BlockInfo selected = null;
            var totalWeight = 0;
            foreach (var candidate in blockSet.BlockInfos)
            {
                if (candidate.MinNoise > noise || candidate.MaxNoise < noise)
                    continue;

                var weight = Mathf.Max(0, candidate.Weight);
                totalWeight += weight;
                if (weight > 0 && rand.Range(totalWeight) >= totalWeight - weight)
                    selected = candidate;
            }

            return selected;
        }

        private static Sprite PickSprite(BlockSetProfile.BlockInfo info, int x, int y)
        {
            var sprites = info.SpriteInfo;
            if (sprites == null || sprites.Length == 0)
                return null;

            var index = ((Hash(Seed ^ SpriteSalt, x, y) % sprites.Length) + sprites.Length) % sprites.Length;
            if (sprites[index] != null)
                return sprites[index];
            foreach (var candidate in sprites)
                if (candidate != null)
                    return candidate;
            return null;
        }

        // Inlined copy of SampleUtilities.Hash (internal to the samples runtime assembly), matching the
        // exact arithmetic MapGenerator seeds its per-cell SimpleRNG with.
        private static int Hash(int first, int second, int third)
        {
            unchecked
            {
                var hash = 23;
                hash = hash * 31 + first;
                hash = hash * 31 + second;
                return hash * 31 + third;
            }
        }

        // Chooses the ship spawn: the largest available clear disk of non-wall cells whose framed view is
        // closest to TargetWallFraction (a dark, rock-dense cave). The disk keeps the articulated craft off
        // the walls; the framing keeps the shot dark for the mean-luminance gate.
        private static void FindSpawnPocket(bool[,] isWall, out int spawnX, out int spawnY, out int spawnRadius)
        {
            foreach (var radius in SpawnPocketRadii)
            {
                if (TryFindOpenPocket(isWall, radius, out spawnX, out spawnY))
                {
                    spawnRadius = radius;
                    return;
                }
            }

            throw new InvalidOperationException(
                "The block field has no open spawn pocket; the cave is too dense to place the ship.");
        }

        private static bool TryFindOpenPocket(bool[,] isWall, int clearRadius, out int px, out int py)
        {
            var scanHalfWidth = WindowHalfWidth - Mathf.CeilToInt(ViewHalfWidth) - 2;
            var scanHalfHeight = WindowHalfHeight - Mathf.CeilToInt(ViewHalfHeight) - 2;
            px = 0;
            py = 0;
            var found = false;
            var bestDelta = float.MaxValue;
            for (var y = -scanHalfHeight; y <= scanHalfHeight; y++)
            {
                for (var x = -scanHalfWidth; x <= scanHalfWidth; x++)
                {
                    if (!IsClearDisk(isWall, x, y, clearRadius))
                        continue;

                    var fraction = FramedWallFraction(isWall, x, y);
                    var delta = Mathf.Abs(fraction - TargetWallFraction);
                    if (delta < bestDelta)
                    {
                        bestDelta = delta;
                        px = x;
                        py = y;
                        found = true;
                    }
                }
            }

            return found;
        }

        private static bool IsClearDisk(bool[,] isWall, int centreX, int centreY, int radius)
        {
            var radiusSquared = radius * radius;
            for (var dy = -radius; dy <= radius; dy++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dy * dy > radiusSquared)
                        continue;
                    if (WallAt(isWall, centreX + dx, centreY + dy))
                        return false;
                }
            }

            return true;
        }

        private static float FramedWallFraction(bool[,] isWall, int centreX, int centreY)
        {
            var halfWidth = Mathf.CeilToInt(ViewHalfWidth);
            var halfHeight = Mathf.CeilToInt(ViewHalfHeight);
            var total = 0;
            var walls = 0;
            for (var dy = -halfHeight; dy <= halfHeight; dy += 3)
            {
                for (var dx = -halfWidth; dx <= halfWidth; dx += 3)
                {
                    total++;
                    if (WallAt(isWall, centreX + dx, centreY + dy))
                        walls++;
                }
            }

            return total == 0 ? 0f : walls / (float)total;
        }

        private static bool WallAt(bool[,] isWall, int x, int y)
        {
            var ix = x + WindowHalfWidth;
            var iy = y + WindowHalfHeight;
            if (ix < 0 || iy < 0 || ix >= isWall.GetLength(0) || iy >= isWall.GetLength(1))
                return false;
            return isWall[ix, iy];
        }

        // Removes MapGenerator's runtime terrain: the MapGenerator component (not its "Globals" host, which
        // also carries InputManager) and the empty "StaticObjects" Container root it fills at runtime.
        private static void RemoveProceduralTerrain()
        {
            var generator = UnityEngine.Object.FindFirstObjectByType<MapGenerator>();
            if (generator == null)
                return;

            var container = generator.Container;
            UnityEngine.Object.DestroyImmediate(generator);
            if (container != null)
                UnityEngine.Object.DestroyImmediate(container.gameObject);
        }

        // Keeps Rocket's Main Camera (LightingSystem + CameraFollower + AudioListener) and its nebula
        // skybox background, only reframing it on the spawn pocket. The Skybox clear flags, background
        // colour and the per-camera Skybox component are inherited unchanged from the Rocket source scene,
        // so the nebula shows through the cave's open cells just as it does in the Rocket reference.
        private static void ReframeCamera(Camera camera, Vector2 spawnWorld)
        {
            camera.orthographic = true;
            camera.orthographicSize = OrthographicSize;
            var cameraTransform = camera.transform;
            var position = cameraTransform.position;
            cameraTransform.position = new Vector3(spawnWorld.x, spawnWorld.y, position.z);
        }

        // Moves the whole articulated craft so its hull (MainRigidbody) sits at the spawn pocket centre,
        // keeping its rotation, scale and internal joint layout intact.
        private static void RepositionSpacecraft(Spacecraft spacecraft, Vector2 spawnWorld)
        {
            var hullPosition = spacecraft.MainRigidbody.transform.position;
            var delta = new Vector3(spawnWorld.x - hullPosition.x, spawnWorld.y - hullPosition.y, 0f);
            spacecraft.transform.position += delta;
        }

        private static void BuildTilemap(Scene scene, List<Vector3Int> positions, List<TileBase> tiles,
            Material obstacleMaterial, LightingSystem lightingSystem)
        {
            var gridObject = new GameObject("Grid");
            SceneManager.MoveGameObjectToScene(gridObject, scene);
            gridObject.AddComponent<Grid>();

            var tilemapObject = new GameObject("Tilemap");
            tilemapObject.transform.SetParent(gridObject.transform, false);
            tilemapObject.layer = 0; // Visible to the source camera (the dark cave).

            var tilemap = tilemapObject.AddComponent<Tilemap>();
            tilemapObject.AddComponent<TilemapRenderer>(); // Enabled: renders the visible tiles (OQ-T1).
            tilemap.SetTiles(positions.ToArray(), tiles.ToArray());
            tilemap.CompressBounds();

            // Solid cave walls for the ship: a static Rigidbody2D plus a TilemapCollider2D merged into a
            // CompositeCollider2D. Only Tile.ColliderType.Sprite (wall) tiles contribute shapes; Background
            // tiles are ColliderType.None and stay pass-through, mirroring MapGenerator's colliding walls.
            var rigidbody = tilemapObject.AddComponent<Rigidbody2D>();
            rigidbody.bodyType = RigidbodyType2D.Static;
            var tilemapCollider = tilemapObject.AddComponent<TilemapCollider2D>();
            var composite = tilemapObject.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            tilemapCollider.compositeOperation = Collider2D.CompositeOperation.Merge;

            // The obstacle companion owns no scene renderer; it emits the texture-aware occluder as
            // draw entries into the obstacle snapshot only.
            var obstacle = tilemapObject.AddComponent<LightObstacleTilemap>();
            obstacle.MultiplicativeColor = new Color32(255, 255, 255, 255);
            obstacle.AdditiveColor = new Color(0f, 0f, 0f, 0f);
            obstacle.Material = obstacleMaterial;
            obstacle.Owner = lightingSystem;
            obstacle.SortingOrder = 0;
        }

        // A bright warm central light on the spawn pocket plus two distinctly coloured crystal lights offset
        // into nearby open cells satisfy the gate's central-peak and >=2-hue-bucket requirements and keep the
        // ship's spawn area visible. Radii are sized well above the source prefabs' natural LightSprite scales
        // (PointLight authored at 35, LightingCrystals' Light child at 20) so three static lights deliver
        // enough energy for the dark-cave mean-luminance floor against the dense, texture-occluding tilemap.
        // The values match the tuning verified after the Built-in vertical-flip fix (central 165, crystals
        // 120): they lift the corrected render into the [0.015, 0.2] band by lighting more occluding rock
        // faces, so the bright-core fraction stays small. Retune the radii here (never the gate) if verify
        // shows the mean drifting out of band.
        private static void SpawnLights(Scene scene, GameObject crystalPrefab, GameObject pointLightPrefab,
            bool[,] isWall, int spawnX, int spawnY, LightingSystem lightingSystem)
        {
            var central = NearestOpenCell(isWall, spawnX, spawnY);
            SpawnLight(scene, pointLightPrefab, central, new Color(1f, 0.82f, 0.5f, 1f), 165f,
                lightingSystem, "Central Light");

            var crystalOne = NearestOpenCell(isWall, spawnX + 6, spawnY + 3);
            SpawnLight(scene, crystalPrefab, crystalOne, new Color(0.25f, 0.8f, 1f, 0.95f), 120f,
                lightingSystem, "Crystal Light Cyan");

            var crystalTwo = NearestOpenCell(isWall, spawnX - 6, spawnY - 3);
            SpawnLight(scene, crystalPrefab, crystalTwo, new Color(1f, 0.25f, 0.7f, 0.95f), 120f,
                lightingSystem, "Crystal Light Magenta");
        }

        private static void SpawnLight(Scene scene, GameObject prefab, Vector2Int cell, Color color, float radius,
            LightingSystem lightingSystem, string name)
        {
            if (prefab == null)
                return;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = name;
            instance.transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);

            // LightingCrystals randomise their colour/scale on Start; disable that so the baked hues are
            // deterministic. PointLight has no randomiser (the lookup is simply null there).
            var randomizer = instance.GetComponentInChildren<LightRandomizer>(true);
            if (randomizer != null)
                randomizer.enabled = false;

            var light = instance.GetComponentInChildren<LightSprite>(true);
            if (light != null)
            {
                light.Color = color;
                light.Owner = lightingSystem;
                light.Radius = radius;
            }
        }

        // Nearest non-colliding cell (Background or Empty) to (x, y) so a light illuminates open space
        // and adjacent wall faces instead of sitting buried inside solid rock.
        private static Vector2Int NearestOpenCell(bool[,] isWall, int x, int y)
        {
            if (!WallAt(isWall, x, y))
                return new Vector2Int(x, y);

            for (var radius = 1; radius <= 24; radius++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    for (var dx = -radius; dx <= radius; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != radius)
                            continue;
                        if (!WallAt(isWall, x + dx, y + dy))
                            return new Vector2Int(x + dx, y + dy);
                    }
                }
            }

            return new Vector2Int(x, y);
        }

        // Matches SampleAutomation.NormalizeSerializedText: trims trailing whitespace from every line and
        // writes canonical LF-only text with a trailing newline. Trailing whitespace after a YAML value is
        // insignificant, so trimming keeps the scene semantically identical while matching the repo norm.
        private static void NormalizeSerializedScene(string path)
        {
            var lines = File.ReadAllLines(path);
            for (var index = 0; index < lines.Length; index++)
                lines[index] = lines[index].TrimEnd(' ', '\t');
            File.WriteAllText(path, string.Join("\n", lines) + "\n");
        }

        private static void MirrorSceneToPackage(string examplesRoot)
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForPackageName(PackageName);
            if (package == null)
                throw new InvalidOperationException($"Package '{PackageName}' is not registered; cannot mirror.");

            var importedRoot = Path.GetFullPath(examplesRoot).Replace('\\', '/');
            var packageRoot = Path.Combine(package.resolvedPath, "Samples~", "Examples").Replace('\\', '/');

            MirrorFile(importedRoot, packageRoot, SceneFileName);
            MirrorFile(importedRoot, packageRoot, SceneFileName + ".meta");

            // A lighting-settings sidecar is only produced when the scene carries baked lighting data;
            // mirror it if SaveScene created one so the trees stay identical.
            var lightingSidecar = SceneFileName.Replace(".unity", "Settings.lighting");
            if (File.Exists(Path.Combine(importedRoot, lightingSidecar)))
            {
                MirrorFile(importedRoot, packageRoot, lightingSidecar);
                MirrorFile(importedRoot, packageRoot, lightingSidecar + ".meta");
            }
        }

        private static void MirrorFile(string importedRoot, string packageRoot, string relative)
        {
            var source = Path.Combine(importedRoot, relative);
            if (!File.Exists(source))
                throw new FileNotFoundException($"Expected baked file missing during mirror: {source}", source);

            var destination = Path.Combine(packageRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(source, destination, true);
        }

        private static T LoadRequired<T>(string guid, string description) where T : UnityEngine.Object
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new InvalidOperationException($"Could not load the {description} (GUID {guid}).");
            return asset;
        }
    }
}
#endif
