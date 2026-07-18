#nullable enable
namespace com.SSS135.EditorAutomation.Editor.Framework
{
    /// <summary>
    /// Bag of the four nullable MCP tool-hint flags. Extracted as a small immutable struct so
    /// <see cref="IMcpTool"/> and the underlying <see cref="McpToolAttribute"/> reuse one shape.
    /// </summary>
    public readonly struct McpToolHints
    {
        /// <summary>
        /// If true, the tool only reads or queries data and does not modify system state.
        /// Null when not explicitly set by the author.
        /// </summary>
        public bool? ReadOnlyHint { get; }

        /// <summary>
        /// If true, the tool may perform destructive updates (e.g., deleting data, overwriting files).
        /// Null when not explicitly set by the author.
        /// </summary>
        public bool? DestructiveHint { get; }

        /// <summary>
        /// If true, calling the tool multiple times with the same arguments has no additional effect
        /// on its environment beyond the first call. Null when not explicitly set by the author.
        /// </summary>
        public bool? IdempotentHint { get; }

        /// <summary>
        /// If true, the tool may interact with an "open world" of external entities
        /// (e.g., the web, external APIs, or real-world systems). Null when not explicitly set by the author.
        /// </summary>
        public bool? OpenWorldHint { get; }

        public McpToolHints(
            bool? readOnlyHint = null,
            bool? destructiveHint = null,
            bool? idempotentHint = null,
            bool? openWorldHint = null)
        {
            ReadOnlyHint = readOnlyHint;
            DestructiveHint = destructiveHint;
            IdempotentHint = idempotentHint;
            OpenWorldHint = openWorldHint;
        }

        /// <summary>Empty hints bag (every flag null).</summary>
        public static readonly McpToolHints None = default;
    }
}
