using System;
using UnityEngine;

namespace com.SSS135.EditorAutomation.Recorder
{
    /// <summary>
    /// Tagged variant for a single recorded sample. Up to four <c>double</c>
    /// components cover every numeric/vector kind; <see cref="S"/> carries
    /// string payloads (and the enum member name).
    /// </summary>
    public struct RecordedValue
    {
        public ValueKind Kind;
        public double F0;
        public double F1;
        public double F2;
        public double F3;
        public string S;

        // --- Typed encoders ---------------------------------------------------

        public static RecordedValue From(float value)
            => new RecordedValue { Kind = ValueKind.Float, F0 = value };

        public static RecordedValue From(double value)
            => new RecordedValue { Kind = ValueKind.Float, F0 = value };

        public static RecordedValue From(int value)
            => new RecordedValue { Kind = ValueKind.Int, F0 = value };

        public static RecordedValue From(long value)
            => new RecordedValue { Kind = ValueKind.Int, F0 = value };

        public static RecordedValue From(bool value)
            => new RecordedValue { Kind = ValueKind.Bool, F0 = value ? 1.0 : 0.0 };

        public static RecordedValue From(Vector2 value)
            => new RecordedValue { Kind = ValueKind.Vec2, F0 = value.x, F1 = value.y };

        public static RecordedValue From(Vector3 value)
            => new RecordedValue { Kind = ValueKind.Vec3, F0 = value.x, F1 = value.y, F2 = value.z };

        public static RecordedValue From(Vector4 value)
            => new RecordedValue { Kind = ValueKind.Vec4, F0 = value.x, F1 = value.y, F2 = value.z, F3 = value.w };

        public static RecordedValue From(Quaternion value)
            => new RecordedValue { Kind = ValueKind.Quat, F0 = value.x, F1 = value.y, F2 = value.z, F3 = value.w };

        public static RecordedValue From(Color value)
            => new RecordedValue { Kind = ValueKind.Color, F0 = value.r, F1 = value.g, F2 = value.b, F3 = value.a };

        public static RecordedValue From(Enum value)
            => new RecordedValue { Kind = ValueKind.Enum, F0 = Convert.ToInt64(value), S = value.ToString() };

        public static RecordedValue From(string value)
            => new RecordedValue { Kind = ValueKind.String, S = value };

        /// <summary>
        /// Boxed-object encoder used by the generic <c>LogVariable</c> fallback.
        /// Resolves the concrete kind; unknown types degrade to their
        /// <c>ToString()</c> as a <see cref="ValueKind.String"/>.
        /// </summary>
        public static RecordedValue From(object value, out ValueKind kind)
        {
            RecordedValue result;
            switch (value)
            {
                case null: result = From(string.Empty); break;
                case float f: result = From(f); break;
                case double d: result = From(d); break;
                case int i: result = From(i); break;
                case long l: result = From(l); break;
                case bool b: result = From(b); break;
                case Vector2 v2: result = From(v2); break;
                case Vector3 v3: result = From(v3); break;
                case Vector4 v4: result = From(v4); break;
                case Quaternion q: result = From(q); break;
                case Color c: result = From(c); break;
                case Enum e: result = From(e); break;
                case string s: result = From(s); break;
                default: result = From(value.ToString()); break;
            }
            kind = result.Kind;
            return result;
        }

        // --- Significance test -----------------------------------------------

        /// <summary>
        /// True when this value differs from <paramref name="other"/> by more than
        /// <paramref name="epsilon"/>. Vector/quat kinds compare Euclidean
        /// magnitude of the component delta; scalars compare absolute delta;
        /// string/enum compare for any difference.
        /// </summary>
        public bool SignificantlyDiffers(in RecordedValue other, float epsilon)
        {
            if (Kind != other.Kind)
                return true;

            switch (Kind)
            {
                case ValueKind.String:
                    return !string.Equals(S, other.S, StringComparison.Ordinal);

                case ValueKind.Enum:
                    // Enum identity is the underlying integer; name follows it.
                    return F0 != other.F0;

                case ValueKind.Bool:
                    return F0 != other.F0;

                case ValueKind.Float:
                case ValueKind.Int:
                    return Math.Abs(F0 - other.F0) > epsilon;

                case ValueKind.Vec2:
                {
                    var dx = F0 - other.F0;
                    var dy = F1 - other.F1;
                    return Math.Sqrt(dx * dx + dy * dy) > epsilon;
                }
                case ValueKind.Vec3:
                {
                    var dx = F0 - other.F0;
                    var dy = F1 - other.F1;
                    var dz = F2 - other.F2;
                    return Math.Sqrt(dx * dx + dy * dy + dz * dz) > epsilon;
                }
                case ValueKind.Vec4:
                case ValueKind.Quat:
                case ValueKind.Color:
                {
                    var dx = F0 - other.F0;
                    var dy = F1 - other.F1;
                    var dz = F2 - other.F2;
                    var dw = F3 - other.F3;
                    return Math.Sqrt(dx * dx + dy * dy + dz * dz + dw * dw) > epsilon;
                }
                default:
                    return true;
            }
        }
    }
}
