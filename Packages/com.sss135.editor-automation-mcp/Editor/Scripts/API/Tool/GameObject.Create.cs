#nullable enable
using System;
using System.ComponentModel;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.Utils;
using EA;
using com.SSS135.EditorAutomation.Runtime.Extensions;
using com.SSS135.EditorAutomation.Runtime.Utils;
using UnityEditor;
using UnityEngine;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_GameObject
    {
        public const string GameObjectCreateToolId = "gameobject-create";
        [McpTool
        (
            GameObjectCreateToolId,
            Title = "GameObject / Create"
        )]
        [Description("Create a new GameObject in opened Prefab or in a Scene. " +
            "If needed - provide proper 'position', 'rotation' and 'scale' to reduce amount of operations.")]
        public GameObjectRef Create
        (
            [Description("Name of the new GameObject.")]
            string name,
            [Description("Parent GameObject reference. If not provided, the GameObject will be created at the root of the scene or prefab.")]
            GameObjectRef? parentGameObjectRef = null,
            [Description("Transform position of the GameObject.")]
            Vector3? position = null,
            [Description("Transform rotation of the GameObject. Euler angles in degrees.")]
            Vector3? rotation = null,
            [Description("Transform scale of the GameObject.")]
            Vector3? scale = null,
            [Description("World or Local space of transform.")]
            bool isLocalSpace = false,
            PrimitiveType? primitiveType = null
        )
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Name cannot be null or empty.", nameof(name));

            return MainThread.Instance.Run(() =>
            {
                var parentGo = default(GameObject);
                if (parentGameObjectRef?.IsValid(out _) == true)
                {
                    parentGo = parentGameObjectRef.FindGameObject(out var error);
                    if (error != null)
                        throw new ArgumentException(error, nameof(parentGameObjectRef));
                }

                position ??= Vector3.zero;
                rotation ??= Vector3.zero;
                scale ??= Vector3.one;

                var go = primitiveType != null
                    ? GameObject.CreatePrimitive(primitiveType.Value)
                    : new GameObject(name);

                go.name = name;

                // Set parent if provided
                if (parentGo != null)
                    go.transform.SetParent(parentGo.transform, false);

                // Set the transform properties
                go.SetTransform(
                    position: position,
                    rotation: rotation,
                    scale: scale,
                    isLocalSpace: isLocalSpace);

                EditorUtility.SetDirty(go);
                EditorUtils.RepaintAllEditorWindows();

                return new GameObjectRef(go);
            });
        }
    }
}
