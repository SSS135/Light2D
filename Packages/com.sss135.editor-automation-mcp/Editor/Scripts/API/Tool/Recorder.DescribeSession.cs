#nullable enable
using System.ComponentModel;
using com.SSS135.EditorAutomation.Editor.Framework;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Recorder
    {
        [McpTool
        (
            "recorder-describe-session",
            Title = "Recorder / Describe Session",
            ReadOnlyHint = true,
            IdempotentHint = true,
            MainThreadRequired = true
        )]
        [Description("MANDATORY FIRST CALL for any recorder query. Returns the session manifest: the object " +
            "catalog (key / label / path / type / frame range), the per-tag variable manifest (tag / kind / unit / " +
            "epsilon), the session's frame and time domain, and the total event count. Use the returned keys and " +
            "tags to drive 'recorder-get-state', 'recorder-summarize-change', 'recorder-get-series', and " +
            "'recorder-find-events'. Pass \"live\" to describe the running session or a recording file path / id.")]
        public SessionManifest DescribeSession(
            [Description("\"live\" for the running session, or a recording file path / id.")]
            string session)
        {
            if (string.IsNullOrEmpty(session))
                throw new System.ArgumentException(Error.SessionRequired(), nameof(session));

            var engine = ResolveEngine(session);
            return RunMain(() => engine.Describe());
        }
    }
}
