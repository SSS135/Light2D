#nullable enable
using System;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.Framework;

namespace com.SSS135.EditorAutomation.Editor.API
{
    /// <summary>
    /// Container for the read-only profiler analysis MCP tools (describe /
    /// summarize-counter / get-series / frame-breakdown / find-spikes /
    /// load-capture / query). The per-tool methods live in sibling partial-class
    /// files and share the helpers below: a main-thread wrapper (the FrameDataView
    /// family must be read on Unity's main thread) and the "no frames" error string.
    /// None of these tools control recording — that stays in the Profiler window.
    /// </summary>
    [McpToolType]
    public partial class Tool_Profiler
    {
        /// <summary>Run <paramref name="body"/> on Unity's main thread (FrameDataView reads require it).</summary>
        static T RunMain<T>(Func<T> body) => MainThread.Instance.Run(body);

        public static class Error
        {
            public static string NoFrames()
                => "No profiler frames available. Record in the Profiler window, or call profiler.load-capture with a .raw/.data file.";

            public static string UnknownCounter(string counter)
                => $"Unknown counter '{counter}'. Call profiler.describe for the available counters " +
                   "(note: thread names like \"Main Thread\" are not counters).";

            public static string CounterNoData(string counter)
                => $"Counter '{counter}' has no per-frame data in this buffer (thread-time and live-only " +
                   "counters aren't stored per frame). Use 'CPU Total Frame Time' / 'CPU Main Thread Frame Time' " +
                   "(ns) for frame time, or profiler.frame-breakdown {frame:-1} for the worst frame.";
        }
    }
}
