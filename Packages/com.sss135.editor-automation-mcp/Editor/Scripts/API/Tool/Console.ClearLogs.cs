#nullable enable
using System;
using System.ComponentModel;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Utils;
using UnityEngine;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Console
    {
        public const string ConsoleClearLogsToolId = "console-clear-logs";
        [McpTool
        (
            ConsoleClearLogsToolId,
            Title = "Console / Clear Logs",
            DestructiveHint = true,
            IdempotentHint = true
        )]
        [Description("Clears the MCP log cache (used by console-get-logs) and the Unity Editor Console window. " +
            "Useful for isolating errors related to a specific action by clearing logs before performing the action.")]
        public void ClearLogs(string? nothing = null)
        {
            MainThread.Instance.Run(() =>
            {
                Debug.ClearDeveloperConsole();

                // EditorLogCollectorService bootstraps an InMemoryLogStorage-backed
                // UnityLogCollector lazily on first access; the legacy bridge-owned
                // log-collector accessor was deleted in Wave 2.
                var logCollector = EditorLogCollectorService.Instance;
                if (logCollector == null)
                    throw new InvalidOperationException("LogCollector is not initialized.");

                logCollector.Clear();
            });
        }
    }
}
