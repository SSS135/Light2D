#nullable enable
using System;
using System.ComponentModel;
using com.SSS135.EditorAutomation;

namespace EA
{
    public sealed class LogsResult
    {
        [Description("Log entries returned for this query.")]
        public LogEntry[] Entries { get; set; } = Array.Empty<LogEntry>();

        [Description("Number of log entries returned.")]
        public int Count { get; set; }
    }
}
