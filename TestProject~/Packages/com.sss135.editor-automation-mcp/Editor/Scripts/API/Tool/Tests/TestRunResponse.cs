#nullable enable
using System.Collections.Generic;
using System.ComponentModel;

namespace com.SSS135.EditorAutomation.Editor.API.TestRunner
{
    public class TestRunResponse
    {
        [Description("Summary of the test run including total, passed, failed, and skipped counts.")]
        public TestSummaryData Summary { get; set; } = new TestSummaryData();

        [Description("List of individual test results with details about each test.")]
        public List<TestResultData> Results { get; set; } = new List<TestResultData>();

        [Description("Log entries captured during test execution.")]
        public List<TestLogEntry>? Logs { get; set; }
    }
}
