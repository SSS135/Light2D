#nullable enable
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Editor.Framework.Jobs;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Job
    {
        public const string JobWaitToolId = "system-job-wait";

        [McpTool
        (
            JobWaitToolId,
            Title = "System / Job / Wait",
            ReadOnlyHint = true,
            IdempotentHint = true,
            MainThreadRequired = false
        )]
        [Description("Block server-side until a job ticket reaches a terminal state or the timeout expires. " +
            "Returns the same payload shape as system-job-status, plus a 'timed_out' flag and an optional " +
            "'status_filter_unmatched' flag when terminalStates excludes the actual outcome.")]
        public static async Task<McpResponse> Wait
        (
            [Description("Ticket id returned by the originating tool (format: 'j_<utc>_<8hex>').")]
            string ticketId,
            [Description("Maximum seconds to wait. Defaults to 30. Must be > 0.")]
            int timeoutSeconds = 30,
            [Description("Optional list of terminal statuses to wait for, from {completed, cancelled, errored}. " +
                "Default: any non-running status ends the wait. When supplied, a terminal outcome not in the list " +
                "still ends the wait but sets status_filter_unmatched: true on the body.")]
            string[]? terminalStates = null
        )
        {
            if (string.IsNullOrEmpty(ticketId))
                return McpResponse.Error("ticketId is required.");

            if (timeoutSeconds <= 0)
                return McpResponse.Error("timeoutSeconds must be > 0.");

            var ticket = JobRegistry.Find(ticketId);
            if (ticket == null)
            {
                if (JobMonitorLog.TryFindTerminalEvent(ticketId, out var envelope) && envelope != null)
                    return McpResponse.SuccessStructured(BuildBodyFromJsonl(ticketId, envelope));

                return McpResponse.SuccessStructured(new JsonObject
                {
                    ["ticket_id"] = ticketId,
                    ["status"] = "unknown_ticket",
                    ["hint"] = "This ticket id is not in the registry. It either never existed in this Editor session, or was cleared by an Editor domain reload (assembly recompile). Start a new request.",
                });
            }

            // Fast path: ticket already terminal at lookup time.
            if (ticket.Status != JobStatus.Running)
                return BuildWaitResponse(ticket, timedOut: false, terminalStates);

            // Block on the terminal TCS or the timeout, whichever fires first.
            var delay = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
            var completed = await Task.WhenAny(ticket.CompletionTcs.Task, delay).ConfigureAwait(false);
            var timedOut = completed != ticket.CompletionTcs.Task;

            return BuildWaitResponse(ticket, timedOut, terminalStates);
        }

        // Build the response body, stamping timed_out and (optionally) status_filter_unmatched
        // when the caller's terminalStates filter excluded the actual outcome.
        static McpResponse BuildWaitResponse(JobTicket ticket, bool timedOut, string[]? terminalStates)
        {
            var body = JobRegistry.BuildStatusBody(ticket);
            body["timed_out"] = timedOut;

            if (!timedOut && terminalStates != null && terminalStates.Length > 0)
            {
                var actual = body["status"]?.GetValue<string>() ?? string.Empty;
                var matched = false;
                foreach (var s in terminalStates)
                {
                    if (string.Equals(s, actual, StringComparison.OrdinalIgnoreCase))
                    {
                        matched = true;
                        break;
                    }
                }
                if (!matched)
                    body["status_filter_unmatched"] = true;
            }

            return McpResponse.SuccessStructured(body);
        }
    }
}
