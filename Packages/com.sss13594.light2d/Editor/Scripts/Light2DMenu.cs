using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

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
            var createdObjects = new List<GameObject>();
            foreach (var renderer in baseObjects)
            {
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
                }
                else
                {
                    var obstacle = Undo.AddComponent<LightObstacleMesh>(obstacleObj);
                    // Parity with the sprite path: CustomSprite.Color defaults to white, while the
                    // runtime LightObstacleMesh default stays (0,0,0,0) for serialized-scene compatibility.
                    obstacle.MultiplicativeColor = new Color32(255, 255, 255, 255);
                }

                var systems = LightingSystem.Instances;
                if (systems.Count == 1)
                    obstacleObj.layer = systems[0].LightObstaclesLayer;
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
            if (LightingSystem.Instance != null)
                obj.layer = LightingSystem.Instance.LightSourcesLayer;
            var light = Undo.AddComponent<LightSprite>(obj);
            var materialPath = AssetDatabase.GUIDToAssetPath("5d8645bc2e5501a4ebd138db5aa609bd");
            light.Material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            light.Sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Packages/com.sss13594.light2d/Runtime/Resources/DefaultLight.psd");
            light.Color = new Color(1, 1, 1, 0.5f);
            Selection.activeGameObject = obj;
        }

        private static bool IsSupportedObstacleRenderer(Renderer renderer)
        {
            return renderer is SpriteRenderer ||
                   renderer is MeshRenderer && renderer.GetComponent<MeshFilter>() != null;
        }
    }
}
