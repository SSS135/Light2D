#nullable enable
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;

namespace com.SSS135.EditorAutomation.Editor.Framework.Runtime
{
    /// <summary>
    /// Per-request bag handed through the framework. The dispatcher builds one of these on each
    /// inbound call before delegating to <see cref="IMcpTool.InvokeAsync"/>; tools can consult it
    /// when they need the raw params or the cancellation token. The Wave 4 job-ticket layer adds
    /// a <c>StartJob</c>-equivalent helper that hangs off this context.
    /// </summary>
    public sealed class RequestContext
    {
        /// <summary>
        /// Per-call request id assigned by the dispatcher. Substituted into parameters marked
        /// with <see cref="RequestIDAttribute"/>.
        /// </summary>
        public string RequestId { get; }

        /// <summary>
        /// The named arguments as parsed from JSON-RPC <c>arguments.args</c>. Keys are the raw
        /// strings the client sent; ReflectorNet's binder performs case-insensitive matching
        /// against the method's actual parameter names.
        /// </summary>
        public IReadOnlyDictionary<string, JsonElement> RawParams { get; }

        /// <summary>
        /// Cancellation token fed from the HTTP listener / dispatcher.
        /// </summary>
        public CancellationToken Ct { get; }

        public RequestContext(
            string requestId,
            IReadOnlyDictionary<string, JsonElement> rawParams,
            CancellationToken ct)
        {
            RequestId = requestId;
            RawParams = rawParams;
            Ct = ct;
        }
    }
}
