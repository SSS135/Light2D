#nullable enable
using System.ComponentModel;

namespace com.SSS135.EditorAutomation.Editor.API
{
    // EnvelopeMeta is defined once in RecorderDtos.cs and reused here unqualified
    // (same namespace). Every profiler result DTO embeds it as `Meta`.

    /// <summary>One available profiler counter, as reported by <c>profiler.describe</c>.</summary>
    public class CounterInfo
    {
        [Description("Counter name, as passed to profiler.summarize-counter / get-series / find-spikes (e.g. \"Main Thread\").")]
        public string Name { get; set; } = "";

        [Description("Profiler category the counter belongs to (e.g. \"Render\"). Empty when unknown (loaded capture).")]
        public string Category { get; set; } = "";

        [Description("Physical unit of the counter values: \"ns\" | \"bytes\" | \"count\" | \"percent\" | \"hz\" | \"\".")]
        public string Unit { get; set; } = "";
    }

    /// <summary>
    /// One downsampled point in a <c>profiler.get-series</c> result. Named to avoid
    /// the recorder's <c>SeriesPoint</c> (same namespace, different shape).
    /// </summary>
    public class ProfilerSeriesPoint
    {
        [Description("Profiler frame index this value was read at.")]
        public int Frame { get; set; }

        [Description("Counter value at the frame, in the series' native unit.")]
        public double Value { get; set; }
    }

    /// <summary>One marker row in a <c>profiler.frame-breakdown</c> result.</summary>
    public class MarkerRow
    {
        [Description("Marker / function name (leaf aggregated by inverted+merged hierarchy).")]
        public string Name { get; set; } = "";

        [Description("Self time spent in this marker, in milliseconds.")]
        public double SelfMs { get; set; }

        [Description("Total time including children, in milliseconds.")]
        public double TotalMs { get; set; }

        [Description("Number of calls aggregated into this row.")]
        public int Calls { get; set; }

        [Description("Managed bytes allocated under this marker (GC.Alloc), in bytes.")]
        public long GcBytes { get; set; }
    }

    /// <summary>One detected spike in a <c>profiler.find-spikes</c> result.</summary>
    public class Spike
    {
        [Description("Profiler frame index where the spike occurred.")]
        public int Frame { get; set; }

        [Description("Counter value at the spike frame, in the counter's native unit.")]
        public double Value { get; set; }
    }

    /// <summary>One thread present in a profiler frame, in a <c>profiler.describe</c> result.</summary>
    public class ThreadInfo
    {
        [Description("Thread name (e.g. \"Main Thread\"). A thread is not a counter.")]
        public string Name { get; set; } = "";
    }

    /// <summary>Result of <c>profiler.describe</c>: the orientation call before other profiler tools.</summary>
    public class DescribeResult
    {
        [Description("True when the Profiler is actively recording (live session).")]
        public bool IsRecording { get; set; }

        [Description("True when the in-memory / loaded buffer holds at least one frame.")]
        public bool HasFrames { get; set; }

        [Description("First available profiler frame index. -1 when no frames.")]
        public int FirstFrame { get; set; }

        [Description("Last available profiler frame index. -1 when no frames.")]
        public int LastFrame { get; set; }

        [Description("Number of frames in the buffer.")]
        public int FrameCount { get; set; }

        [Description("Threads present in the last frame (a thread is not a counter).")]
        public ThreadInfo[] Threads { get; set; } = System.Array.Empty<ThreadInfo>();

        [Description("Available counters that can be summarized / charted.")]
        public CounterInfo[] Counters { get; set; } = System.Array.Empty<CounterInfo>();

        public EnvelopeMeta Meta { get; set; } = new EnvelopeMeta();
    }

    /// <summary>Result of <c>profiler.summarize-counter</c>: stats over a counter's per-frame series.</summary>
    public class CounterSummary
    {
        [Description("Counter that was summarized.")]
        public string Counter { get; set; } = "";

        [Description("Profiler category the counter belongs to. Empty when unknown.")]
        public string Category { get; set; } = "";

        [Description("Physical unit of the values: \"ns\" | \"bytes\" | \"count\" | \"percent\" | \"hz\" | \"\".")]
        public string Unit { get; set; } = "";

        [Description("First frame actually included in the summary.")]
        public int From { get; set; }

        [Description("Last frame actually included in the summary.")]
        public int To { get; set; }

        [Description("Number of frames that contributed a value.")]
        public int Count { get; set; }
        public double? Min { get; set; }
        public double? Max { get; set; }
        public double? Mean { get; set; }

        [Description("95th-percentile value over the window.")]
        public double? P95 { get; set; }

        [Description("99th-percentile value over the window.")]
        public double? P99 { get; set; }

        [Description("Frame at which the maximum occurred — the drill handle for profiler.frame-breakdown.")]
        public int? ArgMaxFrame { get; set; }

        [Description("Frame at which the minimum occurred — a drill-down handle.")]
        public int? ArgMinFrame { get; set; }

        public EnvelopeMeta Meta { get; set; } = new EnvelopeMeta();
    }

    /// <summary>Result of <c>profiler.get-series</c>: a peak-preserving downsampled counter series.</summary>
    public class CounterSeries
    {
        [Description("Counter that was charted.")]
        public string Counter { get; set; } = "";

        [Description("Profiler category the counter belongs to. Empty when unknown.")]
        public string Category { get; set; } = "";

        [Description("Physical unit of the values: \"ns\" | \"bytes\" | \"count\" | \"percent\" | \"hz\" | \"\".")]
        public string Unit { get; set; } = "";

        [Description("Downsampled points (first/last + per-bucket extrema survive).")]
        public ProfilerSeriesPoint[] Points { get; set; } = System.Array.Empty<ProfilerSeriesPoint>();

        public EnvelopeMeta Meta { get; set; } = new EnvelopeMeta();
    }

    /// <summary>Result of <c>profiler.frame-breakdown</c>: top markers for a single frame.</summary>
    public class FrameBreakdown
    {
        [Description("Frame the breakdown was computed for.")]
        public int Frame { get; set; }

        [Description("True when the frame contains a real scene-load marker (Application.LoadLevelAsync) — i.e. the spike is a load, not gameplay.")]
        public bool IsLoadFrame { get; set; }

        [Description("Total CPU frame time, in milliseconds.")]
        public double FrameTimeMs { get; set; }

        [Description("Thread the breakdown was read from.")]
        public string Thread { get; set; } = "";

        [Description("Column the markers were sorted by: \"self\" | \"total\" | \"gc\".")]
        public string SortedBy { get; set; } = "";

        [Description("Top markers for the frame (inverted+merged hierarchy).")]
        public MarkerRow[] Markers { get; set; } = System.Array.Empty<MarkerRow>();

        public EnvelopeMeta Meta { get; set; } = new EnvelopeMeta();
    }

    /// <summary>Result of <c>profiler.find-spikes</c>: outlier frames for a counter.</summary>
    public class SpikeList
    {
        [Description("Counter that was scanned for spikes.")]
        public string Counter { get; set; } = "";

        [Description("Physical unit of the values: \"ns\" | \"bytes\" | \"count\" | \"percent\" | \"hz\" | \"\".")]
        public string Unit { get; set; } = "";

        [Description("Absolute value threshold a frame had to exceed to count as a spike.")]
        public double ThresholdUsed { get; set; }

        [Description("Sigma multiplier used when the threshold was statistical (mean + sigma*stddev). Null for an absolute threshold.")]
        public double? Sigma { get; set; }

        [Description("Detected spikes, ordered by descending value.")]
        public Spike[] Spikes { get; set; } = System.Array.Empty<Spike>();

        public EnvelopeMeta Meta { get; set; } = new EnvelopeMeta();
    }

    /// <summary>Result of <c>profiler.load-capture</c>: the loaded file's frame range.</summary>
    public class LoadCaptureResult
    {
        [Description("Absolute path of the capture that was loaded into the global Profiler buffer.")]
        public string Path { get; set; } = "";

        [Description("First available profiler frame index in the loaded capture.")]
        public int FirstFrame { get; set; }

        [Description("Last available profiler frame index in the loaded capture.")]
        public int LastFrame { get; set; }

        [Description("Number of frames in the loaded capture.")]
        public int FrameCount { get; set; }

        public EnvelopeMeta Meta { get; set; } = new EnvelopeMeta();
    }
}
