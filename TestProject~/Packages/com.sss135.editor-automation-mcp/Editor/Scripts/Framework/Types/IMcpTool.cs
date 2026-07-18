#nullable enable
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace com.SSS135.EditorAutomation.Editor.Framework
{
    /// <summary>
    /// The only shape the dispatcher sees for any tool. <c>RunTool</c> (added in
    /// <c>wave-0-runtime-and-schema</c>) is the canonical implementation; tests may
    /// provide their own.
    /// </summary>
    public interface IMcpTool
    {
        /// <summary>
        /// Dotted tool id (e.g. <c>"assets-copy"</c>) — comes from <see cref="McpToolAttribute.ToolId"/>.
        /// </summary>
        string Name { get; }

        /// <summary>Optional human-readable title.</summary>
        string? Title { get; }

        /// <summary>
        /// Long-form description used in JSON schema and the live wiki.
        /// Sourced from <c>[Description]</c> on the underlying method by default.
        /// </summary>
        string? Description { get; }

        /// <summary>
        /// Optional concise description used for the SKILL.md YAML <c>description:</c> field.
        /// When null the wiki/skill emitters fall back to <see cref="Description"/>.
        /// </summary>
        string? SkillDescription { get; }

        /// <summary>
        /// JSON Schema (draft 2020-12) for the tool's named parameters. The dispatcher reads
        /// <c>required</c> off this node for auto-param validation before invocation.
        /// </summary>
        JsonNode? InputSchema { get; }

        /// <summary>The four MCP tool-hint flags as a snapshot bag.</summary>
        McpToolHints ToolHints { get; }

        /// <summary>
        /// When true (default), the dispatcher marshals invocation onto Unity's main thread.
        /// When false, the dispatcher invokes the tool directly on the worker thread so the call
        /// remains responsive while the Editor is unfocused / recompile-paused. Only tools that
        /// touch no Unity main-thread APIs (e.g. read-only registry queries) should opt out.
        /// </summary>
        bool MainThreadRequired => true;

        /// <summary>
        /// Invoke the underlying method with the supplied JSON-element-valued named params.
        /// </summary>
        /// <param name="requestId">Per-call request id assigned by the dispatcher.</param>
        /// <param name="namedParams">Named arguments parsed from the JSON-RPC <c>arguments.args</c> object.</param>
        /// <param name="ct">Cancellation token (currently fed from the HTTP listener).</param>
        Task<McpResponse> InvokeAsync(
            string requestId,
            IReadOnlyDictionary<string, JsonElement> namedParams,
            CancellationToken ct);
    }
}
