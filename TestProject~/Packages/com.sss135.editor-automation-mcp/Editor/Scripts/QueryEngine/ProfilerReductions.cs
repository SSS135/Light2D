#nullable enable
using System;
using System.Collections.Generic;

namespace com.SSS135.EditorAutomation.Editor.QueryEngine
{
    /// <summary>One per-frame counter reading: a dense profiler series point.</summary>
    public struct ProfilerSample
    {
        public int Frame;
        public double Value;
    }

    /// <summary>
    /// Statistics + downsample reductions over a dense per-frame profiler counter
    /// series. Distinct from the recorder's sparse change-point <c>Reductions</c>:
    /// profiler counters are dense <see cref="ProfilerSample"/> readings, not
    /// change-only points, so they get their own data model.
    /// </summary>
    public static class ProfilerReductions
    {
        /// <summary>
        /// Reduce <paramref name="series"/> to at most <paramref name="maxPoints"/>
        /// points, keeping the first, last, and per-bucket max-magnitude point so
        /// peaks survive. Returns the series unchanged when it already fits.
        /// </summary>
        public static List<ProfilerSample> Downsample(IReadOnlyList<ProfilerSample> series, int maxPoints,
            out int total, out int elided)
        {
            total = series.Count;

            var result = new List<ProfilerSample>();
            if (total == 0)
            {
                elided = 0;
                return result;
            }

            if (total <= maxPoints || maxPoints <= 0)
            {
                for (var i = 0; i < total; i++)
                    result.Add(series[i]);
                elided = 0;
                return result;
            }

            // Always keep endpoints; spend the rest of the budget on per-bucket peaks.
            result.Add(series[0]);
            var interiorBudget = Math.Max(0, maxPoints - 2);
            if (interiorBudget > 0)
            {
                var interiorCount = total - 2;
                if (interiorCount > 0)
                {
                    var bucketSize = (double)interiorCount / interiorBudget;
                    const int interiorStart = 1;
                    for (var b = 0; b < interiorBudget; b++)
                    {
                        var lo = interiorStart + (int)(b * bucketSize);
                        var hi = interiorStart + (int)((b + 1) * bucketSize) - 1;
                        if (hi < lo) hi = lo;
                        if (lo > total - 2) break;
                        if (hi > total - 2) hi = total - 2;

                        var peakIdx = lo;
                        var peakMag = Math.Abs(series[lo].Value);
                        for (var i = lo + 1; i <= hi; i++)
                        {
                            var m = Math.Abs(series[i].Value);
                            if (m > peakMag) { peakMag = m; peakIdx = i; }
                        }
                        result.Add(series[peakIdx]);
                    }
                }
            }
            result.Add(series[total - 1]);

            elided = total - result.Count;
            return result;
        }

        /// <summary>
        /// Single-pass min/max/mean + argmax/argmin frames, plus p95/p99 from a
        /// sorted copy of the values. Returns zeros / <c>-1</c> arg frames for an
        /// empty input.
        /// </summary>
        public static (int count, double min, double max, double mean, double p95, double p99, int argMax, int argMin)
            Summarize(IReadOnlyList<ProfilerSample> series)
        {
            var count = series.Count;
            if (count == 0)
                return (0, 0, 0, 0, 0, 0, -1, -1);

            double min = double.PositiveInfinity, max = double.NegativeInfinity, sum = 0;
            int argMin = series[0].Frame, argMax = series[0].Frame;
            for (var i = 0; i < count; i++)
            {
                var v = series[i].Value;
                sum += v;
                if (v < min) { min = v; argMin = series[i].Frame; }
                if (v > max) { max = v; argMax = series[i].Frame; }
            }

            var values = new double[count];
            for (var i = 0; i < count; i++)
                values[i] = series[i].Value;
            Array.Sort(values);

            var mean = sum / count;
            return (count, min, max, mean, Percentile(values, 0.95), Percentile(values, 0.99), argMax, argMin);
        }

        // Nearest-rank percentile over an ascending-sorted array.
        static double Percentile(double[] sorted, double p)
        {
            if (sorted.Length == 0) return 0;
            if (sorted.Length == 1) return sorted[0];
            var rank = (int)Math.Ceiling(p * sorted.Length) - 1;
            if (rank < 0) rank = 0;
            if (rank >= sorted.Length) rank = sorted.Length - 1;
            return sorted[rank];
        }
    }
}
