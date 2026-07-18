#nullable enable
using System;
using System.ComponentModel;
using System.IO;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Editor.Framework.Util;
using com.SSS135.EditorAutomation.Editor.QueryEngine;
using com.SSS135.EditorAutomation.Utils;
using Microsoft.Extensions.Logging;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Profiler
    {
        [McpTool
        (
            "profiler-query",
            Title = "Profiler / Query (C#)",
            ReadOnlyHint = true,
            IdempotentHint = true,
            MainThreadRequired = true
        )]
        [Description("Run a small C# snippet over the current Profiler buffer and get back a compact result — " +
            "the flexible escape hatch for ad-hoc questions the structured tools (describe, summarize-counter, " +
            "get-series, frame-breakdown, find-spikes) can't express, e.g. joining a counter series to per-frame " +
            "marker breakdowns or custom percentiles. CALL 'profiler-describe' FIRST to learn the frame range and " +
            "counter names.\n" +
            "Your 'body' is a C# method BODY that must `return` a value. In scope: `q` (the read-only " +
            "ProfilerQueryContext), plus System.Linq and the UnityEditor.Profiling / UnityEditorInternal namespaces. " +
            "The `q` surface:\n" +
            "  q.FirstFrame, q.LastFrame, q.FrameCount, q.IsRecording,\n" +
            "  q.AvailableCounters() -> IReadOnlyList<CounterInfo>,\n" +
            "  q.CounterValue(frame, counter) -> double? (null when absent),\n" +
            "  q.CounterSeries(counter, from, to) -> IReadOnlyList<ProfilerSample> { int Frame; double Value },\n" +
            "  q.Breakdown(frame, topN, threadIndex, sortBy) -> MarkerRow[],\n" +
            "  q.WorstFrame() -> worst frame by CPU time, q.IsLoadFrame(frame) -> bool.\n" +
            "RETURN a primitive or a List<Dictionary<string,object>> (one dict per row) — NOT an anonymous type. " +
            "Read-only (no buffer-replacing mutators on `q`); runs on the Editor main thread, so KEEP IT BOUNDED — " +
            "an unbounded loop freezes the Editor. The returned sequence is capped at 'maxRows' " +
            "(Meta reports truncation).\n" +
            "Two input modes: inline 'body' OR a previously saved 'filePath' — supply exactly one. Inline calls are " +
            "persisted to '{projectRoot}/Library/McpQueries/Profiler/<fileName>.cs' (outside Unity's compiled paths; " +
            "survives Editor restart) under 'fileName', overwriting any existing file. The response 'ArtifactPath' " +
            "points at that file, so you can edit-in-place and re-invoke via 'filePath' instead of resubmitting the " +
            "snippet on each iteration. 'fileName' and 'description' are REQUIRED with 'body' and prohibited with 'filePath'. " +
            "The response is a 'ToolRunEnvelope': 'Status' (\"executed\" | \"compile_failed\" | \"runtime_error\"), " +
            "'ArtifactPath', 'Result' (the capped value), 'Meta', plus 'Diagnostics' (file-relative, on compile failure) " +
            "or 'RuntimeError' (on a runtime throw). Runtime errors return structured — they do not throw.\n" +
            "Example body: return Enumerable.Range(q.FirstFrame, q.FrameCount)" +
            ".Select(f => q.CounterValue(f, \"Main Thread\")).Where(v => v.HasValue).Max();")]
        public ToolRunEnvelope Query(
            [Description("C# method body that returns a value, with `q` (the read-only ProfilerQueryContext) and System.Linq in scope. " +
                "Mutually exclusive with `filePath` — supply exactly one.")]
            string? body = null,
            [Description("File name for the persisted query under 'Library/McpQueries/Profiler/'. " +
                "A '.cs' extension is appended if omitted ('Foo', 'Foo.cs' both save as 'Foo.cs'). " +
                "Must be a bare file name — directory separators and other invalid characters are rejected. " +
                "REQUIRED when 'body' is provided; must be omitted when 'filePath' is provided.")]
            string? fileName = null,
            [Description("Short summary of what the query does, written as a '//' comment header at the top of the " +
                "saved file. REQUIRED when 'body' is provided; must be omitted when 'filePath' is provided.")]
            string? description = null,
            [Description("Absolute path to a previously persisted query file (typically the 'ArtifactPath' returned by an " +
                "earlier call). Mutually exclusive with 'body', 'fileName', and 'description'. Compiled verbatim — edit it " +
                "in place to fix a diagnostic, then re-invoke with the same path.")]
            string? filePath = null,
            [Description("Maximum rows returned for an enumerable result. 0 uses the default budget of 200.")]
            int maxRows = 0)
        {
            // Mutual exclusion: exactly one of (body, filePath) must be supplied.
            var hasBody = !string.IsNullOrWhiteSpace(body);
            var hasFilePath = !string.IsNullOrEmpty(filePath);
            if (hasBody == hasFilePath)
                throw new ArgumentException("Exactly one of 'body' or 'filePath' must be provided.");

            // fileName/description name + document the persisted snippet; they belong to inline mode only.
            string? resolvedFileName = null;
            if (hasFilePath)
            {
                if (!string.IsNullOrEmpty(fileName))
                    throw new ArgumentException("'fileName' is not allowed when 'filePath' is provided.");
                if (!string.IsNullOrEmpty(description))
                    throw new ArgumentException("'description' is not allowed when 'filePath' is provided.");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(fileName))
                    throw new ArgumentException("'fileName' is required when 'body' is provided.");
                if (string.IsNullOrWhiteSpace(description))
                    throw new ArgumentException("'description' is required when 'body' is provided.");
                resolvedFileName = ArtifactStore.SanitizeFileName(fileName!, ".cs");
            }

            var cap = maxRows > 0 ? maxRows : 200;

            return RunMain(() =>
            {
                var logger = UnityLoggerFactory.LoggerFactory.CreateLogger("Tool_Profiler.Query");
                var reflector = ReflectorFactory.Instance ?? throw new Exception("Reflector is not available.");

                if (!ProfilerSource.HasFrames)
                    throw new InvalidOperationException(Error.NoFrames());

                var meta = new EnvelopeMeta
                {
                    TimeDomain = "render",
                    FrameRangeCovered = new[] { ProfilerSource.FirstFrame, ProfilerSource.LastFrame }
                };

                // Determine (codeToCompile, savedPath). filePath mode compiles the file verbatim; inline mode
                // builds the header + wrapped fenced source, persists it under Library/McpQueries/Profiler/, and
                // compiles that — so the saved file is byte-identical to the compiled source for edit-and-rerun.
                string codeToCompile;
                string savedPath;
                if (hasFilePath)
                {
                    if (!File.Exists(filePath))
                        throw new ArgumentException($"'filePath' does not exist: {filePath}");
                    codeToCompile = File.ReadAllText(filePath!);
                    savedPath = Path.GetFullPath(filePath!);
                }
                else
                {
                    codeToCompile = ArtifactStore.BuildDescriptionHeader(description!) + ProfilerQueryCompiler.WrapBody(body!);
                    savedPath = ArtifactStore.PersistNamedArtifact("McpQueries/Profiler", resolvedFileName!, codeToCompile);
                }

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var run = ProfilerQueryCompiler.Run(codeToCompile, savedPath, new ProfilerQueryContext(), logger);
                stopwatch.Stop();

                if (!run.Compiled)
                    return new ToolRunEnvelope
                    {
                        Status = "compile_failed",
                        DurationMs = stopwatch.ElapsedMilliseconds,
                        ArtifactPath = savedPath,
                        Diagnostics = run.Diagnostics,
                        Meta = meta
                    };

                if (run.RuntimeError != null)
                    return new ToolRunEnvelope
                    {
                        Status = "runtime_error",
                        DurationMs = stopwatch.ElapsedMilliseconds,
                        ArtifactPath = savedPath,
                        RuntimeError = run.RuntimeError,
                        Meta = meta
                    };

                var (result, truncated, attempted) = ResultCapper.CapResult(run.Value, cap);
                meta.Truncated = truncated;
                if (attempted.HasValue)
                {
                    meta.TotalCount = attempted.Value;
                    meta.Elided = Math.Max(0, attempted.Value - cap);
                }

                return new ToolRunEnvelope
                {
                    Status = "executed",
                    DurationMs = stopwatch.ElapsedMilliseconds,
                    ArtifactPath = savedPath,
                    // Pre-serialize through the configured Reflector (Unity-aware converters + poison-pill
                    // blacklist); a null snippet result maps to a null envelope Result, matching script.execute.
                    Result = result != null ? reflector.Serialize(obj: result, logger: logger) : null,
                    Meta = meta
                };
            });
        }
    }
}
