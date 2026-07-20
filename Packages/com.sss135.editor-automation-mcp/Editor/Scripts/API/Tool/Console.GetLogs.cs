#nullable enable
using System;
using System.ComponentModel;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Utils;
using EA;
using UnityEngine;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Console
    {
        public const string ConsoleGetLogsToolId = "console-get-logs";
        [McpTool
        (
            ConsoleGetLogsToolId,
            Title = "Console / Get Logs",
            ReadOnlyHint = true,
            IdempotentHint = true
        )]
        [Description("Retrieves Unity Editor logs. " +
            "Useful for debugging and monitoring Unity Editor activity.")]
        public LogsResult GetLogs
        (
            [Description("Maximum number of log entries to return. Minimum: 1. Default: 100")]
            int maxEntries = 100,
            [Description("Filter by log type. 'null' means All.")]
            LogType? logTypeFilter = null,
            [Description("Include stack traces in the output. Default: false")]
            bool includeStackTrace = false,
            [Description("Return logs from the last N minutes. If 0, returns all available logs. Default: 0")]
            int lastMinutes = 0
        )
        {
            // Validate parameters
            if (maxEntries < 1)
                throw new ArgumentException(Error.InvalidMaxEntries(maxEntries));

            // EditorLogCollectorService bootstraps an InMemoryLogStorage-backed
            // UnityLogCollector lazily on first access; the legacy bridge-owned
            // log-collector accessor was deleted in Wave 2.
            var logCollector = EditorLogCollectorService.Instance;
            if (logCollector == null)
                throw new InvalidOperationException("LogCollector is not initialized.");

            // Get all log entries as array to avoid concurrent modification
            var logs = logCollector.Query(
                maxEntries: maxEntries,
                logTypeFilter: logTypeFilter,
                includeStackTrace: includeStackTrace,
                lastMinutes: lastMinutes
            );

            return new LogsResult { Entries = logs, Count = logs.Length };
        }
    }
}