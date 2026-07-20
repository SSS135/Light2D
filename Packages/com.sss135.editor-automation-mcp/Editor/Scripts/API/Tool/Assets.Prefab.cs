#nullable enable
using com.SSS135.EditorAutomation.Editor.Framework;
using UnityEditor;

namespace com.SSS135.EditorAutomation.Editor.API
{
    [McpToolType]
    public partial class Tool_Assets_Prefab
    {
        public static class Error
        {
            static string PrefabsPrinted => string.Join("\n", AssetDatabase.FindAssets("t:Prefab"));

            public static string PrefabPathIsEmpty()
                => "Prefab path is empty. Available prefabs:\n" + PrefabsPrinted;

            public static string NotFoundPrefabAtPath(string path)
                => $"Prefab '{path}' not found. Available prefabs:\n" + PrefabsPrinted;

            public static string PrefabPathIsInvalid(string path)
                => $"Prefab path '{path}' is invalid.";

            public static string PrefabStageIsNotOpened()
                => "Prefab stage is not opened. Use 'assets-prefab-open' to open it.";

            public static string PrefabStageIsAlreadyOpened()
                => "Prefab stage is already opened. Use 'assets-prefab-close' to close it.";
        }
    }
}
