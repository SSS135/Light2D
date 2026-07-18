using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
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
            var baseObjects = Selection.gameObjects.Select(o => o.GetComponent<Renderer>()).Where(r => r != null).ToList();
            if (baseObjects.Count == 0)
            {
                Debug.LogError("Can't create light obstacle from selected object. You need to select any object with renderer attached to it to create light obstacle.");
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

                Undo.SetTransformParent(obstacleObj.transform, renderer.transform, "Parent Light Obstacle");
                Undo.RecordObject(obstacleObj.transform, "Reset Light Obstacle Transform");
                obstacleObj.transform.localPosition = Vector3.zero;
                obstacleObj.transform.localRotation = Quaternion.identity;
                obstacleObj.transform.localScale = Vector3.one;

                Undo.AddComponent<LightObstacleSprite>(obstacleObj);
                createdObjects.Add(obstacleObj);
            }

            Undo.CollapseUndoOperations(undoGroup);
            Selection.objects = createdObjects.ToArray();
        }

        [MenuItem("GameObject/Light2D/Light Obstacle", true)]
        private static bool CanCreateLightObstacle()
        {
            return Selection.gameObjects.Any(o => o.GetComponent<Renderer>() != null);
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
            light.Sprite = Resources.Load<Sprite>("DefaultLight");
            light.Color = new Color(1, 1, 1, 0.5f);
            Selection.activeGameObject = obj;
        }

        [MenuItem("GameObject/Light2D/Remove Obsolete 2DTK Support", false, 6)]
        public static void Disable2DToolkitSupport()
        {
            foreach (var target in GetNamedBuildTargets())
                UndefineSymbol("LIGHT2D_2DTK", target);
        }

        private static IEnumerable<NamedBuildTarget> GetNamedBuildTargets()
        {
            var targets = new HashSet<NamedBuildTarget>();
            foreach (BuildTargetGroup group in Enum.GetValues(typeof(BuildTargetGroup)))
            {
                var target = NamedBuildTarget.FromBuildTargetGroup(group);
                if (target != NamedBuildTarget.Unknown)
                    targets.Add(target);
            }

            return targets;
        }

        private static void UndefineSymbol(string symbol, NamedBuildTarget target)
        {
            PlayerSettings.GetScriptingDefineSymbols(target, out var defines);
            var updatedDefines = defines.Where(define => define != symbol).ToArray();
            if (updatedDefines.Length != defines.Length)
                PlayerSettings.SetScriptingDefineSymbols(target, updatedDefines);
        }
    }
}
