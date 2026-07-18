#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using com.IvanMurzak.ReflectorNet.Model;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Editor.Framework.Util;
using com.SSS135.EditorAutomation.Utils;
using Microsoft.Extensions.Logging;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public static partial class Tool_Script
    {
        public const string ScriptExecuteToolId = "script-execute";

        // Fence markers inserted around the user body in body-mode wraps so the caller can
        // edit-in-place via filePath without scrolling past wrapper boilerplate. The fences
        // are plain `//` comments — invisible to the compiler.
        const string AgentCodeStartFence = "        // >>> AGENT CODE START";
        const string AgentCodeEndFence = "        // <<< AGENT CODE END";

        [McpTool
        (
            ScriptExecuteToolId,
            Title = "Script / Execute",
            OpenWorldHint = true
        )]
        [Description("Compiles and executes C# code dynamically using Roslyn. " +
            "Inline-code calls are persisted to '{projectRoot}/Library/McpScripts/<fileName>.cs' " +
            "(outside Unity's compiled paths; survives Editor restart) under the caller-supplied " +
            "`fileName`, overwriting any existing file with that name. The response is a structured " +
            "`ToolRunEnvelope` whose `ArtifactPath` points at that file so callers can edit-in-place " +
            "and re-invoke via the `filePath` parameter instead of resubmitting the full source on " +
            "iteration. " +
            "Supports two input modes: inline code (`csharpCode`, full or body-only) OR a previously " +
            "saved file (`filePath`). Exactly one of those must be provided. " +
            "`fileName` and `description` are required with `csharpCode` and prohibited with `filePath`. " +
            "Both compile failures and runtime errors are returned structured rather than thrown: " +
            "`Status` is \"executed\" | \"compile_failed\" | \"runtime_error\", with `Diagnostics` set " +
            "on compile failure and `RuntimeError` (\"{Type}: {Message}\\n{StackTrace}\") set on a " +
            "runtime error; `ArtifactPath` is always populated so the file can be edited and re-run. " +
            "Unity objects (GameObject, Component, etc.) can be passed as parameters using their " +
            "Ref types (GameObjectRef, ComponentRef, etc.) or directly by type.")]
        public static ToolRunEnvelope Execute
        (
            [Description("C# code to compile and execute. Mutually exclusive with `filePath` — supply " +
                "exactly one. " +
                "In full code mode (default, isMethodBody=false): must define a complete class with a static method. " +
                "Example: 'using UnityEngine; public class Script { public static void Main() { Debug.Log(\"Hello\"); } }'. " +
                "Do NOT use top-level statements. " +
                "In body-only mode (isMethodBody=true): provide only the method body statements. " +
                "The tool auto-generates usings, class, and method header. " +
                "Example body: 'go.SetActive(false);'. " +
                "Leading `using ...;` directives at the top of the body are lifted automatically to file scope, " +
                "so body-only callers can declare namespace aliases or extra imports without falling back to full code mode. " +
                "Custom helper classes can still be defined inline in the body-only string after the main logic, " +
                "but for complex additional class definitions use full code mode instead.")]
            string? csharpCode = null,
            [Description("File name for the persisted script under 'Library/McpScripts/'. " +
                "A '.cs' extension is appended if omitted ('Foo', 'Foo.cs' both save as 'Foo.cs'). " +
                "If a file with this name already exists it is overwritten. " +
                "Must be a bare file name — directory separators and other invalid characters are rejected. " +
                "REQUIRED when 'csharpCode' is provided; must be omitted when 'filePath' is provided.")]
            string? fileName = null,
            [Description("Short summary of what the script does, written as a '//' comment header at the " +
                "top of the saved file. REQUIRED when 'csharpCode' is provided; must be omitted when " +
                "'filePath' is provided.")]
            string? description = null,
            [Description("The name of the class containing the method to execute. " +
                "In body-only mode this becomes the generated class name.")]
            string className = "Script",
            [Description("The name of the method to execute. Must be a static method. " +
                "In body-only mode this becomes the generated method name.")]
            string methodName = "Main",
            [Description("Serialized parameters to pass to the method. Each entry must specify 'name' and 'typeName'. " +
                "Supported parameter types include primitives, strings, and Unity object references: " +
                "- 'UnityEngine.GameObject': resolves an actual GameObject from value '{\"instanceID\": N}', '{\"name\": \"...\"}', or '{\"path\": \"...\"}'. " +
                "- 'UnityEngine.Component' (or any component subtype): resolves from '{\"instanceID\": N}'. " +
                "- 'EA.GameObjectRef': passes a GameObjectRef POCO directly; " +
                "  the method body calls goRef.FindGameObject() to resolve it. " +
                "- 'EA.ComponentRef': passes a ComponentRef POCO. " +
                "- 'EA.ObjectRef': passes a base ObjectRef POCO. " +
                "If the method does not require parameters, leave this empty.")]
            SerializedMemberList? parameters = null,
            [Description("When true, 'csharpCode' is treated as just the method body. " +
                "The tool auto-generates standard using directives (System, UnityEngine, " +
                "EA, com.SSS135.EditorAutomation.Runtime.Extensions, UnityEditor), " +
                "the class definition, and the method signature (void return type). " +
                "Parameters from the 'parameters' list are automatically added to the method signature using their typeName and name. " +
                "When false (default), 'csharpCode' must be a complete C# compilation unit with class and method definitions. " +
                "Ignored when `filePath` is provided (the file is already in final compiled form) — passing isMethodBody=true with filePath is rejected.")]
            bool isMethodBody = false,
            [Description("Absolute path to a previously persisted script file (typically the `ArtifactPath` returned by an earlier call). " +
                "Mutually exclusive with `csharpCode`, `fileName`, and `description`. When supplied, the file content is compiled " +
                "verbatim — no wrapping, no `using` lifting, no description header added (the file already carries its own). " +
                "Use this to iterate on a script that previously compile-failed: read `ArtifactPath` from " +
                "the failure response, edit the file in place to fix the diagnostic, then re-invoke with the same path.")]
            string? filePath = null
        )
        {
            // Mutual exclusion: exactly one of (csharpCode, filePath) must be supplied.
            var hasInlineCode = !string.IsNullOrEmpty(csharpCode);
            var hasFilePath = !string.IsNullOrEmpty(filePath);
            if (hasInlineCode == hasFilePath)
                throw new Exception("Exactly one of 'csharpCode' or 'filePath' must be provided.");
            if (hasFilePath && isMethodBody)
                throw new Exception("'isMethodBody' must be false when 'filePath' is provided — the file is already the final compiled form.");

            // fileName/description belong to inline mode only: they name + document the persisted file.
            // In filePath mode the target file already exists (and carries its own header), so both are
            // rejected. resolvedFileName is the validated `<name>.cs` used to persist in inline mode.
            string? resolvedFileName = null;
            if (hasFilePath)
            {
                if (!string.IsNullOrEmpty(fileName))
                    throw new Exception("'fileName' is not allowed when 'filePath' is provided.");
                if (!string.IsNullOrEmpty(description))
                    throw new Exception("'description' is not allowed when 'filePath' is provided.");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(fileName))
                    throw new Exception("'fileName' is required when 'csharpCode' is provided.");
                if (string.IsNullOrWhiteSpace(description))
                    throw new Exception("'description' is required when 'csharpCode' is provided.");
                resolvedFileName = ArtifactStore.SanitizeFileName(fileName!, ".cs");
            }

            if (string.IsNullOrEmpty(className))
                throw new Exception($"'{nameof(className)}' cannot be null or empty.");

            if (string.IsNullOrEmpty(methodName))
                throw new Exception($"'{nameof(methodName)}' cannot be null or empty.");

            return MainThread.Instance.Run(() =>
            {
                var logger = UnityLoggerFactory.LoggerFactory.CreateLogger("Tool_Script.Execute");

                // Determine (codeToCompile, savedPath). filePath mode reads the file as the final
                // compiled source; inline modes build the final form (wrapped for body, raw for full),
                // prepend the description header, and persist it under Library/McpScripts/ so the caller
                // can edit-and-rerun via filePath.
                string codeToCompile;
                string savedPath;
                if (hasFilePath)
                {
                    if (!File.Exists(filePath))
                        throw new Exception($"'filePath' does not exist: {filePath}");
                    codeToCompile = File.ReadAllText(filePath!);
                    savedPath = Path.GetFullPath(filePath!);
                }
                else
                {
                    var body = isMethodBody
                        ? GenerateFullCode(csharpCode!, className, methodName, parameters)
                        : csharpCode!;
                    // Header goes above everything (including usings) — a leading comment block is legal
                    // C# and inert to Roslyn, so the saved file stays byte-identical to the compiled source.
                    codeToCompile = ArtifactStore.BuildDescriptionHeader(description!) + body;
                    savedPath = ArtifactStore.PersistNamedArtifact("McpScripts", resolvedFileName!, codeToCompile);
                }

                // Sanity check: requested class/method must appear textually in the compiled source.
                // For body mode this is automatic (GenerateFullCode emits both); for full/file modes,
                // a typo here surfaces faster than letting Roslyn complain at the reflection step.
                if (!codeToCompile.Contains(className))
                    throw new Exception($"Compiled code does not contain class '{className}' (source: {savedPath}).");
                if (!codeToCompile.Contains(methodName))
                    throw new Exception($"Compiled code does not contain method '{methodName}' (source: {savedPath}).");

                // Scope a log counter + stopwatch around compile+invoke so the void-success branch
                // can return a confirmation envelope. Counts every Log/Warning/Error/Exception — point
                // is "did anything print?". Non-threaded variant is enough — already on main thread.
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var logCount = 0;
                UnityEngine.Application.LogCallback handler = (condition, stackTrace, type) => logCount++;
                UnityEngine.Application.logMessageReceived += handler;
                try
                {
                    var reflector = ReflectorFactory.Instance ?? throw new Exception("Reflector is not available.");

                    var (assembly, diagnostics) = RoslynCompiler.Compile(codeToCompile, "DynamicAssembly", savedPath, logger);
                    if (assembly == null)
                    {
                        // Structured compile-failure envelope (does not throw). Caller reads ArtifactPath,
                        // edits the file, re-invokes script.execute with filePath=<ArtifactPath>.
                        return new ToolRunEnvelope
                        {
                            Status = "compile_failed",
                            DurationMs = stopwatch.ElapsedMilliseconds,
                            ArtifactPath = savedPath,
                            Diagnostics = diagnostics?.ToArray()
                        };
                    }

                    var type = assembly.GetType(className)
                        ?? throw new Exception($"Class '{className}' not found in the compiled assembly.");
                    var method = type.GetMethod(methodName)
                        ?? throw new Exception($"Method '{methodName}' not found in class '{className}'.");

                    var parsedParameters = parameters
                        ?.Select(p => reflector.Deserialize(data: p, logger: logger))
                        ?.ToArray();

                    object? result;
                    try
                    {
                        result = method.Invoke(null, parsedParameters);
                    }
                    catch (Exception ex)
                    {
                        // Runtime errors return structured (do not throw). Unwrap the reflection wrapper
                        // so callers see the user code's actual exception type + stack.
                        var inner = (ex as TargetInvocationException)?.InnerException ?? ex;
                        return new ToolRunEnvelope
                        {
                            Status = "runtime_error",
                            DurationMs = stopwatch.ElapsedMilliseconds,
                            ArtifactPath = savedPath,
                            RuntimeError = $"{inner.GetType().Name}: {inner.Message}\n{inner.StackTrace}"
                        };
                    }

                    return new ToolRunEnvelope
                    {
                        Status = "executed",
                        DurationMs = stopwatch.ElapsedMilliseconds,
                        ArtifactPath = savedPath,
                        LogCount = logCount,
                        // Pre-serialize the return value with ReflectorNet (void → null) so the outer
                        // envelope stays primitives + one already-serialized member; letting the
                        // framework's System.Text.Json path serialize a raw UnityEngine.Object would throw.
                        Result = result is null ? null : reflector.Serialize(obj: result, logger: logger)
                    };
                }
                finally
                {
                    UnityEngine.Application.logMessageReceived -= handler;
                    stopwatch.Stop();
                }
            });
        }

        static string GenerateFullCode(
            string methodBody,
            string className,
            string methodName,
            SerializedMemberList? parameters)
        {
            var sb = new StringBuilder();

            // Standard using directives
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using UnityEngine;");
            // UnityEngine.UI ships in com.unity.ugui — only emit the using when its assembly
            // is loaded; otherwise Roslyn fails with CS0234 when the package isn't installed.
            if (IsAssemblyLoaded("UnityEngine.UI"))
                sb.AppendLine("using UnityEngine.UI;");
            sb.AppendLine("using UnityEngine.SceneManagement;");
            sb.AppendLine("using EA;");
            sb.AppendLine("using com.SSS135.EditorAutomation.Runtime.Extensions;");
            sb.AppendLine("using UnityEditor;");

            // Lift any leading `using ...;` directives out of the user-supplied body so they land
            // at file scope; C# would otherwise parse them as using-statements inside the method.
            var (extraUsings, strippedBody) = ExtractLeadingUsings(methodBody);
            foreach (var usingLine in extraUsings)
                sb.AppendLine(usingLine);

            sb.AppendLine();

            // Build method parameter list from the parameters SerializedMemberList
            var methodParams = parameters != null && parameters.Count > 0
                ? string.Join(", ", parameters.Select(p => $"{p.typeName ?? "object"} {p.name ?? "param"}"))
                : "";

            sb.AppendLine($"public class {className}");
            sb.AppendLine("{");
            sb.AppendLine($"    public static void {methodName}({methodParams})");
            sb.AppendLine("    {");
            sb.AppendLine(AgentCodeStartFence);

            // Indent each line of the method body
            foreach (var line in strippedBody.Split('\n'))
                sb.AppendLine($"        {line.TrimEnd()}");

            sb.AppendLine(AgentCodeEndFence);
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        // Splits leading `using ...;` directives off `methodBody`. Walks contiguous non-blank lines
        // from the top, collecting trimmed `using ...;` directives; blank lines inside the lift
        // window are skipped silently. The first non-blank line that is not a `using ...;` ends
        // the lift, and everything from that line onward becomes the stripped body.
        static (List<string> Usings, string StrippedBody) ExtractLeadingUsings(string methodBody)
        {
            var usings = new List<string>();
            var lines = methodBody.Split('\n');
            var liftEnd = 0;

            for (var i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].Trim();
                if (trimmed.Length == 0)
                {
                    liftEnd = i + 1;
                    continue;
                }
                if (trimmed.StartsWith("using ") && trimmed.EndsWith(";"))
                {
                    usings.Add(trimmed);
                    liftEnd = i + 1;
                    continue;
                }
                break;
            }

            if (usings.Count == 0)
                return (usings, methodBody);

            var strippedBody = string.Join("\n", lines.Skip(liftEnd));
            return (usings, strippedBody);
        }

        static bool IsAssemblyLoaded(string assemblyName)
            => AssemblyUtils.AllAssemblies.Any(a =>
                string.Equals(a.GetName().Name, assemblyName, StringComparison.Ordinal));
    }
}
