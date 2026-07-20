#nullable enable
using System;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;

namespace com.SSS135.EditorAutomation.Editor.Framework.Jobs
{
    /// <summary>
    /// JSONL append-only sink for job-ticket lifecycle events. Lives under
    /// <c>Library/EditorAutomationHttp/jobs.jsonl</c> alongside the gateway's port file.
    /// Wiped at gateway startup; size-rotated when the active file crosses the configured
    /// byte budget. Each call writes one fully-formed JSON object followed by <c>\n</c>
    /// and is flushed to disk synchronously so an external tail observer always sees
    /// complete lines.
    /// </summary>
    /// <remarks>
    /// Schema (per line):
    /// <code>
    /// { "ts": "2025-05-21T12:34:56.789Z", "ticket_id": "j_...", "event": "started|progress|completed|cancelled|error", "method": "...", "data": { ... } }
    /// </code>
    /// The <c>data</c> field is freeform — progress events carry whatever <see cref="JsonNode"/>
    /// the registry hands in, terminal events carry the final result payload.
    /// Synthetic gateway-level events (<c>gateway_start</c>, <c>gateway_stop</c>) emit the same
    /// shape but with <c>ticket_id: null</c> and <c>method: ""</c>; see <see cref="AppendGatewayEvent"/>.
    /// </remarks>
    public static class JobMonitorLog
    {
        const string DirRel = "Library/EditorAutomationHttp";
        const string FileRel = "Library/EditorAutomationHttp/jobs.jsonl";
        const int MaxRetainedLines = 30;

        const int DefaultRotationKeep = 3;

        static readonly object _lock = new();
        static int _rotationKeep = DefaultRotationKeep;

        public static string FilePath { get; } =
            Path.Combine(Directory.GetCurrentDirectory(), FileRel.Replace('/', Path.DirectorySeparatorChar));

        static string DirectoryPath { get; } =
            Path.Combine(Directory.GetCurrentDirectory(), DirRel.Replace('/', Path.DirectorySeparatorChar));

        /// <summary>
        /// Apply compatibility settings sourced from <c>HttpGatewaySettings</c>. The active
        /// monitor file is retained by line count; rotated siblings are removed at startup.
        /// </summary>
        public static void ConfigureLimits(long maxBytes, int rotationKeep)
        {
            lock (_lock)
            {
                _ = maxBytes;
                _rotationKeep = rotationKeep > 0 ? rotationKeep : DefaultRotationKeep;
            }
        }

        /// <summary>
        /// Keep the monitor across gateway restarts while pruning old entries. Domain reloads
        /// restart the gateway in the same process, and must preserve ticket history for JSONL fallback.
        /// </summary>
        public static void PruneAtStartup()
        {
            lock (_lock)
            {
                try
                {
                    Directory.CreateDirectory(DirectoryPath);
                    DeleteRotatedMonitorFilesUnlocked();
                    TrimToMaxLinesUnlocked();
                }
                catch
                {
                    // Monitor cleanup failures must not block gateway startup; the next Append will
                    // attempt to recreate the directory.
                }
            }
        }

        /// <summary>
        /// Append one structured event line. Acquires an exclusive process-wide lock,
        /// rotates the file if it would cross <see cref="_maxBytes"/>, then flushes.
        /// </summary>
        public static void Append(string ticketId, string @event, string method, JsonNode? data = null)
        {
            if (string.IsNullOrEmpty(ticketId) || string.IsNullOrEmpty(@event))
                return;

            var line = new JsonObject
            {
                ["ts"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                ["ticket_id"] = ticketId,
                ["event"] = @event,
                ["method"] = method ?? string.Empty,
            };
            if (data != null)
                line["data"] = data.DeepClone();

            WriteLine(line);
        }

        /// <summary>
        /// Append a synthetic gateway-level event (e.g. <c>gateway_start</c>, <c>gateway_stop</c>)
        /// that does not belong to any ticket. The line shape matches per-ticket entries so an
        /// external tail observer can demultiplex on the <c>event</c> discriminator; <c>ticket_id</c>
        /// is emitted as JSON null so consumers can tell synthetic events from real tickets.
        /// </summary>
        public static void AppendGatewayEvent(string @event, JsonNode? data = null)
        {
            if (string.IsNullOrEmpty(@event))
                return;

            var line = new JsonObject
            {
                ["ts"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                ["ticket_id"] = null,
                ["event"] = @event,
                ["method"] = string.Empty,
            };
            if (data != null)
                line["data"] = data.DeepClone();

            WriteLine(line);
        }

        /// <summary>
        /// Scan the active JSONL file plus its rotated siblings (<c>.1</c>, <c>.2</c>, ...) newest-first
        /// for the most recent terminal event matching <paramref name="ticketId"/>. Terminal events
        /// are <c>completed</c>, <c>cancelled</c>, and <c>error</c> — they're the only ones carrying
        /// a useful payload to reconstruct from. Returns true on the first match, populating
        /// <paramref name="envelope"/> with the parsed JSON line. Individual line parse / I/O
        /// failures are swallowed and skipped — this is a best-effort recovery path consulted only
        /// after <see cref="JobRegistry.Find"/> has already missed.
        /// </summary>
        public static bool TryFindTerminalEvent(string ticketId, out JsonObject? envelope)
        {
            envelope = null;
            if (string.IsNullOrEmpty(ticketId))
                return false;

            // Walk active file first, then rotated siblings .1, .2, ... up to _rotationKeep.
            // Within each file we scan from the end backwards so the newest terminal event wins.
            lock (_lock)
            {
                if (TryScanFile(FilePath, ticketId, out envelope))
                    return true;

                for (var i = 1; i <= _rotationKeep; i++)
                {
                    var rotated = FilePath + "." + i;
                    if (TryScanFile(rotated, ticketId, out envelope))
                        return true;
                }
            }

            return false;
        }

        static bool TryScanFile(string path, string ticketId, out JsonObject? envelope)
        {
            envelope = null;
            string[] lines;
            try
            {
                if (!File.Exists(path))
                    return false;
                // Read with shared access so we don't race the active appender's FileStream.
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fs, Encoding.UTF8);
                var content = reader.ReadToEnd();
                lines = content.Split('\n');
            }
            catch
            {
                return false;
            }

            for (var idx = lines.Length - 1; idx >= 0; idx--)
            {
                var line = lines[idx];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                JsonObject? obj;
                try
                {
                    obj = JsonNode.Parse(line) as JsonObject;
                }
                catch
                {
                    continue;
                }
                if (obj == null)
                    continue;

                string? lineTicket = null;
                try { lineTicket = obj["ticket_id"]?.GetValue<string>(); } catch { }
                if (!string.Equals(lineTicket, ticketId, StringComparison.Ordinal))
                    continue;

                string? ev = null;
                try { ev = obj["event"]?.GetValue<string>(); } catch { }
                if (ev != "completed" && ev != "cancelled" && ev != "error")
                    continue;

                envelope = obj;
                return true;
            }

            return false;
        }

        static void WriteLine(JsonObject line)
        {
            var serialized = line.ToJsonString() + "\n";
            var bytes = Encoding.UTF8.GetBytes(serialized);

            lock (_lock)
            {
                try
                {
                    Directory.CreateDirectory(DirectoryPath);

                    using (var fs = new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.Read))
                    {
                        fs.Write(bytes, 0, bytes.Length);
                        fs.Flush(flushToDisk: true);
                    }

                    TrimToMaxLinesUnlocked();
                }
                catch
                {
                    // Monitor logging is observability-only — never throw out of Append.
                }
            }
        }

        static void DeleteRotatedMonitorFilesUnlocked()
        {
            for (var i = 1; i <= _rotationKeep + 1; i++)
            {
                var rotated = FilePath + "." + i;
                if (File.Exists(rotated))
                    File.Delete(rotated);
            }
        }

        static void TrimToMaxLinesUnlocked()
        {
            if (!File.Exists(FilePath))
                return;

            string[] lines;
            using (var fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fs, Encoding.UTF8))
            {
                lines = reader.ReadToEnd()
                    .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            }

            if (lines.Length <= MaxRetainedLines)
                return;

            var start = lines.Length - MaxRetainedLines;
            using var write = new FileStream(FilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(write, Encoding.UTF8);
            for (var i = start; i < lines.Length; i++)
                writer.WriteLine(lines[i].TrimEnd('\r'));
        }
    }
}
