#nullable enable
using System;
using System.Globalization;
using UnityEditor;
using UnityEditor.Compilation;

namespace com.SSS135.EditorAutomation.Editor.Utils
{
    /// <summary>
    /// Records when scripts last finished compiling so <c>editor-application-get-state</c> can
    /// report it. A recompile triggers a domain reload that wipes all static fields, so the
    /// timestamp is stored in <see cref="SessionState"/> — which survives the reload and clears
    /// on editor restart, the correct lifetime for a per-session "last recompile" value.
    /// <para>
    /// Two values are persisted from <see cref="CompilationPipeline.compilationFinished"/> (which
    /// fires before the reload): the wall-clock UTC timestamp for human readability, and the
    /// monotonic <see cref="EditorApplication.timeSinceStartup"/> reading so a precise
    /// "seconds since" can be computed on read without any wall-clock-sync assumption.
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    public static class CompileTimeTracker
    {
        const string FinishedUtcKey = "MCP_LastCompilationFinishedUtc";
        const string FinishedSinceStartupKey = "MCP_LastCompilationFinishedSinceStartup";

        static CompileTimeTracker()
            => CompilationPipeline.compilationFinished += OnCompilationFinished;

        static void OnCompilationFinished(object context)
        {
            // Fires before the domain reload, so SessionState carries the value across the wipe.
            SessionState.SetString(FinishedUtcKey,
                DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
            SessionState.SetString(FinishedSinceStartupKey,
                EditorApplication.timeSinceStartup.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// UTC timestamp (ISO 8601) of the last finished compilation this editor session, or an
        /// empty string if no compilation has been observed since the editor started.
        /// </summary>
        public static string LastCompilationFinishedUtc
            => SessionState.GetString(FinishedUtcKey, string.Empty);

        /// <summary>
        /// Seconds elapsed since the last finished compilation, measured on the editor's monotonic
        /// clock, or null if no compilation has been observed this session.
        /// </summary>
        public static double? SecondsSinceLastCompilation
        {
            get
            {
                var raw = SessionState.GetString(FinishedSinceStartupKey, string.Empty);
                if (string.IsNullOrEmpty(raw) ||
                    !double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var finished))
                    return null;
                return EditorApplication.timeSinceStartup - finished;
            }
        }
    }
}
