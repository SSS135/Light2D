#nullable enable
using System.Collections.Generic;
using com.SSS135.EditorAutomation.Recorder;

namespace com.SSS135.EditorAutomation.Editor.QueryEngine
{
    /// <summary>
    /// As-of (state-at-frame) lookups over a change-only series. Series are stored
    /// sorted by frame, so the last change point with <c>Frame ≤ N</c> is the value
    /// in effect at frame N — a binary search, carrying the most recent value
    /// forward (the first value per series is always present as a baseline).
    /// </summary>
    public static class AsOf
    {
        /// <summary>
        /// Index of the last change point with <c>Frame ≤ frame</c>, or -1 when the
        /// series is empty or every point is after <paramref name="frame"/>.
        /// </summary>
        public static int IndexAt(IReadOnlyList<ChangePoint> series, int frame)
        {
            int lo = 0, hi = series.Count - 1, result = -1;
            while (lo <= hi)
            {
                var mid = lo + ((hi - lo) >> 1);
                if (series[mid].Frame <= frame)
                {
                    result = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }
            return result;
        }

        /// <summary>
        /// The value in effect at <paramref name="frame"/> (carried forward from the
        /// last change ≤ N). Returns false when nothing exists at or before N.
        /// </summary>
        public static bool ValueAt(IReadOnlyList<ChangePoint> series, int frame, out ChangePoint point)
        {
            var idx = IndexAt(series, frame);
            if (idx < 0)
            {
                point = default;
                return false;
            }
            point = series[idx];
            return true;
        }

        /// <summary>
        /// Inclusive index span [first, last] of change points whose frame falls in
        /// [<paramref name="from"/>, <paramref name="to"/>]. Returns a zero-count
        /// span (first &gt; last) when no point lies in the window.
        /// </summary>
        public static (int first, int last) SliceRange(IReadOnlyList<ChangePoint> series, int from, int to)
        {
            var first = FirstAtLeast(series, from);
            var last = IndexAt(series, to);
            if (first > last)
                return (0, -1);
            return (first, last);
        }

        /// <summary>Index of the first change point with <c>Frame ≥ frame</c>, or count when none.</summary>
        static int FirstAtLeast(IReadOnlyList<ChangePoint> series, int frame)
        {
            int lo = 0, hi = series.Count - 1, result = series.Count;
            while (lo <= hi)
            {
                var mid = lo + ((hi - lo) >> 1);
                if (series[mid].Frame >= frame)
                {
                    result = mid;
                    hi = mid - 1;
                }
                else
                {
                    lo = mid + 1;
                }
            }
            return result;
        }
    }
}
