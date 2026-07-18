#nullable enable
using System;
using UnityEngine;

namespace com.SSS135.EditorAutomation.Editor.API.TestRunner
{
    public class TestLogEntry
    {
        public string Condition { get; set; }
        public string? StackTrace { get; set; }
        public LogType Type { get; set; }
        public DateTime Timestamp { get; set; }

        public int LogLevel => ToLogLevel(Type);

        public TestLogEntry(LogType type, string condition, string? stackTrace = null) : this(type, condition, stackTrace, DateTime.Now)
        {
            // none
        }
        public TestLogEntry(LogType type, string condition, string? stackTrace, DateTime timestamp)
        {
            Condition = condition;
            StackTrace = stackTrace;
            Type = type;
            Timestamp = timestamp;
        }

        public string ToStringFormat(bool includeType, bool includeStacktrace)
        {
            return includeStacktrace && !string.IsNullOrEmpty(StackTrace)
                ? includeType
                    ? $"[{Timestamp:HH:mm:ss}] [{Type}] {Condition}\n{StackTrace}"
                    : $"[{Timestamp:HH:mm:ss}] {Condition}\n{StackTrace}"
                : includeType
                    ? $"[{Timestamp:HH:mm:ss}] [{Type}] {Condition}"
                    : $"[{Timestamp:HH:mm:ss}] {Condition}";
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(StackTrace)
                ? $"[{Timestamp:HH:mm:ss}] [{Type}] {Condition}"
                : $"[{Timestamp:HH:mm:ss}] [{Type}] {Condition}\n{StackTrace}";
        }

        public static int ToLogLevel(LogType type)
        {
            return type switch
            {
                LogType.Log => 1,
                LogType.Warning => 2,
                LogType.Assert => 3,
                LogType.Error => 4,
                LogType.Exception => 5,
                _ => 6
            };
        }
    }
}
