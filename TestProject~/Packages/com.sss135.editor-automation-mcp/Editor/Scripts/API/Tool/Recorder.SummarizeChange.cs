#nullable enable
using System.ComponentModel;
using com.SSS135.EditorAutomation.Editor.Framework;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Recorder
    {
        [McpTool
        (
            "recorder-summarize-change",
            Title = "Recorder / Summarize Change",
            ReadOnlyHint = true,
            IdempotentHint = true,
            MainThreadRequired = true
        )]
        [Description("Summarize how one tag of one object changed over a frame window: start/end value, " +
            "net delta, min/max/mean, the argmax/argmin frames (drill-down handles), and the number of " +
            "change points. The compact workhorse for \"how did X change from A to B\" — feed the argmax " +
            "frame back into 'recorder-get-state' or 'recorder-get-series' to inspect the peak.")]
        public ChangeSummary SummarizeChange(
            [Description("\"live\" for the running session, or a recording file path / id.")]
            string session,
            [Description("Object key, e.g. \"iid:123\". From the describe-session object catalog.")]
            string key,
            [Description("Tag to summarize, e.g. \"hp\". From the describe-session variable manifest.")]
            string tag,
            [Description("Window start frame (inclusive).")]
            int from = 0,
            [Description("Window end frame (inclusive).")]
            int to = 0)
        {
            if (string.IsNullOrEmpty(session))
                throw new System.ArgumentException(Error.SessionRequired(), nameof(session));

            var engine = ResolveEngine(session);
            return RunMain(() => engine.SummarizeChange(key, tag, from, to));
        }
    }
}
