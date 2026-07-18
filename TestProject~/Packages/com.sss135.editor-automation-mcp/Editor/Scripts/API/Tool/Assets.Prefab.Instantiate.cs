#nullable enable
using System;
using System.ComponentModel;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.Utils;
using EA;
using com.SSS135.EditorAutomation.Runtime.Utils;
using UnityEditor;
using UnityEngine;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Assets_Prefab
    {
        public const string AssetsPrefabInstantiateToolId = "assets-prefab-instantiate";
        [McpTool
        (
            AssetsPrefabInstantiateToolId,
            Title = "Assets / Prefab / Instantiate"
        )]
        [Description("Instantiates prefab in the current active scene. " +
            "Use '" + Tool_Assets.AssetsFindToolId + "' tool to find prefab assets in the project.")]
        public GameObjectRef Instantiate
        (
            [Description("Prefab asset path.")]
            string prefabAssetPath,
            [Description("GameObject path in the current active scene.")]
            string gameObjectPath,
            [Description("Transform position of the GameObject.")]
            Vector3? position = default,
            [Description("Transform rotation of the GameObject. Euler angles in degrees.")]
            Vector3? rotation = default,
            [Description("Transform scale of the GameObject.")]
            Vector3? scale = default,
            [Description("World or Local space of transform.")]
            bool isLocalSpace = false
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath);
                if (prefab == null)
                    throw new ArgumentException($"Prefab not found at path '{prefabAssetPath}'.", nameof(prefabAssetPath));

                var parentGo = default(GameObject);
                if (StringUtils.Path_ParseParent(gameObjectPath, out var parentPath, out var name))
                {
                    parentGo = GameObjectUtils.FindByPath(parentPath);
                    if (parentGo == null)
                        throw new ArgumentException($"GameObject with path '{parentPath}' not found.", nameof(gameObjectPath));
                }

                position ??= Vector3.zero;
                rotation ??= Vector3.zero;
                scale ??= Vector3.one;

                var go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (go == null)
                    throw new Exception($"Failed to instantiate prefab from path '{prefabAssetPath}'.");

                go.name = name ?? prefab.name;
                if (parentGo != null)
                    go.transform.SetParent(parentGo.transform, false);
                go.SetTransform(position, rotation, scale, isLocalSpace);

                EditorUtility.SetDirty(go);
                EditorUtils.RepaintAllEditorWindows();

                return new GameObjectRef(go);
            });
        }
    }
}
