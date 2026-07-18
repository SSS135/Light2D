#nullable enable
using System;
using System.ComponentModel;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Editor.QueryEngine;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Profiler
    {
        [McpTool
        (
            "profiler-frame-breakdown",
            Title = "Profiler / Frame Breakdown",
            ReadOnlyHint = true,
            IdempotentHint = true,
            MainThreadRequired = true
        )]
        [Description("Top markers for one frame, the inverted+merged hierarchy (leaf functions aggregated by " +
            "name), sorted by self time / total time / GC bytes. This is the drill-down step: get the worst / " +
            "spike frame from profiler.summarize-counter (ArgMaxFrame) or profiler.find-spikes, then break it " +
            "down here to see which markers dominated it. Pass frame < 0 to auto-pick the worst frame by CPU " +
            "frame time — the result's IsLoadFrame flags whether that frame is a scene load rather than gameplay. " +
            "Mirrors the Profiler window's CPU Usage timeline with Invert Call Hierarchy on.")]
        public FrameBreakdown FrameBreakdown(
            [Description("Frame to break down. Negative picks the worst frame by CPU frame time.")]
            int frame = -1,
            [Description("Number of top markers to return.")]
            int topN = 15,
            [Description("Thread index to read (0 = main thread). From profiler.describe's thread list.")]
            int threadIndex = 0,
            [Description("Sort column: \"self\" (self time) | \"total\" (total time) | \"gc\" (GC.Alloc bytes).")]
            string sortBy = "self")
            => RunMain(() =>
            {
                if (!ProfilerSource.HasFrames)
                    throw new System.InvalidOperationException(Error.NoFrames());

                if (sortBy != "self" && sortBy != "total" && sortBy != "gc")
                    throw new System.ArgumentException(
                        $"Unsupported sortBy \"{sortBy}\". Expected \"self\", \"total\", or \"gc\".", nameof(sortBy));

                var target = frame < 0
                    ? ProfilerSource.WorstFrame()
                    : Math.Clamp(frame, ProfilerSource.FirstFrame, ProfilerSource.LastFrame);

                var threads = ProfilerSource.ThreadNames(target);
                var thread = threadIndex >= 0 && threadIndex < threads.Length ? threads[threadIndex] : "";

                var result = new FrameBreakdown
                {
                    Frame = target,
                    IsLoadFrame = ProfilerSource.IsLoadFrame(target),
                    FrameTimeMs = ProfilerSource.FrameTimeMs(target),
                    Thread = thread,
                    SortedBy = sortBy,
                    Markers = ProfilerSource.Breakdown(target, topN, threadIndex, sortBy),
                    Meta =
                    {
                        TimeDomain = "render",
                        FrameRangeCovered = new[] { target, target }
                    }
                };
                return result;
            });
    }
}
