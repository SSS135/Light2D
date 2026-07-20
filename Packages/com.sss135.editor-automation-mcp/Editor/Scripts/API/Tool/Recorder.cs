#nullable enable
using System;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Editor.QueryEngine;
using com.SSS135.EditorAutomation.Recorder;

namespace com.SSS135.EditorAutomation.Editor.API
{
    /// <summary>
    /// Container for the gameplay-recorder MCP query tools. The per-tool methods
    /// (list / describe / get-state / summarize / series / find-events / query)
    /// live in sibling partial-class files and share the helpers below: session
    /// resolution and a main-thread wrapper for the live-session tools (which need
    /// a consistent snapshot).
    /// </summary>
    [McpToolType]
    public partial class Tool_Recorder
    {
        /// <summary>
        /// Resolve a <c>session</c> argument (<c>"live"</c> or a file path / id) to a
        /// <see cref="RecorderQueryEngine"/> over the corresponding session.
        /// </summary>
        static RecorderQueryEngine ResolveEngine(string session)
            => new RecorderQueryEngine(SessionResolver.Resolve(session));

        /// <summary>Run <paramref name="body"/> on Unity's main thread (live tools need a consistent snapshot).</summary>
        static T RunMain<T>(Func<T> body) => MainThread.Instance.Run(body);

        public static class Error
        {
            public static string SessionRequired()
                => "'session' is required: pass \"live\" for the running session or a recording file path/id.";
        }
    }
}
