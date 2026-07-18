#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.API.TestRunner;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Editor.Framework.Jobs;
using com.SSS135.EditorAutomation.Editor.Utils;
using com.SSS135.EditorAutomation.Utils;
using Microsoft.Extensions.Logging;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public static partial class Tool_Tests
    {
        public const string TestsRunToolId = "tests-run";
        [McpTool
        (
            TestsRunToolId,
            Title = "Tests / Run"
        )]
        [Description("Execute Unity tests and return detailed results. " +
            "Supports filtering by test mode, assembly, namespace, class, and method. " +
            "Recommended to use '" + nameof(TestMode.EditMode) + "' for faster iteration during development. " +
            "Precondition: every open scene MUST be saved (no unsaved changes). If any open scene is dirty, " +
            "this tool throws an InvalidOperationException listing the dirty scenes; save them and retry.")]
        public static async Task<McpResponse> Run
        (
            [Description("Test mode to run. Options: '" + nameof(TestMode.EditMode) + "', '" + nameof(TestMode.PlayMode) + "'. Default: '" + nameof(TestMode.EditMode) + "'")]
            TestMode testMode = TestMode.EditMode,
            [Description("Specific test assembly name to run (optional). Example: 'Assembly-CSharp-Editor-testable'")]
            string? testAssembly = null,
            [Description("Specific test namespace to run (optional). Example: 'MyTestNamespace'")]
            string? testNamespace = null,
            [Description("Specific test class name to run (optional). Example: 'MyTestClass'")]
            string? testClass = null,
            [Description("Specific fully qualified test method to run (optional). Example: 'MyTestNamespace.FixtureName.TestName'")]
            string? testMethod = null,

            [Description("Include details for all tests, both passing and failing (default: false). If you just need details for failing tests, set to false.")]
            bool includePassingTests = false,
            [Description("Include test result messages in the test results (default: true). If you just need pass/fail status, set to false.")]
            bool includeMessages = true,
            [Description("Include stack traces in the test results (default: false).")]
            bool includeStacktrace = false,

            [Description("Include console logs in the test results (default: false).")]
            bool includeLogs = false,
            [Description("Log type filter for console logs. Options: '" + nameof(LogType.Log) + "', '" + nameof(LogType.Warning) + "', '" + nameof(LogType.Assert) + "', '" + nameof(LogType.Error) + "', '" + nameof(LogType.Exception) + "'. (default: '" + nameof(LogType.Warning) + "')")]
            LogType logType = LogType.Warning,
            [Description("Include stack traces for console logs in the test results (default: false). This is huge amount of data, use only if really needed.")]
            bool includeLogsStacktrace = false,

            [Description("If > 0, block server-side up to this many seconds waiting for the run to finish, returning the structured result in one call. " +
                "0 (default) preserves the existing async behaviour: return immediately with the ticket envelope. " +
                "Default when sync is requested without an explicit value is 30 seconds. " +
                "On timeout, the tool consults the JSONL monitor log (via JobMonitorLog.TryFindTerminalEvent) so PlayMode reload-survived results still come back synchronously when available.")]
            int waitSeconds = 0
        )
        {
            // Hop to the main thread for scene-state inspection and PlayerPrefs writes; the job-ticket
            // allocation itself happens after every synchronous precondition has passed so we don't
            // create orphan tickets for callers that fail validation.
            var preflight = await MainThread.Instance.RunAsync(() =>
            {
                try
                {
                    // Abort before any state mutation if any open scene has unsaved changes.
                    // Running tests against a dirty scene is unsafe: Unity may reload the scene on
                    // play-mode entry, silently discarding edits and producing non-reproducible
                    // results. Throws InvalidOperationException listing every dirty scene so the
                    // caller can save and retry. MUST run before PlayerPrefs writes and before
                    // AssetDatabase.Refresh so nothing is side-effected on abort.
                    ThrowIfAnyOpenSceneIsDirty();
                }
                catch (Exception ex)
                {
                    return McpResponse.Error(ex.Message);
                }
                return null!;
            });
            if (preflight != null)
                return preflight;

            var startResponse = JobHelpers.StartJob(
                method: "tests.run",
                startedPayload: new JsonObject
                {
                    ["test_mode"] = testMode.ToString(),
                    ["filters"] = new JsonObject
                    {
                        ["assembly"] = testAssembly,
                        ["namespace"] = testNamespace,
                        ["class"] = testClass,
                        ["method"] = testMethod,
                    },
                },
                bind: (ticketId, complete) =>
                {
                    _ = MainThread.Instance.RunAsync(async () =>
                    {
                        try
                        {
                            // Persist the ticket id + display options BEFORE AssetDatabase.Refresh —
                            // any of these can trigger a domain reload that wipes managed state.
                            TestResultCollector.TestCallRequestID.Value = ticketId;
                            TestResultCollector.IncludePassingTests.Value = includePassingTests;
                            TestResultCollector.IncludeMessage.Value = includeMessages;
                            TestResultCollector.IncludeMessageStacktrace.Value = includeStacktrace;

                            TestResultCollector.IncludeLogs.Value = includeLogs;
                            TestResultCollector.IncludeLogsMinLevel.Value = (int)logType;
                            TestResultCollector.IncludeLogsStacktrace.Value = includeLogsStacktrace;

                            // Trigger AssetDatabase.Refresh to detect and compile any changed scripts
                            if (EditorAutomationLogConfig.IsLogEnabled(LogLevel.Information))
                                Debug.Log($"[TestRunner] Refreshing AssetDatabase before running {testMode} tests...");

                            UnityEditor.AssetDatabase.Refresh();

                            // Check if compilation was triggered (scripts changed on disk)
                            if (UnityEditor.EditorApplication.isCompiling)
                            {
                                if (EditorAutomationLogConfig.IsLogEnabled(LogLevel.Information))
                                    Debug.Log($"[TestRunner] Scripts are compiling. Deferring test run until after domain reload.");

                                // Save filter params to SessionState (survives domain reload)
                                SavePendingTestRun(testMode, testAssembly, testNamespace, testClass, testMethod);

                                // Register resume callback now — handles compilation failure (no domain reload).
                                // If compilation succeeds, domain reload destroys this registration,
                                // but the static constructor re-registers it after reload.
                                UnityEditor.EditorApplication.update += ResumePendingTestRunOnce;
                                return;
                            }

                            // Check for pre-existing compilation errors (no new compilation triggered)
                            if (UnityEditor.EditorUtility.scriptCompilationFailed)
                            {
                                TestResultCollector.TestCallRequestID.Value = string.Empty;
                                var errorDetails = ScriptUtils.GetCompilationErrorDetails();
                                complete(McpResponse.Error($"Cannot run tests: Unity project has compilation errors.\n\n{errorDetails}"));
                                return;
                            }

                            // No compilation needed — run tests immediately
                            if (EditorAutomationLogConfig.IsLogEnabled(LogLevel.Information))
                                Debug.Log($"[TestRunner] No compilation needed. Running {testMode} tests immediately.");

                            var filterParams = new TestFilterParameters(testAssembly, testNamespace, testClass, testMethod);

                            if (EditorAutomationLogConfig.IsLogEnabled(LogLevel.Information))
                                Debug.Log($"[TestRunner] Running {testMode} tests with filters: {filterParams}");

                            var validation = await ValidateTestFilters(TestRunnerApi, testMode, filterParams);
                            if (validation != null)
                            {
                                TestResultCollector.TestCallRequestID.Value = string.Empty;
                                complete(McpResponse.Error(validation));
                                return;
                            }

                            var filter = CreateTestFilter(testMode, filterParams);

                            // Kick the runner. RunFinished on TestResultCollector calls JobRegistry.Complete
                            // with the ticket id we stored in TestCallRequestID above.
                            // Capture the TestRunnerApi run guid into RunGuidRegistry (PlayerPrefs-backed so it
                            // survives the PlayMode domain reload) so tests-cancel can look it up by ticket id.
                            MainThread.Instance.Run(() =>
                            {
                                var runGuid = TestRunnerApi.Execute(new ExecutionSettings(filter));
                                RunGuidRegistry.RunGuid.Value = runGuid ?? string.Empty;
                            });
                        }
                        catch (Exception ex)
                        {
                            if (EditorAutomationLogConfig.IsLogEnabled(LogLevel.Error))
                            {
                                Debug.LogException(ex);
                                Debug.LogError($"[TestRunner] ------------------------------------- Exception {testMode} tests.");
                            }
                            TestResultCollector.TestCallRequestID.Value = string.Empty;
                            complete(McpResponse.Error(Error.TestExecutionFailed(ex.Message)));
                        }
                    });
                });

            // waitSeconds == 0 keeps the historical async behaviour: return the Processing envelope
            // immediately and let the caller poll system-job-status.
            if (waitSeconds <= 0)
                return startResponse;

            // Sync mode. Pull the ticket id out of the Processing envelope's structured body so we
            // can await the same TaskCompletionSource that JobRegistry.Complete signals.
            var ticketId = (startResponse.StructuredResult as JsonObject)?["ticket_id"]?.GetValue<string>();
            if (string.IsNullOrEmpty(ticketId))
                return startResponse;

            var ticket = JobRegistry.Find(ticketId!);
            // Defensive: JobHelpers.StartJob allocated the ticket synchronously, but a bind delegate
            // that completes inline + TTL eviction in tests + domain-reload timing could in theory
            // wipe it before this lookup. Fall through to the envelope rather than awaiting a null.
            if (ticket == null)
                return startResponse;

            // Already terminal at lookup time (synchronous-complete binder path) — return the live
            // result directly without entering Task.WhenAny.
            if (ticket.Status != JobStatus.Running)
                return ticket.CompletionTcs.Task.IsCompletedSuccessfully
                    ? ticket.CompletionTcs.Task.Result
                    : McpResponse.SuccessStructured(JobRegistry.BuildStatusBody(ticket));

            // Block on the terminal TCS (set by JobRegistry.Complete via TestResultCollector.RunFinished)
            // or the timeout, whichever fires first.
            var delay = Task.Delay(TimeSpan.FromSeconds(waitSeconds));
            var winner = await Task.WhenAny(ticket.CompletionTcs.Task, delay).ConfigureAwait(false);

            if (winner == ticket.CompletionTcs.Task)
                return ticket.CompletionTcs.Task.Result;

            // Timeout. A PlayMode domain reload during the await would have evicted the original
            // ticket and its TCS, so WhenAny resolved on the Task.Delay branch even though the
            // run may have actually finished. Consult the JSONL monitor log for a terminal event;
            // if one exists, synthesize a Success body from it (carrying the source: jsonl-fallback
            // marker per the B-job-ticket-lost-on-reload chunk's contract).
            if (JobMonitorLog.TryFindTerminalEvent(ticketId!, out var envelope) && envelope != null)
                return McpResponse.SuccessStructured(BuildSyncBodyFromJsonl(ticketId!, envelope));

            // No JSONL terminal line either. Refresh the ticket reference in case the registry was
            // wiped, then return the best-effort body with timed_out: true so the caller can poll.
            var post = JobRegistry.Find(ticketId!);
            JsonObject timedOutBody;
            if (post != null)
            {
                timedOutBody = JobRegistry.BuildStatusBody(post);
            }
            else
            {
                timedOutBody = new JsonObject
                {
                    ["ticket_id"] = ticketId,
                    ["status"] = "running",
                };
            }
            timedOutBody["timed_out"] = true;
            return McpResponse.SuccessStructured(timedOutBody);
        }

        // Reconstruct a sync-mode body from a single terminal JSONL envelope. Mirrors the
        // BuildBodyFromJsonl helper in SystemTool/JobStatus.cs: started_at falls back to the line's
        // ts when no separate `started` line is consulted, and the source: "jsonl-fallback" marker
        // tells the caller the payload was reconstructed from disk (no live in-memory progress).
        static JsonObject BuildSyncBodyFromJsonl(string ticketId, JsonObject envelope)
        {
            string? method = null;
            try { method = envelope["method"]?.GetValue<string>(); } catch { }

            string? ev = null;
            try { ev = envelope["event"]?.GetValue<string>(); } catch { }

            string? ts = null;
            try { ts = envelope["ts"]?.GetValue<string>(); } catch { }

            var status = ev switch
            {
                "completed" => "completed",
                "cancelled" => "cancelled",
                "error" => "errored",
                _ => "unknown",
            };

            var body = new JsonObject
            {
                ["ticket_id"] = ticketId,
                ["method"] = method ?? "tests.run",
                ["status"] = status,
                ["source"] = "jsonl-fallback",
            };

            if (!string.IsNullOrEmpty(ts))
            {
                body["started_at"] = ts;
                body["completed_at"] = ts;
            }

            var data = envelope["data"];
            if (data != null)
                body["result"] = data.DeepClone();

            return body;
        }

        static Filter CreateTestFilter(TestMode testMode, TestFilterParameters filterParams)
        {
            var filter = new Filter
            {
                testMode = testMode
            };

            if (!string.IsNullOrEmpty(filterParams.TestAssembly))
                filter.assemblyNames = new[] { filterParams.TestAssembly };

            var groupNames = new List<string>();
            var testNames = new List<string>();

            // Handle specific test method in FixtureName.TestName format
            if (!string.IsNullOrEmpty(filterParams.TestMethod))
                testNames.Add(filterParams.TestMethod!);

            // Handle namespace filtering with regex (shared pattern ensures validation sync)
            if (!string.IsNullOrEmpty(filterParams.TestNamespace))
                groupNames.Add(CreateNamespaceRegexPattern(filterParams.TestNamespace!));

            // Handle class filtering with regex (shared pattern ensures validation sync)
            if (!string.IsNullOrEmpty(filterParams.TestClass))
                groupNames.Add(CreateClassRegexPattern(filterParams.TestClass!));

            if (groupNames.Any())
                filter.groupNames = groupNames.ToArray();

            if (testNames.Any())
                filter.testNames = testNames.ToArray();

            return filter;
        }

        /// <summary>
        /// Creates a regex pattern for namespace filtering that matches Unity's Filter.groupNames behavior.
        /// This ensures our validation logic (CountFilteredTests) matches exactly what Unity's TestRunner will execute.
        /// Pattern: "^{namespace}\." - matches tests in the specified namespace and its sub namespaces.
        /// </summary>
        /// <param name="namespaceName">The namespace to filter by</param>
        /// <returns>Regex pattern for Unity's Filter.groupNames field</returns>
        private static string CreateNamespaceRegexPattern(string namespaceName)
            => $"^{EscapeRegex(namespaceName)}\\.";

        /// <summary>
        /// Creates a regex pattern for class filtering that matches Unity's Filter.groupNames behavior.
        /// This ensures our validation logic (CountFilteredTests) matches exactly what Unity's TestRunner will execute.
        /// Pattern: "^.*\.{className}\.[^\.]+$" - matches any test class with the specified name followed by a method name.
        /// </summary>
        /// <param name="className">The class name to filter by</param>
        /// <returns>Regex pattern for Unity's Filter.groupNames field</returns>
        static string CreateClassRegexPattern(string className)
            => $"^.*\\.{EscapeRegex(className)}\\.[^\\.]+$";

        /// <summary>
        /// Escapes special regex characters to ensure literal string matching.
        /// Used by the shared regex pattern builders to safely handle user input that may contain regex meta characters.
        /// </summary>
        /// <param name="input">The string to escape</param>
        /// <returns>Regex-safe escaped string</returns>
        static string EscapeRegex(string input)
            => Regex.Escape(input);

        static async Task<int> GetMatchingTestCount(TestRunnerApi testRunnerApi, TestMode testMode, TestFilterParameters filterParams)
        {
            try
            {
                var tcs = new TaskCompletionSource<int>();

                testRunnerApi.RetrieveTestList(testMode, (testRoot) =>
                {
                    var testCount = testRoot != null
                        ? CountFilteredTests(testRoot, filterParams)
                        : 0;

                    if (EditorAutomationLogConfig.IsLogEnabled(LogLevel.Information))
                        Debug.Log($"[TestRunner] {testCount} {testMode} tests matched for {filterParams}");

                    tcs.SetResult(testCount);
                });

                // Wait for the test count result with timeout
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completedTask == timeoutTask)
                    throw new OperationCanceledException("Test list retrieval timed out");

                return await tcs.Task;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        static async Task<string?> ValidateTestFilters(TestRunnerApi testRunnerApi, TestMode testMode, TestFilterParameters filterParams)
        {
            try
            {
                var testCount = await GetMatchingTestCount(testRunnerApi, testMode, filterParams);
                if (testCount == 0)
                    return Error.NoTestsFound(filterParams);

                return null; // No error, tests found
            }
            catch (Exception ex)
            {
                return Error.TestExecutionFailed($"Filter validation failed: {ex.Message}");
            }
        }

        static int CountFilteredTests(ITestAdaptor test, TestFilterParameters filterParams)
        {
            // If no filters are specified, count all tests
            if (!filterParams.HasAnyFilter)
                return TestResultCollector.CountTests(test);

            var count = 0;

            // Check if this test matches the filters
            if (!test.IsSuite)
            {
                var matches = false;

                // Check assembly filter using UniqueName which contains assembly information
                if (!string.IsNullOrEmpty(filterParams.TestAssembly))
                {
                    var dllIndex = test.UniqueName.ToLowerInvariant().IndexOf(".dll");
                    if (dllIndex > 0)
                    {
                        var assemblyName = test.UniqueName[..dllIndex];
                        if (assemblyName.Equals(filterParams.TestAssembly, StringComparison.OrdinalIgnoreCase))
                            matches = true;
                    }
                }

                // Check namespace filter using same regex pattern as Filter.groupNames (ensures sync with Unity's execution)
                if (!matches && !string.IsNullOrEmpty(filterParams.TestNamespace))
                {
                    var namespacePattern = CreateNamespaceRegexPattern(filterParams.TestNamespace!);
                    if (Regex.IsMatch(test.FullName, namespacePattern))
                        matches = true;
                }

                // Check class filter using same regex pattern as Filter.groupNames (ensures sync with Unity's execution)
                if (!matches && !string.IsNullOrEmpty(filterParams.TestClass))
                {
                    var classPattern = CreateClassRegexPattern(filterParams.TestClass!);
                    if (Regex.IsMatch(test.FullName, classPattern))
                        matches = true;
                }

                // Check method filter (FixtureName.TestName format, same as Filter.testNames)
                if (!matches && !string.IsNullOrEmpty(filterParams.TestMethod))
                {
                    if (test.FullName.Equals(filterParams.TestMethod, StringComparison.OrdinalIgnoreCase))
                        matches = true;
                }

                if (matches)
                    count = 1;
            }

            // Recursively check children
            if (test.HasChildren)
            {
                foreach (var child in test.Children)
                    count += CountFilteredTests(child, filterParams);
            }

            return count;
        }
    }
}
