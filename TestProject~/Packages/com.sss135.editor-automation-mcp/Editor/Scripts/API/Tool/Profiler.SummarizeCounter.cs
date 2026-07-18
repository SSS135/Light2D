#nullable enable
using System.ComponentModel;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Editor.QueryEngine;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Profiler
    {
        [McpTool
        (
            "profiler-summarize-counter",
            Title = "Profiler / Summarize Counter",
            ReadOnlyHint = true,
            IdempotentHint = true,
            MainThreadRequired = true
        )]
        [Description("Summary stats (min/max/mean/p95/p99 + argmax/argmin frames) for one profiler counter " +
            "over a frame window. Report p95/p99, not just the mean, so a few spike frames don't mask the " +
            "steady state. The returned ArgMaxFrame is the drill handle: feed it to profiler.frame-breakdown " +
            "to see which markers dominated the worst frame. Call profiler.describe first for the counter list " +
            "and frame range. Operates on per-frame counters; an unknown name or one with no per-frame data " +
            "(e.g. a thread name like \"Main Thread\") throws. Use \"CPU Total Frame Time\" (ns) for CPU frame time.")]
        public CounterSummary SummarizeCounter(
            [Description("Counter name from profiler.describe, e.g. \"CPU Total Frame Time\" or \"GC Allocated In Frame\".")]
            string counter,
            [Description("Window start frame (inclusive). 0 / negative selects the first available frame.")]
            int from = 0,
            [Description("Window end frame (inclusive). 0 / negative selects the last available frame.")]
            int to = 0)
            => RunMain(() =>
            {
                if (!ProfilerSource.HasFrames)
                    throw new System.InvalidOperationException(Error.NoFrames());

                var (category, unit) = ProfilerSource.CounterMeta(counter);
                var result = new CounterSummary
                {
                    Counter = counter,
                    Category = category,
                    Unit = unit,
                    Meta = { TimeDomain = "render", Units = unit }
                };

                var samples = ProfilerSource.CounterSeries(counter, from, to);

                if (samples.Count == 0)
                    throw new System.InvalidOperationException(
                        string.IsNullOrEmpty(category) && string.IsNullOrEmpty(unit)
                            ? Error.UnknownCounter(counter)
                            : Error.CounterNoData(counter));

                var (count, min, max, mean, p95, p99, argMax, argMin) = ProfilerReductions.Summarize(samples);
                result.From = samples[0].Frame;
                result.To = samples[samples.Count - 1].Frame;
                result.Count = count;
                result.Min = min;
                result.Max = max;
                result.Mean = mean;
                result.P95 = p95;
                result.P99 = p99;
                result.ArgMaxFrame = argMax;
                result.ArgMinFrame = argMin;
                result.Meta.FrameRangeCovered = new[] { samples[0].Frame, samples[samples.Count - 1].Frame };
                return result;
            });
    }
}
