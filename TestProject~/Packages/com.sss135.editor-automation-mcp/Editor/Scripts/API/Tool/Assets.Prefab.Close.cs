#nullable enable
using System;
using System.ComponentModel;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.Utils;
using EA;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Assets_Prefab
    {
        public const string AssetsPrefabCloseToolId = "assets-prefab-close";
        [McpTool
        (
            AssetsPrefabCloseToolId,
            Title = "Assets / Prefab / Close"
        )]
        [Description("Close currently opened prefab. " +
            "Use it when you are in prefab editing mode in Unity Editor. " +
            "Use '" + AssetsPrefabOpenToolId + "' tool to open a prefab first.")]
        public AssetObjectRef Close
        (
            [Description("True to save prefab. False to discard changes.")]
            bool save = true
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                if (prefabStage == null)
                    throw new InvalidOperationException(Error.PrefabStageIsNotOpened());

                var prefabGo = prefabStage.prefabContentsRoot;
                if (prefabGo == null)
                    throw new InvalidOperationException(Error.PrefabStageIsNotOpened());

                var assetPath = prefabStage.assetPath;

                if (save)
                    PrefabUtility.SaveAsPrefabAsset(prefabGo, assetPath);

                prefabStage.ClearDirtiness();

                StageUtility.GoBackToPreviousStage();

                EditorUtils.RepaintAllEditorWindows();

                var prefabAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(assetPath);

                return new AssetObjectRef(prefabAsset);
            });
        }
    }
}
