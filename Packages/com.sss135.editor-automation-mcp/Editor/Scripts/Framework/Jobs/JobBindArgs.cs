#nullable enable
using System;
using System.Text.Json.Nodes;

namespace com.SSS135.EditorAutomation.Editor.Framework.Jobs
{
    /// <summary>
    /// Arguments passed to <see cref="JobHelpers.StartJob"/>. Mirrors the shape of Unreal's
    /// <c>FJobBindArgs</c>: the dotted method id, optional immediate-response payload that
    /// will be returned synchronously alongside the ticket id, and a bind delegate the
    /// caller fills in with the background work.
    /// </summary>
    /// <remarks>
    /// The bind delegate receives a single argument: an <see cref="Action{T}"/> callback
    /// the tool invokes exactly once when the long-running operation has finished. The
    /// supplied <see cref="McpResponse"/> becomes the ticket's final result and the body
    /// of the matching <c>system.job_status</c> response.
    /// </remarks>
    public sealed class JobBindArgs
    {
        /// <summary>Dotted method id of the originating tool (e.g. <c>"package.add"</c>).</summary>
        public string Method { get; }

        /// <summary>
        /// Optional payload returned synchronously inside the <see cref="McpResponse.Processing(JsonNode?)"/>
        /// envelope. Typically describes the work that was queued; the ticket id is appended
        /// to it automatically by <see cref="JobHelpers"/>.
        /// </summary>
        public JsonObject? StartedPayload { get; }

        /// <summary>
        /// Delegate that registers the actual background work. Receives the allocated ticket id
        /// plus a <c>complete</c> callback the tool must invoke exactly once with the final
        /// <see cref="McpResponse"/>. The ticket id is exposed so binders can persist it across
        /// a domain reload (typical for Package.Add / Script.Execute) and call
        /// <see cref="JobRegistry.Complete"/> directly from the post-reload notifier.
        /// </summary>
        public Action<string, Action<McpResponse>> BindNativeDelegate { get; }

        public JobBindArgs(string method, Action<string, Action<McpResponse>> bindNativeDelegate, JsonObject? startedPayload = null)
        {
            Method = method ?? string.Empty;
            BindNativeDelegate = bindNativeDelegate ?? throw new ArgumentNullException(nameof(bindNativeDelegate));
            StartedPayload = startedPayload;
        }
    }
}
