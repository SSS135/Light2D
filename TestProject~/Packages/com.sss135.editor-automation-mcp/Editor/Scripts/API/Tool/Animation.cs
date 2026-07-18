#nullable enable

using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Utils;
using Microsoft.Extensions.Logging;

namespace com.SSS135.EditorAutomation.Editor.API
{
    [McpToolType]
    public static partial class Tool_Animation
    {
        // Cached logger reused by sibling partials (Animation.Create.cs, Animation.GetData.cs, Animation.Modify.cs).
        static readonly ILogger _logger =
            UnityLoggerFactory.LoggerFactory.CreateLogger(typeof(Tool_Animation));
    }
}
