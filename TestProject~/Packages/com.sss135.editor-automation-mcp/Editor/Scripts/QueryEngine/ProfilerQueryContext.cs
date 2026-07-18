#nullable enable
using System.Collections.Generic;
using com.SSS135.EditorAutomation.Editor.API;

namespace com.SSS135.EditorAutomation.Editor.QueryEngine
{
    /// <summary>
    /// Read-only view of the current Profiler buffer handed to a compiled
    /// <c>profiler-query</c> C# snippet as <c>q</c>. It forwards to
    /// <see cref="ProfilerSource"/> but exposes only the read surface — the
    /// buffer-replacing <see cref="ProfilerSource.LoadCapture"/> is deliberately
    /// unreachable, so a snippet can never swap out the live recording. There is
    /// intentionally no passthrough to the underlying source.
    /// </summary>
    public sealed class ProfilerQueryContext
    {
        public int FirstFrame => ProfilerSource.FirstFrame;
        public int LastFrame => ProfilerSource.LastFrame;
        public int FrameCount => ProfilerSource.FrameCount;
        public bool IsRecording => ProfilerSource.IsRecording;

        /// <summary>Every counter the live session exposes, with its category and unit string.</summary>
        public IReadOnlyList<CounterInfo> AvailableCounters() => ProfilerSource.AvailableCounters();

        /// <summary>
        /// Counter value for <paramref name="frame"/>, searching every thread.
        /// Null when no thread carries the counter at that frame.
        /// </summary>
        public double? CounterValue(int frame, string counter)
            => ProfilerSource.TryReadCounter(frame, counter, out var v) ? v : (double?)null;

        /// <summary>
        /// Per-frame readings of <paramref name="counter"/> over [from, to], clamped
        /// to the available range; frames with no value are skipped.
        /// </summary>
        public IReadOnlyList<ProfilerSample> CounterSeries(string counter, int from, int to)
            => ProfilerSource.CounterSeries(counter, from, to);

        /// <summary>
        /// Top <paramref name="topN"/> markers for <paramref name="frame"/> on
        /// <paramref name="threadIndex"/>, inverted + merged, sorted by
        /// <paramref name="sortBy"/> ("self" | "total" | "gc").
        /// </summary>
        public MarkerRow[] Breakdown(int frame, int topN, int threadIndex, string sortBy)
            => ProfilerSource.Breakdown(frame, topN, threadIndex, sortBy);

        /// <summary>Argmax of CPU frame time over the available range.</summary>
        public int WorstFrame() => ProfilerSource.WorstFrame();

        /// <summary>True when <paramref name="frame"/> carries a real scene-load marker (Application.LoadLevelAsync).</summary>
        public bool IsLoadFrame(int frame) => ProfilerSource.IsLoadFrame(frame);
    }
}
