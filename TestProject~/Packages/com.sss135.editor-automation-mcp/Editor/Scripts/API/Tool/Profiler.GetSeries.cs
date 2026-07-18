#nullable enable
using System.ComponentModel;
using System.Linq;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Editor.QueryEngine;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Profiler
    {
        [McpTool
        (
            "profiler-get-series",
            Title = "Profiler / Get Series",
            ReadOnlyHint = true,
            IdempotentHint = true,
            MainThreadRequired = true
        )]
        [Description("Chart one counter's per-frame values over a window, peak-preserving downsampled to a bounded " +
            "point budget (the first, last, and per-bucket extrema survive, so spikes are never smoothed away). " +
            "Run 'profiler-summarize-counter' first to find the interesting window and the argmax frame, then drill " +
            "into a single frame with 'profiler-frame-breakdown'. Values are in the counter's native unit (time " +
            "counters in nanoseconds, e.g. \"CPU Total Frame Time\"); see the 'unit' field. An unknown " +
            "counter or one with no per-frame data throws. Reads whatever is currently loaded — a live recording " +
            "or the last 'profiler-load-capture'.")]
        public CounterSeries GetSeries(
            [Description("Counter name from the profiler-describe list, e.g. \"CPU Total Frame Time\".")]
            string counter,
            [Description("Window start frame (inclusive). 0 / negative selects the first available frame.")]
            int from = 0,
            [Description("Window end frame (inclusive). 0 / negative selects the last available frame.")]
            int to = 0,
            [Description("Maximum number of points to return. 0 uses the default budget of 200.")]
            int maxPoints = 0)
            => RunMain(() =>
            {
                if (!ProfilerSource.HasFrames)
                    throw new System.InvalidOperationException(Error.NoFrames());

                var samples = ProfilerSource.CounterSeries(counter, from, to);
                if (samples.Count == 0)
                {
                    var (cat, u) = ProfilerSource.CounterMeta(counter);
                    throw new System.InvalidOperationException(
                        string.IsNullOrEmpty(cat) && string.IsNullOrEmpty(u)
                            ? Error.UnknownCounter(counter)
                            : Error.CounterNoData(counter));
                }

                var points = ProfilerReductions.Downsample(samples, maxPoints > 0 ? maxPoints : 200,
                    out var total, out var elided);

                var seriesPoints = points
                    .Select(p => new ProfilerSeriesPoint { Frame = p.Frame, Value = p.Value })
                    .ToArray();

                var (category, unit) = ProfilerSource.CounterMeta(counter);
                return new CounterSeries
                {
                    Counter = counter,
                    Category = category,
                    Unit = unit,
                    Points = seriesPoints,
                    Meta = new EnvelopeMeta
                    {
                        Units = unit,
                        TimeDomain = "render",
                        FrameRangeCovered = seriesPoints.Length > 0
                            ? new[] { seriesPoints[0].Frame, seriesPoints[seriesPoints.Length - 1].Frame }
                            : null,
                        Reduction = "downsample",
                        TotalCount = total,
                        Elided = elided,
                        Truncated = elided > 0
                    }
                };
            });
    }
}
