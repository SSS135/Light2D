#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using com.IvanMurzak.ReflectorNet.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;

namespace com.SSS135.EditorAutomation.Editor.Framework.Util
{
    /// <summary>
    /// Shared Roslyn front end for the "compile user C# then run it" tools (<c>script.execute</c>,
    /// <c>recorder-query</c>, <c>profiler-query</c>). Owns the metadata-reference policy and the
    /// parse/emit/load step; each caller keeps its own <c>GetType</c>/<c>GetMethod</c>/<c>Invoke</c>
    /// reflection on the returned assembly.
    /// </summary>
    public static class RoslynCompiler
    {
        /// <summary>
        /// Builds the metadata references for dynamic compilation: every loaded, non-dynamic
        /// assembly with a file location. Per-assembly failures are logged (when a logger is
        /// supplied) and skipped rather than aborting the whole compilation.
        /// </summary>
        public static MetadataReference[] BuildReferences(ILogger? logger = null)
            => AssemblyUtils.AllAssemblies
                .Where(a => !a.IsDynamic) // Exclude dynamic assemblies
                .Where(a => !string.IsNullOrEmpty(a.Location))
                .Select(a =>
                {
                    try
                    {
                        return MetadataReference.CreateFromFile(a.Location);
                    }
                    catch (DirectoryNotFoundException ex)
                    {
                        logger?.LogWarning(ex, "Directory not found for assembly '{AssemblyName}' at '{Location}': {Error}",
                            a.GetName().Name, a.Location, ex.Message);
                        return null;
                    }
                    catch (FileNotFoundException ex)
                    {
                        logger?.LogWarning(ex, "File not found for assembly '{AssemblyName}' at '{Location}': {Error}",
                            a.GetName().Name, a.Location, ex.Message);
                        return null;
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "Failed to load metadata reference for assembly '{AssemblyName}' at '{Location}': {Error}",
                            a.GetName().Name, a.Location, ex.Message);
                        return null;
                    }
                })
                .OfType<MetadataReference>()
                .ToArray();

        /// <summary>
        /// Compiles <paramref name="code"/> into an in-memory assembly. On success returns the loaded
        /// <see cref="Assembly"/> with a null diagnostics list; on failure returns a null assembly and
        /// the errors-only diagnostic strings. Passing <paramref name="savedPath"/> as the parse path
        /// makes Roslyn diagnostics file-relative — <c>Foo.cs(17,9): error CS0103: ...</c> — so the
        /// caller can resolve the offending location into the persisted file directly.
        /// </summary>
        public static (Assembly? assembly, IReadOnlyList<string>? diagnostics) Compile(
            string code,
            string assemblyName,
            string savedPath,
            ILogger? logger = null)
        {
            var compilation = CSharpCompilation.Create(
                assemblyName: assemblyName,
                syntaxTrees: new[] { CSharpSyntaxTree.ParseText(code, path: savedPath) },
                references: BuildReferences(logger),
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var ms = new MemoryStream();
            var emitResult = compilation.Emit(ms);
            if (!emitResult.Success)
            {
                var errors = emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.ToString())
                    .ToList();
                return (null, errors);
            }

            ms.Seek(0, SeekOrigin.Begin);
            var assembly = Assembly.Load(ms.ToArray());
            return (assembly, null);
        }
    }
}
