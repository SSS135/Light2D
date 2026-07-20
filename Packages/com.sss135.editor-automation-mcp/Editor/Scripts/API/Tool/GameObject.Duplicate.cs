#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.IvanMurzak.ReflectorNet.Utils;
using EA;
using com.SSS135.EditorAutomation.Runtime.Extensions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_GameObject
    {
        public const string GameObjectDuplicateToolId = "gameobject-duplicate";
        [McpTool
        (
            GameObjectDuplicateToolId,
            Title = "GameObject / Duplicate"
        )]
        [Description("Duplicate GameObjects in opened Prefab or in a Scene. " +
            "Use '" + GameObjectFindToolId + "' tool to find the target GameObjects first.")]
        public DuplicatedObjectsResult Duplicate(GameObjectRefList gameObjectRefs)
        {
            return MainThread.Instance.Run(() =>
            {
                var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();

                var gos = new List<GameObject>(gameObjectRefs.Count);

                for (int i = 0; i < gameObjectRefs.Count; i++)
                {
                    var gameObjectRef = gameObjectRefs[i];
                    var go = gameObjectRefs[i].FindGameObject(out var error);
                    if (error != null)
                        throw new System.Exception(error);
                    if (go == null)
                        throw new System.Exception($"GameObject by {nameof(gameObjectRefs)}[{i}] not found.");

                    gos.Add(go);
                }

#if UNITY_6000_3_OR_NEWER
                Selection.entityIds = gos.Select(go => go.GetEntityId()).ToArray();
#else
                Selection.instanceIDs = gos.Select(go => go.GetInstanceID()).ToArray();
#endif

                Unsupported.DuplicateGameObjectsUsingPasteboard();
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

                var modifiedScenes = Selection.gameObjects
                    .Select(go => go.scene)
                    .Distinct()
                    .ToList();

                foreach (var scene in modifiedScenes)
                    EditorSceneManager.MarkSceneDirty(scene);

                var duplicated = gos
                    .Select(go => new GameObjectRef(go))
                    .ToArray();
                return new DuplicatedObjectsResult { GameObjects = duplicated, Count = duplicated.Length };
            });
        }
    }
}
