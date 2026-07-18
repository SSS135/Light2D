#nullable enable
using System.Collections.Generic;
using System.Linq;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Editor.Framework.Jobs;
using UnityEditor;

namespace com.SSS135.EditorAutomation.Editor.Utils
{
    /// <summary>
    /// Utility class for handling package operations that may trigger domain reloads.
    /// Uses SessionState to persist notification data across domain reloads.
    /// </summary>
    public static class PackageUtils
    {
        private const string PendingNotificationKeysKey = "MCP_PendingPackageNotificationKeys";
        private const string NotificationDataSeparator = "<MCP_PKG_SEP>";

        private static bool _processPendingScheduled = false;

        public static void Init()
        {
            // Process any pending notifications after domain reload
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
        /// Schedules a notification to be sent after Unity domain reload completes.
        /// Uses SessionState to persist across domain reloads.
        /// </summary>
        /// <param name="ticketId">The job-ticket id allocated by <see cref="JobHelpers.StartJob"/>; persisted into SessionState so the post-reload notifier can call <see cref="JobRegistry.Complete"/>.</param>
        /// <param name="packageIdentifier">The package identifier that was operated on</param>
        /// <param name="operationType">The type of operation performed (Add, Remove)</param>
        /// <param name="expectedResult">Whether the operation was expected to succeed before domain reload</param>
        public static void SchedulePostDomainReloadNotification(string ticketId, string packageIdentifier, string operationType, bool expectedResult)
        {
            var notificationKey = $"MCP_PendingPackageNotification_{ticketId}";
            var notificationData = $"{ticketId}{NotificationDataSeparator}{packageIdentifier}{NotificationDataSeparator}{operationType}{NotificationDataSeparator}{expectedResult}";

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
        /// Called by InitializeOnLoad to process any pending notifications after domain reload.
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
                if (parts.Length != 4)
                {
                    processedKeys.Add(key);
                    continue;
                }

                var ticketId = parts[0];
                var packageIdentifier = parts[1];
                var operationType = parts[2];
                var expectedResult = bool.Parse(parts[3]);

                // Check for compilation errors after package operation
                var hasCompilationErrors = ScriptUtils.HasCompilationErrors();

                McpResponse response;
                if (hasCompilationErrors)
                {
                    var errorDetails = ScriptUtils.GetCompilationErrorDetails();
                    response = McpResponse.Success(
                        $"[Warning] Package {operationType} completed: {packageIdentifier}, but compilation errors occurred after domain reload. Details:\n{errorDetails}");
                }
                else if (expectedResult)
                {
                    response = McpResponse.Success(
                        $"[Success] Package {operationType} completed: {packageIdentifier}. Domain reload finished successfully.");
                }
                else
                {
                    response = McpResponse.Error($"[Error] Package {operationType} failed: {packageIdentifier}");
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
    }
}
