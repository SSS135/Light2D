#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using com.SSS135.EditorAutomation.Recorder;

namespace com.SSS135.EditorAutomation.Editor.QueryEngine
{
    /// <summary>
    /// Rebuilds an in-memory <see cref="RecorderSession"/> from an NDJSON session
    /// file. Constructs the session with a null writer (read-only) and replays
    /// each line through the same mutators the live recorder uses, so file and
    /// live sessions share one query surface. The line schema mirrors
    /// <see cref="NdjsonSessionWriter"/> exactly.
    /// </summary>
    public static class NdjsonSessionLoader
    {
        public static RecorderSession Load(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Recording file not found: {filePath}", filePath);

            var session = new RecorderSession(writer: null);
            session.SessionId = Path.GetFileNameWithoutExtension(filePath);

            foreach (var rawLine in File.ReadLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(rawLine))
                    continue;

                JsonNode? node;
                try
                {
                    node = JsonNode.Parse(rawLine);
                }
                catch (JsonException)
                {
                    // A crash/exit can leave the final line half-written; stop cleanly.
                    break;
                }

                if (node is not JsonObject obj)
                    continue;

                var k = Str(obj, "k");
                switch (k)
                {
                    case "header": ApplyHeader(session, obj); break;
                    case "obj": ApplyObject(session, obj); break;
                    case "var": ApplyVariable(session, obj); break;
                    case "val": ApplyValue(session, obj); break;
                    case "evt": ApplyEvent(session, obj); break;
                }
            }

            return session;
        }

        // --- line handlers ----------------------------------------------------

        static void ApplyHeader(RecorderSession session, JsonObject obj)
        {
            session.SessionId = Str(obj, "session") ?? session.SessionId;
            session.UnityVersion = Str(obj, "unity");
            session.StartUtc = Str(obj, "startUtc");
            session.FrameBase = Str(obj, "frameBase");
        }

        static void ApplyObject(RecorderSession session, JsonObject obj)
        {
            session.UpsertObject(new ObjectRecord
            {
                Key = Str(obj, "key") ?? "",
                Label = Str(obj, "label"),
                Path = Str(obj, "path"),
                ParentKey = Str(obj, "parent"),
                Type = Str(obj, "type"),
                FrameFirst = Int(obj, "f0") ?? 0,
                FrameDestroyed = Int(obj, "fd")
            });
        }

        static void ApplyVariable(RecorderSession session, JsonObject obj)
        {
            var tag = Str(obj, "tag") ?? "";
            var kind = ParseKind(Str(obj, "kind"));
            var eps = Float(obj, "eps");
            var frame = Int(obj, "f0") ?? 0;

            var manifest = session.UpsertVariable(tag, kind, eps, frame);
            var unit = Str(obj, "unit");
            if (unit != null)
                manifest.Unit = unit;
        }

        static void ApplyValue(RecorderSession session, JsonObject obj)
        {
            var frame = Int(obj, "f") ?? 0;
            var key = Str(obj, "key") ?? "";
            var tag = Str(obj, "tag") ?? "";
            var kind = ParseKind(Str(obj, "kind"));

            var value = ReadValue(obj, kind);
            session.TryAppendValue(key, tag, frame, in value);
        }

        static void ApplyEvent(RecorderSession session, JsonObject obj)
        {
            session.AppendEvent(new EventRecord
            {
                EventId = Int(obj, "id") ?? 0,
                Frame = Int(obj, "f") ?? 0,
                Key = Str(obj, "key"),
                Name = Str(obj, "name") ?? "",
                Severity = ParseSeverity(Str(obj, "sev")),
                Props = ReadProps(obj["props"] as JsonObject)
            });
        }

        // --- value reconstruction --------------------------------------------

        static RecordedValue ReadValue(JsonObject obj, ValueKind kind)
        {
            switch (kind)
            {
                case ValueKind.String:
                    return new RecordedValue { Kind = ValueKind.String, S = Str(obj, "s") };

                case ValueKind.Enum:
                    return new RecordedValue
                    {
                        Kind = ValueKind.Enum,
                        F0 = Double(obj, "v") ?? 0.0,
                        S = Str(obj, "s")
                    };

                case ValueKind.Vec2:
                case ValueKind.Vec3:
                case ValueKind.Vec4:
                case ValueKind.Quat:
                case ValueKind.Color:
                {
                    var arr = obj["v"] as JsonArray;
                    var value = new RecordedValue { Kind = kind };
                    if (arr != null)
                    {
                        if (arr.Count > 0) value.F0 = ToDouble(arr[0]);
                        if (arr.Count > 1) value.F1 = ToDouble(arr[1]);
                        if (arr.Count > 2) value.F2 = ToDouble(arr[2]);
                        if (arr.Count > 3) value.F3 = ToDouble(arr[3]);
                    }
                    return value;
                }

                // Float / Int / Bool — scalar in "v".
                default:
                    return new RecordedValue { Kind = kind, F0 = Double(obj, "v") ?? 0.0 };
            }
        }

        static Dictionary<string, object>? ReadProps(JsonObject? props)
        {
            if (props == null || props.Count == 0)
                return null;

            var dict = new Dictionary<string, object>(props.Count);
            foreach (var kv in props)
                dict[kv.Key] = NodeToClr(kv.Value);
            return dict;
        }

        // --- JSON accessors ---------------------------------------------------

        static string? Str(JsonObject obj, string name)
        {
            var v = obj[name];
            return v is JsonValue val && val.TryGetValue<string>(out var s) ? s : null;
        }

        static int? Int(JsonObject obj, string name)
        {
            var v = obj[name];
            return v is JsonValue val && val.TryGetValue<int>(out var i) ? i : (int?)null;
        }

        static float? Float(JsonObject obj, string name)
        {
            var d = Double(obj, name);
            return d.HasValue ? (float)d.Value : (float?)null;
        }

        static double? Double(JsonObject obj, string name)
        {
            var v = obj[name];
            return v is JsonValue val && val.TryGetValue<double>(out var d) ? d : (double?)null;
        }

        static double ToDouble(JsonNode? node)
            => node is JsonValue val && val.TryGetValue<double>(out var d) ? d : 0.0;

        static object NodeToClr(JsonNode? node)
        {
            if (node is JsonValue val)
            {
                if (val.TryGetValue<bool>(out var b)) return b;
                if (val.TryGetValue<long>(out var l)) return l;
                if (val.TryGetValue<double>(out var d)) return d;
                if (val.TryGetValue<string>(out var s)) return s;
            }
            return node?.ToJsonString() ?? "null";
        }

        static ValueKind ParseKind(string? s)
            => Enum.TryParse<ValueKind>(s, out var kind) ? kind : ValueKind.String;

        static Severity ParseSeverity(string? s)
            => Enum.TryParse<Severity>(s, out var sev) ? sev : Severity.Info;
    }
}
