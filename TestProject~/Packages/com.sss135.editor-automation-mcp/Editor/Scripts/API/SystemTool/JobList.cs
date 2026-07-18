#nullable enable
using System.ComponentModel;
using System.Text.Json.Nodes;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Editor.Framework.Jobs;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Job
    {
        public const string JobListToolId = "system-job-list";

        [McpTool
        (
            JobListToolId,
            Title = "System / Job / List",
            ReadOnlyHint = true,
            IdempotentHint = true,
            MainThreadRequired = false
        )]
        [Description("List every job ticket the gateway currently tracks (running plus completed-within-TTL). " +
            "Returns slim entries — ticket_id, method, status, started_at. " +
            "Use 'system-job-status' on a specific ticket id for the full result body.")]
        public static McpResponse List()
        {
            var entries = JobRegistry.ListSlim();
            var arr = new JsonArray();
            foreach (var entry in entries)
                arr.Add(entry.DeepClone());

            return McpResponse.SuccessStructured(new JsonObject
            {
                ["tickets"] = arr,
                ["count"] = entries.Count,
            });
        }
    }
}
