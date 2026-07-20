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
            "profiler-load-capture",
            Title = "Profiler / Load Capture",
            ReadOnlyHint = false,
            IdempotentHint = true,
            MainThreadRequired = true
        )]
        [Description("Load a saved .raw/.data Profiler capture into the global Profiler buffer. WARNING: this REPLACES " +
            "the current in-memory data — the Profiler window and every other profiler.* read now reflect the loaded " +
            "file until you re-record. This is the only stateful profiler.* tool; the read tools always operate on " +
            "whatever buffer is current. Throws when the file does not exist. Returns the loaded capture's frame range.")]
        public LoadCaptureResult LoadCapture(
            [Description("Absolute path to a saved .raw/.data Profiler capture.")]
            string path)
            => RunMain(() =>
            {
                var result = ProfilerSource.LoadCapture(path);
                result.Meta.TimeDomain = "render";
                if (ProfilerSource.HasFrames)
                    result.Meta.FrameRangeCovered = new[] { result.FirstFrame, result.LastFrame };
                return result;
            });
    }
}
