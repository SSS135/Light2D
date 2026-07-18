#nullable enable
using System;
using System.Linq;
using System.Reflection;
using com.SSS135.EditorAutomation.Editor.Framework.Util;
using Microsoft.Extensions.Logging;

namespace com.SSS135.EditorAutomation.Editor.QueryEngine
{
    /// <summary>Outcome of compiling + running one <c>recorder-query</c> C# snippet.</summary>
    public sealed class RecorderQueryRunResult
    {
        public bool Compiled;
        public string[]? Diagnostics;   // file-relative, only when Compiled == false
        public object? Value;           // the snippet's return value, when Compiled == true and no throw
        public string? RuntimeError;    // exception summary when the snippet threw at runtime
    }

    /// <summary>
    /// Wraps a small C# snippet body into <c>object Run(RecorderQueryContext q)</c>, compiles it via
    /// the shared <see cref="RoslynCompiler"/>, and invokes it. The tool owns persistence (the wrapped
    /// source is written to <c>Library/McpQueries/Recorder/</c> before compiling, so diagnostics are
    /// file-relative); this class owns wrap + compile + reflect + invoke. Posture is lightweight — same
    /// full reference set / same blast radius as <c>script-execute</c>; the read-only constraint is the
    /// <see cref="RecorderQueryContext"/> facade, not a sandbox.
    /// </summary>
    public static class RecorderQueryCompiler
    {
        const string ClassName = "__RecorderQuery";
        const string MethodName = "Run";
        const string AssemblyName = "RecorderQueryAssembly";

        // Fence markers around the user body so a caller editing the persisted file via filePath can
        // find their snippet without scrolling past wrapper boilerplate. Plain `//` — inert to Roslyn.
        const string AgentCodeStartFence = "        // >>> AGENT CODE START";
        const string AgentCodeEndFence = "        // <<< AGENT CODE END";

        /// <summary>
        /// Wraps a user-supplied method <paramref name="queryBody"/> into the fully-compilable fenced
        /// source for <c>__RecorderQuery.Run(q)</c>. The result is what the tool persists (under its
        /// description header) and then compiles.
        /// </summary>
        public static string WrapBody(string queryBody)
        {
            return
                "using System;\n" +
                "using System.Collections;\n" +
                "using System.Collections.Generic;\n" +
                "using System.Linq;\n" +
                "using UnityEngine;\n" +
                "using com.SSS135.EditorAutomation.Recorder;\n" +
                "using com.SSS135.EditorAutomation.Editor.QueryEngine;\n" +
                "public static class " + ClassName + " {\n" +
                "    public static object " + MethodName +
                    "(com.SSS135.EditorAutomation.Editor.QueryEngine.RecorderQueryContext q) {\n" +
                AgentCodeStartFence + "\n" +
                queryBody + "\n" +
                AgentCodeEndFence + "\n" +
                "    }\n}\n";
        }

        /// <summary>
        /// Compiles the already-persisted <paramref name="source"/> (passing <paramref name="savedPath"/>
        /// so diagnostics are file-relative), reflects the fixed <c>__RecorderQuery.Run</c> entry point,
        /// and invokes it with <paramref name="context"/>. Used by both inline mode (source is the
        /// freshly-wrapped body) and <c>filePath</c> mode (source is the file read verbatim).
        /// </summary>
        public static RecorderQueryRunResult Run(string source, string savedPath, RecorderQueryContext context, ILogger? logger = null)
        {
            var (assembly, diagnostics) = RoslynCompiler.Compile(source, AssemblyName, savedPath, logger);
            if (assembly == null)
                return new RecorderQueryRunResult { Compiled = false, Diagnostics = diagnostics?.ToArray() };

            var method = assembly.GetType(ClassName)?.GetMethod(MethodName)
                ?? throw new Exception(
                    $"Entry point '{ClassName}.{MethodName}' not found — was this file created by recorder-query? ({savedPath})");

            try
            {
                var value = method.Invoke(null, new object[] { context });
                return new RecorderQueryRunResult { Compiled = true, Value = value };
            }
            catch (TargetInvocationException ex)
            {
                var inner = ex.InnerException ?? ex;
                return new RecorderQueryRunResult { Compiled = true, RuntimeError = $"{inner.GetType().Name}: {inner.Message}" };
            }
        }
    }
}
