using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace com.SSS135.EditorAutomation.Recorder
{
    /// <summary>
    /// Public, engine-agnostic entry point for the gameplay recorder. Devs call
    /// <see cref="LogVariable(UnityEngine.Object, string, object)"/> /
    /// <see cref="LogEvent(UnityEngine.Object, string, Dictionary{string, object}, Severity)"/>
    /// during play; the active session records significant changes to an NDJSON
    /// file and an in-memory buffer. Every call is a cheap no-op while no session
    /// is active (recording disabled, or outside play mode).
    /// </summary>
    public static class Recorder
    {
        /// <summary>NDJSON line-format version emitted in the header.</summary>
        const int Fmt = 1;

        /// <summary>Directory (relative to project root) holding session files.</summary>
        const string RecordingsDir = "Temp/EditorAutomationRecordings";

        /// <summary>Newest-N session files kept on disk; older ones are pruned on <see cref="BeginSession"/>.</summary>
        const int RetentionCap = 20;

        /// <summary>
        /// PlayerPrefs key gating recording. Shared editor↔runtime (the Wave 4
        /// Project Settings toggle writes the same key), and runtime-readable
        /// because the runtime assembly cannot reach editor-only settings.
        /// </summary>
        public const string EnabledPrefKey = "EditorAutomation.Recorder.Enabled";

        static int? _frameOverride;
        static int _nextEventId;

        /// <summary>The session for the current play run, or null when not recording.</summary>
        public static RecorderSession ActiveSession { get; private set; }

        /// <summary>
        /// Master on/off switch (default ON). When off, <see cref="BeginSession"/>
        /// no-ops so the active session stays null and all logging costs nothing.
        /// </summary>
        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(EnabledPrefKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(EnabledPrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        // --- Lifecycle --------------------------------------------------------

        /// <summary>
        /// Open a new recording session for this play run. No-ops when recording
        /// is disabled or a session is already open. Creates the session file,
        /// writes the header, and prunes old files past the retention cap.
        /// </summary>
        public static void BeginSession(string label = null)
        {
            if (!Enabled || ActiveSession != null)
                return;

            _frameOverride = null;
            _nextEventId = 0;

            var startUtc = DateTime.UtcNow;
            var sessionId = Guid.NewGuid().ToString("N");
            var fileName = "session-" + startUtc.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
                + "-" + sessionId + ".ndjson";
            var filePath = Path.Combine(RecordingsDir, fileName);

            Directory.CreateDirectory(RecordingsDir);
            EnforceRetentionCap();

            var unityVersion = Application.unityVersion;
            var startUtcIso = startUtc.ToString("o", CultureInfo.InvariantCulture);

            var writer = new NdjsonSessionWriter(filePath);
            writer.WriteHeader(sessionId, Fmt, unityVersion, startUtcIso, "render");

            ActiveSession = new RecorderSession(writer)
            {
                SessionId = sessionId,
                Label = label,
                UnityVersion = unityVersion,
                StartUtc = startUtcIso,
                FrameBase = "render"
            };
        }

        /// <summary>Flush, close, and clear the active session.</summary>
        public static void EndSession()
        {
            var session = ActiveSession;
            if (session == null)
                return;

            ActiveSession = null;
            session.Dispose();
        }

        /// <summary>
        /// Override the frame stamp for subsequent logs. Sticky for the session:
        /// once set, every log uses this frame until called again. Pass a new
        /// value each tick to drive a custom (e.g. simulation) frame clock.
        /// </summary>
        public static void SetFrame(int frame) => _frameOverride = frame;

        static int CurrentFrame => _frameOverride ?? Time.frameCount;

        // --- Variable logging -------------------------------------------------

        public static void LogVariable(UnityEngine.Object obj, string tag, float value) => Append(obj, tag, RecordedValue.From(value), ValueKind.Float);
        public static void LogVariable(UnityEngine.Object obj, string tag, double value) => Append(obj, tag, RecordedValue.From(value), ValueKind.Float);
        public static void LogVariable(UnityEngine.Object obj, string tag, int value) => Append(obj, tag, RecordedValue.From(value), ValueKind.Int);
        public static void LogVariable(UnityEngine.Object obj, string tag, long value) => Append(obj, tag, RecordedValue.From(value), ValueKind.Int);
        public static void LogVariable(UnityEngine.Object obj, string tag, bool value) => Append(obj, tag, RecordedValue.From(value), ValueKind.Bool);
        public static void LogVariable(UnityEngine.Object obj, string tag, Vector2 value) => Append(obj, tag, RecordedValue.From(value), ValueKind.Vec2);
        public static void LogVariable(UnityEngine.Object obj, string tag, Vector3 value) => Append(obj, tag, RecordedValue.From(value), ValueKind.Vec3);
        public static void LogVariable(UnityEngine.Object obj, string tag, Vector4 value) => Append(obj, tag, RecordedValue.From(value), ValueKind.Vec4);
        public static void LogVariable(UnityEngine.Object obj, string tag, Quaternion value) => Append(obj, tag, RecordedValue.From(value), ValueKind.Quat);
        public static void LogVariable(UnityEngine.Object obj, string tag, Color value) => Append(obj, tag, RecordedValue.From(value), ValueKind.Color);
        public static void LogVariable(UnityEngine.Object obj, string tag, Enum value) => Append(obj, tag, RecordedValue.From(value), ValueKind.Enum);
        public static void LogVariable(UnityEngine.Object obj, string tag, string value) => Append(obj, tag, RecordedValue.From(value), ValueKind.String);

        public static void LogVariable(string key, string tag, float value) => Append(key, tag, RecordedValue.From(value), ValueKind.Float);
        public static void LogVariable(string key, string tag, double value) => Append(key, tag, RecordedValue.From(value), ValueKind.Float);
        public static void LogVariable(string key, string tag, int value) => Append(key, tag, RecordedValue.From(value), ValueKind.Int);
        public static void LogVariable(string key, string tag, long value) => Append(key, tag, RecordedValue.From(value), ValueKind.Int);
        public static void LogVariable(string key, string tag, bool value) => Append(key, tag, RecordedValue.From(value), ValueKind.Bool);
        public static void LogVariable(string key, string tag, Vector2 value) => Append(key, tag, RecordedValue.From(value), ValueKind.Vec2);
        public static void LogVariable(string key, string tag, Vector3 value) => Append(key, tag, RecordedValue.From(value), ValueKind.Vec3);
        public static void LogVariable(string key, string tag, Vector4 value) => Append(key, tag, RecordedValue.From(value), ValueKind.Vec4);
        public static void LogVariable(string key, string tag, Quaternion value) => Append(key, tag, RecordedValue.From(value), ValueKind.Quat);
        public static void LogVariable(string key, string tag, Color value) => Append(key, tag, RecordedValue.From(value), ValueKind.Color);
        public static void LogVariable(string key, string tag, Enum value) => Append(key, tag, RecordedValue.From(value), ValueKind.Enum);
        public static void LogVariable(string key, string tag, string value) => Append(key, tag, RecordedValue.From(value), ValueKind.String);

        /// <summary>
        /// Generic fallback for types without a typed overload: the boxed value is
        /// classified by <see cref="RecordedValue.From(object, out ValueKind)"/>
        /// (unknown types degrade to their <c>ToString()</c>).
        /// </summary>
        public static void LogVariable(UnityEngine.Object obj, string tag, object value)
        {
            if (ActiveSession == null) return;
            var recorded = RecordedValue.From(value, out var kind);
            Append(obj, tag, recorded, kind);
        }

        public static void LogVariable(string key, string tag, object value)
        {
            if (ActiveSession == null) return;
            var recorded = RecordedValue.From(value, out var kind);
            Append(key, tag, recorded, kind);
        }

        /// <summary>
        /// Pre-seed or update a tag's manifest (unit/epsilon) before any value is
        /// logged. Optional sugar — auto-declaration happens on first log too.
        /// </summary>
        public static void DeclareVariable(string tag, string unit = null, float? epsilon = null)
        {
            var session = ActiveSession;
            if (session == null) return;

            var manifest = session.UpsertVariable(tag, ValueKind.Float, epsilon, CurrentFrame);
            if (unit != null)
                manifest.Unit = unit;
        }

        // --- Event logging ----------------------------------------------------

        public static void LogEvent(UnityEngine.Object obj, string name, Dictionary<string, object> props = null, Severity severity = Severity.Info)
        {
            var session = ActiveSession;
            if (session == null) return;

            var key = obj != null ? ResolveUnityKey(session, obj) : null;
            EmitEvent(session, key, name, props, severity);
        }

        public static void LogEvent(string key, string name, Dictionary<string, object> props = null, Severity severity = Severity.Info)
        {
            var session = ActiveSession;
            if (session == null) return;
            EmitEvent(session, key, name, props, severity);
        }

        /// <summary>Global (object-less) event.</summary>
        public static void LogEvent(string name, Dictionary<string, object> props = null, Severity severity = Severity.Info)
        {
            var session = ActiveSession;
            if (session == null) return;
            EmitEvent(session, null, name, props, severity);
        }

        // --- internals --------------------------------------------------------

        static void Append(UnityEngine.Object obj, string tag, RecordedValue value, ValueKind kind)
        {
            var session = ActiveSession;
            if (session == null) return;

            var key = obj != null ? ResolveUnityKey(session, obj) : Identity.KeyFor(null);
            EmitValue(session, key, tag, value, kind);
        }

        static void Append(string key, string tag, RecordedValue value, ValueKind kind)
        {
            var session = ActiveSession;
            if (session == null) return;
            EmitValue(session, key, tag, value, kind);
        }

        static void EmitValue(RecorderSession session, string key, string tag, RecordedValue value, ValueKind kind)
        {
            var frame = CurrentFrame;
            session.UpsertVariable(tag, kind, DefaultEpsilon(kind), frame);
            session.TryAppendValue(key, tag, frame, in value);
        }

        static void EmitEvent(RecorderSession session, string key, string name, Dictionary<string, object> props, Severity severity)
        {
            session.AppendEvent(new EventRecord
            {
                EventId = _nextEventId++,
                Frame = CurrentFrame,
                Key = key,
                Name = name,
                Severity = severity,
                Props = props
            });
        }

        /// <summary>
        /// Register a catalog entry under a CALLER-supplied key. The string-key
        /// logging overloads never populate the <see cref="RecorderSession.Objects"/>
        /// catalog, so callers that key on a synthetic per-life identity must call
        /// this once to make the unit appear in describe-session. No-op when no
        /// session is active, the key is empty, or the key is already cataloged.
        /// <paramref name="labelSource"/> is an optional Unity object used only for
        /// its label / hierarchy path / type — the key stays the caller's string.
        /// </summary>
        public static void RegisterObject(string key, UnityEngine.Object labelSource = null)
        {
            var session = ActiveSession;
            if (session == null || string.IsNullOrEmpty(key) || session.Objects.ContainsKey(key)) return;
            string label = null, path = null, parentKey = null, type = null;
            if (labelSource != null)
            {
                (label, path, parentKey) = Identity.DescribeUnityObject(labelSource);
                type = labelSource.GetType().Name;
            }
            session.UpsertObject(new ObjectRecord
            {
                Key = key,
                Label = label,
                Path = path,
                ParentKey = parentKey,
                Type = type,
                FrameFirst = CurrentFrame
            });
        }

        /// <summary>
        /// Resolve a Unity object's key and register its catalog entry on first
        /// sight (label / hierarchy path / parent key).
        /// </summary>
        static string ResolveUnityKey(RecorderSession session, UnityEngine.Object obj)
        {
            var key = Identity.KeyFor(obj);
            if (!session.Objects.ContainsKey(key))
            {
                var (label, path, parentKey) = Identity.DescribeUnityObject(obj);
                session.UpsertObject(new ObjectRecord
                {
                    Key = key,
                    Label = label,
                    Path = path,
                    ParentKey = parentKey,
                    Type = obj.GetType().Name,
                    FrameFirst = CurrentFrame
                });
            }
            return key;
        }

        /// <summary>
        /// Per-kind default change threshold: floats/vectors use a small epsilon
        /// to kill noise; int/bool/enum/string compare exactly (null epsilon → 0).
        /// </summary>
        static float? DefaultEpsilon(ValueKind kind)
        {
            switch (kind)
            {
                case ValueKind.Float:
                case ValueKind.Vec2:
                case ValueKind.Vec3:
                case ValueKind.Vec4:
                case ValueKind.Quat:
                case ValueKind.Color:
                    return 0.01f;
                default:
                    return null;
            }
        }

        /// <summary>Delete the oldest session files once the directory exceeds the retention cap.</summary>
        static void EnforceRetentionCap()
        {
            if (!Directory.Exists(RecordingsDir))
                return;

            try
            {
                var files = Directory.GetFiles(RecordingsDir, "session-*.ndjson");
                if (files.Length < RetentionCap)
                    return;

                var stale = files
                    .OrderByDescending(p => File.GetCreationTimeUtc(p))
                    .Skip(RetentionCap - 1);
                foreach (var path in stale)
                {
                    try { File.Delete(path); }
                    catch { /* best-effort cleanup; a locked file is harmless */ }
                }
            }
            catch
            {
                // Retention is housekeeping only; failures must not block recording.
            }
        }
    }
}
