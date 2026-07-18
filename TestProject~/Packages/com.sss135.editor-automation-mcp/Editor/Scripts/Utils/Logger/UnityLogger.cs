#nullable enable
using System;
using Microsoft.Extensions.Logging;

namespace com.SSS135.EditorAutomation.Utils
{
    using LogLevelMicrosoft = Microsoft.Extensions.Logging.LogLevel;

    public class UnityLogger : ILogger
    {
        readonly string _categoryName;
        readonly string _categoryColor;

        // Colors that look good on dark gray background
        private static readonly string[] CategoryColors = new[]
        {
            "#FF6B9D", // Pink
            "#4ECDC4", // Teal
            "#FFD93D", // Yellow
            "#95E1D3", // Mint
            "#F38181", // Coral
            "#AA96DA", // Purple
            "#6BCF7F", // Green
            "#5DADE2", // Blue
            "#F8B739", // Orange
            "#EC7063", // Red
            "#48C9B0", // Turquoise
            "#AF7AC5", // Violet
            "#58D68D", // Lime
            "#F5B041", // Amber
            "#85C1E2"  // Sky Blue
        };

        public UnityLogger(string categoryName)
        {
            _categoryName = categoryName.Contains('.')
                ? categoryName.Substring(categoryName.LastIndexOf('.') + 1)
                : categoryName;

            // Use hashcode to consistently select a color for this category
            var colorIndex = Math.Abs(categoryName.GetHashCode()) % CategoryColors.Length;
            _categoryColor = CategoryColors[colorIndex];
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null!;

        public bool IsEnabled(LogLevelMicrosoft logLevel)
        {
            // Prevent infinite loop during early init: bypass the threshold gate when not ready.
            if (!EditorAutomationLogConfig.HasAnyInstance)
            {
                // During initialization, allow all log levels
                return true;
            }

            // EditorAutomationLogConfig now stores the threshold as Microsoft.Extensions.Logging.LogLevel
            // directly — the prior Runtime.Utils.LogLevel switch is no longer needed.
            return EditorAutomationLogConfig.IsLogEnabled(logLevel);
        }

        static bool ShouldSuppress(string categoryName, LogLevelMicrosoft logLevel, string message)
        {
            if (categoryName == "HttpGateway" &&
                (logLevel == LogLevelMicrosoft.Trace ||
                 logLevel == LogLevelMicrosoft.Debug ||
                 logLevel == LogLevelMicrosoft.Information))
            {
                return true;
            }

            if (categoryName == "McpManagerClientHub" &&
                logLevel == LogLevelMicrosoft.Warning &&
                message.Contains("EnsureConnection Connection not available and auto-reconnect disabled for endpoint: /hub/mcp-server", StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }

        public void Log<TState>(LogLevelMicrosoft logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            if (formatter == null) throw new ArgumentNullException(nameof(formatter));
            if (state == null) throw new ArgumentNullException(nameof(state));

            var text = formatter(state, exception);
            if (ShouldSuppress(_categoryName, logLevel, text))
                return;

            // Map LogLevel to short names
#if UNITY_EDITOR
            string logLevelShort = logLevel switch
            {
                LogLevelMicrosoft.Critical => "<color=#ff0000>crit:</color>",
                LogLevelMicrosoft.Error => "<color=#ff6b6b>fail:</color>",
                LogLevelMicrosoft.Warning => "<color=#ffaa00>warn:</color>",
                LogLevelMicrosoft.Information => "<color=#00ff00>info:</color>",
                LogLevelMicrosoft.Debug => "<color=#00ffff>dbug:</color>",
                LogLevelMicrosoft.Trace => "<color=#aaaaaa>trce:</color>",
                _ => "<color=#ffffff>none</color>"
            };
            var message = $"{logLevelShort} [{DateTime.Now:HH:mm:ss:ffff}] <color=#B4FF32>[AI]</color> <color={_categoryColor}><b>{_categoryName}</b></color> {text}";
#else
            string logLevelShort = logLevel switch
            {
                LogLevelMicrosoft.Critical => "crit:",
                LogLevelMicrosoft.Error => "fail:",
                LogLevelMicrosoft.Warning => "warn:",
                LogLevelMicrosoft.Information => "info:",
                LogLevelMicrosoft.Debug => "dbug:",
                LogLevelMicrosoft.Trace => "trce:",
                _ => "none"
            };
            var message = $"{logLevelShort} [{DateTime.Now:HH:mm:ss:ffff}] [AI] {_categoryName} {text}";
#endif

            switch (logLevel)
            {
                case LogLevelMicrosoft.Critical:
                case LogLevelMicrosoft.Error:
                    if (exception != null)
                        UnityEngine.Debug.LogException(exception);
                    UnityEngine.Debug.LogError(message);
                    break;

                case LogLevelMicrosoft.Warning:
                    if (exception != null)
                        UnityEngine.Debug.LogWarning(exception);
                    UnityEngine.Debug.LogWarning(message);
                    break;

                default:
                    if (exception != null)
                        UnityEngine.Debug.Log(exception);
                    UnityEngine.Debug.Log(message);
                    break;
            }
        }
    }
}
