#nullable enable
using Microsoft.Extensions.Logging;

namespace com.SSS135.EditorAutomation.Utils
{
    /// <summary>
    /// Process-wide log-level filter for the Editor Automation plugin. Extracted from the deleted
    /// <c>EditorAutomationPlugin.Log.cs</c> so the threshold survives the Runtime/ tree deletion.
    /// Threshold uses <see cref="LogLevel"/> (Microsoft.Extensions.Logging) so callers can
    /// stay on the industry-standard enum and skip the legacy Runtime.Utils.LogLevel hop.
    /// </summary>
    public static class EditorAutomationLogConfig
    {
        // Default threshold mirrors the prior EditorAutomationPlugin behavior (Warning and above shown).
        private static LogLevel _configuredLogLevel = LogLevel.Warning;

        // Microsoft.Extensions.Logging.LogLevel orders most-verbose (Trace = 0) → least-verbose
        // (None = 6), so a configured threshold <= requested level means "enabled".
        public static bool IsLogEnabled(LogLevel level) => _configuredLogLevel <= level;
        public static void ApplyLogLevel(LogLevel level) => _configuredLogLevel = level;

        // The prior EditorAutomationPlugin tracked an instance count to allow early logging before init.
        // With the bridge gone there is no init handshake gate; always treat the logger as ready.
        public static bool HasAnyInstance => true;
    }
}
