#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Utils;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace com.SSS135.EditorAutomation
{
    using ILogger = Microsoft.Extensions.Logging.ILogger;

    /// <summary>
    /// In-memory ring buffer log storage. Drop-in replacement for the file-backed
    /// log storage classes; never touches disk so it can never collide with another
    /// Unity process holding the same log file open.
    /// </summary>
    public class InMemoryLogStorage : ILogStorage
    {
        public const int DefaultCapacity = 5000;

        protected readonly ILogger _logger;
        protected readonly object _bufferMutex = new();
        protected readonly Queue<LogEntry> _buffer;
        protected readonly int _capacity;
        protected readonly ThreadSafeBool _isDisposed = new(false);

        public InMemoryLogStorage(ILogger? logger = null, int capacity = DefaultCapacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");

            _logger = logger ?? UnityLoggerFactory.LoggerFactory.CreateLogger(GetType().GetTypeShortName());
            _capacity = capacity;
            _buffer = new Queue<LogEntry>(capacity);
        }

        public Task AppendAsync(params LogEntry[] entries)
        {
            Append(entries);
            return Task.CompletedTask;
        }

        public void Append(params LogEntry[] entries)
        {
            if (_isDisposed.Value || entries == null || entries.Length == 0)
                return;

            lock (_bufferMutex)
            {
                foreach (var entry in entries)
                {
                    if (entry == null)
                        continue;

                    while (_buffer.Count >= _capacity)
                        _buffer.Dequeue();

                    _buffer.Enqueue(entry);
                }
            }
        }

        public Task FlushAsync() => Task.CompletedTask;
        public void Flush() { }

        public Task<LogEntry[]> QueryAsync(
            int maxEntries = 100,
            LogType? logTypeFilter = null,
            bool includeStackTrace = false,
            int lastMinutes = 0)
        {
            return Task.FromResult(Query(maxEntries, logTypeFilter, includeStackTrace, lastMinutes));
        }

        public LogEntry[] Query(
            int maxEntries = 100,
            LogType? logTypeFilter = null,
            bool includeStackTrace = false,
            int lastMinutes = 0)
        {
            if (_isDisposed.Value || maxEntries <= 0)
                return Array.Empty<LogEntry>();

            var cutoffTime = lastMinutes > 0
                ? DateTime.Now.AddMinutes(-lastMinutes)
                : (DateTime?)null;

            lock (_bufferMutex)
            {
                // Newest entries are at the tail of the queue; collect newest-first then reverse so the
                // returned array is oldest-first (matches the behavior of the file-backed storage).
                var collected = new List<LogEntry>(Math.Min(maxEntries, _buffer.Count));
                var snapshot = _buffer.ToArray();
                for (int i = snapshot.Length - 1; i >= 0; i--)
                {
                    var entry = snapshot[i];
                    if (logTypeFilter.HasValue && entry.LogType != logTypeFilter.Value)
                        continue;
                    if (cutoffTime.HasValue && entry.Timestamp < cutoffTime.Value)
                        break;

                    collected.Add(entry);
                    if (collected.Count >= maxEntries)
                        break;
                }

                if (collected.Count == 0)
                    return Array.Empty<LogEntry>();

                collected.Reverse();
                return collected.ToArray();
            }
        }

        public void Clear()
        {
            if (_isDisposed.Value)
                return;

            lock (_bufferMutex)
            {
                _buffer.Clear();
            }
        }

        public void Dispose()
        {
            if (!_isDisposed.TrySetTrue())
                return;

            lock (_bufferMutex)
            {
                _buffer.Clear();
            }

            GC.SuppressFinalize(this);
        }

        ~InMemoryLogStorage() => Dispose();
    }
}
