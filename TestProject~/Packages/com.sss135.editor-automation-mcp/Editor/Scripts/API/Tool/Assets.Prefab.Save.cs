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
        public const string AssetsPrefabSaveToolId = "assets-prefab-save";
        [McpTool
        (
            AssetsPrefabSaveToolId,
            Title = "Assets / Prefab / Save",
            IdempotentHint = true
        )]
        [Description("Save a prefab. " +
            "Use it when you are in prefab editing mode in Unity Editor. " +
            "Use '" + AssetsPrefabOpenToolId + "' tool to open a prefab first.")]
        public AssetObjectRef Save(string? nothing = null) => MainThread.Instance.Run(() =>
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null)
                throw new InvalidOperationException(Error.PrefabStageIsNotOpened());

            var prefabGo = prefabStage.prefabContentsRoot;
            if (prefabGo == null)
                throw new InvalidOperationException(Error.PrefabStageIsNotOpened());

            var assetPath = prefabStage.assetPath;
            var goName = prefabGo.name;

            PrefabUtility.SaveAsPrefabAsset(prefabGo, assetPath);
            prefabStage.ClearDirtiness();

            EditorUtils.RepaintAllEditorWindows();

            var assetPrefab = AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(assetPath);
            return new AssetObjectRef(assetPrefab);
        });
    }
}