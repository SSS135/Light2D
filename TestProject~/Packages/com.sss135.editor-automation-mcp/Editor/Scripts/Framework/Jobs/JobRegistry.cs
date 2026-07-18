#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace com.SSS135.EditorAutomation.Editor.Framework.Jobs
{
    /// <summary>
    /// Lifecycle of a job ticket. Mirrors Unreal's job-state enum so wire bodies match exactly.
    /// </summary>
    public enum JobStatus
    {
        Running = 0,
        Completed = 1,
        Cancelled = 2,
        Errored = 3,
    }

    /// <summary>
    /// Single in-memory ticket. Exposed read-only to RPC callers via <see cref="JobRegistry"/>.
    /// </summary>
    public sealed class JobTicket
    {
        public string Id { get; }
        public string Method { get; }
        public DateTime StartedAtUtc { get; }
        public DateTime? CompletedAtUtc { get; internal set; }
        public JobStatus Status { get; internal set; }
        public McpResponse? Result { get; internal set; }

        // Bounded ring of recent progress payloads. The dispatcher's monitor log is the
        // authoritative timeline; this ring exists so `system.job_status` can return the
        // tail without re-reading JSONL.
        internal readonly List<JsonNode> ProgressRing = new List<JsonNode>();
        internal long LastProgressTicks;

        // Cancellation token observed by JobHelpers binders that opt into cancellation.
        internal readonly CancellationTokenSource Cts = new CancellationTokenSource();

        // Signalled by JobRegistry.Complete with the terminal McpResponse. system-job-wait and
        // sync-mode tests-run await this so callers can block server-side instead of polling.
        // RunContinuationsAsynchronously prevents an awaiter from running inline on the completer's
        // thread — required because Complete signals while holding the registry lock.
        internal readonly TaskCompletionSource<McpResponse> CompletionTcs =
            new TaskCompletionSource<McpResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

        public JobTicket(string id, string method)
        {
            Id = id;
            Method = method ?? string.Empty;
            StartedAtUtc = DateTime.UtcNow;
            Status = JobStatus.Running;
        }
    }

    /// <summary>
    /// Process-static thread-safe registry of long-running job tickets. The HTTP gateway
    /// ticks <see cref="EvictExpired"/> on <c>EditorApplication.update</c> so completed
    /// tickets age out after the configured TTL; <see cref="JobMonitorLog"/> retains the
    /// full history on disk.
    /// </summary>
    /// <remarks>
    /// Ticket id format: <c>j_&lt;utc_compact_stamp&gt;_&lt;8-hex&gt;</c>. Matches Unreal's
    /// <c>FJobRegistry::AllocateId</c> exactly so cross-engine tooling can pattern-match.
    /// Capacity caps are intentionally bounded: <see cref="MaxProgressEvents"/> = 50 entries
    /// per ticket; older progress payloads are dropped from the in-memory ring but remain
    /// in the JSONL monitor.
    ///
    /// Jobs always run to completion. There is no public cancellation API — internal
    /// cancellation (per-call timeout, gateway shutdown) still occurs through the per-ticket
    /// <see cref="JobTicket.Cts"/>, but no caller can request a cancel from outside.
    /// </remarks>
    public static class JobRegistry
    {
        public const int MaxProgressEvents = 50;

        static readonly object _lock = new();
        static readonly Dictionary<string, JobTicket> _tickets = new(StringComparer.Ordinal);

        static long _completedTtlSeconds = 600;
        static long _progressMinIntervalMs = 250;

        /// <summary>
        /// Apply runtime limits from <c>HttpGatewaySettings</c>. Non-positive values fall
        /// back to safe defaults so a misconfigured settings file cannot disable eviction.
        /// </summary>
        public static void ConfigureLimits(long completedTtlSeconds, long progressMinIntervalMs)
        {
            Interlocked.Exchange(ref _completedTtlSeconds, completedTtlSeconds > 0 ? completedTtlSeconds : 600);
            Interlocked.Exchange(ref _progressMinIntervalMs, progressMinIntervalMs > 0 ? progressMinIntervalMs : 250);
        }

        /// <summary>
        /// Generate a fresh ticket id of the form <c>j_yyyyMMddTHHmmssfffZ_xxxxxxxx</c>.
        /// </summary>
        public static string AllocateId()
        {
            var stamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture);
            var hex = Guid.NewGuid().ToString("N").Substring(0, 8);
            return "j_" + stamp + "_" + hex;
        }

        /// <summary>
        /// Allocate a new ticket and write its <c>started</c> line to the monitor log.
        /// </summary>
        public static JobTicket Start(string method, JsonNode? startedPayload = null)
        {
            var ticket = new JobTicket(AllocateId(), method);
            lock (_lock)
            {
                _tickets[ticket.Id] = ticket;
            }
            JobMonitorLog.Append(ticket.Id, "started", ticket.Method, startedPayload);
            return ticket;
        }

        /// <summary>
        /// Look up a ticket by id. Returns null when the ticket has already aged out.
        /// </summary>
        public static JobTicket? Find(string ticketId)
        {
            if (string.IsNullOrEmpty(ticketId))
                return null;
            lock (_lock)
            {
                return _tickets.TryGetValue(ticketId, out var t) ? t : null;
            }
        }

        /// <summary>
        /// Slim entries used by <c>system.job_list</c>. Matches Unreal's implementation
        /// shape (ticket_id / method / status / started_at) rather than its inaccurate wiki.
        /// </summary>
        public static IReadOnlyList<JsonObject> ListSlim()
        {
            lock (_lock)
            {
                var entries = new List<JsonObject>(_tickets.Count);
                foreach (var ticket in _tickets.Values)
                {
                    entries.Add(new JsonObject
                    {
                        ["ticket_id"] = ticket.Id,
                        ["method"] = ticket.Method,
                        ["status"] = StatusToString(ticket.Status),
                        ["started_at"] = ticket.StartedAtUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    });
                }
                entries.Sort((a, b) => string.Compare(
                    a["started_at"]?.GetValue<string>() ?? string.Empty,
                    b["started_at"]?.GetValue<string>() ?? string.Empty,
                    StringComparison.Ordinal));
                return entries;
            }
        }

        /// <summary>
        /// Record a progress event. Rate-limited per ticket by <c>ProgressEventMinIntervalMs</c>;
        /// events arriving faster than the budget are dropped to keep the monitor log readable.
        /// Returns true when the event was accepted.
        /// </summary>
        public static bool Progress(string ticketId, JsonNode? payload)
        {
            JobTicket? ticket;
            lock (_lock)
            {
                if (!_tickets.TryGetValue(ticketId, out ticket))
                    return false;
                if (ticket.Status != JobStatus.Running)
                    return false;

                var nowTicks = DateTime.UtcNow.Ticks;
                var minDeltaTicks = Interlocked.Read(ref _progressMinIntervalMs) * TimeSpan.TicksPerMillisecond;
                if (ticket.LastProgressTicks != 0 && (nowTicks - ticket.LastProgressTicks) < minDeltaTicks)
                    return false;
                ticket.LastProgressTicks = nowTicks;

                if (payload != null)
                {
                    ticket.ProgressRing.Add(payload.DeepClone());
                    if (ticket.ProgressRing.Count > MaxProgressEvents)
                        ticket.ProgressRing.RemoveAt(0);
                }
            }

            JobMonitorLog.Append(ticketId, "progress", ticket.Method, payload);
            return true;
        }

        /// <summary>
        /// Mark a ticket as completed with the supplied <paramref name="result"/>. The matching
        /// <c>completed</c> / <c>cancelled</c> / <c>error</c> line is appended to the monitor log.
        /// Safe to call multiple times — only the first call mutates the in-memory ticket;
        /// subsequent calls still emit a JSONL line so a duplicate from a post-domain-reload
        /// notifier remains observable when the original in-memory ticket has been wiped.
        /// </summary>
        public static void Complete(string ticketId, McpResponse result)
        {
            if (string.IsNullOrEmpty(ticketId) || result == null)
                return;

            JobTicket? ticket;
            JobStatus terminalStatus;
            string methodForLog;
            lock (_lock)
            {
                if (_tickets.TryGetValue(ticketId, out ticket) && ticket.Status == JobStatus.Running)
                {
                    terminalStatus = MapResponseToStatus(result.Status);
                    ticket.Status = terminalStatus;
                    ticket.Result = result;
                    ticket.CompletedAtUtc = DateTime.UtcNow;
                    methodForLog = ticket.Method;
                    // Wake awaiters of system-job-wait / sync tests-run. TrySetResult is idempotent
                    // so a duplicate post-domain-reload Complete won't throw. The TCS's
                    // RunContinuationsAsynchronously flag keeps the awaiter from resuming under
                    // this lock.
                    ticket.CompletionTcs.TrySetResult(result);
                }
                else
                {
                    // Ticket aged out (typical after a domain reload between Start and Complete).
                    // Map the response status anyway so the JSONL event carries the right verb.
                    terminalStatus = MapResponseToStatus(result.Status);
                    methodForLog = ticket?.Method ?? string.Empty;
                    // Idempotent signal in case the ticket exists but is already terminal —
                    // TrySetResult is a no-op when the TCS has already completed.
                    ticket?.CompletionTcs.TrySetResult(result);
                }
            }

            JobMonitorLog.Append(
                ticketId,
                terminalStatus switch
                {
                    JobStatus.Cancelled => "cancelled",
                    JobStatus.Errored => "error",
                    _ => "completed",
                },
                methodForLog,
                BuildResultPayload(result));
        }

        /// <summary>
        /// Drop completed tickets older than <c>CompletedTicketTtlSeconds</c>. The HTTP
        /// gateway calls this on <c>EditorApplication.update</c>.
        /// </summary>
        public static void EvictExpired()
        {
            var ttlSeconds = Interlocked.Read(ref _completedTtlSeconds);
            if (ttlSeconds <= 0)
                return;

            var threshold = DateTime.UtcNow - TimeSpan.FromSeconds(ttlSeconds);
            lock (_lock)
            {
                if (_tickets.Count == 0)
                    return;

                List<string>? toRemove = null;
                foreach (var kvp in _tickets)
                {
                    var t = kvp.Value;
                    if (t.Status == JobStatus.Running)
                        continue;
                    if (!t.CompletedAtUtc.HasValue)
                        continue;
                    if (t.CompletedAtUtc.Value > threshold)
                        continue;
                    (toRemove ??= new List<string>()).Add(kvp.Key);
                }

                if (toRemove != null)
                {
                    foreach (var id in toRemove)
                    {
                        if (_tickets.TryGetValue(id, out var t))
                        {
                            try { t.Cts.Dispose(); } catch { }
                            _tickets.Remove(id);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Drop every ticket. Test-only — production callers should rely on TTL eviction.
        /// </summary>
        public static void Reset()
        {
            lock (_lock)
            {
                foreach (var t in _tickets.Values)
                {
                    try { t.Cts.Dispose(); } catch { }
                }
                _tickets.Clear();
            }
        }

        /// <summary>
        /// Build the <c>system.job_status</c> response body for a ticket. Includes the
        /// final result when present plus the in-memory progress tail.
        /// </summary>
        public static JsonObject BuildStatusBody(JobTicket ticket)
        {
            var body = new JsonObject
            {
                ["ticket_id"] = ticket.Id,
                ["method"] = ticket.Method,
                ["status"] = StatusToString(ticket.Status),
                ["started_at"] = ticket.StartedAtUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            };

            if (ticket.CompletedAtUtc.HasValue)
                body["completed_at"] = ticket.CompletedAtUtc.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

            if (ticket.Result != null)
                body["result"] = BuildResultPayload(ticket.Result);

            lock (_lock)
            {
                if (ticket.ProgressRing.Count > 0)
                {
                    var arr = new JsonArray(ticket.ProgressRing.Count);
                    foreach (var p in ticket.ProgressRing)
                        arr.Add(p.DeepClone());
                    body["progress"] = arr;
                }
            }

            return body;
        }

        static JsonObject BuildResultPayload(McpResponse response)
        {
            var payload = new JsonObject
            {
                ["status"] = response.Status.ToString(),
            };

            if (response.StructuredResult != null)
                payload["structured"] = response.StructuredResult.DeepClone();

            var message = response.GetMessage();
            if (!string.IsNullOrEmpty(message))
                payload["message"] = message;

            if (response.ErrorCode.HasValue)
                payload["errorCode"] = response.ErrorCode.Value;

            return payload;
        }

        static JobStatus MapResponseToStatus(McpStatus status) => status switch
        {
            McpStatus.Success => JobStatus.Completed,
            McpStatus.Cancelled => JobStatus.Cancelled,
            McpStatus.Error => JobStatus.Errored,
            // A Processing response from a terminal callback is nonsensical; map to Completed
            // so the ticket leaves the running set rather than wedging forever.
            _ => JobStatus.Completed,
        };

        static string StatusToString(JobStatus status) => status switch
        {
            JobStatus.Running => "running",
            JobStatus.Completed => "completed",
            JobStatus.Cancelled => "cancelled",
            JobStatus.Errored => "errored",
            _ => "unknown",
        };
    }
}
