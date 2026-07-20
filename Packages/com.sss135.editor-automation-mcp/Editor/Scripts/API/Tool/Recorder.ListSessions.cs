#nullable enable
using System.ComponentModel;
using System.Linq;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Editor.QueryEngine;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Recorder
    {
        [McpTool
        (
            "recorder-list-sessions",
            Title = "Recorder / List Sessions",
            ReadOnlyHint = true,
            IdempotentHint = true,
            MainThreadRequired = false
        )]
        [Description("List discoverable gameplay-recorder sessions: every recording file on disk plus the live " +
            "in-memory session when one is recording. Each entry gives the id, path (or \"live\"), label, and " +
            "frame range — use one as the 'session' argument of the other recorder tools. " +
            "Pure file IO, so it stays callable while the Editor is busy recompiling.")]
        public SessionListResult ListSessions()
        {
            var sessions = SessionResolver.ListSessions().ToArray();
            return new SessionListResult
            {
                Sessions = sessions,
                Meta = new EnvelopeMeta { TotalCount = sessions.Length }
            };
        }
    }
}
