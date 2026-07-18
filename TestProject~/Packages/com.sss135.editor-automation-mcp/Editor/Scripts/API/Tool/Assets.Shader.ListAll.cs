#nullable enable
using System.ComponentModel;
using System.Linq;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Runtime.Utils;
using EA;
using UnityEditor;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Assets_Shader
    {
        public const string AssetsShaderListAllToolId = "assets-shader-list-all";
        [McpTool
        (
            AssetsShaderListAllToolId,
            Title = "Assets / List Shaders",
            ReadOnlyHint = true,
            IdempotentHint = true
        )]
        [Description("List all available shaders in the project assets and packages. " +
            "Returns their names. " +
            "Use this to find a shader name for '" + Tool_Assets.AssetsMaterialCreateToolId + "' tool.")]
        public ShaderListResult ListAll(string? nothing = null) => MainThread.Instance.Run(() =>
        {
            var shaders = ShaderUtils.GetAllShaders()
                .Where(shader => shader != null)
                .Select(shader => shader.name)
                .OrderBy(name => name)
                .ToArray();
            return new ShaderListResult { Shaders = shaders, Count = shaders.Length };
        });
    }
}