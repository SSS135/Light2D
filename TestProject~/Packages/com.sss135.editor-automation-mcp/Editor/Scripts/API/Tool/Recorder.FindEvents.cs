#nullable enable
using System;
using System.ComponentModel;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Recorder;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Recorder
    {
        [McpTool
        (
            "recorder-find-events",
            Title = "Recorder / Find Events",
            ReadOnlyHint = true,
            IdempotentHint = true,
            MainThreadRequired = true
        )]
        [Description("Find recorded events matching a filter — the \"locate the bad moment\" tool. Filter by " +
            "event name, minimum severity, object key, and a frame window; results are ordered by frame and " +
            "carry the anchor frame plus the event's props. Feed an anchor frame back into 'recorder-get-state' " +
            "or 'recorder-summarize-change' to inspect what the state was around it.")]
        public EventsResult FindEvents(
            [Description("\"live\" for the running session, or a recording file path / id.")]
            string session,
            [Description("Exact event name to match (e.g. \"hit\", \"death\"). Omit (null) to match any name.")]
            string? name = null,
            [Description("Minimum severity to include: Trace | Debug | Info | Warning | Error | Fatal. " +
                "Omit (null) for any severity.")]
            string? severityMin = null,
            [Description("Restrict to events on one object key. Omit (null) to match every object plus global events.")]
            string? key = null,
            [Description("Window start frame (inclusive). Pass -1 (default) for no lower bound.")]
            int from = -1,
            [Description("Window end frame (inclusive). Pass -1 (default) for no upper bound.")]
            int to = -1,
            [Description("Maximum number of events to return. Capped at the engine's hard row limit regardless. " +
                "0 uses the default limit.")]
            int limit = 0)
        {
            if (string.IsNullOrEmpty(session))
                throw new ArgumentException(Error.SessionRequired(), nameof(session));

            Severity? severity = null;
            if (!string.IsNullOrEmpty(severityMin) && Enum.TryParse<Severity>(severityMin, true, out var parsed))
                severity = parsed;

            // -1 sentinel = unbounded; the engine takes int? bounds.
            int? fromFrame = from < 0 ? (int?)null : from;
            int? toFrame = to < 0 ? (int?)null : to;

            var engine = ResolveEngine(session);
            return RunMain(() => engine.FindEvents(name, severity, key, fromFrame, toFrame, limit));
        }
    }
}
