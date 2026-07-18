#nullable enable
using System.ComponentModel;
using com.IvanMurzak.ReflectorNet.Model;

namespace com.SSS135.EditorAutomation.Editor.API
{
    /// <summary>
    /// Shared response shape for the three "compile user C# then run it" tools
    /// (<c>script.execute</c>, <c>recorder-query</c>, <c>profiler-query</c>). Optional
    /// fields are null when not relevant to the branch so unused keys serialize away.
    /// </summary>
    public sealed class ToolRunEnvelope
    {
        [Description("Outcome of the run: \"executed\" | \"compile_failed\" | \"runtime_error\".")]
        public string Status { get; set; } = "";

        [Description("Wall-clock duration of the invocation, in milliseconds.")]
        public long DurationMs { get; set; }

        [Description("Path to the persisted .cs artifact (script or query) — edit it and re-run via filePath.")]
        public string? ArtifactPath { get; set; }

        [Description("Compiler errors (file-relative line:col); set only when Status is \"compile_failed\".")]
        public string[]? Diagnostics { get; set; }

        [Description("Runtime exception summary \"{Type}: {Message}\\n{StackTrace}\"; set only when Status is \"runtime_error\".")]
        public string? RuntimeError { get; set; }

        [Description("Number of editor log entries captured during the run; script.execute only.")]
        public int? LogCount { get; set; }

        [Description("Pre-serialized return value of the invoked code; null on void return or error.")]
        public SerializedMember? Result { get; set; }

        [Description("Self-describing query metadata (units, time domain, truncation); query tools only.")]
        public EnvelopeMeta? Meta { get; set; }
    }
}
