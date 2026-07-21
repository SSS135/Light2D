using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
#if LIGHT2D_TILEMAP
using UnityEngine.Tilemaps;
#endif

namespace Light2D
{
    public static class Light2DMenu
    {
        [MenuItem("GameObject/Light2D/Lighting System", false, 6)]
        public static void CreateLightingSystem()
        {
            LightingSystemCreationWindow.CreateWindow();
        }

        [MenuItem("GameObject/Light2D/Light Obstacle", false, 6)]
        public static void CreateLightObstacle()
        {
            var baseObjects = Selection.gameObjects.Select(o => o.GetComponent<Renderer>())
                .Where(IsSupportedObstacleRenderer).ToList();
            if (baseObjects.Count == 0)
            {
                Debug.LogError("Can't create a light obstacle. Select an object with a SpriteRenderer, or a MeshRenderer with a MeshFilter.");
                return;
            }

            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Light Obstacles");
            var settings = Light2DObstacleSettings.GetOrCreate();
            var createdObjects = new List<GameObject>();
            foreach (var renderer in baseObjects)
            {
#if LIGHT2D_TILEMAP
                if (renderer is TilemapRenderer)
                {
                    // The Tilemap occluder reads the in-place Tilemap, so it lives ON THE SAME GameObject
                    // as the tilemap rather than in a child obstacle object.
                    foreach (var existing in renderer.GetComponents<LightObstacleTilemap>())
                        Undo.DestroyObjectImmediate(existing);
                    var tilemapObstacle = Undo.AddComponent<LightObstacleTilemap>(renderer.gameObject);
                    tilemapObstacle.MultiplicativeColor = settings.TexturedMultiplicativeColor;
                    tilemapObstacle.AdditiveColor = settings.AdditiveColor;
                    createdObjects.Add(renderer.gameObject);
                    continue;
                }
#endif

                var name = renderer.name + " Light Obstacle";

                var child = renderer.transform.Find(name);
                var obstacleObj = child == null ? new GameObject(name) : child.gameObject;
                if (child == null)
                    Undo.RegisterCreatedObjectUndo(obstacleObj, "Create Light Obstacle");

                foreach (var obstacleSprite in obstacleObj.GetComponents<LightObstacleSprite>())
                    Undo.DestroyObjectImmediate(obstacleSprite);
                foreach (var obstacleMesh in obstacleObj.GetComponents<LightObstacleMesh>())
                    Undo.DestroyObjectImmediate(obstacleMesh);

                Undo.SetTransformParent(obstacleObj.transform, renderer.transform, "Parent Light Obstacle");
                Undo.RecordObject(obstacleObj.transform, "Reset Light Obstacle Transform");
                obstacleObj.transform.localPosition = Vector3.zero;
                obstacleObj.transform.localRotation = Quaternion.identity;
                obstacleObj.transform.localScale = Vector3.one;

                if (renderer is SpriteRenderer)
                {
                    var obstacle = Undo.AddComponent<LightObstacleSprite>(obstacleObj);
                    obstacle.GameSpriteRenderer = renderer;
                    // Sprites are always textured, so the sprite alpha shapes the shadow; the textured
                    // default passes it through untinted.
                    obstacle.Color = settings.TexturedMultiplicativeColor;
                    obstacle.AdditiveColor = settings.AdditiveColor;
                }
                else
                {
                    var obstacle = Undo.AddComponent<LightObstacleMesh>(obstacleObj);
                    // Texture-conditional default: a textured source mesh keeps the texture-shaped white
                    // default, while a solid (untextured) mesh gets opaque black so it casts a full shadow
                    // instead of a silent no-op.
                    var textured = renderer.sharedMaterial != null && renderer.sharedMaterial.mainTexture != null;
                    obstacle.MultiplicativeColor = settings.MultiplicativeFor(textured);
                    obstacle.AdditiveColor = settings.AdditiveColor;
                }

                createdObjects.Add(obstacleObj);
            }

            Undo.CollapseUndoOperations(undoGroup);
            Selection.objects = createdObjects.ToArray();
        }

        [MenuItem("GameObject/Light2D/Light Obstacle", true)]
        private static bool CanCreateLightObstacle()
        {
            return Selection.gameObjects.Any(o => IsSupportedObstacleRenderer(o.GetComponent<Renderer>()));
        }

        [MenuItem("GameObject/Light2D/Light Source", false, 6)]
        public static void CreateLightSource()
        {
            var obj = new GameObject("Light");
            Undo.RegisterCreatedObjectUndo(obj, "Create Light Source");
            var light = Undo.AddComponent<LightSprite>(obj);
            var materialPath = AssetDatabase.GUIDToAssetPath("5d8645bc2e5501a4ebd138db5aa609bd");
            light.Material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            light.Sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Packages/com.sss13594.light2d/Runtime/Resources/DefaultLight.psd");
            light.Color = new Color(1, 1, 1, 0.5f);
            Selection.activeGameObject = obj;
        }

        [MenuItem("GameObject/Light2D/Ambient Emitter", false, 6)]
        public static void CreateAmbientEmitter()
        {
            var obj = new GameObject("Ambient Emitter");
            Undo.RegisterCreatedObjectUndo(obj, "Create Ambient Emitter");
            // AddComponent pulls in the required MeshFilter/MeshRenderer; the emitter's editor OnEnable
            // assigns its default material. Seeding the sprite gives an immediately visible emitter,
            // mirroring CreateLightSource; leave both sprite sources empty for self-mesh mode instead.
            var emitter = Undo.AddComponent<LightAmbientEmitter>(obj);
            emitter.Sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Packages/com.sss13594.light2d/Runtime/Resources/DefaultLight.psd");
            Selection.activeGameObject = obj;
        }

        [MenuItem("GameObject/Light2D/Light Volume", false, 6)]
        public static void CreateLightVolume()
        {
            var obj = new GameObject("Light Volume");
            Undo.RegisterCreatedObjectUndo(obj, "Create Light Volume");
            // The volume owns a procedural shape mesh and an internal coverage material, so nothing to
            // seed here; the default Box shape is visible through the component's authoring gizmo.
            Undo.AddComponent<Light2DVolume>(obj);
            Selection.activeGameObject = obj;
        }

        private static bool IsSupportedObstacleRenderer(Renderer renderer)
        {
            return renderer is SpriteRenderer ||
#if LIGHT2D_TILEMAP
                   renderer is TilemapRenderer ||
#endif
                   renderer is MeshRenderer && renderer.GetComponent<MeshFilter>() != null;
        }
    }
}
