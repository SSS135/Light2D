#nullable enable
using System;
using System.Text.Json.Nodes;
using System.Threading;

namespace com.SSS135.EditorAutomation.Editor.Framework.Jobs
{
    /// <summary>
    /// Static helper for tools that need to return immediately while work continues
    /// in the background. Equivalent of Unreal's <c>Ctx.StartJob</c>: allocates a ticket
    /// via <see cref="JobRegistry"/>, invokes the caller's bind delegate with a
    /// <c>complete</c> callback wired to <see cref="JobRegistry.Complete"/>, and returns
    /// an <see cref="McpResponse"/> carrying the synchronous ticket payload.
    /// </summary>
    public static class JobHelpers
    {
        /// <summary>
        /// Start a long-running tool operation. Returns a <see cref="McpResponse.Processing(JsonNode?)"/>
        /// envelope whose structured body carries the ticket id and any supplied
        /// <paramref name="startedPayload"/> fields.
        /// </summary>
        /// <param name="method">Dotted method id used in monitor-log lines (e.g. <c>"package.add"</c>).</param>
        /// <param name="bind">
        /// Delegate that registers the background work. Receives the allocated ticket id and
        /// a <c>complete</c> callback the tool MUST invoke exactly once with the final
        /// <see cref="McpResponse"/>. Tools that survive a domain reload should persist the
        /// ticket id in <c>SessionState</c> and call <see cref="JobRegistry.Complete"/>
        /// directly from the post-reload notifier.
        /// </param>
        /// <param name="startedPayload">
        /// Optional structured fields stamped into both the synchronous Processing body and
        /// the monitor log's <c>started</c> event.
        /// </param>
        public static McpResponse StartJob(string method, Action<string, Action<McpResponse>> bind, JsonObject? startedPayload = null)
        {
            if (bind == null)
                throw new ArgumentNullException(nameof(bind));

            var args = new JobBindArgs(method, bind, startedPayload);
            return StartJob(args);
        }

        /// <summary>
        /// Lower-level overload mirroring Unreal's <c>FJobBindArgs</c> shape directly.
        /// </summary>
        public static McpResponse StartJob(JobBindArgs args)
        {
            if (args == null)
                throw new ArgumentNullException(nameof(args));

            var ticket = JobRegistry.Start(args.Method, args.StartedPayload);

            // The complete callback must remain re-entrancy safe: a binder may invoke it
            // synchronously before StartJob returns, which would otherwise race with the
            // caller observing the Processing envelope. JobRegistry.Complete is idempotent
            // (silently no-ops on second-call) so we can dispatch the callback without locking.
            void Complete(McpResponse response)
            {
                if (response == null)
                    return;
                JobRegistry.Complete(ticket.Id, response);
            }

            try
            {
                args.BindNativeDelegate.Invoke(ticket.Id, Complete);
            }
            catch (Exception ex)
            {
                // A bind delegate that throws synchronously must still leave the ticket in a
                // terminal state, otherwise it wedges forever in `running`.
                Complete(McpResponse.Error(ex));
                throw;
            }

            var body = new JsonObject
            {
                ["ticket_id"] = ticket.Id,
                ["method"] = ticket.Method,
                ["status"] = "running",
            };
            if (args.StartedPayload != null)
            {
                foreach (var kvp in args.StartedPayload)
                {
                    if (kvp.Key == "ticket_id" || kvp.Key == "method" || kvp.Key == "status")
                        continue;
                    body[kvp.Key] = kvp.Value?.DeepClone();
                }
            }
            return McpResponse.Processing(body);
        }

        /// <summary>
        /// Cancellation token bound to the ticket — passed to tools that opt into cooperative
        /// cancellation via <c>system.job_cancel</c>. Returns <see cref="CancellationToken.None"/>
        /// when the ticket is unknown.
        /// </summary>
        public static CancellationToken CancellationFor(string ticketId)
        {
            var ticket = JobRegistry.Find(ticketId);
            return ticket?.Cts.Token ?? CancellationToken.None;
        }
    }
}
