#nullable enable
using System;
using System.ComponentModel;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.Utils;
using EA;
using com.SSS135.EditorAutomation.Runtime.Extensions;
using com.SSS135.EditorAutomation.Runtime.Utils;
using com.SSS135.EditorAutomation.Utils;
using Microsoft.Extensions.Logging;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_GameObject
    {
        public const string GameObjectDestroyToolId = "gameobject-destroy";
        [McpTool
        (
            GameObjectDestroyToolId,
            Title = "GameObject / Destroy",
            DestructiveHint = true
        )]
        [Description("Destroy GameObject and all nested GameObjects recursively in opened Prefab or in a Scene. " +
            "Use '" + GameObjectFindToolId + "' tool to find the target GameObject first.")]
        public DestroyGameObjectResult Destroy(GameObjectRef gameObjectRef)
        {
            if (gameObjectRef == null)
                throw new ArgumentNullException(nameof(gameObjectRef), "No GameObject reference provided.");

            if (!gameObjectRef.IsValid(out var gameObjectValidationError))
                throw new ArgumentException(gameObjectValidationError, nameof(gameObjectRef));

            return MainThread.Instance.Run(() =>
            {
                var logger = UnityLoggerFactory.LoggerFactory.CreateLogger<Tool_GameObject>();

                var go = gameObjectRef.FindGameObject(out var error);
                if (error != null)
                    throw new Exception(error);

                var destroyedName = go!.name;
                var destroyedPath = go.GetPath();
                var destroyedInstanceId = go.GetEntityId();

                logger.LogInformation("Destroying GameObject '{Name}' (InstanceID: {InstanceId}) at path '{Path}'",
                    destroyedName, destroyedInstanceId, destroyedPath);

                UnityEngine.Object.DestroyImmediate(go);

                logger.LogInformation("Successfully destroyed GameObject '{Name}' (InstanceID: {InstanceId})",
                    destroyedName, destroyedInstanceId);

                EditorUtils.RepaintAllEditorWindows();

                return new DestroyGameObjectResult
                {
                    DestroyedName = destroyedName,
                    DestroyedPath = destroyedPath,
                    DestroyedInstanceId = destroyedInstanceId
                };
            });
        }

    }
}
