#nullable enable
using System.Collections.Generic;
using System.ComponentModel;

namespace com.SSS135.EditorAutomation.Editor.API
{
    /// <summary>
    /// Self-describing envelope attached to every recorder query result. The
    /// consumer is a token-bounded LLM: these fields let it understand units,
    /// time domain, and whether the result was clipped without dumping raw data.
    /// </summary>
    public class EnvelopeMeta
    {
        [Description("Physical unit of the values, when known (e.g. \"m\", \"hp\"). Null when undeclared.")]
        public string? Units { get; set; }

        [Description("Frame clock used by the session: \"render\" = Unity render frames, or a custom sim clock.")]
        public string? TimeDomain { get; set; }

        [Description("[minFrame, maxFrame] actually covered by the returned data.")]
        public int[]? FrameRangeCovered { get; set; }

        [Description("Reduction applied to a series result: \"summary\" | \"diff\" | \"downsample\". Null for non-series results.")]
        public string? Reduction { get; set; }

        [Description("Change threshold (epsilon) in effect for the queried tag, when applicable.")]
        public float? ThresholdApplied { get; set; }

        [Description("Number of rows omitted because a cap was hit. 0 when nothing was dropped.")]
        public int Elided { get; set; }

        [Description("True when the result was clipped by a hard row/time cap.")]
        public bool Truncated { get; set; }

        [Description("Total rows available before any cap was applied.")]
        public int TotalCount { get; set; }

        [Description("Opaque cursor for the next page, or null when the result is complete.")]
        public string? NextCursor { get; set; }

        [Description("Schema version of the query result envelope.")]
        public int SchemaVersion { get; set; } = 1;
    }

    /// <summary>One catalog entry in a session manifest.</summary>
    public class ObjectInfo
    {
        public string Key { get; set; } = "";
        public string? Label { get; set; }
        public string? Path { get; set; }
        public string? ParentKey { get; set; }
        public string? Type { get; set; }
        public int FrameFirst { get; set; }
        public int? FrameDestroyed { get; set; }
    }

    /// <summary>One per-tag manifest entry in a session manifest.</summary>
    public class VariableInfo
    {
        public string Tag { get; set; } = "";
        public string Kind { get; set; } = "";
        public string? Unit { get; set; }
        public float? Epsilon { get; set; }
        public int FirstFrame { get; set; }

        [Description("True when the tag's value kind drifted across rows.")]
        public bool Mixed { get; set; }
    }

    /// <summary>Lightweight listing entry for one discoverable session.</summary>
    public class SessionInfo
    {
        public string Id { get; set; } = "";

        [Description("Absolute file path, or \"live\" for the running in-memory session.")]
        public string Path { get; set; } = "";
        public string? Label { get; set; }
        public int MinFrame { get; set; }
        public int MaxFrame { get; set; }

        [Description("True when this is the currently recording in-memory session.")]
        public bool IsLive { get; set; }
    }

    /// <summary>Result of <c>recorder-list-sessions</c>.</summary>
    public class SessionListResult
    {
        public SessionInfo[] Sessions { get; set; } = System.Array.Empty<SessionInfo>();
        public EnvelopeMeta Meta { get; set; } = new EnvelopeMeta();
    }

    /// <summary>Result of <c>recorder-describe-session</c>: the mandatory first call.</summary>
    public class SessionManifest
    {
        public string SessionId { get; set; } = "";
        public string? Label { get; set; }
        public string? UnityVersion { get; set; }
        public string? StartUtc { get; set; }
        public ObjectInfo[] Objects { get; set; } = System.Array.Empty<ObjectInfo>();
        public VariableInfo[] Variables { get; set; } = System.Array.Empty<VariableInfo>();
        public int EventCount { get; set; }
        public EnvelopeMeta Meta { get; set; } = new EnvelopeMeta();
    }

    /// <summary>A single tag's as-of value at a queried frame.</summary>
    public class TagState
    {
        public string Tag { get; set; } = "";
        public string Kind { get; set; } = "";

        [Description("Scalar payload (float/int/bool/enum-ordinal). Null for vector/string kinds.")]
        public double? Value { get; set; }

        [Description("Vector payload for Vec2/Vec3/Vec4/Quat/Color kinds. Null for scalars/strings.")]
        public double[]? Vector { get; set; }

        [Description("String/enum-name payload. Null for numeric/vector kinds.")]
        public string? Text { get; set; }

        [Description("Frame the returned value was last set at (≤ queried frame).")]
        public int? AtFrame { get; set; }

        [Description("True when no value existed at or before the queried frame.")]
        public bool NoData { get; set; }
    }

    /// <summary>Result of <c>recorder-get-state</c>: as-of values for one object.</summary>
    public class StateResult
    {
        public string Key { get; set; } = "";
        public int Frame { get; set; }
        public TagState[] Tags { get; set; } = System.Array.Empty<TagState>();
        public EnvelopeMeta Meta { get; set; } = new EnvelopeMeta();
    }

    /// <summary>Result of <c>recorder-summarize-change</c>: the workhorse delta summary.</summary>
    public class ChangeSummary
    {
        public string Key { get; set; } = "";
        public string Tag { get; set; } = "";
        public string Kind { get; set; } = "";
        public int From { get; set; }
        public int To { get; set; }

        [Description("As-of value at the window start (scalar magnitude for vectors).")]
        public double? StartValue { get; set; }

        [Description("As-of value at the window end (scalar magnitude for vectors).")]
        public double? EndValue { get; set; }

        [Description("EndValue - StartValue (or magnitude delta for vector kinds).")]
        public double? NetDelta { get; set; }
        public double? Min { get; set; }
        public double? Max { get; set; }
        public double? Mean { get; set; }

        [Description("Frame at which the maximum occurred — a drill-down handle.")]
        public int? ArgMaxFrame { get; set; }

        [Description("Frame at which the minimum occurred — a drill-down handle.")]
        public int? ArgMinFrame { get; set; }

        [Description("Number of change points inside the window.")]
        public int ChangeCount { get; set; }
        public EnvelopeMeta Meta { get; set; } = new EnvelopeMeta();
    }

    /// <summary>One point in a returned series.</summary>
    public class SeriesPoint
    {
        public int Frame { get; set; }
        public double? Value { get; set; }
        public double[]? Vector { get; set; }
        public string? Text { get; set; }
    }

    /// <summary>Result of <c>recorder-get-series</c>.</summary>
    public class SeriesResult
    {
        public string Key { get; set; } = "";
        public string Tag { get; set; } = "";
        public string Kind { get; set; } = "";
        public SeriesPoint[] Points { get; set; } = System.Array.Empty<SeriesPoint>();
        public EnvelopeMeta Meta { get; set; } = new EnvelopeMeta();
    }

    /// <summary>One matched event from <c>recorder-find-events</c>.</summary>
    public class EventHit
    {
        public int EventId { get; set; }
        public int Frame { get; set; }
        public string? Key { get; set; }
        public string Name { get; set; } = "";
        public string Severity { get; set; } = "";
        public Dictionary<string, object>? Props { get; set; }
    }

    /// <summary>Result of <c>recorder-find-events</c>: anchor frames for "the bad moment".</summary>
    public class EventsResult
    {
        public EventHit[] Events { get; set; } = System.Array.Empty<EventHit>();
        public EnvelopeMeta Meta { get; set; } = new EnvelopeMeta();
    }
}
