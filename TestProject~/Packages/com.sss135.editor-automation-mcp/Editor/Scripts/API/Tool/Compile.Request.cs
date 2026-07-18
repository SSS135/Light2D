#nullable enable
using System.ComponentModel;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Editor.Framework.Jobs;
using com.SSS135.EditorAutomation.Editor.Utils;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Compile
    {
        public const string CompileRequestToolId = "compile-request";

        [McpTool
        (
            CompileRequestToolId,
            Title = "Compile / Request",
            IdempotentHint = false
        )]
        [Description("Starts a normal Unity refresh/import compile check. " +
            "The call returns a job ticket before AssetDatabase.Refresh runs so the result survives compilation and domain reload. " +
            "Poll system.job-status with the returned ticket id for compile/no-compile status and compiler diagnostics.")]
        public McpResponse Request()
        {
            if (CompileRequestState.TryGetPendingTicket(out var pendingTicketId))
            {
                return McpResponse.Processing(new JsonObject
                {
                    ["ticket_id"] = pendingTicketId,
                    ["method"] = "compile.request",
                    ["status"] = "running",
                    ["coalesced"] = true,
                });
            }

            return JobHelpers.StartJob(
                method: "compile.request",
                startedPayload: new JsonObject
                {
                    ["operation"] = "normal_refresh_compile_check",
                },
                bind: (ticketId, complete) =>
                {
                    CompileRequestState.Start(ticketId);
                    MainThread.Instance.RunAsync(async () =>
                    {
                        await Task.Yield();
                        EditorWin32.TryWakeEditor();
                        CompileRequestState.RunNormalRefresh(ticketId);
                    });
                });
        }
    }
}
