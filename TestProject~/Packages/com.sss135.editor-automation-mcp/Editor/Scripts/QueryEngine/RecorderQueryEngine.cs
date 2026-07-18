#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using com.SSS135.EditorAutomation.Editor.API;
using com.SSS135.EditorAutomation.Recorder;

namespace com.SSS135.EditorAutomation.Editor.QueryEngine
{
    /// <summary>
    /// Summary-first query surface over a single <see cref="RecorderSession"/>
    /// (live or loaded). Every method returns a token-bounded DTO with a
    /// self-describing <see cref="EnvelopeMeta"/>; hard row/time caps are enforced
    /// by construction so the LLM consumer can never trigger a raw dump.
    /// </summary>
    public sealed class RecorderQueryEngine
    {
        /// <summary>Absolute upper bound on rows any single result may carry, regardless of request.</summary>
        public const int HardRowCap = 2000;

        /// <summary>Default series point budget when the caller does not specify one.</summary>
        public const int DefaultMaxPoints = 200;

        readonly RecorderSession _session;

        public RecorderQueryEngine(RecorderSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        // --- describe ---------------------------------------------------------

        public SessionManifest Describe()
        {
            var objects = _session.Objects.Values
                .Select(o => new ObjectInfo
                {
                    Key = o.Key,
                    Label = o.Label,
                    Path = o.Path,
                    ParentKey = o.ParentKey,
                    Type = o.Type,
                    FrameFirst = o.FrameFirst,
                    FrameDestroyed = o.FrameDestroyed
                })
                .ToArray();

            var variables = _session.Variables.Values
                .Select(v => new VariableInfo
                {
                    Tag = v.Tag,
                    Kind = v.ValueKind.ToString(),
                    Unit = v.Unit,
                    Epsilon = v.Epsilon,
                    FirstFrame = v.FirstFrame,
                    Mixed = v.Mixed
                })
                .ToArray();

            return new SessionManifest
            {
                SessionId = _session.SessionId ?? "",
                Label = _session.Label,
                UnityVersion = _session.UnityVersion,
                StartUtc = _session.StartUtc,
                Objects = objects,
                Variables = variables,
                EventCount = _session.Events.Count,
                Meta = BaseEnvelope(totalCount: objects.Length + variables.Length)
            };
        }

        // --- get-state --------------------------------------------------------

        public StateResult GetState(string key, IReadOnlyList<string>? tags, int frame)
        {
            var wanted = tags != null && tags.Count > 0
                ? tags
                : _session.SeriesKeys.Where(s => s.key == key).Select(s => s.tag).Distinct().ToList();

            var states = new List<TagState>();
            foreach (var tag in wanted)
            {
                var state = new TagState { Tag = tag, Kind = KindOf(tag) };
                if (_session.TryGetSeries(key, tag, out var series) && AsOf.ValueAt(series, frame, out var point))
                {
                    Fill(state, in point.V);
                    state.AtFrame = point.Frame;
                }
                else
                {
                    state.NoData = true;
                }
                states.Add(state);
            }

            return new StateResult
            {
                Key = key,
                Frame = frame,
                Tags = states.ToArray(),
                Meta = BaseEnvelope(totalCount: states.Count)
            };
        }

        // --- summarize-change -------------------------------------------------

        public ChangeSummary SummarizeChange(string key, string tag, int from, int to)
        {
            var result = new ChangeSummary { Key = key, Tag = tag, Kind = KindOf(tag), From = from, To = to };
            var meta = BaseEnvelope(totalCount: 0);
            meta.Reduction = "summary";
            meta.Units = UnitOf(tag);
            meta.ThresholdApplied = EpsilonOf(tag);

            if (!_session.TryGetSeries(key, tag, out var series) || series.Count == 0)
            {
                result.Meta = meta;
                return result;
            }

            var s = Reductions.Summarize(series, from, to);
            result.StartValue = s.Start;
            result.EndValue = s.End;
            result.NetDelta = s.NetDelta;
            result.Min = s.Min;
            result.Max = s.Max;
            result.Mean = s.Mean;
            result.ArgMaxFrame = s.ArgMaxFrame;
            result.ArgMinFrame = s.ArgMinFrame;
            result.ChangeCount = s.ChangeCount;
            meta.TotalCount = s.ChangeCount;
            meta.FrameRangeCovered = new[] { from, to };
            result.Meta = meta;
            return result;
        }

        // --- get-series -------------------------------------------------------

        public SeriesResult GetSeries(string key, string tag, int from, int to,
            string reduction, int maxPoints, string? cursor)
        {
            var kind = KindOf(tag);
            var meta = BaseEnvelope(totalCount: 0);
            meta.Reduction = reduction;
            meta.Units = UnitOf(tag);
            meta.ThresholdApplied = EpsilonOf(tag);
            meta.FrameRangeCovered = new[] { from, to };

            var result = new SeriesResult { Key = key, Tag = tag, Kind = kind, Meta = meta };

            if (!_session.TryGetSeries(key, tag, out var series) || series.Count == 0)
                return result;

            var cap = Math.Min(maxPoints <= 0 ? DefaultMaxPoints : maxPoints, HardRowCap);

            List<ChangePoint> points;
            int total, elided;
            int? nextCursorIndex = null;

            switch (reduction)
            {
                case "diff":
                    var cursorIndex = ParseCursor(cursor);
                    points = Reductions.Diff(series, from, to, cap, cursorIndex, out total, out elided, out nextCursorIndex);
                    break;
                case "downsample":
                    points = Reductions.Downsample(series, from, to, cap, out total, out elided);
                    break;
                default: // "summary" reduction over a series falls back to the full (capped) downsample shape.
                    points = Reductions.Downsample(series, from, to, cap, out total, out elided);
                    break;
            }

            result.Points = points.Select(ToSeriesPoint).ToArray();
            meta.TotalCount = total;
            meta.Elided = Math.Max(0, elided);
            meta.Truncated = elided > 0;
            meta.NextCursor = nextCursorIndex.HasValue ? nextCursorIndex.Value.ToString() : null;
            return result;
        }

        // --- find-events ------------------------------------------------------

        public EventsResult FindEvents(string? name, Severity? severityMin, string? key,
            int? from, int? to, int limit)
        {
            var cap = Math.Min(limit <= 0 ? 100 : limit, HardRowCap);

            var matches = _session.Events.Where(e =>
                (name == null || string.Equals(e.Name, name, StringComparison.Ordinal)) &&
                (!severityMin.HasValue || e.Severity >= severityMin.Value) &&
                (key == null || string.Equals(e.Key, key, StringComparison.Ordinal)) &&
                (!from.HasValue || e.Frame >= from.Value) &&
                (!to.HasValue || e.Frame <= to.Value));

            var ordered = matches.OrderBy(e => e.Frame).ToList();
            var total = ordered.Count;
            var page = ordered.Take(cap)
                .Select(e => new EventHit
                {
                    EventId = e.EventId,
                    Frame = e.Frame,
                    Key = e.Key,
                    Name = e.Name,
                    Severity = e.Severity.ToString(),
                    Props = e.Props
                })
                .ToArray();

            var meta = BaseEnvelope(totalCount: total);
            meta.Elided = Math.Max(0, total - page.Length);
            meta.Truncated = total > page.Length;
            if (from.HasValue && to.HasValue)
                meta.FrameRangeCovered = new[] { from.Value, to.Value };

            return new EventsResult { Events = page, Meta = meta };
        }

        // --- helpers ----------------------------------------------------------

        EnvelopeMeta BaseEnvelope(int totalCount) => new EnvelopeMeta
        {
            TimeDomain = _session.FrameBase,
            FrameRangeCovered = HasFrames
                ? new[] { _session.MinFrame, _session.MaxFrame }
                : null,
            TotalCount = totalCount
        };

        bool HasFrames => _session.MinFrame != int.MaxValue && _session.MaxFrame != int.MinValue;

        string KindOf(string tag)
            => _session.Variables.TryGetValue(tag, out var v) ? v.ValueKind.ToString() : "Float";

        string? UnitOf(string tag)
            => _session.Variables.TryGetValue(tag, out var v) ? v.Unit : null;

        float? EpsilonOf(string tag)
            => _session.Variables.TryGetValue(tag, out var v) ? v.Epsilon : null;

        static int ParseCursor(string? cursor)
            => int.TryParse(cursor, out var v) && v > 0 ? v : 0;

        static SeriesPoint ToSeriesPoint(ChangePoint cp)
        {
            var p = new SeriesPoint { Frame = cp.Frame };
            switch (cp.V.Kind)
            {
                case ValueKind.String:
                    p.Text = cp.V.S;
                    break;
                case ValueKind.Enum:
                    p.Value = cp.V.F0;
                    p.Text = cp.V.S;
                    break;
                case ValueKind.Vec2:
                    p.Vector = new[] { cp.V.F0, cp.V.F1 };
                    break;
                case ValueKind.Vec3:
                    p.Vector = new[] { cp.V.F0, cp.V.F1, cp.V.F2 };
                    break;
                case ValueKind.Vec4:
                case ValueKind.Quat:
                case ValueKind.Color:
                    p.Vector = new[] { cp.V.F0, cp.V.F1, cp.V.F2, cp.V.F3 };
                    break;
                default:
                    p.Value = cp.V.F0;
                    break;
            }
            return p;
        }

        static void Fill(TagState state, in RecordedValue v)
        {
            switch (v.Kind)
            {
                case ValueKind.String:
                    state.Text = v.S;
                    break;
                case ValueKind.Enum:
                    state.Value = v.F0;
                    state.Text = v.S;
                    break;
                case ValueKind.Vec2:
                    state.Vector = new[] { v.F0, v.F1 };
                    break;
                case ValueKind.Vec3:
                    state.Vector = new[] { v.F0, v.F1, v.F2 };
                    break;
                case ValueKind.Vec4:
                case ValueKind.Quat:
                case ValueKind.Color:
                    state.Vector = new[] { v.F0, v.F1, v.F2, v.F3 };
                    break;
                default:
                    state.Value = v.F0;
                    break;
            }
        }
    }
}
