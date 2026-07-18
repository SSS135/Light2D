#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Editor.Framework.Jobs;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using UnityEditor;
using UnityEditor.Compilation;

namespace com.SSS135.EditorAutomation.Editor.Utils
{
    [InitializeOnLoad]
    public static partial class ScriptUtils
    {
        private const string PendingNotificationKeysKey = "MCP_PendingNotificationKeys";
        private const string NotificationDataSeparator = "<MCP_SEP>";

        private static bool _processPendingScheduled = false;

        // Store compilation messages (thread-safe with lock)
        private static readonly List<CompilerMessage> _lastCompilationMessages = new List<CompilerMessage>();
        private static readonly object _compilationMessagesLock = new object();

        static ScriptUtils()
        {
            // Process any pending notifications after domain reload (successful compilation)
            ScheduleProcessPendingNotifications();

            // Also listen for compilation event to handle both success and failure cases
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
        }

        private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            lock (_compilationMessagesLock)
            {
                _lastCompilationMessages.Clear();
                _lastCompilationMessages.AddRange(messages);
            }
            ScheduleProcessPendingNotifications();
        }

        private static void ScheduleProcessPendingNotifications()
        {
            if (_processPendingScheduled)
                return;

            _processPendingScheduled = true;
            EditorApplication.update += ProcessPendingNotificationsOnce;
        }

        private static void ProcessPendingNotificationsOnce()
        {
            EditorApplication.update -= ProcessPendingNotificationsOnce;

            if (!_processPendingScheduled)
                return;

            _processPendingScheduled = false;
            ProcessPendingNotifications();
        }

        /// <summary>
        /// Checks if the provided C# code has valid syntax.
        /// This method uses Roslyn to parse the code and check for syntax errors.
        /// </summary>
        /// <param name="code">
        /// <param name="errors"></param>
        /// <returns>True if the code has valid syntax; otherwise, false.</returns>
        public static bool IsValidCSharpSyntax(string code, out IEnumerable<Diagnostic> errors)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var diagnostics = syntaxTree.GetDiagnostics();

            errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
            return !errors.Any();
        }

        /// <summary>
        /// Schedules a notification to be sent after Unity compilation completes.
        /// Uses SessionState to persist across domain reloads.
        /// </summary>
        /// <param name="ticketId">The job-ticket id allocated by <see cref="JobHelpers.StartJob"/>; persisted into SessionState so the post-compile notifier can call <see cref="JobRegistry.Complete"/>.</param>
        /// <param name="filePath">The file path that was modified</param>
        /// <param name="operationType">The type of operation performed</param>
        public static void SchedulePostCompilationNotification(string ticketId, string filePath, string operationType)
        {
            var notificationKey = $"MCP_PendingNotification_{ticketId}";
            var notificationData = $"{ticketId}{NotificationDataSeparator}{filePath}{NotificationDataSeparator}{operationType}";

            // Store the notification data
            SessionState.SetString(notificationKey, notificationData);

            // Add to the key list
            var existingKeys = SessionState.GetString(PendingNotificationKeysKey, string.Empty);
            var keyList = string.IsNullOrEmpty(existingKeys)
                ? new List<string>()
                : existingKeys.Split(',').Where(k => !string.IsNullOrEmpty(k)).ToList();

            if (!keyList.Contains(notificationKey))
            {
                keyList.Add(notificationKey);
                SessionState.SetString(PendingNotificationKeysKey, string.Join(",", keyList));
            }
        }

        /// <summary>
        /// Called by InitializeOnLoad to process any pending notifications after compilation.
        /// </summary>
        public static void ProcessPendingNotifications()
        {
            // Get the list of pending notification keys from SessionState
            var pendingKeys = SessionState.GetString(PendingNotificationKeysKey, string.Empty);
            if (string.IsNullOrEmpty(pendingKeys))
                return;

            var keys = pendingKeys.Split(',').Where(k => !string.IsNullOrEmpty(k)).ToList();
            if (keys.Count == 0)
                return;

            var processedKeys = new List<string>();

            foreach (var key in keys)
            {
                var notificationData = SessionState.GetString(key, string.Empty);
                if (string.IsNullOrEmpty(notificationData))
                {
                    processedKeys.Add(key);
                    continue;
                }

                var parts = notificationData.Split(NotificationDataSeparator);
                if (parts.Length != 3)
                {
                    processedKeys.Add(key);
                    continue;
                }

                var ticketId = parts[0];
                var filePath = parts[1];
                var operationType = parts[2];

                // Check for compilation errors
                var hasErrors = HasCompilationErrors();

                McpResponse response;
                if (hasErrors)
                {
                    var errorDetails = GetCompilationErrorDetails();
                    var message = $"[Warning] {operationType} completed: {filePath}, but compilation errors occurred. Details:\n{errorDetails}";
                    response = McpResponse.Success(message);
                }
                else
                {
                    var message = $"[Success] {operationType} completed: {filePath}";
                    response = McpResponse.Success(message);
                }

                // Complete the matching job ticket. The in-memory ticket may have aged out across
                // the domain reload — JobRegistry.Complete still appends the terminal JSONL line
                // so a tail observer (or the on-disk monitor) records the outcome regardless.
                JobRegistry.Complete(ticketId, response);
                processedKeys.Add(key);
            }

            // Clean up processed keys
            foreach (var key in processedKeys)
                SessionState.EraseString(key);

            // Update the key list
            var remainingKeys = keys.Except(processedKeys).ToList();
            if (remainingKeys.Count > 0)
            {
                SessionState.SetString(PendingNotificationKeysKey, string.Join(",", remainingKeys));
            }
            else
            {
                SessionState.EraseString(PendingNotificationKeysKey);
            }
        }

        /// <summary>
        /// Checks if there are any compilation errors by examining Unity's console and compilation pipeline.
        /// </summary>
        /// <returns>True if compilation errors exist</returns>
        public static bool HasCompilationErrors() => EditorUtility.scriptCompilationFailed;

        /// <summary>
        /// Retrieves detailed compilation error messages from the last compilation.
        /// </summary>
        /// <returns>Detailed compilation error messages</returns>
        public static string GetCompilationErrorDetails()
        {
            var errors = new List<string>();

            lock (_compilationMessagesLock)
            {
                foreach (var message in _lastCompilationMessages)
                {
                    if (message.type == CompilerMessageType.Error)
                        errors.Add($"[{message.file}] {message.message} (Line: {message.line})");
                }
            }

            if (errors.Count == 0)
            {
                // Since we can reliably check if compilation failed with EditorUtility.scriptCompilationFailed,
                // but getting detailed messages is complex, provide a simple fallback
                if (EditorUtility.scriptCompilationFailed)
                    return "Compilation errors detected. Please check the Unity Console window for detailed error messages.";

                errors.Add("No detailed compilation errors found.");
            }

            return string.Join("\n", errors);
        }

        /// <summary>
        /// Returns the last compilation's error diagnostics as a structured array, mirroring the
        /// <c>{type, assemblyPath, file, line, column, message}</c> shape produced by
        /// <see cref="com.SSS135.EditorAutomation.Editor.Utils.CompileRequestState"/> so machine consumers
        /// see one consistent diagnostic schema. Built from the same <c>_lastCompilationMessages</c>
        /// source as <see cref="GetCompilationErrorDetails"/>; <c>assemblyPath</c> is empty because the
        /// per-message list does not retain it (Unity's <see cref="UnityEditor.Compilation.CompilerMessage"/>
        /// carries no assembly path).
        /// </summary>
        /// <returns>A <see cref="JsonArray"/> of diagnostic objects; empty when there are no error messages.</returns>
        public static JsonArray GetCompilationErrorList()
        {
            var arr = new JsonArray();

            lock (_compilationMessagesLock)
            {
                foreach (var message in _lastCompilationMessages)
                {
                    if (message.type != CompilerMessageType.Error)
                        continue;

                    arr.Add(new JsonObject
                    {
                        ["type"] = message.type.ToString(),
                        ["assemblyPath"] = string.Empty,
                        ["file"] = message.file ?? string.Empty,
                        ["line"] = message.line,
                        ["column"] = message.column,
                        ["message"] = message.message ?? string.Empty,
                    });
                }
            }

            // Match the GetCompilationErrorDetails fallback: report failure even when detailed
            // per-message diagnostics were not captured (e.g. errors observed before this domain).
            if (arr.Count == 0 && EditorUtility.scriptCompilationFailed)
            {
                arr.Add(new JsonObject
                {
                    ["type"] = "Error",
                    ["assemblyPath"] = string.Empty,
                    ["file"] = string.Empty,
                    ["line"] = 0,
                    ["column"] = 0,
                    ["message"] = "Compilation errors detected. Please check the Unity Console window for detailed error messages.",
                });
            }

            return arr;
        }
    }
}