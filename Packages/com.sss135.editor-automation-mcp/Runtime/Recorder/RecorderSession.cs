using System;
using System.Collections.Generic;

namespace com.SSS135.EditorAutomation.Recorder
{
    /// <summary>
    /// In-memory store for one recording session. The single shape used by both
    /// the live recorder and the editor file loader: a live session is
    /// constructed with a <see cref="NdjsonSessionWriter"/> (every mutator also
    /// emits an NDJSON line); a loaded session is constructed with a null writer
    /// and rebuilt purely in memory through the same mutators.
    /// </summary>
    public sealed class RecorderSession : IDisposable
    {
        readonly NdjsonSessionWriter _writer;

        readonly Dictionary<string, ObjectRecord> _objects = new Dictionary<string, ObjectRecord>();
        readonly Dictionary<string, VariableManifest> _variables = new Dictionary<string, VariableManifest>();
        readonly Dictionary<(string, string), List<ChangePoint>> _series = new Dictionary<(string, string), List<ChangePoint>>();
        readonly List<EventRecord> _events = new List<EventRecord>();

        public string SessionId;
        public string Label;
        public string UnityVersion;
        public string StartUtc;
        public string FrameBase;
        public int MinFrame = int.MaxValue;
        public int MaxFrame = int.MinValue;

        /// <param name="writer">
        /// NDJSON sink for a live session; null for a read-only loaded session.
        /// </param>
        public RecorderSession(NdjsonSessionWriter writer = null)
        {
            _writer = writer;
        }

        // --- Read access ------------------------------------------------------

        public IReadOnlyDictionary<string, ObjectRecord> Objects => _objects;
        public IReadOnlyDictionary<string, VariableManifest> Variables => _variables;
        public IReadOnlyList<EventRecord> Events => _events;
        public IEnumerable<(string key, string tag)> SeriesKeys => _series.Keys;

        public bool TryGetSeries(string key, string tag, out IReadOnlyList<ChangePoint> points)
        {
            if (_series.TryGetValue((key, tag), out var list))
            {
                points = list;
                return true;
            }
            points = null;
            return false;
        }

        // --- Mutators (shared by live recorder + file loader) -----------------

        /// <summary>
        /// Insert the object catalog entry on first sight; writes the <c>obj</c>
        /// line only when a writer is attached and the key is new.
        /// </summary>
        public void UpsertObject(ObjectRecord record)
        {
            if (_objects.ContainsKey(record.Key))
                return;

            _objects[record.Key] = record;
            _writer?.WriteObject(record);
        }

        /// <summary>
        /// Insert (or fetch) the per-tag manifest entry. A later kind that
        /// disagrees with the recorded one marks the manifest <c>Mixed</c>. The
        /// <c>var</c> line is written once, on first sight, when a writer is
        /// attached.
        /// </summary>
        public VariableManifest UpsertVariable(string tag, ValueKind kind, float? epsilon, int frame)
        {
            if (_variables.TryGetValue(tag, out var existing))
            {
                if (existing.ValueKind != kind)
                    existing.Mixed = true;
                if (epsilon.HasValue)
                    existing.Epsilon = epsilon;
                return existing;
            }

            var manifest = new VariableManifest
            {
                Tag = tag,
                ValueKind = kind,
                Epsilon = epsilon,
                FirstFrame = frame
            };
            _variables[tag] = manifest;
            _writer?.WriteVariable(manifest);
            return manifest;
        }

        /// <summary>
        /// Append a change point for <paramref name="key"/>/<paramref name="tag"/>
        /// when it significantly differs from the last point (the first value per
        /// series always emits). Writes the <c>val</c> line on emit and updates
        /// the frame range. Returns whether a point was emitted.
        /// </summary>
        public bool TryAppendValue(string key, string tag, int frame, in RecordedValue value)
        {
            if (!_series.TryGetValue((key, tag), out var list))
            {
                list = new List<ChangePoint>();
                _series[(key, tag)] = list;
            }

            if (list.Count > 0)
            {
                var epsilon = _variables.TryGetValue(tag, out var manifest) && manifest.Epsilon.HasValue
                    ? manifest.Epsilon.Value
                    : 0f;
                var previous = list[list.Count - 1].V;
                if (!value.SignificantlyDiffers(in previous, epsilon))
                    return false;
            }

            list.Add(new ChangePoint { Frame = frame, V = value });
            _writer?.WriteValue(frame, key, tag, in value);
            TrackFrame(frame);
            return true;
        }

        /// <summary>
        /// Append a free-form event; writes the <c>evt</c> line when a writer is
        /// attached and tracks the frame range.
        /// </summary>
        public void AppendEvent(EventRecord record)
        {
            _events.Add(record);
            _writer?.WriteEvent(record);
            TrackFrame(record.Frame);
        }

        void TrackFrame(int frame)
        {
            if (frame < MinFrame) MinFrame = frame;
            if (frame > MaxFrame) MaxFrame = frame;
        }

        /// <summary>Flush and close the underlying writer; no-op for a loaded (null-writer) session.</summary>
        public void Dispose() => _writer?.Dispose();
    }
}
