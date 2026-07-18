#nullable enable
using System.Collections;
using System.Collections.Generic;

namespace com.SSS135.EditorAutomation.Editor.QueryEngine
{
    /// <summary>
    /// Caps an arbitrary snippet result to a row budget for the <c>profiler-query</c> and
    /// <c>recorder-query</c> C# escape hatches.
    /// </summary>
    public static class ResultCapper
    {
        /// <summary>
        /// Cap an enumerable result to <paramref name="cap"/> rows. Strings and
        /// scalars pass through. A materialized collection reports its true count
        /// cheaply; a lazy sequence is read at most cap+1 to detect overflow without
        /// enumerating an unbounded source. Returns the (possibly clipped) value,
        /// whether it was clipped, and the true total when known (null for a clipped
        /// lazy sequence).
        /// </summary>
        public static (object? result, bool truncated, int? attempted) CapResult(object? raw, int cap)
        {
            if (raw == null || raw is string)
                return (raw, false, null);

            if (raw is ICollection collection)
            {
                var total = collection.Count;
                if (total <= cap)
                    return (raw, false, total);
                var clipped = new List<object?>(cap);
                foreach (var item in (IEnumerable)raw)
                {
                    if (clipped.Count >= cap) break;
                    clipped.Add(item);
                }
                return (clipped, true, total);
            }

            if (raw is IEnumerable enumerable)
            {
                var rows = new List<object?>(cap);
                var truncated = false;
                foreach (var item in enumerable)
                {
                    if (rows.Count >= cap) { truncated = true; break; }
                    rows.Add(item);
                }
                return (rows, truncated, truncated ? (int?)null : rows.Count);
            }

            return (raw, false, null); // scalar / single object
        }
    }
}
