#nullable enable
using System;
using System.Linq;
using System.Reflection;
using com.SSS135.EditorAutomation.Editor.Framework.Util;
using Microsoft.Extensions.Logging;

namespace com.SSS135.EditorAutomation.Editor.QueryEngine
{
    /// <summary>Outcome of compiling + running one <c>profiler-query</c> C# snippet.</summary>
    public sealed class ProfilerQueryRunResult
    {
        public bool Compiled;
        public string[]? Diagnostics;   // file-relative, only when Compiled == false
        public object? Value;           // the snippet's return value, when Compiled == true and no throw
        public string? RuntimeError;    // exception message when the snippet threw at runtime
    }

    /// <summary>
    /// Wraps a <c>profiler-query</c> body into a compilable <c>object __ProfilerQuery.Run(q)</c>
    /// source and runs the compiled entry point against a <see cref="ProfilerQueryContext"/>.
    /// Compilation routes through the shared <see cref="RoslynCompiler"/> (same reference set /
    /// same blast radius as <c>script-execute</c>); the read-only constraint is the
    /// <see cref="ProfilerQueryContext"/> facade, not a sandbox. Persistence of the snippet file
    /// lives in the tool layer — this type only wraps, compiles, reflects, and invokes.
    /// </summary>
    public static class ProfilerQueryCompiler
    {
        const string ClassName = "__ProfilerQuery";
        const string MethodName = "Run";

        // Fence markers around the user body so the caller can edit-in-place via filePath without
        // scrolling past wrapper boilerplate. Plain `//` comments — invisible to the compiler.
        const string AgentCodeStartFence = "        // >>> AGENT CODE START";
        const string AgentCodeEndFence = "        // <<< AGENT CODE END";

        /// <summary>
        /// Wraps the user <paramref name="queryBody"/> into the fully-compilable fenced source
        /// (<c>public static class __ProfilerQuery { public static object Run(q) { … } }</c>).
        /// The returned text is what the tool persists and compiles, so the saved file stays
        /// byte-identical to the compiled source.
        /// </summary>
        public static string WrapBody(string queryBody)
            => "using System;\n" +
               "using System.Collections;\n" +
               "using System.Collections.Generic;\n" +
               "using System.Linq;\n" +
               "using UnityEngine;\n" +
               "using UnityEditor;\n" +
               "using UnityEditor.Profiling;\n" +
               "using UnityEditorInternal;\n" +
               "using com.SSS135.EditorAutomation.Editor.QueryEngine;\n" +
               "public static class " + ClassName + " {\n" +
               "    public static object " + MethodName +
                   "(com.SSS135.EditorAutomation.Editor.QueryEngine.ProfilerQueryContext q) {\n" +
               AgentCodeStartFence + "\n" +
               queryBody + "\n" +
               AgentCodeEndFence + "\n" +
               "    }\n}\n";

        /// <summary>
        /// Compiles <paramref name="code"/> (already the full wrapped source, header included)
        /// with <paramref name="savedPath"/> as the parse path so diagnostics are file-relative,
        /// then reflects + invokes <c>__ProfilerQuery.Run(q)</c> against <paramref name="context"/>.
        /// Non-throwing: a runtime exception surfaces in <see cref="ProfilerQueryRunResult.RuntimeError"/>.
        /// </summary>
        public static ProfilerQueryRunResult Run(string code, string savedPath, ProfilerQueryContext context, ILogger? logger = null)
        {
            var (assembly, diagnostics) = RoslynCompiler.Compile(code, "ProfilerQueryAssembly", savedPath, logger);
            if (assembly == null)
                return new ProfilerQueryRunResult { Compiled = false, Diagnostics = diagnostics?.ToArray() };

            var method = assembly.GetType(ClassName)?.GetMethod(MethodName)
                ?? throw new Exception(
                    "Profiler query entry point not found — was this file created by profiler-query? " +
                    $"Expected a '{ClassName}.{MethodName}(ProfilerQueryContext)' method.");

            try
            {
                var value = method.Invoke(null, new object[] { context });
                return new ProfilerQueryRunResult { Compiled = true, Value = value };
            }
            catch (TargetInvocationException ex)
            {
                var inner = ex.InnerException ?? ex;
                return new ProfilerQueryRunResult { Compiled = true, RuntimeError = $"{inner.GetType().Name}: {inner.Message}\n{inner.StackTrace}" };
            }
        }
    }
}
