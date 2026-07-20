#nullable enable
using System.ComponentModel;
using com.SSS135.EditorAutomation.Editor.Framework;

namespace com.SSS135.EditorAutomation.Editor.API
{
    [McpToolType]
    public partial class Tool_Ping
    {
        public const string PingToolId = "ping";
        [McpTool
        (
            PingToolId,
            Title = "Ping",
            ReadOnlyHint = true,
            IdempotentHint = true
        )]
        [Description("Lightweight readiness probe. Returns the input message or 'pong' if omitted.")]
        public string Ping
        (
            [Description("Optional message to echo back.")]
            string? message = null
        )
        {
            return message ?? "pong";
        }
    }
}
