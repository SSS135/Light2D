#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using com.SSS135.EditorAutomation.Editor.API;
using Unity.Profiling;
using Unity.Profiling.LowLevel;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor.Profiling;
using UnityEditorInternal;

namespace com.SSS135.EditorAutomation.Editor.QueryEngine
{
    /// <summary>
    /// Read-only facade over Unity's in-memory Profiler buffer
    /// (<see cref="ProfilerDriver"/> + the <c>UnityEditor.Profiling.*FrameDataView</c>
    /// family). Every member reads back whatever is currently loaded — a live
    /// recording or the last-loaded capture — and assumes the Unity main thread;
    /// the tool layer marshals via <c>RunMain</c>. Nothing here starts, stops, or
    /// otherwise controls recording.
    /// </summary>
    public static class ProfilerSource
    {
        // name -> (category, unit), rebuilt lazily from the live counter list.
        static Dictionary<string, (string category, string unit)>? _counterMeta;

        public static int FirstFrame => ProfilerDriver.firstFrameIndex;
        public static int LastFrame => ProfilerDriver.lastFrameIndex;
        public static bool HasFrames => FirstFrame >= 0 && LastFrame >= FirstFrame;
        public static int FrameCount => HasFrames ? LastFrame - FirstFrame + 1 : 0;
        public static bool IsRecording => ProfilerDriver.enabled;

        /// <summary>Number of valid threads in <paramref name="frame"/>.</summary>
        public static int ThreadCount(int frame)
        {
            var count = 0;
            while (true)
            {
                using var fd = ProfilerDriver.GetRawFrameDataView(frame, count);
                if (!fd.valid) break;
                count++;
            }
            return count;
        }

        /// <summary>Thread names present in <paramref name="frame"/>, in thread-index order.</summary>
        public static string[] ThreadNames(int frame)
        {
            var names = new List<string>();
            var ti = 0;
            while (true)
            {
                using var fd = ProfilerDriver.GetRawFrameDataView(frame, ti);
                if (!fd.valid) break;
                names.Add(fd.threadName);
                ti++;
            }
            return names.ToArray();
        }

        /// <summary>Total CPU frame time of <paramref name="frame"/> in ms; NaN when the frame is invalid.</summary>
        public static double FrameTimeMs(int frame)
        {
            using var fd = ProfilerDriver.GetRawFrameDataView(frame, 0);
            return fd.valid ? fd.frameTimeMs : double.NaN;
        }

        /// <summary>
        /// Read a counter value for <paramref name="frame"/>, searching every thread
        /// (a counter such as "Draw Calls Count" may live off the main thread).
        /// Returns false when no thread carries it.
        /// </summary>
        public static bool TryReadCounter(int frame, string counter, out double value)
        {
            var ti = 0;
            while (true)
            {
                using var fd = ProfilerDriver.GetRawFrameDataView(frame, ti);
                if (!fd.valid) break;
                var id = fd.GetMarkerId(counter);
                if (id != FrameDataView.invalidMarkerId && fd.HasCounterValue(id))
                {
                    value = fd.GetCounterValueAsDouble(id);
                    return true;
                }
                ti++;
            }
            value = 0;
            return false;
        }

        /// <summary>Every counter the live session exposes, with its category and unit string.</summary>
        public static IReadOnlyList<CounterInfo> AvailableCounters()
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);

            var result = new List<CounterInfo>(handles.Count);
            var meta = new Dictionary<string, (string category, string unit)>();
            foreach (var handle in handles)
            {
                if (!handle.Valid) continue;
                var desc = ProfilerRecorderHandle.GetDescription(handle);
                var name = desc.Name;
                if (string.IsNullOrEmpty(name)) continue;
                var category = desc.Category.Name ?? "";
                var unit = UnitString(desc.UnitType);
                result.Add(new CounterInfo { Name = name, Category = category, Unit = unit });
                meta[name] = (category, unit);
            }

            _counterMeta = meta;
            return result;
        }

        /// <summary>
        /// Category + unit for <paramref name="name"/> from the cached live counter
        /// map (built lazily). Unknown counters — e.g. from a loaded capture not in
        /// the live availability list — report empty strings.
        /// </summary>
        public static (string category, string unit) CounterMeta(string name)
        {
            if (_counterMeta == null)
                AvailableCounters();
            return _counterMeta != null && _counterMeta.TryGetValue(name, out var m) ? m : ("", "");
        }

        static string UnitString(ProfilerMarkerDataUnit u) => u switch
        {
            ProfilerMarkerDataUnit.TimeNanoseconds => "ns",
            ProfilerMarkerDataUnit.Bytes => "bytes",
            ProfilerMarkerDataUnit.Count => "count",
            ProfilerMarkerDataUnit.Percent => "percent",
            ProfilerMarkerDataUnit.FrequencyHz => "hz",
            _ => ""
        };

        /// <summary>
        /// Per-frame readings of <paramref name="counter"/> over [from, to], clamped
        /// to the available range; frames with no value are skipped. A non-positive
        /// <paramref name="from"/>/<paramref name="to"/> selects the full range.
        /// </summary>
        public static List<ProfilerSample> CounterSeries(string counter, int from, int to)
        {
            var samples = new List<ProfilerSample>();
            if (!HasFrames) return samples;

            var lo = from < 0 ? FirstFrame : Math.Max(from, FirstFrame);
            var hi = to <= 0 ? LastFrame : Math.Min(to, LastFrame);
            for (var f = lo; f <= hi; f++)
                if (TryReadCounter(f, counter, out var v))
                    samples.Add(new ProfilerSample { Frame = f, Value = v });
            return samples;
        }

        static readonly string[] LoadSampleNames =
        {
            // Only the genuine scene-load marker. NOT Loading.AwakeFromLoad /
            // Loading.UpdatePreloading — those fire on ordinary runtime object
            // instantiation and the per-frame preload phase, so matching them
            // misclassifies normal gameplay frames (e.g. unit spawns) as loads.
            "Application.LoadLevelAsync Integrate"
        };

        /// <summary>True when <paramref name="frame"/> contains a real scene-load marker (Application.LoadLevelAsync) on the main thread.</summary>
        public static bool IsLoadFrame(int frame)
        {
            using var fd = ProfilerDriver.GetRawFrameDataView(frame, 0);
            if (!fd.valid) return false;
            var count = fd.sampleCount;
            for (var i = 0; i < count; i++)
            {
                var name = fd.GetSampleName(i);
                for (var n = 0; n < LoadSampleNames.Length; n++)
                    if (name == LoadSampleNames[n])
                        return true;
            }
            return false;
        }

        /// <summary>Argmax of <see cref="FrameTimeMs"/> over the available range; -1 when no frames.</summary>
        public static int WorstFrame()
        {
            if (!HasFrames) return -1;

            var worst = -1;
            var worstMs = double.NegativeInfinity;
            for (var f = FirstFrame; f <= LastFrame; f++)
            {
                var ms = FrameTimeMs(f);
                if (double.IsNaN(ms)) continue;
                if (ms > worstMs) { worstMs = ms; worst = f; }
            }
            return worst;
        }

        /// <summary>
        /// Top <paramref name="topN"/> markers for <paramref name="frame"/> on
        /// <paramref name="threadIndex"/>, inverted + name-merged (leaf functions
        /// aggregated), sorted by <paramref name="sortBy"/> ("self" | "total" | "gc").
        /// </summary>
        public static MarkerRow[] Breakdown(int frame, int topN, int threadIndex, string sortBy)
        {
            var sortColumn = sortBy switch
            {
                "self" => HierarchyFrameDataView.columnSelfTime,
                "total" => HierarchyFrameDataView.columnTotalTime,
                "gc" => HierarchyFrameDataView.columnGcMemory,
                _ => HierarchyFrameDataView.columnSelfTime
            };

            const HierarchyFrameDataView.ViewModes vm =
                HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName |
                HierarchyFrameDataView.ViewModes.InvertHierarchy;

            using var hv = ProfilerDriver.GetHierarchyFrameDataView(frame, threadIndex, vm, sortColumn, false);
            if (!hv.valid) return Array.Empty<MarkerRow>();

            var children = new List<int>();
            hv.GetItemChildren(hv.GetRootItemID(), children);

            var n = Math.Min(topN, children.Count);
            var rows = new MarkerRow[n];
            for (var i = 0; i < n; i++)
            {
                var id = children[i];
                rows[i] = new MarkerRow
                {
                    Name = hv.GetItemName(id),
                    SelfMs = hv.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnSelfTime),
                    TotalMs = hv.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnTotalTime),
                    Calls = (int)hv.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnCalls),
                    GcBytes = (long)hv.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnGcMemory)
                };
            }
            return rows;
        }

        /// <summary>
        /// Load a saved <c>.raw</c>/<c>.data</c> capture into the global Profiler
        /// buffer. This REPLACES the current in-memory data and is reflected by the
        /// Profiler window and every other <c>profiler.*</c> read until you
        /// re-record. Throws <see cref="FileNotFoundException"/> when the file is
        /// missing.
        /// </summary>
        public static LoadCaptureResult LoadCapture(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Profiler capture not found: {path}", path);

            ProfilerDriver.LoadProfile(path, false);

            return new LoadCaptureResult
            {
                Path = path,
                FirstFrame = FirstFrame,
                LastFrame = LastFrame,
                FrameCount = FrameCount
            };
        }
    }
}
