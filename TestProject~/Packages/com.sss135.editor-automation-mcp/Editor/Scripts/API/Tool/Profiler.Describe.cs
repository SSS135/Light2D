#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Editor.QueryEngine;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Profiler
    {
        [McpTool
        (
            "profiler-describe",
            Title = "Profiler / Describe",
            ReadOnlyHint = true,
            IdempotentHint = true,
            MainThreadRequired = true
        )]
        [Description("Orientation call before any other profiler.* tool. Reports whether the buffer is from a live " +
            "recording or a loaded capture, the available frame range, the threads present, the counters you can " +
            "summarize / chart / scan for spikes. Counter availability reflects the LIVE session — a capture " +
            "loaded via profiler.load-capture may carry counters not in this list. Reports the empty state rather " +
            "than throwing when nothing is recorded. The counter list is filtered to those readable per-frame (so the " +
            "other tools never get handed a name that yields nothing). For CPU/GPU frame time use Unity's readable " +
            "\"CPU Total Frame Time\" / \"CPU Main Thread Frame Time\" / \"GPU Frame Time\" counters (ns). Use the " +
            "returned counter names and frame indices to drive profiler.summarize-counter, profiler.get-series, " +
            "profiler.frame-breakdown, and profiler.find-spikes.")]
        public McpResponse Describe()
            => RunMain(() =>
            {
                // Returns a plain-text summary instead of a structured DescribeResult:
                // the structured path came back empty over MCP for this tool
                // specifically. Text content renders reliably, so frame/counter
                // discovery is usable. Threads/counters are best-effort -- a read
                // failure must not blank the summary; the frame range always prints.
                var sb = new System.Text.StringBuilder();
                var hasFrames = ProfilerSource.HasFrames;
                var first = ProfilerSource.FirstFrame;
                var last = ProfilerSource.LastFrame;

                sb.AppendLine(hasFrames
                    ? $"Profiler buffer: frames {first}..{last} ({ProfilerSource.FrameCount}), recording={ProfilerSource.IsRecording}."
                    : "Profiler buffer: no frames. Record in the Profiler window, or load a capture with profiler.load-capture.");

                if (hasFrames)
                {
                    try
                    {
                        var names = ProfilerSource.ThreadNames(last);
                        if (names.Length > 0)
                            sb.AppendLine($"Threads ({names.Length}): {string.Join(", ", names)}");
                    }
                    catch (System.Exception ex)
                    {
                        sb.AppendLine($"(thread enumeration failed: {ex.Message})");
                    }

                    try
                    {
                        // Only list counters actually readable per-frame (so the other
                        // tools never get handed a name that yields nothing), grouped by
                        // category to stay compact -- the verbose per-line form pushed the
                        // response over the HTTP spill threshold (~10 KB) and it came back
                        // empty.
                        var byCategory = new SortedDictionary<string, List<string>>(System.StringComparer.Ordinal);
                        var readableCount = 0;
                        foreach (var info in ProfilerSource.AvailableCounters())
                        {
                            if (!ProfilerSource.TryReadCounter(last, info.Name, out _))
                                continue;
                            readableCount++;
                            var category = string.IsNullOrEmpty(info.Category) ? "(uncategorized)" : info.Category;
                            if (!byCategory.TryGetValue(category, out var list))
                            {
                                list = new List<string>();
                                byCategory[category] = list;
                            }
                            list.Add(string.IsNullOrEmpty(info.Unit) ? info.Name : $"{info.Name} [{info.Unit}]");
                        }

                        sb.AppendLine($"Readable counters ({readableCount}), by category:");
                        foreach (var kvp in byCategory)
                            sb.AppendLine($"  {kvp.Key}: {string.Join(", ", kvp.Value)}");
                    }
                    catch (System.Exception ex)
                    {
                        sb.AppendLine($"(counter enumeration failed: {ex.Message})");
                    }
                }

                return McpResponse.Success(sb.ToString());
            });
    }
}
