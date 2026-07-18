#nullable enable
using System.ComponentModel;
using System.Text.Json.Nodes;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Editor.Framework.Jobs;

namespace com.SSS135.EditorAutomation.Editor.API
{
    [McpToolType]
    public partial class Tool_Job
    {
        public const string JobStatusToolId = "system-job-status";

        [McpTool
        (
            JobStatusToolId,
            Title = "System / Job / Status",
            ReadOnlyHint = true,
            IdempotentHint = true,
            MainThreadRequired = false
        )]
        [Description("Fetch the current status and (when finished) the final result of a long-running job ticket. " +
            "Use this after a tool returns a Processing response carrying a ticket id. " +
            "Returns NOT_FOUND-shaped error data when the ticket has aged out beyond the configured TTL.")]
        public static McpResponse Status
        (
            [Description("Ticket id returned by the originating tool (format: 'j_<utc>_<8hex>').")]
            string ticketId
        )
        {
            if (string.IsNullOrEmpty(ticketId))
                return McpResponse.Error("ticketId is required.");

            var ticket = JobRegistry.Find(ticketId);
            if (ticket == null)
            {
                // In-memory miss. Before reporting unknown_ticket, consult the JSONL monitor log —
                // a PlayMode domain reload wipes JobRegistry._tickets but TestResultCollector still
                // writes the terminal payload via JobRegistry.Complete -> JobMonitorLog.Append, so
                // a "completed" / "cancelled" / "error" line for this ticket may exist on disk.
                if (JobMonitorLog.TryFindTerminalEvent(ticketId, out var envelope) && envelope != null)
                    return McpResponse.SuccessStructured(BuildBodyFromJsonl(ticketId, envelope));

                return McpResponse.SuccessStructured(new JsonObject
                {
                    ["ticket_id"] = ticketId,
                    ["status"] = "unknown_ticket",
                    ["hint"] = "This ticket id is not in the registry. It either never existed in this Editor session, or was cleared by an Editor domain reload (assembly recompile). Start a new request.",
                });
            }

            return McpResponse.SuccessStructured(JobRegistry.BuildStatusBody(ticket));
        }

        // Reconstruct a system-job-status body from a single terminal JSONL envelope. Mirrors
        // JobRegistry.BuildStatusBody best-effort: started_at falls back to the line's ts when
        // no separate `started` line is consulted (we read only the terminal line on purpose).
        // The `source: "jsonl-fallback"` marker tells callers the payload was reconstructed
        // from disk and may be partial (no in-memory progress ring, no live updates).
        static JsonObject BuildBodyFromJsonl(string ticketId, JsonObject envelope)
        {
            string? method = null;
            try { method = envelope["method"]?.GetValue<string>(); } catch { }

            string? ev = null;
            try { ev = envelope["event"]?.GetValue<string>(); } catch { }

            string? ts = null;
            try { ts = envelope["ts"]?.GetValue<string>(); } catch { }

            var status = ev switch
            {
                "completed" => "completed",
                "cancelled" => "cancelled",
                "error" => "errored",
                _ => "unknown",
            };

            var body = new JsonObject
            {
                ["ticket_id"] = ticketId,
                ["method"] = method ?? string.Empty,
                ["status"] = status,
                ["source"] = "jsonl-fallback",
            };

            if (!string.IsNullOrEmpty(ts))
            {
                body["started_at"] = ts;
                body["completed_at"] = ts;
            }

            var data = envelope["data"];
            if (data != null)
                body["result"] = data.DeepClone();

            return body;
        }
    }
}
