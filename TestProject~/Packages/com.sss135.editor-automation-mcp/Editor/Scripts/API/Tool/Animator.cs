#nullable enable

using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Utils;
using Microsoft.Extensions.Logging;

namespace com.SSS135.EditorAutomation.Editor.API
{
    [McpToolType]
    public static partial class Tool_Animator
    {
        // Cached logger reused by sibling partials (Animator.Create.cs, Animator.GetData.cs, Animator.Modify.cs).
        static readonly ILogger _logger =
            UnityLoggerFactory.LoggerFactory.CreateLogger(typeof(Tool_Animator));
    }
}
