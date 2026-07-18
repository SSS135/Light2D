using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace com.SSS135.EditorAutomation.Recorder
{
    /// <summary>
    /// Hand-rolled NDJSON writer for a recording session. The dependency-free
    /// runtime assembly has no JSON library, so each line is built directly into
    /// a <see cref="StringBuilder"/> with invariant-culture numbers and escaped
    /// strings. Lines match the schema the editor loader parses.
    /// </summary>
    public sealed class NdjsonSessionWriter : IDisposable
    {
        readonly StreamWriter _stream;
        readonly StringBuilder _sb = new StringBuilder(256);
        bool _disposed;

        public NdjsonSessionWriter(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            _stream = new StreamWriter(filePath, append: false, encoding: new UTF8Encoding(false));
        }

        public void WriteHeader(string sessionId, int fmt, string unityVersion, string startUtcIso, string frameBase)
        {
            _sb.Clear();
            _sb.Append('{');
            AppendString("k", "header"); _sb.Append(',');
            AppendString("session", sessionId); _sb.Append(',');
            AppendRaw("fmt", fmt.ToString(CultureInfo.InvariantCulture)); _sb.Append(',');
            AppendString("unity", unityVersion); _sb.Append(',');
            AppendString("startUtc", startUtcIso); _sb.Append(',');
            AppendString("frameBase", frameBase);
            _sb.Append('}');
            WriteLine();
        }

        public void WriteObject(ObjectRecord o)
        {
            _sb.Clear();
            _sb.Append('{');
            AppendString("k", "obj"); _sb.Append(',');
            AppendString("key", o.Key); _sb.Append(',');
            AppendString("label", o.Label); _sb.Append(',');
            AppendString("path", o.Path); _sb.Append(',');
            AppendString("parent", o.ParentKey); _sb.Append(',');
            AppendString("type", o.Type); _sb.Append(',');
            AppendRaw("f0", o.FrameFirst.ToString(CultureInfo.InvariantCulture)); _sb.Append(',');
            AppendRaw("fd", o.FrameDestroyed.HasValue
                ? o.FrameDestroyed.Value.ToString(CultureInfo.InvariantCulture)
                : "null");
            _sb.Append('}');
            WriteLine();
        }

        public void WriteVariable(VariableManifest v)
        {
            _sb.Clear();
            _sb.Append('{');
            AppendString("k", "var"); _sb.Append(',');
            AppendString("tag", v.Tag); _sb.Append(',');
            AppendString("kind", v.ValueKind.ToString()); _sb.Append(',');
            AppendString("unit", v.Unit); _sb.Append(',');
            AppendRaw("eps", v.Epsilon.HasValue
                ? FormatNumber(v.Epsilon.Value)
                : "null"); _sb.Append(',');
            AppendRaw("f0", v.FirstFrame.ToString(CultureInfo.InvariantCulture));
            _sb.Append('}');
            WriteLine();
        }

        public void WriteValue(int frame, string key, string tag, in RecordedValue value)
        {
            _sb.Clear();
            _sb.Append('{');
            AppendString("k", "val"); _sb.Append(',');
            AppendRaw("f", frame.ToString(CultureInfo.InvariantCulture)); _sb.Append(',');
            AppendString("key", key); _sb.Append(',');
            AppendString("tag", tag); _sb.Append(',');
            AppendString("kind", value.Kind.ToString()); _sb.Append(',');
            AppendValuePayload(value);
            _sb.Append('}');
            WriteLine();
        }

        public void WriteEvent(EventRecord e)
        {
            _sb.Clear();
            _sb.Append('{');
            AppendString("k", "evt"); _sb.Append(',');
            AppendRaw("id", e.EventId.ToString(CultureInfo.InvariantCulture)); _sb.Append(',');
            AppendRaw("f", e.Frame.ToString(CultureInfo.InvariantCulture)); _sb.Append(',');
            AppendString("key", e.Key); _sb.Append(',');
            AppendString("name", e.Name); _sb.Append(',');
            AppendString("sev", e.Severity.ToString()); _sb.Append(',');
            _sb.Append("\"props\":");
            AppendPropsObject(e.Props);
            _sb.Append('}');
            WriteLine();
        }

        public void Flush() => _stream.Flush();

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _stream.Flush();
            _stream.Dispose();
        }

        // --- payload helpers --------------------------------------------------

        void AppendValuePayload(in RecordedValue value)
        {
            switch (value.Kind)
            {
                case ValueKind.String:
                    AppendString("s", value.S);
                    break;

                case ValueKind.Enum:
                    // Enum stores the integer in F0 and the member name in S.
                    AppendRaw("v", FormatNumber(value.F0)); _sb.Append(',');
                    AppendString("s", value.S);
                    break;

                case ValueKind.Float:
                case ValueKind.Int:
                case ValueKind.Bool:
                    AppendRaw("v", FormatNumber(value.F0));
                    break;

                case ValueKind.Vec2:
                    AppendRaw("v", "[" + FormatNumber(value.F0) + "," + FormatNumber(value.F1) + "]");
                    break;

                case ValueKind.Vec3:
                    AppendRaw("v", "[" + FormatNumber(value.F0) + "," + FormatNumber(value.F1) + "," + FormatNumber(value.F2) + "]");
                    break;

                case ValueKind.Vec4:
                case ValueKind.Quat:
                case ValueKind.Color:
                    AppendRaw("v", "[" + FormatNumber(value.F0) + "," + FormatNumber(value.F1) + "," + FormatNumber(value.F2) + "," + FormatNumber(value.F3) + "]");
                    break;
            }
        }

        void AppendPropsObject(Dictionary<string, object> props)
        {
            if (props == null || props.Count == 0)
            {
                _sb.Append("{}");
                return;
            }

            _sb.Append('{');
            var first = true;
            foreach (var kv in props)
            {
                if (!first)
                    _sb.Append(',');
                first = false;
                AppendEscapedString(kv.Key);
                _sb.Append(':');
                AppendPropValue(kv.Value);
            }
            _sb.Append('}');
        }

        void AppendPropValue(object value)
        {
            switch (value)
            {
                case null:
                    _sb.Append("null");
                    break;
                case bool b:
                    _sb.Append(b ? "true" : "false");
                    break;
                case float f:
                    _sb.Append(FormatNumber(f));
                    break;
                case double d:
                    _sb.Append(FormatNumber(d));
                    break;
                case int i:
                    _sb.Append(i.ToString(CultureInfo.InvariantCulture));
                    break;
                case long l:
                    _sb.Append(l.ToString(CultureInfo.InvariantCulture));
                    break;
                case string s:
                    AppendEscapedString(s);
                    break;
                default:
                    AppendEscapedString(value.ToString());
                    break;
            }
        }

        // --- low-level JSON helpers ------------------------------------------

        void AppendString(string name, string value)
        {
            AppendEscapedString(name);
            _sb.Append(':');
            if (value == null)
                _sb.Append("null");
            else
                AppendEscapedString(value);
        }

        void AppendRaw(string name, string rawJson)
        {
            AppendEscapedString(name);
            _sb.Append(':');
            _sb.Append(rawJson);
        }

        void AppendEscapedString(string s)
        {
            _sb.Append('"');
            foreach (var ch in s)
            {
                switch (ch)
                {
                    case '"': _sb.Append("\\\""); break;
                    case '\\': _sb.Append("\\\\"); break;
                    case '\b': _sb.Append("\\b"); break;
                    case '\f': _sb.Append("\\f"); break;
                    case '\n': _sb.Append("\\n"); break;
                    case '\r': _sb.Append("\\r"); break;
                    case '\t': _sb.Append("\\t"); break;
                    default:
                        if (ch < 0x20)
                            _sb.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            _sb.Append(ch);
                        break;
                }
            }
            _sb.Append('"');
        }

        static string FormatNumber(double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d))
                return "null";
            // "R" round-trips; invariant culture keeps '.' as the decimal point.
            return d.ToString("R", CultureInfo.InvariantCulture);
        }

        void WriteLine()
        {
            _stream.Write(_sb.ToString());
            _stream.Write('\n');
            _stream.Flush();
        }
    }
}
