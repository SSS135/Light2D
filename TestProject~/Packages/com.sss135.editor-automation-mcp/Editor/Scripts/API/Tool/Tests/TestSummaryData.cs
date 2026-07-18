#nullable enable
using System;

namespace com.SSS135.EditorAutomation.Editor.API.TestRunner
{
    public class TestSummaryData
    {
        public TestRunStatus Status { get; set; } = TestRunStatus.Unknown;
        public int TotalTests { get; set; }
        public int PassedTests { get; set; }
        public int FailedTests { get; set; }
        public int SkippedTests { get; set; }
        public TimeSpan Duration { get; set; }

        public void Clear()
        {
            Status = TestRunStatus.Unknown;
            TotalTests = 0;
            PassedTests = 0;
            FailedTests = 0;
            SkippedTests = 0;
            Duration = TimeSpan.Zero;
        }
    }
}
