#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using com.SSS135.EditorAutomation.Editor.Framework;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Recorder
    {
        [McpTool
        (
            "recorder-get-state",
            Title = "Recorder / Get State",
            ReadOnlyHint = true,
            IdempotentHint = true,
            MainThreadRequired = true
        )]
        [Description("Get the as-of value of one recorded object's tags at a specific frame. " +
            "For each requested tag the last value set at or before 'frame' is returned (carry-forward); " +
            "a tag with no value at or before the frame is marked NoData. " +
            "Call 'recorder-describe-session' first to discover valid keys and tags.")]
        public StateResult GetState(
            [Description("\"live\" for the running session, or a recording file path / id.")]
            string session,
            [Description("Object key, e.g. \"iid:123\". From the describe-session object catalog.")]
            string key,
            [Description("Tags to read. Omit (null) for every tag recorded on the object.")]
            List<string>? tags = null,
            [Description("Frame to evaluate the as-of state at.")]
            int frame = 0)
        {
            if (string.IsNullOrEmpty(session))
                throw new System.ArgumentException(Error.SessionRequired(), nameof(session));

            var engine = ResolveEngine(session);
            return RunMain(() => engine.GetState(key, tags, frame));
        }
    }
}
