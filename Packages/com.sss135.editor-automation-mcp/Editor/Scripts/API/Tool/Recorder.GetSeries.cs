#nullable enable
using System.ComponentModel;
using com.SSS135.EditorAutomation.Editor.Framework;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Recorder
    {
        [McpTool
        (
            "recorder-get-series",
            Title = "Recorder / Get Series",
            ReadOnlyHint = true,
            IdempotentHint = true,
            MainThreadRequired = true
        )]
        [Description("Get the change-point series of one tag of one object over a frame window, reduced to a " +
            "bounded set of points. Reductions: \"summary\"/\"downsample\" return a peak-preserving budget of " +
            "points (the first, last, and per-bucket extrema survive); \"diff\" returns the raw change list " +
            "paginated by 'cursor'. The result is hard-capped regardless of request — there is no raw dump over " +
            "a wide range; an over-broad window comes back truncated with the elided count in the envelope. " +
            "Use 'recorder-summarize-change' first to find the interesting window, then page through it here.")]
        public SeriesResult GetSeries(
            [Description("\"live\" for the running session, or a recording file path / id.")]
            string session,
            [Description("Object key, e.g. \"iid:123\". From the describe-session object catalog.")]
            string key,
            [Description("Tag to fetch, e.g. \"pos\". From the describe-session variable manifest.")]
            string tag,
            [Description("Window start frame (inclusive).")]
            int from = 0,
            [Description("Window end frame (inclusive).")]
            int to = 0,
            [Description("Reduction: \"summary\" | \"downsample\" (peak-preserving point budget) or \"diff\" " +
                "(raw change list, paginated). Defaults to \"summary\".")]
            string reduction = "summary",
            [Description("Maximum number of points to return. Capped at the engine's hard row limit regardless. " +
                "0 uses the default budget.")]
            int maxPoints = 0,
            [Description("Opaque pagination cursor for \"diff\"; pass the 'nextCursor' from a prior page. Null for the first page.")]
            string? cursor = null)
        {
            if (string.IsNullOrEmpty(session))
                throw new System.ArgumentException(Error.SessionRequired(), nameof(session));

            var engine = ResolveEngine(session);
            return RunMain(() => engine.GetSeries(key, tag, from, to, reduction, maxPoints, cursor));
        }
    }
}
