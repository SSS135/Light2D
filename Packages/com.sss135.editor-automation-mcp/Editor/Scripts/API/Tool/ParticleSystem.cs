#nullable enable
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Utils;
using Microsoft.Extensions.Logging;

namespace com.SSS135.EditorAutomation.Editor.API
{
    [McpToolType]
    public partial class Tool_ParticleSystem
    {
        // Shared per-carrier logger (matches Reflection.MethodCall.cs:30-31 pattern).
        // Sibling partial files reference `_logger` instead of allocating per call.
        static readonly ILogger _logger =
            UnityLoggerFactory.LoggerFactory.CreateLogger<Tool_ParticleSystem>();
    }
}
