#nullable enable
using System;
using System.ComponentModel;
using System.IO;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Editor.Framework.Util;
using com.SSS135.EditorAutomation.Editor.QueryEngine;
using com.SSS135.EditorAutomation.Utils;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Recorder
    {
        [McpTool
        (
            "recorder-query",
            Title = "Recorder / Query (C#)",
            ReadOnlyHint = true,
            MainThreadRequired = true
        )]
        [Description("Run a small C# LINQ snippet over a recorded or live session and get back a compact result — " +
            "the flexible escape hatch for ad-hoc questions the summary tools (get-state, summarize-change, " +
            "get-series, find-events) can't express, e.g. event-anchored windows that join the variable and event " +
            "streams. CALL 'recorder-describe-session' FIRST to learn the object keys and tags.\n" +
            "Your 'query' is a C# method BODY that must `return` a value. In scope: `q` (the read-only session), " +
            "plus System.Linq. The `q` surface:\n" +
            "  q.Objects (key->ObjectRecord), q.Variables (tag->VariableManifest), q.Events (list of EventRecord),\n" +
            "  q.SeriesKeys ((key,tag) pairs), q.Series(key,tag) -> IReadOnlyList<ChangePoint>,\n" +
            "  q.ValueAt(key,tag,frame) -> ChangePoint? (as-of), q.Magnitude(in RecordedValue) -> double,\n" +
            "  q.MinFrame, q.MaxFrame, q.SessionId, q.FrameBase.\n" +
            "A ChangePoint has { int Frame; RecordedValue V }; RecordedValue has { ValueKind Kind; double F0,F1,F2,F3; string S }.\n" +
            "RETURN a primitive or a List<Dictionary<string,object>> (one dict per row) — NOT an anonymous type. " +
            "Read-only (no mutators on `q`); runs on the Editor main thread, so KEEP IT BOUNDED — an unbounded loop " +
            "freezes the Editor. The returned sequence is capped at 'maxRows' (Meta.Truncated/Meta.TotalCount report it).\n" +
            "Inline 'query' is persisted to '{projectRoot}/Library/McpQueries/Recorder/<fileName>.cs' under the " +
            "caller-supplied `fileName` (survives Editor restart, overwriting any existing file). The response's " +
            "`ArtifactPath` points at that file so you can edit-in-place and re-invoke via `filePath` instead of " +
            "resubmitting the whole snippet on iteration. On a compile error the response carries `Status: " +
            "\"compile_failed\"` + file-relative `Diagnostics`; a runtime throw is reported in `RuntimeError`.\n" +
            "Example query: return q.Series(\"iid:1234\",\"health_fraction\").Where(p => p.Frame>=100 && p.Frame<=200)" +
            ".Select(p => new Dictionary<string,object>{{\"frame\",p.Frame},{\"h\",p.V.F0}}).ToList();")]
        public ToolRunEnvelope Query(
            [Description("\"live\" for the running session, or a recording file path / id. Required in both modes.")]
            string session,
            [Description("A C# method body that returns a value, with `q` (the read-only session) and System.Linq in scope. " +
                "Mutually exclusive with `filePath` — supply exactly one.")]
            string? query = null,
            [Description("File name for the persisted snippet under 'Library/McpQueries/Recorder/'. " +
                "A '.cs' extension is appended if omitted ('Foo', 'Foo.cs' both save as 'Foo.cs'). " +
                "If a file with this name already exists it is overwritten. " +
                "Must be a bare file name — directory separators and other invalid characters are rejected. " +
                "REQUIRED when 'query' is provided; must be omitted when 'filePath' is provided.")]
            string? fileName = null,
            [Description("Short summary of what the snippet does, written as a '//' comment header at the top of the " +
                "saved file. REQUIRED when 'query' is provided; must be omitted when 'filePath' is provided.")]
            string? description = null,
            [Description("Absolute path to a previously persisted snippet file (typically the `ArtifactPath` returned by " +
                "an earlier call). Mutually exclusive with `query`, `fileName`, and `description`. The file is compiled " +
                "verbatim. Use this to iterate on a query that compile-failed: read `ArtifactPath` from the failure " +
                "response, edit the file in place, then re-invoke with the same path (and the same `session`).")]
            string? filePath = null,
            [Description("Maximum rows returned for an enumerable result (capped at the engine hard limit). Default 200.")]
            int maxRows = 200)
        {
            if (string.IsNullOrEmpty(session))
                throw new ArgumentException(Error.SessionRequired(), nameof(session));

            // Mutual exclusion: exactly one of (query, filePath). fileName/description belong to inline
            // mode only (they name + document the persisted file); in filePath mode the file already
            // exists and carries its own header. `session` is required in both modes — the query context
            // is built at invoke time, so the file itself is session-agnostic.
            var hasInlineQuery = !string.IsNullOrWhiteSpace(query);
            var hasFilePath = !string.IsNullOrEmpty(filePath);
            if (hasInlineQuery == hasFilePath)
                throw new ArgumentException("Exactly one of 'query' or 'filePath' must be provided.");

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
                    throw new ArgumentException("'fileName' is required when 'query' is provided.");
                if (string.IsNullOrWhiteSpace(description))
                    throw new ArgumentException("'description' is required when 'query' is provided.");
                resolvedFileName = ArtifactStore.SanitizeFileName(fileName!, ".cs");
            }

            var cap = maxRows <= 0 ? 200 : Math.Min(maxRows, RecorderQueryEngine.HardRowCap);

            return RunMain(() =>
            {
                var logger = UnityLoggerFactory.LoggerFactory.CreateLogger("Tool_Recorder.Query");

                // Determine (source, savedPath). filePath mode compiles the file verbatim; inline mode
                // wraps the body, prepends the description header, and persists under McpQueries/Recorder/.
                string source;
                string savedPath;
                if (hasFilePath)
                {
                    if (!File.Exists(filePath))
                        throw new FileNotFoundException($"'filePath' does not exist: {filePath}");
                    source = File.ReadAllText(filePath!);
                    savedPath = Path.GetFullPath(filePath!);
                }
                else
                {
                    source = ArtifactStore.BuildDescriptionHeader(description!) + RecorderQueryCompiler.WrapBody(query!);
                    savedPath = ArtifactStore.PersistNamedArtifact("McpQueries/Recorder", resolvedFileName!, source);
                }

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                var session_ = SessionResolver.Resolve(session);
                var meta = new EnvelopeMeta
                {
                    TimeDomain = session_.FrameBase,
                    FrameRangeCovered = session_.MinFrame != int.MaxValue && session_.MaxFrame != int.MinValue
                        ? new[] { session_.MinFrame, session_.MaxFrame }
                        : null
                };

                var run = RecorderQueryCompiler.Run(source, savedPath, new RecorderQueryContext(session_), logger);

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

                var reflector = ReflectorFactory.Instance ?? throw new Exception("Reflector is not available.");
                return new ToolRunEnvelope
                {
                    Status = "executed",
                    DurationMs = stopwatch.ElapsedMilliseconds,
                    ArtifactPath = savedPath,
                    Result = reflector.Serialize(obj: result, logger: logger),
                    Meta = meta
                };
            });
        }
    }
}
