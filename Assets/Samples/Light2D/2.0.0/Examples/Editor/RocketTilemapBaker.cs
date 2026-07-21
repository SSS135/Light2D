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
    /// One-time editor bake that authors the static "Rocket Tilemap" sample scene and its Tile assets
    /// (Phase 2, §5). It does NOT ship a runtime generator: it reproduces MapGenerator's procedural
    /// block field for a fixed seed over a fixed window (mirroring SampleTestSupport.MapProbe's exact
    /// selection), turns each eligible cell into a Tilemap tile that REUSES the sample block sprites
    /// (no new textures), and assembles a Grid+Tilemap+LightObstacleTilemap cave lit by a few colored
    /// crystal lights plus one bright central light. The visible TilemapRenderer stays enabled (OQ-T1):
    /// it draws the dark cave while LightObstacleTilemap emits the texture-aware occluder as draw
    /// entries only. The output (scene + Tiles/*.asset + metas) is written into the imported sample
    /// tree and mirrored byte-identically into Samples~/Examples so both trees match.
    ///
    /// Run headlessly with:
    ///   -executeMethod Light2D.Examples.RocketTilemapBaker.Bake
    /// or via the menu "Light2D/Samples/Bake Rocket Tilemap Scene". The bake is a single-shot editor
    /// step; it replaces any previously baked scene/tiles. This whole file is gated on LIGHT2D_TILEMAP,
    /// which the sample Editor asmdef defines only when com.unity.2d.tilemap is installed.
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

        // Package default obstacle material (Runtime/Materials/DualColor.mat) — the material
        // LightObstacleTilemap loads by default; assigned explicitly so the serialized scene is
        // deterministic and works outside the editor default-load path.
        private const string ObstacleMaterialGuid = "0a8c132f813f5514b9f5b48ff3956482";
        private const string BlockSetProfileGuid = "99b2aa50cb51ad9409451b6056775bd6";
        private const string RocketAmbientLightMaterialGuid = "a1a7a1f483ec2844fb07f8ad165328c3";
        private const string RocketLightOverlayMaterialGuid = "7683540b59b5765439da1f6cab25c7a9";
        private const string LightingCrystalsPrefabGuid = "9f10243117669f046af3db5fc661dc8d";
        private const string PointLightPrefabGuid = "f1f87948c09d89b48acf75b3f771eece";

        private const string PackageName = "com.sss13594.light2d";
        private const string SceneFileName = "Rocket Tilemap.unity";
        private const string TilesFolderName = "Tiles";

        // Custom layers defined by the project's TagManager (LightSources/LightObstacles/AmbientLight);
        // a source camera must exclude all three so the light meshes never reach the game image.
        private const int LightSourcesLayer = 9;
        private const int LightObstaclesLayer = 10;
        private const int AmbientLightLayer = 11;

        [MenuItem("Light2D/Samples/Bake Rocket Tilemap Scene")]
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
            var ambientMaterial = LoadRequired<Material>(RocketAmbientLightMaterialGuid, "ambient light material");
            var overlayMaterial = LoadRequired<Material>(RocketLightOverlayMaterialGuid, "light overlay material");
            var crystalPrefab = LoadRequired<GameObject>(LightingCrystalsPrefabGuid, "LightingCrystals prefab");
            var pointLightPrefab = LoadRequired<GameObject>(PointLightPrefabGuid, "PointLight prefab");

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
            var tileCache = new Dictionary<TileKey, Tile>();
            var createdTiles = new List<Tile>();
            var usedNames = new HashSet<string>(StringComparer.Ordinal);

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

                    var tile = GetOrCreateTile(tileCache, createdTiles, usedNames, sprite, colliding);
                    positions.Add(new Vector3Int(x, y, 0));
                    placedTiles.Add(tile);
                }
            }

            if (createdTiles.Count == 0)
                throw new InvalidOperationException("The block field produced no tiles; nothing to bake.");

            // 2. Pick a rock-dense-but-not-buried framing centre.
            FindFocus(isWall, out var focusX, out var focusY);

            // 3. Persist the Tile assets under Examples/Tiles.
            var tilesFolderAsset = examplesRoot + "/" + TilesFolderName;
            if (AssetDatabase.IsValidFolder(tilesFolderAsset))
                AssetDatabase.DeleteAsset(tilesFolderAsset);
            var sceneAsset = examplesRoot + "/" + SceneFileName;
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(sceneAsset)))
                AssetDatabase.DeleteAsset(sceneAsset);
            AssetDatabase.CreateFolder(examplesRoot, TilesFolderName);

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var tile in createdTiles)
                    AssetDatabase.CreateAsset(tile, tilesFolderAsset + "/" + tile.name + ".asset");
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            // AssetDatabase.Refresh below reimports the freshly created Tile assets and invalidates these
            // managed wrappers, so capture their asset names now for the byte-identical package mirror (step 5).
            var createdTileNames = new List<string>(createdTiles.Count);
            foreach (var tile in createdTiles)
                createdTileNames.Add(tile.name);

            AssetDatabase.SaveAssets();

            // 4. Author the scene.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightingSystem = BuildCamera(scene, ambientMaterial, overlayMaterial, focusX, focusY);
            BuildTilemap(scene, positions, placedTiles, obstacleMaterial, lightingSystem);
            SpawnLights(scene, crystalPrefab, pointLightPrefab, isWall, focusX, focusY, lightingSystem);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, sceneAsset))
                throw new InvalidOperationException($"Failed to save the baked scene to '{sceneAsset}'.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // 5. Mirror scene + tiles + metas into the package Samples~ source, byte-identical.
            MirrorToPackage(examplesRoot, createdTileNames);

            Debug.Log($"Baked '{SceneFileName}' with {placedTiles.Count} tiles ({createdTiles.Count} distinct) " +
                      $"framed at cell ({focusX}, {focusY}); mirrored to Samples~/Examples.");
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

        private readonly struct TileKey : IEquatable<TileKey>
        {
            private readonly Sprite _sprite;
            private readonly bool _colliding;

            internal TileKey(Sprite sprite, bool colliding)
            {
                _sprite = sprite;
                _colliding = colliding;
            }

            public bool Equals(TileKey other) => ReferenceEquals(_sprite, other._sprite) &&
                                                 _colliding == other._colliding;

            public override bool Equals(object obj) => obj is TileKey other && Equals(other);

            public override int GetHashCode() =>
                (_sprite != null ? _sprite.GetEntityId().GetHashCode() : 0) * 397 ^ (_colliding ? 1 : 0);
        }

        private static Tile GetOrCreateTile(Dictionary<TileKey, Tile> cache, List<Tile> created,
            HashSet<string> usedNames, Sprite sprite, bool colliding)
        {
            var key = new TileKey(sprite, colliding);
            if (cache.TryGetValue(key, out var existing))
                return existing;

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.color = Color.white;
            // Only colliding walls occlude; background tiles stay visible but pass light (Tile.ColliderType.None).
            tile.colliderType = colliding ? Tile.ColliderType.Sprite : Tile.ColliderType.None;
            tile.name = UniqueName((colliding ? "Wall_" : "Background_") + MakeSafeName(sprite.name), usedNames);

            cache[key] = tile;
            created.Add(tile);
            return tile;
        }

        private static string MakeSafeName(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return "Tile";
            var builder = new System.Text.StringBuilder(raw.Length);
            foreach (var character in raw)
                builder.Append(char.IsLetterOrDigit(character) || character == '_' || character == '-'
                    ? character
                    : '_');
            return builder.ToString();
        }

        private static string UniqueName(string baseName, HashSet<string> used)
        {
            var candidate = baseName;
            var suffix = 1;
            while (!used.Add(candidate))
                candidate = baseName + "_" + suffix++;
            return candidate;
        }

        // Chooses the framing whose framed wall fraction is closest to TargetWallFraction, inside a scan
        // region kept far enough from the window edge that the whole framed view stays tiled.
        private static void FindFocus(bool[,] isWall, out int focusX, out int focusY)
        {
            var scanHalfWidth = WindowHalfWidth - Mathf.CeilToInt(ViewHalfWidth) - 2;
            var scanHalfHeight = WindowHalfHeight - Mathf.CeilToInt(ViewHalfHeight) - 2;
            focusX = 0;
            focusY = 0;
            var bestDelta = float.MaxValue;
            for (var y = -scanHalfHeight; y <= scanHalfHeight; y += 3)
            {
                for (var x = -scanHalfWidth; x <= scanHalfWidth; x += 3)
                {
                    var fraction = FramedWallFraction(isWall, x, y);
                    var delta = Mathf.Abs(fraction - TargetWallFraction);
                    if (delta < bestDelta)
                    {
                        bestDelta = delta;
                        focusX = x;
                        focusY = y;
                    }
                }
            }
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

        private static LightingSystem BuildCamera(Scene scene, Material ambientMaterial, Material overlayMaterial,
            int focusX, int focusY)
        {
            var cameraObject = new GameObject("Main Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(focusX + 0.5f, focusY + 0.5f, -100f);

            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = OrthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            // Dark clear so sky/void pockets and unlit rock read dark for the dark-cave gate.
            camera.backgroundColor = new Color(0.02f, 0.02f, 0.03f, 0f);
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1000f;
            camera.depth = 0f;
            // Exclude the three Light2D layers (LightSources/LightObstacles/AmbientLight) from the source view.
            camera.cullingMask = ~((1 << LightSourcesLayer) | (1 << LightObstaclesLayer) | (1 << AmbientLightLayer));
            cameraObject.AddComponent<AudioListener>();

            var lightingSystem = cameraObject.AddComponent<LightingSystem>();
            // Values mirror the migrated Rocket.unity LightingSystem.
            lightingSystem.LightPixelSize = 0.5f;
            lightingSystem.LightCameraSizeAdd = 8f;
            lightingSystem.LightCameraFovAdd = 30f;
            lightingSystem.EnableAmbientLight = true;
            lightingSystem.BlurLightSources = false;
            lightingSystem.BlurAmbientLight = false;
            lightingSystem.HDR = true;
            lightingSystem.LightObstaclesAntialiasing = false;
            lightingSystem.LightObstaclesDistance = 10f;
            lightingSystem.LightTexturesFilterMode = FilterMode.Bilinear;
            lightingSystem.EnableNormalMapping = false;
            lightingSystem.AffectOnlyThisCamera = false;
            lightingSystem.AmbientLightComputeMaterial = ambientMaterial;
            lightingSystem.LightOverlayMaterial = overlayMaterial;
            lightingSystem.LightSourcesBlurMaterial = null;
            lightingSystem.AmbientLightBlurMaterial = null;
            lightingSystem.XZPlane = false;
            return lightingSystem;
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

            // The obstacle companion owns no scene renderer; it emits the texture-aware occluder as
            // draw entries into the obstacle snapshot only.
            var obstacle = tilemapObject.AddComponent<LightObstacleTilemap>();
            obstacle.MultiplicativeColor = new Color32(255, 255, 255, 255);
            obstacle.AdditiveColor = new Color(0f, 0f, 0f, 0f);
            obstacle.Material = obstacleMaterial;
            obstacle.Owner = lightingSystem;
            obstacle.SortingOrder = 0;
        }

        // A bright central light plus two distinctly coloured crystal lights, all inside the central
        // third of the framed view, satisfy the gate's central-peak and >=2-hue-bucket requirements.
        // Radii are sized well above the source prefabs' natural LightSprite scales (PointLight authored
        // at 35, LightingCrystals' Light child at 20) so three static lights deliver enough energy for
        // the dark-cave mean-luminance floor against the dense, texture-occluding tilemap. The radii were
        // retuned upward (central 66->165, crystals 48->120) after the Built-in vertical-flip fix (3e447ad):
        // that correction landed the light textures upright instead of mirrored, which lowered the framed
        // mean luminance to ~0.0149 (just under the [0.015, 0.2] dark-cave floor). The larger radii lift the
        // corrected render comfortably into band (~0.03-0.05) without loosening the gate; they raise the mean
        // mainly by lighting more occluding rock faces, so the bright-core fraction stays small.
        private static void SpawnLights(Scene scene, GameObject crystalPrefab, GameObject pointLightPrefab,
            bool[,] isWall, int focusX, int focusY, LightingSystem lightingSystem)
        {
            var central = NearestOpenCell(isWall, focusX, focusY);
            SpawnLight(scene, pointLightPrefab, central, new Color(1f, 0.82f, 0.5f, 1f), 165f,
                lightingSystem, "Central Light");

            var crystalOne = NearestOpenCell(isWall, focusX + 6, focusY + 3);
            SpawnLight(scene, crystalPrefab, crystalOne, new Color(0.25f, 0.8f, 1f, 0.95f), 120f,
                lightingSystem, "Crystal Light Cyan");

            var crystalTwo = NearestOpenCell(isWall, focusX - 6, focusY - 3);
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
                light.transform.localScale = Vector3.one * radius;
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

        private static void MirrorToPackage(string examplesRoot, List<string> createdTileNames)
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForPackageName(PackageName);
            if (package == null)
                throw new InvalidOperationException($"Package '{PackageName}' is not registered; cannot mirror.");

            var importedRoot = Path.GetFullPath(examplesRoot).Replace('\\', '/');
            var packageRoot = Path.Combine(package.resolvedPath, "Samples~", "Examples").Replace('\\', '/');

            // A clean rebake: drop the previous mirrored tiles/scene so stale files never linger.
            var packageTilesDirectory = Path.Combine(packageRoot, TilesFolderName);
            if (Directory.Exists(packageTilesDirectory))
                Directory.Delete(packageTilesDirectory, true);
            var packageTilesMeta = packageTilesDirectory + ".meta";
            if (File.Exists(packageTilesMeta))
                File.Delete(packageTilesMeta);

            MirrorFile(importedRoot, packageRoot, SceneFileName);
            MirrorFile(importedRoot, packageRoot, SceneFileName + ".meta");
            MirrorFile(importedRoot, packageRoot, TilesFolderName + ".meta");
            foreach (var tileName in createdTileNames)
            {
                var relative = TilesFolderName + "/" + tileName + ".asset";
                MirrorFile(importedRoot, packageRoot, relative);
                MirrorFile(importedRoot, packageRoot, relative + ".meta");
            }

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
