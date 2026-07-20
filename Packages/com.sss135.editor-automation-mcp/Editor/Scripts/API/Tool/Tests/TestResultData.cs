#nullable enable
using System;

namespace com.SSS135.EditorAutomation.Editor.API.TestRunner
{
    public class TestResultData
    {
        public string Name { get; set; } = string.Empty;
        public TestResultStatus Status { get; set; } = TestResultStatus.Skipped;
        public TimeSpan Duration { get; set; }
        public string? Message { get; set; }
        public string? StackTrace { get; set; }
    }
}
