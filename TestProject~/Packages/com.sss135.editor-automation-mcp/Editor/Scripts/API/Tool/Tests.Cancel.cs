#nullable enable
using System.ComponentModel;
using System.Text.Json.Nodes;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.API.TestRunner;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Editor.Framework.Jobs;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public static partial class Tool_Tests
    {
        public const string TestsCancelToolId = "tests-cancel";

        [McpTool
        (
            TestsCancelToolId,
            Title = "Tests / Cancel",
            DestructiveHint = true,
            IdempotentHint = true,
            MainThreadRequired = false
        )]
        [Description("Cancel an in-flight tests-run job. Reads the captured TestRunner GUID from " +
            "RunGuidRegistry and calls TestRunnerApi.CancelTestRun. On a successful cancel the ticket is " +
            "completed by TestResultCollector.RunFinished; when CancelTestRun returns false (no active run) " +
            "this tool completes the ticket directly with a Cancelled envelope so callers do not see a dangling ticket.")]
        public static McpResponse Cancel
        (
            [Description("Ticket id returned by tests-run. Used for response correlation; the actual cancel uses the captured TestRunner GUID.")]
            string ticketId
        )
        {
            if (string.IsNullOrEmpty(ticketId))
                return McpResponse.Error("ticketId is required.");

            var ticket = JobRegistry.Find(ticketId);
            if (ticket == null)
            {
                return McpResponse.SuccessStructured(new JsonObject
                {
                    ["ticket_id"] = ticketId,
                    ["status"] = "unknown_ticket",
                    ["hint"] = "This ticket id is not in the registry. It either never existed in this Editor session, or was cleared by an Editor domain reload (assembly recompile). Start a new request.",
                });
            }

            var guid = RunGuidRegistry.RunGuid.Value ?? string.Empty;
            if (string.IsNullOrEmpty(guid))
            {
                return McpResponse.SuccessStructured(new JsonObject
                {
                    ["ticket_id"] = ticketId,
                    ["status"] = "no_active_run",
                    ["hint"] = "No TestRunner run guid is currently captured. Either no tests-run is in flight, or the run already finished. Nothing to cancel.",
                });
            }

            // CancelTestRun touches Unity test framework internals — hop to the main thread.
            // Use the fully qualified type name: Tool_Tests exposes a static instance property
            // also named "TestRunnerApi", which shadows the type within this partial class.
            var cancelled = MainThread.Instance.Run(
                () => UnityEditor.TestTools.TestRunner.Api.TestRunnerApi.CancelTestRun(guid));

            if (cancelled)
            {
                // Unity will fire RunFinished shortly. TestResultCollector calls JobRegistry.Complete with
                // the Cancelled-shaped envelope; do not complete the ticket here or we double-signal it.
                return McpResponse.SuccessStructured(new JsonObject
                {
                    ["ticket_id"] = ticketId,
                    ["status"] = "cancel_requested",
                    ["guid"] = guid,
                });
            }

            // CancelTestRun returned false — the run guid is no longer active (already finished,
            // already cancelling, or never matched). Complete the ticket directly with a Cancelled
            // envelope so the caller's system-job-status / system-job-wait sees a terminal state
            // instead of forever-`running`.
            JobRegistry.Complete(ticketId, BuildCancelledEnvelope(ticketId, guid));

            return McpResponse.SuccessStructured(new JsonObject
            {
                ["ticket_id"] = ticketId,
                ["status"] = "cancel_failed",
                ["guid"] = guid,
                ["hint"] = "no active run for this guid; ticket marked Cancelled to avoid dangling.",
            });
        }

        // Build the Cancelled-status McpResponse used to complete the ticket directly when
        // CancelTestRun returns false. McpResponse.Cancelled takes only a message string; the
        // structured payload is attached on the side so callers reading via system-job-status
        // see why the ticket was cancelled.
        static McpResponse BuildCancelledEnvelope(string ticketId, string guid)
        {
            return new McpResponse(
                status: McpStatus.Cancelled,
                content: null,
                structuredResult: new JsonObject
                {
                    ["ticket_id"] = ticketId,
                    ["guid"] = guid,
                    ["reason"] = "cancel_failed_no_active_run",
                });
        }
    }
}
