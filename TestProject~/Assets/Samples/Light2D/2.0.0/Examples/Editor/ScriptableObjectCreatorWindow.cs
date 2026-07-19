using UnityEditor;
using UnityEngine;

namespace Light2D.Examples
{
    internal static class BlockSetProfileCreator
    {
        [MenuItem("Assets/Create/Light2D Samples/Block Set Profile")]
        private static void Create()
        {
            var asset = ScriptableObject.CreateInstance<BlockSetProfile>();
            var path = AssetDatabase.GenerateUniqueAssetPath("Assets/Block Set Profile.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;
        }
    }
}
