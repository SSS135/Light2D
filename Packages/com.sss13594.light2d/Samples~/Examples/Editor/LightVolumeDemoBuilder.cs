using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Light2D.Examples
{
    /// <summary>
    /// Editor builder that AUTHORS the "Light Volume" demo scene: a large bright world with a small dark
    /// cave carved out of it by a <see cref="Light2DVolume"/>. The Main Camera's <see cref="LightingSystem"/>
    /// is configured from the sample materials exactly like the Rocket camera (RocketAmbientLight /
    /// RocketLightOverlay), but with <c>DefaultCoverage = 0</c> so the world baseline is "lighting bypassed":
    /// every pixel outside a volume shows the raw, full-brightness game image (the bright SolidColor clear).
    /// A single <c>Shape=Box, Coverage=1, Priority=1</c> volume re-enables Light2D over the cave region, so
    /// inside it the world reads dark and is lit only by the torches (a PointLight and a LightingCrystals
    /// instance) while a LightObstacleSprite wall casts a shadow.
    ///
    /// The output scene (+ meta) is written into the imported sample tree and mirrored byte-identically into
    /// Samples~/Examples so both trees match — the same write-then-mirror flow RocketTilemapBaker uses. The
    /// referenced materials/sprites/prefabs are untouched and stay identical in both trees.
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
        // the exact ones the Rocket scene's LightingSystem uses; the prefabs are the same torches the Rocket
        // Tilemap baker spawns; the obstacle material is the package default LightObstacleSprite loads.
        private const string LightOverlayMaterialGuid = "7683540b59b5765439da1f6cab25c7a9";  // RocketLightOverlay.mat
        private const string AmbientComputeMaterialGuid = "a1a7a1f483ec2844fb07f8ad165328c3"; // RocketAmbientLight.mat
        private const string PointLightPrefabGuid = "f1f87948c09d89b48acf75b3f771eece";
        private const string LightingCrystalsPrefabGuid = "9f10243117669f046af3db5fc661dc8d";
        private const string WallSpriteGuid = "bf51b8bd609221d40ba75811f82d2a6a";           // Images/Castle/Wall.psd
        private const string ObstacleMaterialGuid = "0a8c132f813f5514b9f5b48ff3956482";       // Runtime/Materials/DualColor.mat

        // Scene layout. These world coordinates are COUPLED to the pixel-region rectangles the visual smoke
        // test samples (LightVolumeVisualSmokeTest); at orthographic size 5 with a 640x360 capture the view
        // spans x in [-8.888, 8.888], y in [-5, 5] (36 px/world-unit). The small cave box sits in the right
        // portion so the test's left "world" rect stays raw-bright and its "cave" rect stays dark-with-a-core.
        private const float OrthographicSize = 5f;
        private static readonly Vector3 CameraPosition = new Vector3(0f, 0f, -10f);
        private static readonly Color WorldClearColor = new Color(0.86f, 0.86f, 0.92f, 1f);

        private static readonly Vector3 CaveCenter = new Vector3(5f, 0f, 0f);
        private static readonly Vector2 CaveSize = new Vector2(5f, 5f);
        private const float CaveBlendDistance = 0.4f;
        private const int CavePriority = 1;

        // Torch light radii (LightSprite localScale). Kept modest so the empty cave (no light-absorbing rock,
        // unlike the Rocket Tilemap cave) stays mostly dark for the cave-mean gate while the radial cores stay
        // bright for the central-core gate. A verify-time fix pass may retune these radii against the captured
        // statistics; it must NEVER loosen the smoke test's luminance gates.
        private static readonly Vector3 WarmTorchPosition = new Vector3(5f, 0f, 0f);
        private static readonly Color WarmTorchColor = new Color(1f, 0.85f, 0.55f, 1f);
        private const float WarmTorchRadius = 2.2f;

        private static readonly Vector3 CyanTorchPosition = new Vector3(6.3f, -1f, 0f);
        private static readonly Color CyanTorchColor = new Color(0.4f, 0.8f, 1f, 1f);
        private const float CyanTorchRadius = 5.0f;

        private static readonly Vector3 WallPosition = new Vector3(3.5f, 0f, 0f);
        private static readonly Vector3 WallScale = new Vector3(0.4f, 3.5f, 1f);

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
            var crystalPrefab = LoadRequired<GameObject>(LightingCrystalsPrefabGuid, "LightingCrystals prefab");
            var wallSprite = LoadRequired<Sprite>(WallSpriteGuid, "wall sprite");
            var obstacleMaterial = LoadRequired<Material>(ObstacleMaterialGuid, "obstacle material");

            // Author into a fresh empty scene so nothing but the demo content is serialized.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightingSystem = CreateCamera(overlayMaterial, ambientMaterial);
            CreateCaveVolume(lightingSystem);
            SpawnTorch(scene, pointLightPrefab, WarmTorchPosition, WarmTorchColor, WarmTorchRadius,
                lightingSystem, "Warm Torch");
            SpawnTorch(scene, crystalPrefab, CyanTorchPosition, CyanTorchColor, CyanTorchRadius,
                lightingSystem, "Cyan Torch");
            CreateWall(wallSprite, obstacleMaterial, lightingSystem);

            EditorSceneManager.MarkSceneDirty(scene);
            var sceneAsset = examplesRoot + "/" + SceneFileName;
            if (!EditorSceneManager.SaveScene(scene, sceneAsset))
                throw new InvalidOperationException($"Failed to save the demo scene to '{sceneAsset}'.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // Normalize to the repo convention (trailing whitespace stripped, LF-only, trailing newline) so
            // this machine-generated scene matches the hand-maintained sample scenes and git whitespace checks.
            NormalizeSerializedScene(Path.GetFullPath(examplesRoot + "/" + SceneFileName));

            // Mirror the scene (+meta) into the package Samples~ source, byte-identical.
            MirrorSceneToPackage(examplesRoot);

            Debug.Log($"Built '{SceneFileName}' (bright world + coverage cave) and mirrored to Samples~/Examples.");
        }

        // Main Camera (orthographic) with a LightingSystem configured from the sample materials exactly like
        // the Rocket camera, but with DefaultCoverage = 0 so the world baseline is "lighting bypassed" (raw
        // bright game image); coverage volumes re-enable Light2D locally.
        private static LightingSystem CreateCamera(Material overlayMaterial, Material ambientMaterial)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";

            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = OrthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = WorldClearColor;
            camera.transform.position = CameraPosition;

            cameraObject.AddComponent<AudioListener>();

            var lightingSystem = cameraObject.AddComponent<LightingSystem>();
            lightingSystem.AmbientLightComputeMaterial = ambientMaterial;
            lightingSystem.LightOverlayMaterial = overlayMaterial;
            lightingSystem.DefaultCoverage = 0f;
            return lightingSystem;
        }

        // The single coverage volume: a small Box over the cave region, Coverage = 1 (lighting fully applies),
        // Priority above the world baseline so it re-enables Light2D where it overlaps.
        private static void CreateCaveVolume(LightingSystem lightingSystem)
        {
            var volumeObject = new GameObject("Cave Volume");
            volumeObject.transform.position = CaveCenter;

            var volume = volumeObject.AddComponent<Light2DVolume>();
            volume.Shape = Light2DVolume.VolumeShape.Box;
            volume.Size = CaveSize;
            volume.BlendDistance = CaveBlendDistance;
            volume.Priority = CavePriority;
            volume.Coverage = 1f;
            volume.Owner = lightingSystem;
        }

        // Instantiates a torch prefab (PointLight or LightingCrystals), disables its colour/scale randomiser
        // so the baked hues are deterministic, and drives its LightSprite child colour/owner/radius — the
        // exact idiom RocketTilemapBaker.SpawnLight uses.
        private static void SpawnTorch(Scene scene, GameObject prefab, Vector3 position, Color color,
            float radius, LightingSystem lightingSystem, string name)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = name;
            instance.transform.position = position;

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

        // A visible wall sprite (drawn into the game image inside the cave) with a child LightObstacleSprite
        // that mirrors it into the obstacle layer so the torches cast a shadow. LightObstacleSprite auto-fills
        // its GameSpriteRenderer from the parent renderer, so the child is parented under the sprite object.
        private static void CreateWall(Sprite wallSprite, Material obstacleMaterial, LightingSystem lightingSystem)
        {
            var wallObject = new GameObject("Cave Wall");
            wallObject.transform.position = WallPosition;
            wallObject.transform.localScale = WallScale;

            var spriteRenderer = wallObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = wallSprite;
            spriteRenderer.color = Color.white;
            spriteRenderer.sortingOrder = 0;

            var obstacleObject = new GameObject("Cave Wall Obstacle");
            obstacleObject.transform.SetParent(wallObject.transform, false);

            var obstacle = obstacleObject.AddComponent<LightObstacleSprite>();
            obstacle.Material = obstacleMaterial;
            obstacle.Owner = lightingSystem;
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
