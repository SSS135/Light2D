#if LIGHT2D_TILEMAP
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Light2D.Examples
{
    /// <summary>
    /// Editor bake that AUTHORS the "Light Volume" demo scene as a STATIC Unity Tilemap SQUARE-BLOCK world
    /// (Minecraft/Terraria-style tile terrain) with a cave carved into it. Above ground is the raw,
    /// full-brightness world — a blue-sky clear colour with a sun and a few clouds. Below the surface line
    /// a <see cref="Grid"/> holds two child <see cref="Tilemap"/>s of 1x1 world-unit tiles: a "Terrain"
    /// tilemap (grass-topped surface row on solid earth blocks) and a "Background" tilemap (recessed
    /// <c>cave_bg</c> back-wall tiles behind the carved void). The tiles are placed once here and SERIALIZED
    /// into the Tilemap components, so the saved scene is fully static — nothing generates blocks at play or
    /// build time. The Terrain tilemap also carries a <see cref="LightObstacleTilemap"/> (owned by the scene
    /// <see cref="LightingSystem"/>) so the solid tiles are texture-aware Light2D occluders and the lantern
    /// casts real block-shaped shadows in the cave instead of a flat dark box.
    ///
    /// The Main Camera's <see cref="LightingSystem"/> is configured from the sample materials exactly like
    /// the Rocket camera (RocketAmbientLight / RocketLightOverlay), with <c>DefaultCoverage = 0</c> so the
    /// world baseline is "lighting bypassed" (raw bright game image). A single Box-shaped
    /// <see cref="Light2DVolume"/> (Coverage = 1) re-enables Light2D over the cave chamber + shaft, so inside
    /// it the world reads dark and is lit only by the mouse-controlled lantern (lantern sprite + halo glow +
    /// a warm PointLight owned by the scene LightingSystem); the surrounding blocks and surface stay
    /// raw/bright.
    ///
    /// The tile grid and cave carve exactly replicate <c>scratchpad/art/_block_mock.py</c>: columns
    /// bx = -9..8, terrain rows by = 0 (surface) down to -5 (bottom). With the Grid at the origin and unit
    /// cells, tilemap cell (bx, by) occupies world (bx..bx+1, by..by+1) — identical placement to the mock's
    /// per-block centring — and <see cref="IsCave"/> mirrors that script's chamber ellipse + entrance shaft.
    ///
    /// The output scene (+ meta), the three LightVolume Tile assets (+ metas + folder meta) and the
    /// LightVolume art folder are written into the imported sample tree and mirrored byte-identically into
    /// Samples~/Examples so both trees match — the same write-then-mirror flow RocketTilemapBaker uses. The
    /// referenced materials/sprites/prefabs are untouched and stay identical in both trees. This whole file
    /// is gated on LIGHT2D_TILEMAP, which the sample Editor asmdef defines only when com.unity.2d.tilemap is
    /// installed (the same gate RocketTilemapBaker uses); the baked scene is static and needs neither the
    /// bake tool nor that define to load.
    ///
    /// Run headlessly with:
    ///   -executeMethod Light2D.Examples.LightVolumeDemoBuilder.Build
    /// or via the menu "Light2D/Samples/Build Light Volume Scene".
    /// </summary>
    internal static class LightVolumeDemoBuilder
    {
        private const string PackageName = "com.sss13594.light2d";
        private const string SceneFileName = "Light Volume.unity";

        // Sample assets reused by reference (their .meta GUIDs never churn). The two lighting materials are
        // the exact ones the Rocket scene's LightingSystem uses; the PointLight prefab is the same light the
        // Rocket Tilemap baker spawns.
        private const string LightOverlayMaterialGuid = "7683540b59b5765439da1f6cab25c7a9";  // RocketLightOverlay.mat
        private const string AmbientComputeMaterialGuid = "a1a7a1f483ec2844fb07f8ad165328c3"; // RocketAmbientLight.mat
        private const string PointLightPrefabGuid = "f1f87948c09d89b48acf75b3f771eece";
        // The shared PointLight prefab ships with a DANGLING LightSprite.Sprite reference (its guid resolves
        // to no asset in the project), so a bare instance renders no light. The Rocket scenes override the
        // sprite and material per-instance; this builder does the same with the exact assets they use.
        private const string LightSpriteGuid = "de67f3bc3c6d41f4f9811ec4e3393c09";            // Images/Lights/PointLight6.psd
        private const string LightMaterialGuid = "ab37f3042fbc56e4b9288c3ba9cd32d8";          // Runtime/Materials/Light40Points.mat

        // Sky decor sprites (kept from the previous scene).
        private const string SunSpriteGuid = "e9eae21f10514754837d9b2daebcf04e";
        private const string CloudSpriteGuid = "96d35c4992844cb09b0a4ca299540c16";
        private const string LanternSpriteGuid = "14bcc10bdf0e48e187e8c777047a6519";
        private const string HaloSpriteGuid = "6d2a69ff3f494eb592918efdb9c21d06";

        // Static Tile assets under Tiles/LightVolume (authored by hand with fixed GUIDs; each references a
        // 128-PPU block sprite so one tile = 1 world unit). The solid tiles carry ColliderType.Sprite so the
        // LightObstacleTilemap occludes them; the cave-background tile is ColliderType.None so it passes light.
        private const string GrassTileGuid = "379ad2d4598c448ebbc0e2eafa33d6c5";
        private const string EarthTileGuid = "15ecb667f85b4869a07db1b257718d36";
        private const string CaveBgTileGuid = "16479e56dce8439194db2b5dfbafcfee";
        // Package default obstacle material (Runtime/Materials/DualColor.mat) — the material
        // LightObstacleTilemap loads by default; assigned explicitly so the serialized scene is deterministic
        // and works outside the editor default-load path. Same material the Rocket Tilemap baker assigns.
        private const string ObstacleMaterialGuid = "0a8c132f813f5514b9f5b48ff3956482";

        // Tile assets that must be mirrored into Samples~ so both trees carry the same static tiles.
        private const string TilesFolderMetaRelative = "Tiles/LightVolume.meta";
        private const string TilesFolderRelative = "Tiles/LightVolume";
        private static readonly string[] TileAssetNames = { "GrassTile", "EarthTile", "CaveBgTile" };

        // Art payload mirrored alongside the scene so both sample trees stay byte-identical. The previous
        // painted-cave art (grass/cave_bg/stalactite/cave_mouth/cave_mask/boulder) is no longer placed by
        // the scene but is still mirrored so the two trees never diverge.
        private const string ArtFolderMetaRelative = "Images/LightVolume.meta";
        private const string ArtFolderRelative = "Images/LightVolume";
        private static readonly string[] ArtFileNames =
        {
            "sun", "cloud", "grass", "cave_bg", "stalactite", "lantern", "halo",
            "cave_mouth", "cave_mask", "boulder", "grass_block", "earth_block", "cave_bg_block"
        };

        // Coordinate system: orthographic size 5 at world origin -> 36 px/world-unit in a 640x360 capture;
        // the view frames world x in [-8.888, 8.888], y in [-5, 5].
        private const float OrthographicSize = 5f;
        private static readonly Vector3 CameraPosition = new Vector3(0f, 0f, -10f);
        private static readonly Color SkyClearColor = new Color(0.56f, 0.78f, 0.925f, 1f);

        // Tile grid (replicates scratchpad/art/_block_mock.py). Columns bx = -9..8, terrain rows by = 0
        // (grass surface) down to -5 (bottom). With the Grid at the origin and unit cells, cell (bx, by)
        // occupies world (bx..bx+1, by..by+1), so the grass surface row's top edge lands at world y = +1.
        private const int TerrainMinColumn = -9;
        private const int TerrainMaxColumn = 8;   // inclusive
        private const int SurfaceRow = 0;
        private const int TerrainBottomRow = -5;  // inclusive

        // Cave-coverage box over the carved chamber + shaft. Coverage = 1 makes the void + its immediate
        // block walls read DARK and lantern-lit, while the rest of the terrain + surface stay raw/bright
        // (DefaultCoverage = 0).
        private static readonly Vector3 CaveVolumeCenter = new Vector3(4.6f, -1.6f, 0f);
        private static readonly Vector2 CaveVolumeSize = new Vector2(5.6f, 4.2f);
        private const float CaveVolumeBlendDistance = 0.3f;
        private const int CaveVolumePriority = 1;

        // Lantern (mouse-controlled). Rests in the cave chamber for the deterministic capture; MouseFollower
        // drives it at play. Its warm light reveals the covered cave void.
        private static readonly Vector3 LanternStartPosition = new Vector3(4.6f, -1.2f, 0f);
        private static readonly Color LanternLightColor = new Color(1f, 0.82f, 0.5f, 1f);
        private const float LanternLightRadius = 4.0f;

        // Draw order (sortingOrder), back to front. The tilemaps are sorted against the sky and lantern
        // SpriteRenderers in the same default sorting layer.
        private const int SunOrder = -30;
        private const int CloudOrder = -20;
        private const int BackgroundTilemapOrder = -2; // recessed cave_bg back wall of the cave void
        private const int TerrainTilemapOrder = 0;     // grass surface row + solid earth underground
        private const int HaloOrder = 8;
        private const int LanternOrder = 10;

        [MenuItem("Light2D/Samples/Build Light Volume Scene")]
        public static void Build()
        {
            try
            {
                BuildInternal();
            }
            catch (Exception exception)
            {
                Debug.LogError("Light Volume scene build failed: " + exception);
                throw;
            }
        }

        private static void BuildInternal()
        {
            var overlayPath = AssetDatabase.GUIDToAssetPath(LightOverlayMaterialGuid);
            var separatorIndex = string.IsNullOrEmpty(overlayPath)
                ? -1
                : overlayPath.IndexOf("/Materials/", StringComparison.Ordinal);
            if (separatorIndex < 0)
                throw new InvalidOperationException(
                    "Could not locate the imported sample Examples root from the RocketLightOverlay material. " +
                    "Import the Light2D 'Examples' sample before building.");

            var examplesRoot = overlayPath.Substring(0, separatorIndex);

            var overlayMaterial = LoadRequired<Material>(LightOverlayMaterialGuid, "light overlay material");
            var ambientMaterial = LoadRequired<Material>(AmbientComputeMaterialGuid, "ambient compute material");
            var pointLightPrefab = LoadRequired<GameObject>(PointLightPrefabGuid, "PointLight prefab");

            var sunSprite = LoadRequired<Sprite>(SunSpriteGuid, "sun sprite");
            var cloudSprite = LoadRequired<Sprite>(CloudSpriteGuid, "cloud sprite");
            var lanternSprite = LoadRequired<Sprite>(LanternSpriteGuid, "lantern sprite");
            var haloSprite = LoadRequired<Sprite>(HaloSpriteGuid, "halo sprite");

            var grassTile = LoadRequired<TileBase>(GrassTileGuid, "grass tile");
            var earthTile = LoadRequired<TileBase>(EarthTileGuid, "earth tile");
            var caveBgTile = LoadRequired<TileBase>(CaveBgTileGuid, "cave background tile");
            var obstacleMaterial = LoadRequired<Material>(ObstacleMaterialGuid, "obstacle material");

            // Author into a fresh empty scene so nothing but the demo content is serialized.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightingSystem = CreateCamera(overlayMaterial, ambientMaterial);

            CreateSkyElements(sunSprite, cloudSprite);
            BuildTilemaps(grassTile, earthTile, caveBgTile, obstacleMaterial, lightingSystem);
            CreateCaveCoverage(lightingSystem);
            CreateLantern(lanternSprite, haloSprite, pointLightPrefab, lightingSystem);

            EditorSceneManager.MarkSceneDirty(scene);
            var sceneAsset = examplesRoot + "/" + SceneFileName;
            if (!EditorSceneManager.SaveScene(scene, sceneAsset))
                throw new InvalidOperationException($"Failed to save the demo scene to '{sceneAsset}'.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // Normalize to the repo convention (trailing whitespace stripped, LF-only, trailing newline) so
            // this machine-generated scene matches the hand-maintained sample scenes and git whitespace checks.
            NormalizeSerializedScene(Path.GetFullPath(examplesRoot + "/" + SceneFileName));

            // Mirror the scene (+meta), the static Tile assets (+metas + folder meta) and the LightVolume art
            // folder into the package Samples~ source so the two sample trees stay byte-identical.
            MirrorSceneToPackage(examplesRoot);
            MirrorTilesToPackage(examplesRoot);
            MirrorArtToPackage(examplesRoot);

            Debug.Log($"Built '{SceneFileName}' (static Tilemap block world + LightObstacleTilemap cave) and " +
                      "mirrored to Samples~/Examples.");
        }

        // Main Camera (orthographic) with a LightingSystem configured from the sample materials exactly like
        // the Rocket camera, but with DefaultCoverage = 0 so the world baseline is "lighting bypassed" (raw
        // bright game image); the cave coverage box re-enables Light2D locally. Ambient light is on but the
        // scene has no LightAmbientEmitter, so the cave stays lit only by the lantern.
        private static LightingSystem CreateCamera(Material overlayMaterial, Material ambientMaterial)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";

            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = OrthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = SkyClearColor;
            camera.transform.position = CameraPosition;

            cameraObject.AddComponent<AudioListener>();

            var lightingSystem = cameraObject.AddComponent<LightingSystem>();
            lightingSystem.AmbientLightComputeMaterial = ambientMaterial;
            lightingSystem.LightOverlayMaterial = overlayMaterial;
            lightingSystem.DefaultCoverage = 0f;
            // EnableAmbientLight matches the working Rocket scenes (same PointLight prefab). With no
            // LightAmbientEmitter in the scene the ambient stays ~black, so the cave is still lit only by the
            // lantern, but this keeps the LightingSystem on the proven ambient-on path.
            lightingSystem.EnableAmbientLight = true;
            return lightingSystem;
        }

        // A sun in the upper-left plus a few scattered clouds over the blue-sky clear colour.
        private static void CreateSkyElements(Sprite sunSprite, Sprite cloudSprite)
        {
            CreateSprite("Sun", sunSprite, new Vector3(-5.5f, 3.0f, 0f), 0.72f, SunOrder);

            CreateSprite("Cloud A", cloudSprite, new Vector3(-1.9f, 3.55f, 0f), 0.95f, CloudOrder);
            CreateSprite("Cloud B", cloudSprite, new Vector3(-4.0f, 2.05f, 0f), 0.6f, CloudOrder);
            CreateSprite("Cloud C", cloudSprite, new Vector3(2.6f, 2.55f, 0f), 0.8f, CloudOrder);
        }

        // The square-block terrain, placed as STATIC tiles in a Grid + two Tilemaps (Terrain solid blocks +
        // Background cave_bg wall). For every terrain cell (bx,by): a cave cell (IsCave) writes a cave_bg tile
        // into the Background tilemap (the recessed back wall of the void) and leaves the Terrain cell empty
        // (the void); otherwise a grass tile on the surface row and an earth tile below fill the Terrain cell.
        // The Terrain tilemap also carries the LightObstacleTilemap so the solid tiles occlude the lantern.
        private static void BuildTilemaps(TileBase grassTile, TileBase earthTile, TileBase caveBgTile,
            Material obstacleMaterial, LightingSystem lightingSystem)
        {
            // Grid at the origin with default unit cells: cell (bx, by) -> world (bx..bx+1, by..by+1).
            var gridObject = new GameObject("Grid");
            gridObject.transform.position = Vector3.zero;
            gridObject.AddComponent<Grid>();

            var terrainObject = new GameObject("Terrain");
            terrainObject.transform.SetParent(gridObject.transform, false);
            var terrainMap = terrainObject.AddComponent<Tilemap>();
            var terrainRenderer = terrainObject.AddComponent<TilemapRenderer>(); // Enabled: draws the visible terrain.
            terrainRenderer.sortingOrder = TerrainTilemapOrder;

            var backgroundObject = new GameObject("Background");
            backgroundObject.transform.SetParent(gridObject.transform, false);
            var backgroundMap = backgroundObject.AddComponent<Tilemap>();
            var backgroundRenderer = backgroundObject.AddComponent<TilemapRenderer>();
            backgroundRenderer.sortingOrder = BackgroundTilemapOrder;

            var terrainPositions = new List<Vector3Int>();
            var terrainTiles = new List<TileBase>();
            var backgroundPositions = new List<Vector3Int>();
            var backgroundTiles = new List<TileBase>();

            for (var by = SurfaceRow; by >= TerrainBottomRow; by--)
            {
                for (var bx = TerrainMinColumn; bx <= TerrainMaxColumn; bx++)
                {
                    var cell = new Vector3Int(bx, by, 0);
                    if (IsCave(bx, by))
                    {
                        backgroundPositions.Add(cell);
                        backgroundTiles.Add(caveBgTile);
                    }
                    else
                    {
                        terrainPositions.Add(cell);
                        terrainTiles.Add(by == SurfaceRow ? grassTile : earthTile);
                    }
                }
            }

            terrainMap.SetTiles(terrainPositions.ToArray(), terrainTiles.ToArray());
            backgroundMap.SetTiles(backgroundPositions.ToArray(), backgroundTiles.ToArray());
            terrainMap.CompressBounds();
            backgroundMap.CompressBounds();

            // The obstacle companion owns no scene renderer; it emits the texture-aware occluder as draw
            // entries into the obstacle snapshot only. Only the ColliderType.Sprite terrain tiles occlude,
            // so the lantern casts real block-shaped shadows against the cave walls.
            var obstacle = terrainObject.AddComponent<LightObstacleTilemap>();
            obstacle.MultiplicativeColor = new Color32(255, 255, 255, 255);
            obstacle.AdditiveColor = new Color(0f, 0f, 0f, 0f);
            obstacle.Material = obstacleMaterial;
            obstacle.Owner = lightingSystem;
            obstacle.SortingOrder = 0;
        }

        // Exact replica of _block_mock.py's is_cave(bx,by) using cell centres cx = bx+0.5, cy = by+0.5:
        // a chamber ellipse on the right OR a narrow entrance shaft running up to the surface. These cells
        // are the cave VOID (cave_bg back wall, no solid Terrain tile).
        private static bool IsCave(int bx, int by)
        {
            var cx = bx + 0.5f;
            var cy = by + 0.5f;
            var ellipseX = (cx - 4.6f) / 2.7f;
            var ellipseY = (cy + 1.7f) / 1.9f;
            if (ellipseX * ellipseX + ellipseY * ellipseY < 1.0f)
                return true; // chamber
            if (cx >= 4.0f && cx <= 5.4f && cy >= -2.0f && cy <= 1.2f)
                return true; // entrance shaft to the surface
            return false;
        }

        // One Box-shaped coverage volume over the carved cave (chamber + shaft). Coverage = 1 re-enables
        // Light2D there so the void reads dark and is revealed only by the lantern; outside the box the
        // world stays raw/bright (DefaultCoverage = 0).
        private static void CreateCaveCoverage(LightingSystem lightingSystem)
        {
            var volumeObject = new GameObject("Cave Coverage Volume");
            volumeObject.transform.position = CaveVolumeCenter;

            var volume = volumeObject.AddComponent<Light2DVolume>();
            volume.Shape = Light2DVolume.VolumeShape.Box;
            volume.Size = CaveVolumeSize;
            volume.Coverage = 1f;
            volume.Priority = CaveVolumePriority;
            volume.BlendDistance = CaveVolumeBlendDistance;
            volume.Owner = lightingSystem;
        }

        // The mouse-controlled lantern, authored as ONE hierarchy so a single MouseFollower drives the whole
        // rig. The root "Lantern" (scale 1, one MouseFollower) parents three children: the visible lantern
        // SpriteRenderer, the halo glow, and a "Light" child carrying the warm LightSprite. The LightSprite's
        // Radius sizes the light mesh directly (the whole rig stays unit-scaled so nothing cascades into the
        // sprites/halo), and the Light child sits at the root origin so it tracks the lantern exactly.
        //
        // Parenting the light is safe: the earlier "black covered cave" was caused by the shared PointLight
        // prefab's DANGLING LightSprite.Sprite (no mesh, no emission), not by the parenting. This builder
        // overrides the sprite/material with the proven light assets below, so the child LightSprite builds a
        // real mesh and lights the cave; its world cull bounds follow the parent transform each frame.
        // (Verified by the Light Volume visual smoke test's CAVE bright-core gate.)
        private static void CreateLantern(Sprite lanternSprite, Sprite haloSprite, GameObject pointLightPrefab,
            LightingSystem lightingSystem)
        {
            var lantern = new GameObject("Lantern");
            lantern.transform.position = LanternStartPosition;
            lantern.transform.localScale = Vector3.one;   // no scale cascade into the child sprites/halo/light
            lantern.AddComponent<MouseFollower>();

            var halo = CreateSprite("Lantern Halo", haloSprite, Vector3.zero, 0.55f, HaloOrder);
            halo.transform.SetParent(lantern.transform, false);

            var sprite = CreateSprite("Lantern Sprite", lanternSprite, Vector3.zero, 0.9f, LanternOrder);
            sprite.transform.SetParent(lantern.transform, false);

            var lightInstance = (GameObject)PrefabUtility.InstantiatePrefab(pointLightPrefab, lightingSystem.gameObject.scene);
            lightInstance.name = "Light";
            lightInstance.transform.SetParent(lantern.transform, false);            // child of the lantern root
            lightInstance.transform.localPosition = Vector3.zero;                   // at the lantern origin
            lightInstance.transform.localRotation = Quaternion.identity;
            lightInstance.transform.localScale = Vector3.one;                       // unit-scaled: size comes from LightSprite.Radius, not the Transform

            var randomizer = lightInstance.GetComponentInChildren<LightRandomizer>(true);
            if (randomizer != null)
                randomizer.enabled = false;

            var light = lightInstance.GetComponentInChildren<LightSprite>(true);
            if (light != null)
            {
                // Override the prefab's dangling sprite/material with the proven light assets (the Rocket
                // scenes do the same); without a valid sprite the LightSprite builds no mesh and emits nothing.
                light.Sprite = LoadRequired<Sprite>(LightSpriteGuid, "lantern light sprite");
                light.Material = LoadRequired<Material>(LightMaterialGuid, "lantern light material");
                light.Color = LanternLightColor;
                light.Owner = lightingSystem;
                light.Radius = LanternLightRadius;  // size the light mesh via Radius, not the Transform scale
            }
        }

        // A Simple-draw SpriteRenderer scaled uniformly. Returns the GameObject so the caller can parent or
        // rescale it.
        private static GameObject CreateSprite(string name, Sprite sprite, Vector3 position, float scale,
            int sortingOrder)
        {
            var spriteObject = new GameObject(name);
            spriteObject.transform.position = position;
            spriteObject.transform.localScale = Vector3.one * scale;

            var renderer = spriteObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.sortingOrder = sortingOrder;
            return spriteObject;
        }

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

        // Mirrors the three static Tile assets (+ metas) and their folder meta into Samples~/Examples so both
        // trees carry byte-identical static tiles. The assets are hand-authored under Assets and settle
        // through the AssetDatabase refresh above; this copies the settled bytes across.
        private static void MirrorTilesToPackage(string examplesRoot)
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForPackageName(PackageName);
            if (package == null)
                throw new InvalidOperationException($"Package '{PackageName}' is not registered; cannot mirror.");

            var importedRoot = Path.GetFullPath(examplesRoot).Replace('\\', '/');
            var packageRoot = Path.Combine(package.resolvedPath, "Samples~", "Examples").Replace('\\', '/');

            MirrorFile(importedRoot, packageRoot, TilesFolderMetaRelative);
            foreach (var tileName in TileAssetNames)
            {
                MirrorFile(importedRoot, packageRoot, TilesFolderRelative + "/" + tileName + ".asset");
                MirrorFile(importedRoot, packageRoot, TilesFolderRelative + "/" + tileName + ".asset.meta");
            }
        }

        // Mirrors the imported (Unity-canonicalized) LightVolume art PNGs + their metas and the folder meta
        // into Samples~/Examples so the two sample trees stay byte-identical for the art payload too. Unity
        // never imports the Samples~ tree (the trailing ~ hides it), so it just receives the settled copies.
        private static void MirrorArtToPackage(string examplesRoot)
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForPackageName(PackageName);
            if (package == null)
                throw new InvalidOperationException($"Package '{PackageName}' is not registered; cannot mirror.");

            var importedRoot = Path.GetFullPath(examplesRoot).Replace('\\', '/');
            var packageRoot = Path.Combine(package.resolvedPath, "Samples~", "Examples").Replace('\\', '/');

            MirrorFile(importedRoot, packageRoot, ArtFolderMetaRelative);
            foreach (var artName in ArtFileNames)
            {
                MirrorFile(importedRoot, packageRoot, ArtFolderRelative + "/" + artName + ".png");
                MirrorFile(importedRoot, packageRoot, ArtFolderRelative + "/" + artName + ".png.meta");
            }
        }

        private static void MirrorFile(string importedRoot, string packageRoot, string relative)
        {
            var source = Path.Combine(importedRoot, relative);
            if (!File.Exists(source))
                throw new FileNotFoundException($"Expected built file missing during mirror: {source}", source);

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
