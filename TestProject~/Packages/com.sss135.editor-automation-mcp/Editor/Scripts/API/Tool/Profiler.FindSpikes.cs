#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Editor.QueryEngine;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Profiler
    {
        // Hard cap on returned spike rows; the rest are reported as elided in Meta.
        const int SpikeCap = 100;
        const double DefaultSigma = 3.0;

        [McpTool
        (
            "profiler-find-spikes",
            Title = "Profiler / Find Spikes",
            ReadOnlyHint = true,
            IdempotentHint = true,
            MainThreadRequired = true
        )]
        [Description("Find the frames where one profiler counter spikes. Pass an absolute 'threshold' (a frame " +
            "counts as a spike when its value exceeds it) OR an n-'sigma' multiplier (spike when value > " +
            "mean + sigma*stddev over the window). 'threshold' wins when both are set; both 0 defaults to 3-sigma. " +
            "Results are ordered by descending value and capped — the worst frames survive, the count of " +
            "any dropped ones is in the envelope. Feed a spike frame to profiler.frame-breakdown to see the cause. " +
            "Operates on per-frame counters; an unknown name or one with no per-frame data throws. Use " +
            "\"CPU Total Frame Time\" to find CPU frame-time spikes.")]
        public SpikeList FindSpikes(
            [Description("Counter name from profiler.describe, e.g. \"CPU Total Frame Time\".")]
            string counter,
            [Description("Absolute value threshold; a frame is a spike when its value exceeds it. 0 falls back to sigma.")]
            double threshold = 0,
            [Description("Spike when value > mean + sigma*stddev. Used only when 'threshold' is 0; 0 here defaults to 3.")]
            double sigma = 0,
            [Description("Window start frame (inclusive). 0 / negative selects the first available frame.")]
            int from = 0,
            [Description("Window end frame (inclusive). 0 / negative selects the last available frame.")]
            int to = 0)
            => RunMain(() =>
            {
                if (!ProfilerSource.HasFrames)
                    throw new System.InvalidOperationException(Error.NoFrames());

                var (category, unit) = ProfilerSource.CounterMeta(counter);
                var result = new SpikeList
                {
                    Counter = counter,
                    Unit = unit,
                    Meta = { TimeDomain = "render", Units = unit }
                };

                var samples = ProfilerSource.CounterSeries(counter, from, to);

                if (samples.Count == 0)
                    throw new System.InvalidOperationException(
                        string.IsNullOrEmpty(category) && string.IsNullOrEmpty(unit)
                            ? Error.UnknownCounter(counter)
                            : Error.CounterNoData(counter));

                double effectiveThreshold;
                if (threshold > 0)
                {
                    effectiveThreshold = threshold;
                }
                else
                {
                    var s = sigma > 0 ? sigma : DefaultSigma;
                    var (mean, stddev) = MeanStdDev(samples);
                    effectiveThreshold = mean + s * stddev;
                    result.Sigma = s;
                }
                result.ThresholdUsed = effectiveThreshold;

                var spikes = samples
                    .Where(p => p.Value > effectiveThreshold)
                    .OrderByDescending(p => p.Value)
                    .Select(p => new Spike { Frame = p.Frame, Value = p.Value })
                    .ToList();

                result.Meta.FrameRangeCovered = new[] { samples[0].Frame, samples[samples.Count - 1].Frame };
                result.Meta.TotalCount = spikes.Count;
                if (spikes.Count > SpikeCap)
                {
                    result.Meta.Truncated = true;
                    result.Meta.Elided = spikes.Count - SpikeCap;
                    spikes = spikes.Take(SpikeCap).ToList();
                }
                result.Spikes = spikes.ToArray();
                return result;
            });

        /// <summary>Population mean and standard deviation of a counter series.</summary>
        static (double mean, double stddev) MeanStdDev(List<ProfilerSample> samples)
        {
            var count = samples.Count;
            double sum = 0;
            for (var i = 0; i < count; i++)
                sum += samples[i].Value;
            var mean = sum / count;

            double sqSum = 0;
            for (var i = 0; i < count; i++)
            {
                var d = samples[i].Value - mean;
                sqSum += d * d;
            }
            return (mean, Math.Sqrt(sqSum / count));
        }
    }
}
