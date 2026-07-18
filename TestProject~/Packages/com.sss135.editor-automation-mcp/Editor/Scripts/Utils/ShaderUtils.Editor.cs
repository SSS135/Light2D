#nullable enable
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace com.SSS135.EditorAutomation.Runtime.Utils
{
    public static partial class ShaderUtils
    {
        public static IEnumerable<Shader> GetAllShaders()
        {
            var shaderGuids = AssetDatabase.FindAssets("t:Shader");
            return shaderGuids
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => AssetDatabase.LoadAssetAtPath<Shader>(path))
                .Where(shader => shader != null);
        }
    }
}
#endif
