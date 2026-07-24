#if LIGHT2D_TILEMAP
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Light2D.Examples
{
    /// <summary>
    /// Editor bake that authors a static Unity TILE PALETTE asset for the "Light Volume" sample's three block
    /// tiles, so the 2D Tilemap "Tile Palette" window lists it in its dropdown and the user can select and
    /// paint Grass / Earth / CaveBg. A Tile Palette asset is an ordinary prefab (a <see cref="Grid"/> root with
    /// a child <see cref="Tilemap"/>) that also carries a <see cref="GridPalette"/> settings sub-asset embedded
    /// in the same .prefab file; the window discovers palettes by that sub-asset. This bake reproduces exactly
    /// what the window's "Create New Tile Palette" produces, then paints the three existing Tile assets into
    /// the palette Tilemap at cells (0,0)=Grass, (1,0)=Earth, (2,0)=CaveBg and serializes them into the prefab.
    ///
    /// The three Tile assets are REUSED by reference (their .meta GUIDs never churn); the bake never creates or
    /// edits tile assets, scenes, or runtime code. The palette is written under Examples/Palettes/LightVolume;
    /// its folders, prefab and GridPalette sub-asset are then given fixed GUIDs and mirrored byte-identically
    /// into Samples~/Examples so both sample trees match — the finalize/mirror is done outside the editor.
    ///
    /// The logic is a faithful, dependency-light replica of UnityEditor.Tilemaps.GridPaletteUtility.CreateNewPalette
    /// (that utility lives in the optional com.unity.2d.tilemap Editor assembly; replicating it here keeps this
    /// gated sample editor code from taking a hard assembly reference that would break compilation when the
    /// tilemap package is absent). Every type used — <see cref="Grid"/>, <see cref="Tilemap"/>,
    /// <see cref="TilemapRenderer"/> (built-in tilemap module), <see cref="GridPalette"/> (built-in editor
    /// tilemap module) and <see cref="PrefabUtility"/>/<see cref="AssetDatabase"/> (editor core) — is available
    /// to any editor assembly without the package reference.
    ///
    /// Run headlessly with:
    ///   -executeMethod Light2D.Examples.LightVolumePaletteBaker.Bake
    /// This whole file is gated on LIGHT2D_TILEMAP,
    /// which the sample Editor asmdef defines only when com.unity.2d.tilemap is installed (the same gate
    /// RocketTilemapBaker and LightVolumeDemoBuilder use).
    /// </summary>
    internal static class LightVolumePaletteBaker
    {
        // The three static Tile assets under Tiles/LightVolume (authored with fixed GUIDs; each references a
        // 128-PPU block sprite so one tile = 1 world unit). Reused by reference so their .meta GUIDs persist.
        private const string GrassTileGuid = "379ad2d4598c448ebbc0e2eafa33d6c5";
        private const string EarthTileGuid = "15ecb667f85b4869a07db1b257718d36";
        private const string CaveBgTileGuid = "16479e56dce8439194db2b5dfbafcfee";

        private const string PaletteName = "LightVolume Blocks";
        private const string PalettesFolderName = "Palettes";
        private const string PaletteSubFolderName = "LightVolume";

        // Rectangle cells sized 1x1 world unit (Manual sizing) so the painted blocks land on the same unit grid
        // the Light Volume scene uses and the palette never resizes itself on open.
        private static readonly Vector3 CellSize = new Vector3(1f, 1f, 0f);
        private static readonly Vector3 SortAxis = new Vector3(0f, 0f, 1f);

        public static void Bake()
        {
            try
            {
                BakeInternal();
            }
            catch (Exception exception)
            {
                Debug.LogError("Light Volume palette bake failed: " + exception);
                throw;
            }
        }

        private static void BakeInternal()
        {
            // Resolve the imported sample Examples root from the Grass tile's path (…/Examples/Tiles/LightVolume).
            var grassPath = AssetDatabase.GUIDToAssetPath(GrassTileGuid);
            var separatorIndex = string.IsNullOrEmpty(grassPath)
                ? -1
                : grassPath.IndexOf("/Tiles/", StringComparison.Ordinal);
            if (separatorIndex < 0)
                throw new InvalidOperationException(
                    "Could not locate the imported sample Examples root from the GrassTile asset. " +
                    "Import the Light2D 'Examples' sample before baking.");

            var examplesRoot = grassPath.Substring(0, separatorIndex);

            var grassTile = LoadRequired<TileBase>(GrassTileGuid, "grass tile");
            var earthTile = LoadRequired<TileBase>(EarthTileGuid, "earth tile");
            var caveBgTile = LoadRequired<TileBase>(CaveBgTileGuid, "cave background tile");

            var palettesFolder = EnsureFolder(examplesRoot, PalettesFolderName);
            var paletteFolder = EnsureFolder(palettesFolder, PaletteSubFolderName);
            var prefabPath = paletteFolder + "/" + PaletteName + ".prefab";

            // Idempotent re-bake: drop any prior palette (prefab + its embedded GridPalette sub-asset) so a
            // fresh one is written instead of a name-suffixed duplicate.
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(prefabPath)))
                AssetDatabase.DeleteAsset(prefabPath);

            // Build the palette GameObject exactly like GridPaletteUtility.CreateNewPalette: a Grid root with a
            // single "Layer1" child carrying a Tilemap + TilemapRenderer.
            var paletteGO = new GameObject(PaletteName);
            try
            {
                var grid = paletteGO.AddComponent<Grid>();
                grid.cellSize = CellSize;
                grid.cellLayout = GridLayout.CellLayout.Rectangle;
                grid.cellSwizzle = GridLayout.CellSwizzle.XYZ;

                var layerGO = new GameObject("Layer1");
                layerGO.transform.SetParent(paletteGO.transform, false);
                layerGO.layer = paletteGO.layer;
                var tilemap = layerGO.AddComponent<Tilemap>();
                layerGO.AddComponent<TilemapRenderer>();

                // Paint the three blocks in a single row so the palette reads Grass | Earth | CaveBg.
                tilemap.SetTile(new Vector3Int(0, 0, 0), grassTile);
                tilemap.SetTile(new Vector3Int(1, 0, 0), earthTile);
                tilemap.SetTile(new Vector3Int(2, 0, 0), caveBgTile);
                tilemap.CompressBounds();

                // Save as a prefab asset and connect the temporary instance, then embed the GridPalette settings
                // sub-asset into the same .prefab file (this is what the Tile Palette window keys on) and apply.
                var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(paletteGO, prefabPath,
                    InteractionMode.AutomatedAction);

                var gridPalette = ScriptableObject.CreateInstance<GridPalette>();
                gridPalette.name = "Palette Settings";
                gridPalette.cellSizing = GridPalette.CellSizing.Manual;
                gridPalette.transparencySortMode = TransparencySortMode.Default;
                gridPalette.transparencySortAxis = SortAxis;
                AssetDatabase.AddObjectToAsset(gridPalette, prefab);

                PrefabUtility.ApplyPrefabInstance(paletteGO, InteractionMode.AutomatedAction);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(paletteGO);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // Verify the saved palette round-trips: three tiles on the layer Tilemap and a GridPalette sub-asset.
            var savedTileCount = CountPaletteTiles(prefabPath);
            var hasSettings = LoadGridPaletteSubAsset(prefabPath) != null;
            Debug.Log($"Baked Tile Palette '{PaletteName}' at '{prefabPath}' (GUID " +
                      $"{AssetDatabase.AssetPathToGUID(prefabPath)}); tiles={savedTileCount}, " +
                      $"GridPalette sub-asset present={hasSettings}.");

            if (savedTileCount != 3 || !hasSettings)
                throw new InvalidOperationException(
                    $"Baked palette is malformed (tiles={savedTileCount}, settings={hasSettings}); expected 3 tiles " +
                    "and a GridPalette sub-asset.");
        }

        // Loads the palette prefab and counts non-null tiles on its child Tilemap.
        private static int CountPaletteTiles(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                return 0;
            var tilemap = prefab.GetComponentInChildren<Tilemap>(true);
            if (tilemap == null)
                return 0;

            var count = 0;
            var bounds = tilemap.cellBounds;
            foreach (var position in bounds.allPositionsWithin)
                if (tilemap.GetTile(position) != null)
                    count++;
            return count;
        }

        private static GridPalette LoadGridPaletteSubAsset(string prefabPath)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(prefabPath))
                if (asset is GridPalette palette)
                    return palette;
            return null;
        }

        private static string EnsureFolder(string parentFolder, string name)
        {
            var path = parentFolder + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parentFolder, name);
            return path;
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
