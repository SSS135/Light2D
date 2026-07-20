#nullable enable
using System;

namespace com.SSS135.EditorAutomation.Editor.Framework
{
    /// <summary>
    /// Marks a method as an MCP tool exposed via the in-Editor HTTP gateway.
    /// Same shape as the legacy <c>[McpPluginTool]</c> so Wave 1 migrations are mechanical
    /// attribute renames. No Standard/System split — every <c>[McpTool]</c> joins one registry.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class McpToolAttribute : Attribute
    {
        /// <summary>
        /// Dotted tool id, kebab-case (e.g. <c>"assets-copy"</c>).
        /// </summary>
        public string ToolId { get; }

        /// <summary>
        /// Optional human-readable title (e.g. <c>"Assets / Copy"</c>).
        /// </summary>
        public string? Title { get; set; }

        // Backing storage for the four MCP hint flags. Each is tracked with a "set" companion
        // so callers can distinguish "explicitly false" from "not provided".

        private bool _readOnlyHint;
        private bool _readOnlyHintSet;

        private bool _destructiveHint;
        private bool _destructiveHintSet;

        private bool _idempotentHint;
        private bool _idempotentHintSet;

        private bool _openWorldHint;
        private bool _openWorldHintSet;

        // Default true preserves existing behavior — every tool marshals to the main thread
        // unless explicitly opted out. Internal threading concern, not part of MCP hint surface.
        private bool _mainThreadRequired = true;
        private bool _mainThreadRequiredSet;

        /// <summary>
        /// If true, the tool only reads or queries data and does not modify system state.
        /// </summary>
        public bool ReadOnlyHint
        {
            get => _readOnlyHint;
            set { _readOnlyHint = value; _readOnlyHintSet = true; }
        }

        /// <summary>
        /// If true, the tool may perform destructive updates (e.g., deleting data, overwriting files).
        /// </summary>
        public bool DestructiveHint
        {
            get => _destructiveHint;
            set { _destructiveHint = value; _destructiveHintSet = true; }
        }

        /// <summary>
        /// If true, calling the tool multiple times with the same arguments will have no additional effect
        /// on its environment beyond the first call.
        /// </summary>
        public bool IdempotentHint
        {
            get => _idempotentHint;
            set { _idempotentHint = value; _idempotentHintSet = true; }
        }

        /// <summary>
        /// If true, the tool may interact with an "open world" of external entities
        /// (e.g., the web, external APIs, or real-world systems).
        /// </summary>
        public bool OpenWorldHint
        {
            get => _openWorldHint;
            set { _openWorldHint = value; _openWorldHintSet = true; }
        }

        /// <summary>The <see cref="ReadOnlyHint"/> value, or null if it was not explicitly set.</summary>
        public bool? ReadOnlyHintValue => _readOnlyHintSet ? _readOnlyHint : (bool?)null;

        /// <summary>The <see cref="DestructiveHint"/> value, or null if it was not explicitly set.</summary>
        public bool? DestructiveHintValue => _destructiveHintSet ? _destructiveHint : (bool?)null;

        /// <summary>The <see cref="IdempotentHint"/> value, or null if it was not explicitly set.</summary>
        public bool? IdempotentHintValue => _idempotentHintSet ? _idempotentHint : (bool?)null;

        /// <summary>The <see cref="OpenWorldHint"/> value, or null if it was not explicitly set.</summary>
        public bool? OpenWorldHintValue => _openWorldHintSet ? _openWorldHint : (bool?)null;

        /// <summary>
        /// If true (default), tool dispatches are marshalled onto Unity's main thread.
        /// Set to false for worker-thread-safe tools (e.g. read-only registry queries) so they
        /// remain dispatchable while the Editor is unfocused with a pending recompile.
        /// </summary>
        public bool MainThreadRequired
        {
            get => _mainThreadRequired;
            set { _mainThreadRequired = value; _mainThreadRequiredSet = true; }
        }

        /// <summary>True if <see cref="MainThreadRequired"/> was explicitly set by the attribute author.</summary>
        public bool MainThreadRequiredSet => _mainThreadRequiredSet;

        public McpToolAttribute(string toolId, string? title = null)
        {
            ToolId = toolId;
            Title = title;
        }

        /// <summary>
        /// Snapshot the four hint flags into a <see cref="McpToolHints"/> bag.
        /// </summary>
        public McpToolHints GetHints() => new McpToolHints(
            readOnlyHint: ReadOnlyHintValue,
            destructiveHint: DestructiveHintValue,
            idempotentHint: IdempotentHintValue,
            openWorldHint: OpenWorldHintValue);
    }
}
