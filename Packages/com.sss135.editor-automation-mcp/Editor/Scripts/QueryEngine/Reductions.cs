#nullable enable
using System;
using System.Collections.Generic;
using com.SSS135.EditorAutomation.Recorder;

namespace com.SSS135.EditorAutomation.Editor.QueryEngine
{
    /// <summary>
    /// Scalar magnitude + endpoint summary over a (key, tag) series window, plus
    /// the <c>diff</c> (capped change list with cursor) and <c>downsample</c>
    /// (peak-preserving point budget) series reductions. A value's scalar
    /// magnitude is its component norm, so vector kinds get a meaningful min/max.
    /// </summary>
    public static class Reductions
    {
        /// <summary>Endpoint + extremum summary of a series window. All fields null when the window is empty.</summary>
        public readonly struct Summary
        {
            public readonly double? Start;
            public readonly double? End;
            public readonly double? NetDelta;
            public readonly double? Min;
            public readonly double? Max;
            public readonly double? Mean;
            public readonly int? ArgMaxFrame;
            public readonly int? ArgMinFrame;
            public readonly int ChangeCount;

            public Summary(double? start, double? end, double? netDelta, double? min, double? max,
                double? mean, int? argMaxFrame, int? argMinFrame, int changeCount)
            {
                Start = start;
                End = end;
                NetDelta = netDelta;
                Min = min;
                Max = max;
                Mean = mean;
                ArgMaxFrame = argMaxFrame;
                ArgMinFrame = argMinFrame;
                ChangeCount = changeCount;
            }
        }

        /// <summary>Scalar magnitude of a value: abs for scalars, Euclidean norm for vector kinds, NaN for strings.</summary>
        public static double Magnitude(in RecordedValue v)
        {
            switch (v.Kind)
            {
                case ValueKind.Float:
                case ValueKind.Int:
                case ValueKind.Bool:
                case ValueKind.Enum:
                    return v.F0;
                case ValueKind.Vec2:
                    return Math.Sqrt(v.F0 * v.F0 + v.F1 * v.F1);
                case ValueKind.Vec3:
                    return Math.Sqrt(v.F0 * v.F0 + v.F1 * v.F1 + v.F2 * v.F2);
                case ValueKind.Vec4:
                case ValueKind.Quat:
                case ValueKind.Color:
                    return Math.Sqrt(v.F0 * v.F0 + v.F1 * v.F1 + v.F2 * v.F2 + v.F3 * v.F3);
                default:
                    return double.NaN;
            }
        }

        /// <summary>
        /// Summarize the change of <paramref name="series"/> over [from, to]:
        /// as-of endpoints (carried forward), net delta, min/max/mean of the
        /// magnitude, the frames where the extrema occurred, and the count of
        /// change points inside the window.
        /// </summary>
        public static Summary Summarize(IReadOnlyList<ChangePoint> series, int from, int to)
        {
            double? start = null, end = null;
            if (AsOf.ValueAt(series, from, out var startPoint))
                start = Magnitude(in startPoint.V);
            if (AsOf.ValueAt(series, to, out var endPoint))
                end = Magnitude(in endPoint.V);

            var (first, last) = AsOf.SliceRange(series, from, to);
            if (first > last)
            {
                var netNoChange = start.HasValue && end.HasValue ? end - start : null;
                return new Summary(start, end, netNoChange, start, end, start, null, null, 0);
            }

            double min = double.PositiveInfinity, max = double.NegativeInfinity, sum = 0;
            int argMin = series[first].Frame, argMax = series[first].Frame;
            var count = 0;
            for (var i = first; i <= last; i++)
            {
                var m = Magnitude(series[i].V);
                sum += m;
                count++;
                if (m < min) { min = m; argMin = series[i].Frame; }
                if (m > max) { max = m; argMax = series[i].Frame; }
            }

            var mean = count > 0 ? sum / count : (double?)null;
            var net = start.HasValue && end.HasValue ? end - start : null;
            return new Summary(start, end, net, min, max, mean, argMax, argMin, count);
        }

        /// <summary>
        /// Capped change list over [from, to]. Returns the change points starting at
        /// <paramref name="cursor"/> (an index into the window), up to
        /// <paramref name="maxPoints"/>; sets <paramref name="nextCursor"/> when more
        /// remain and reports the total window size and how many were elided.
        /// </summary>
        public static List<ChangePoint> Diff(IReadOnlyList<ChangePoint> series, int from, int to,
            int maxPoints, int cursor, out int total, out int elided, out int? nextCursor)
        {
            var (first, last) = AsOf.SliceRange(series, from, to);
            total = first > last ? 0 : last - first + 1;

            var result = new List<ChangePoint>();
            if (total == 0)
            {
                elided = 0;
                nextCursor = null;
                return result;
            }

            var startOffset = Math.Max(0, cursor);
            var startIndex = first + startOffset;
            var emitted = 0;
            for (var i = startIndex; i <= last && emitted < maxPoints; i++, emitted++)
                result.Add(series[i]);

            var consumed = startOffset + emitted;
            elided = total - consumed;
            nextCursor = consumed < total ? consumed : (int?)null;
            return result;
        }

        /// <summary>
        /// Reduce the window to at most <paramref name="maxPoints"/> points,
        /// preserving the first, last, and per-bucket magnitude extrema so peaks
        /// survive. Returns the original window unchanged when it already fits.
        /// </summary>
        public static List<ChangePoint> Downsample(IReadOnlyList<ChangePoint> series, int from, int to,
            int maxPoints, out int total, out int elided)
        {
            var (first, last) = AsOf.SliceRange(series, from, to);
            total = first > last ? 0 : last - first + 1;

            var result = new List<ChangePoint>();
            if (total == 0)
            {
                elided = 0;
                return result;
            }

            if (total <= maxPoints || maxPoints <= 0)
            {
                for (var i = first; i <= last; i++)
                    result.Add(series[i]);
                elided = 0;
                return result;
            }

            // Always keep endpoints; spend the rest of the budget on per-bucket peaks.
            result.Add(series[first]);
            var interiorBudget = Math.Max(0, maxPoints - 2);
            if (interiorBudget > 0)
            {
                var interiorCount = total - 2;
                if (interiorCount > 0)
                {
                    var bucketSize = (double)interiorCount / interiorBudget;
                    var interiorStart = first + 1;
                    for (var b = 0; b < interiorBudget; b++)
                    {
                        var lo = interiorStart + (int)(b * bucketSize);
                        var hi = interiorStart + (int)((b + 1) * bucketSize) - 1;
                        if (hi < lo) hi = lo;
                        if (lo > last - 1) break;
                        if (hi > last - 1) hi = last - 1;

                        var peakIdx = lo;
                        var peakMag = Magnitude(series[lo].V);
                        for (var i = lo + 1; i <= hi; i++)
                        {
                            var m = Magnitude(series[i].V);
                            if (m > peakMag) { peakMag = m; peakIdx = i; }
                        }
                        result.Add(series[peakIdx]);
                    }
                }
            }
            result.Add(series[last]);

            elided = total - result.Count;
            return result;
        }
    }
}
