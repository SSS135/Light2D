#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Editor.Framework.Jobs;
using com.SSS135.EditorAutomation.Utils;
using Extensions.Unity.PlayerPrefsEx;
using Microsoft.Extensions.Logging;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
// Disambiguate ILogger: UnityEngine.ILogger and Microsoft.Extensions.Logging.ILogger collide
// once both namespaces are imported. The post-bridge logger field uses the MEL flavour.
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace com.SSS135.EditorAutomation.Editor.API.TestRunner
{
    public class TestResultCollector : ICallbacks
    {
        // Bridge-era plugin-instance log methods were deleted in Wave 2.
        // Use a class-scoped Microsoft.Extensions.Logging.ILogger sourced from UnityLoggerFactory,
        // matching the post-bridge pattern adopted across the API tools.
        static readonly ILogger _logger =
            UnityLoggerFactory.LoggerFactory.CreateLogger<TestResultCollector>();

        static volatile int counter = 0;

        readonly object _logsMutex = new();
        readonly List<TestResultData> _results = new();
        readonly TestSummaryData _summary = new();
        readonly List<TestLogEntry> _logs = new();
        readonly TestMode _testMode;

        DateTime startTime;

        // Captured at RunStarted so TestStarted/TestFinished can emit progress without
        // re-reading PlayerPrefs on every callback. Cleared once the run finishes.
        string _runTicketId = string.Empty;

        public List<TestResultData> GetResults() => _results;
        public TestSummaryData GetSummary() => _summary;
        public List<TestLogEntry> GetLogs()
        {
            lock (_logsMutex)
            {
                return _logs.ToList();
            }
        }
        public TestMode GetTestMode() => _testMode;

        public string TestModeAsString => _testMode switch
        {
            TestMode.EditMode => "EditMode",
            TestMode.PlayMode => "PlayMode",
            _ => "Unknown"
        };

        public static PlayerPrefsString TestCallRequestID = new PlayerPrefsString("EditorAutomation_TestRunner_TestCallRequestID");

        public static PlayerPrefsBool IncludePassingTests = new PlayerPrefsBool("EditorAutomation_TestRunner_IncludePassingTests");
        public static PlayerPrefsBool IncludeMessage = new PlayerPrefsBool("EditorAutomation_TestRunner_IncludeMessage", true);
        public static PlayerPrefsBool IncludeMessageStacktrace = new PlayerPrefsBool("EditorAutomation_TestRunner_IncludeStacktrace");

        public static PlayerPrefsBool IncludeLogs = new PlayerPrefsBool("EditorAutomation_TestRunner_IncludeLogs");
        public static PlayerPrefsInt IncludeLogsMinLevel = new PlayerPrefsInt("EditorAutomation_TestRunner_IncludeLogsMinLevel", (int)LogType.Warning);
        public static PlayerPrefsBool IncludeLogsStacktrace = new PlayerPrefsBool("EditorAutomation_TestRunner_IncludeLogsStacktrace");

        public TestResultCollector()
        {
            int newCount = System.Threading.Interlocked.Increment(ref counter);

            _logger.LogTrace("Ctor {Type}", nameof(TestResultCollector));

            if (newCount > 1)
                throw new InvalidOperationException($"Only one instance of {nameof(TestResultCollector)} is allowed. Current count: {newCount}");
        }

        public void RunStarted(ITestAdaptor testsToRun)
        {
            _logger.LogInformation("RunStarted");

            startTime = DateTime.Now;
            var testCount = CountTests(testsToRun);

            // Stash the ticket id so TestStarted/TestFinished can emit progress without
            // re-reading PlayerPrefs on every callback.
            _runTicketId = TestCallRequestID.Value ?? string.Empty;

            lock (_logsMutex)
            {
                _logs.Clear();
            }
            _results.Clear();
            _summary.Clear();
            _summary.TotalTests = testCount;

            // Subscribe to log messages (using threaded version to catch logs from all threads)
            Application.logMessageReceivedThreaded -= OnLogMessageReceived;
            Application.logMessageReceivedThreaded += OnLogMessageReceived;

            _logger.LogInformation("Run {testMode} started: {testCount} tests.",
                TestModeAsString, testCount);
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            _logger.LogInformation("RunFinished");

            // Unsubscribe from log messages
            Application.logMessageReceivedThreaded -= OnLogMessageReceived;

            var duration = DateTime.Now - startTime;
            _summary.Duration = DateTime.Now - startTime;
            _summary.TotalTests = CountTests(result.Test);
            if (_summary.FailedTests > 0)
            {
                _summary.Status = TestRunStatus.Failed;
            }
            else if (_summary.PassedTests > 0)
            {
                _summary.Status = TestRunStatus.Passed;
            }
            else
            {
                _summary.Status = TestRunStatus.Unknown;
            }

            _logger.LogInformation("Run {testMode} finished with {totalTests} test results. Result status: {status}",
                TestModeAsString, _summary.TotalTests, result.TestStatus);
            _logger.LogInformation("Final duration: {duration:mm\\:ss\\.fff}. Completed: {completed}/{total}",
                duration, _results.Count, _summary.TotalTests);

            // BuildMcpPluginIfNeeded / ConnectIfNeeded were tied to the SignalR bridge and
            // were deleted in Wave 2. The job ticket layer handles result delivery; there is
            // nothing to rebuild or reconnect post-bridge.

            var ticketId = TestCallRequestID.Value;
            TestCallRequestID.Value = string.Empty;
            _runTicketId = string.Empty;
            // Clear the captured TestRunnerApi run guid so tests-cancel does not act on a
            // stale guid after this run is fully finished. A stale guid is harmless
            // (TestRunnerApi.CancelTestRun returns false) but the cleared field makes the
            // no-active-run path explicit.
            if (!string.IsNullOrEmpty(RunGuidRegistry.RunGuid.Value))
                RunGuidRegistry.RunGuid.Value = string.Empty;
            if (string.IsNullOrEmpty(ticketId) == false)
            {
                var structuredResponse = CreateStructuredResponse(
                    includePassingTests: IncludePassingTests.Value,
                    includeMessage: IncludeMessage.Value,
                    includeLogs: IncludeLogs.Value,
                    includeMessageStacktrace: IncludeMessageStacktrace.Value,
                    includeLogsStacktrace: IncludeLogsStacktrace.Value);

                // Route the structured payload through the relocated framework Reflector so the same
                // Unity-type JSON converters that serve every other tool also serve the test-run body.
                var structuredNode = ReflectorFactory.Instance.JsonSerializer.SerializeToNode(structuredResponse);
                var response = McpResponse.SuccessStructured(structuredNode);

                // Complete the matching job ticket. RunFinished can fire after a domain reload
                // (PlayMode tests in particular) — JobRegistry.Complete is idempotent and still
                // appends the terminal JSONL line when the in-memory ticket has aged out.
                JobRegistry.Complete(ticketId, response);
            }
        }

        public void TestStarted(ITestAdaptor test)
        {
            // Emit a progress event so callers polling system-job-status see live motion.
            // Wrapped in try/catch — JobRegistry.Progress is rate-limited internally, but
            // an exception escaping here would propagate into Unity's test framework and
            // abort the run.
            try
            {
                if (!string.IsNullOrEmpty(_runTicketId))
                {
                    JobRegistry.Progress(_runTicketId, BuildProgressPayload(test?.FullName));
                }
            }
            catch { }
        }

        public void TestFinished(ITestResultAdaptor result)
        {
            // Only count actual tests, not test suites
            if (!result.Test.IsSuite)
            {
                var testResult = new TestResultData
                {
                    Name = result.Test.FullName,
                    Status = ConvertTestStatus(result.TestStatus),
                    Duration = TimeSpan.FromSeconds(result.Duration),
                    Message = result.Message,
                    StackTrace = result.StackTrace
                };

                _results.Add(testResult);

                var statusEmoji = result.TestStatus switch
                {
                    TestStatus.Passed => "<color=green>✅</color>",
                    TestStatus.Failed => "<color=red>❌</color>",
                    TestStatus.Skipped => "<color=yellow>⚠️</color>",
                    _ => string.Empty
                };

                _logger.LogInformation("{emoji} Test finished ({counter}/{total}): {testName} - {testStatus}",
                    statusEmoji, _results.Count, _summary.TotalTests, result.Test.FullName, result.TestStatus);

                // Update summary counts
                switch (result.TestStatus)
                {
                    case TestStatus.Passed:
                        _summary.PassedTests++;
                        break;
                    case TestStatus.Failed:
                        _summary.FailedTests++;
                        break;
                    case TestStatus.Skipped:
                        _summary.SkippedTests++;
                        break;
                }

                // Update duration as tests complete
                _summary.Duration = DateTime.Now - startTime;

                // Emit a progress event with the updated counters. Wrapped in try/catch
                // so registry hiccups can't abort the run; the 250 ms throttle inside
                // JobRegistry.Progress absorbs high-frequency runs.
                try
                {
                    if (!string.IsNullOrEmpty(_runTicketId))
                    {
                        JobRegistry.Progress(_runTicketId, BuildProgressPayload(result.Test?.FullName));
                    }
                }
                catch { }

                // Check if all tests are complete
                if (_results.Count >= _summary.TotalTests)
                {
                    _logger.LogInformation("All tests completed via TestFinished. Final duration: {duration:mm\\:ss\\.fff}",
                        _summary.Duration);
                }
            }
        }

        JsonObject BuildProgressPayload(string? currentTest)
        {
            return new JsonObject
            {
                ["currentTest"] = currentTest ?? string.Empty,
                ["completed"] = _results.Count,
                ["total"] = _summary.TotalTests,
                ["passed"] = _summary.PassedTests,
                ["failed"] = _summary.FailedTests,
                ["skipped"] = _summary.SkippedTests,
            };
        }

        void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            var entry = new TestLogEntry(type, condition, stackTrace);
            lock (_logsMutex)
            {
                _logs.Add(entry);
            }
        }

        TestRunResponse CreateStructuredResponse(bool includePassingTests, bool includeMessage, bool includeMessageStacktrace, bool includeLogs, bool includeLogsStacktrace)
        {
            var results = GetResults();
            var summary = GetSummary();
            var logs = GetLogs();

            var response = new TestRunResponse
            {
                Summary = summary,
                Results = new List<TestResultData>()
            };

            // Filter test results based on includePassingTests, includeMessage and includeMessageStacktrace
            foreach (var result in results)
            {
                // Skip passing tests if includePassingTests is false
                if (!includePassingTests && result.Status == TestResultStatus.Passed)
                    continue;

                var filteredResult = new TestResultData
                {
                    Name = result.Name,
                    Status = result.Status,
                    Duration = result.Duration,
                    Message = includeMessage ? result.Message : null,
                    StackTrace = includeMessageStacktrace ? result.StackTrace : null
                };
                response.Results.Add(filteredResult);
            }

            // Include logs if requested
            if (includeLogs && logs.Any())
            {
                var minLogLevel = TestLogEntry.ToLogLevel((LogType)IncludeLogsMinLevel.Value);
                response.Logs = logs
                    .Where(log => log.LogLevel >= minLogLevel)
                    .Select(log => includeLogsStacktrace
                        ? log
                        : new TestLogEntry(log.Type, log.Condition, null, log.Timestamp))
                    .ToList();
            }

            return response;
        }

        public static int CountTests(ITestAdaptor test)
        {
            try
            {
                if (test == null)
                    return 0;

                if (test.HasChildren && test.Children != null)
                    return test.Children.Sum(CountTests);

                return test.IsSuite ? 0 : 1;
            }
            catch
            {
                return 0;
            }
        }

        static TestResultStatus ConvertTestStatus(TestStatus testStatus)
        {
            return testStatus switch
            {
                TestStatus.Passed => TestResultStatus.Passed,
                TestStatus.Failed => TestResultStatus.Failed,
                TestStatus.Skipped => TestResultStatus.Skipped,
                _ => TestResultStatus.Skipped
            };
        }
    }
}
