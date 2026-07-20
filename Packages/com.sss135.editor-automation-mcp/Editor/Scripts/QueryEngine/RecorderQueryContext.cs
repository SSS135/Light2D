#nullable enable
using System;
using System.Collections.Generic;
using com.SSS135.EditorAutomation.Recorder;

namespace com.SSS135.EditorAutomation.Editor.QueryEngine
{
    /// <summary>
    /// Read-only view of a <see cref="RecorderSession"/> handed to a compiled
    /// <c>recorder-query</c> C# snippet as <c>q</c>. It WRAPS the session and
    /// exposes only the read surface plus a couple of query primitives — the
    /// session's mutators (<c>UpsertObject</c>/<c>UpsertVariable</c>/
    /// <c>TryAppendValue</c>/<c>AppendEvent</c>/<c>Dispose</c>) are deliberately
    /// not reachable, so a snippet can never append to or dispose the live
    /// recording. There is intentionally no passthrough to the underlying session.
    /// </summary>
    public sealed class RecorderQueryContext
    {
        static readonly IReadOnlyList<ChangePoint> EmptySeries = Array.Empty<ChangePoint>();

        readonly RecorderSession _session;

        public RecorderQueryContext(RecorderSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        /// <summary>Object catalog: key → identity/label/path/parent/type/lifespan.</summary>
        public IReadOnlyDictionary<string, ObjectRecord> Objects => _session.Objects;

        /// <summary>Per-tag manifest: tag → value kind / unit / epsilon / first frame.</summary>
        public IReadOnlyDictionary<string, VariableManifest> Variables => _session.Variables;

        /// <summary>Every recorded event, in insertion (≈ frame) order.</summary>
        public IReadOnlyList<EventRecord> Events => _session.Events;

        /// <summary>Every (object key, tag) pair that has a recorded series.</summary>
        public IEnumerable<(string key, string tag)> SeriesKeys => _session.SeriesKeys;

        /// <summary>
        /// The change-only series for <paramref name="key"/>/<paramref name="tag"/>,
        /// sorted by frame. Returns an empty list (never null) when there is none.
        /// </summary>
        public IReadOnlyList<ChangePoint> Series(string key, string tag)
            => _session.TryGetSeries(key, tag, out var points) ? points : EmptySeries;

        /// <summary>
        /// As-of value: the most recent change for <paramref name="key"/>/<paramref name="tag"/>
        /// at or before <paramref name="frame"/> (carried forward). Null when no
        /// value existed at or before the frame.
        /// </summary>
        public ChangePoint? ValueAt(string key, string tag, int frame)
        {
            if (_session.TryGetSeries(key, tag, out var points) && AsOf.ValueAt(points, frame, out var point))
                return point;
            return null;
        }

        /// <summary>Scalar magnitude of a value: abs for scalars, Euclidean norm for vectors, NaN for strings.</summary>
        public double Magnitude(in RecordedValue value) => Reductions.Magnitude(in value);

        public int MinFrame => _session.MinFrame;
        public int MaxFrame => _session.MaxFrame;
        public string SessionId => _session.SessionId;

        /// <summary>Frame clock base of the session ("render" or a custom sim clock).</summary>
        public string FrameBase => _session.FrameBase;
    }
}
