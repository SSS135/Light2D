#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Editor.Framework.Util;

namespace com.SSS135.EditorAutomation.Editor.Framework.Builder
{
    /// <summary>
    /// Pure-metadata descriptor for a single discovered tool method. Carries the declaring class,
    /// the <see cref="MethodInfo"/>, the parsed <see cref="McpToolAttribute"/>, and the collapsed
    /// <see cref="McpToolHints"/> bag. No instances are created at scan time — the registry
    /// turns descriptors into <c>IMcpTool</c> instances during <see cref="ToolRegistry.Rebuild"/>.
    /// </summary>
    public sealed class ToolDescriptor
    {
        public Type ClassType { get; }
        public MethodInfo MethodInfo { get; }
        public McpToolAttribute Attribute { get; }
        public McpToolHints Hints { get; }
        // Snapshotted at scan time so the dispatcher reads it without re-reflecting the attribute.
        // Routed alongside Hints (not through McpToolHints) because this is an internal threading
        // concern, not part of the MCP protocol hint surface.
        public bool MainThreadRequired { get; }

        public ToolDescriptor(Type classType, MethodInfo methodInfo, McpToolAttribute attribute, McpToolHints hints, bool mainThreadRequired)
        {
            ClassType = classType;
            MethodInfo = methodInfo;
            Attribute = attribute;
            Hints = hints;
            MainThreadRequired = mainThreadRequired;
        }
    }

    /// <summary>
    /// Walks every assembly returned by <see cref="AssemblyEnumerator.Enumerate()"/>, finds types
    /// marked with <c>[McpToolType]</c>, and emits one <see cref="ToolDescriptor"/> per method
    /// inside such a type that carries an <c>[McpTool]</c>. Idempotent and pure — calling
    /// <see cref="Scan"/> twice with the same loaded assemblies returns the same descriptors.
    /// </summary>
    public static class AttributeScanner
    {
        const BindingFlags MethodBindingFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        /// <summary>
        /// Scan every non-ignored loaded assembly and return one descriptor per <c>[McpTool]</c>-
        /// marked method declared inside an <c>[McpToolType]</c>-marked class.
        /// </summary>
        public static IReadOnlyList<ToolDescriptor> Scan()
            => Scan(AssemblyEnumerator.Enumerate());

        /// <summary>
        /// Scan an explicit assembly set. Used by callers that have already filtered the assembly
        /// list (e.g. tests). Some Unity / netstandard hybrid assemblies emit
        /// <see cref="ReflectionTypeLoadException"/> without a clean way to recover; the scanner
        /// keeps whatever types loaded successfully (via <see cref="ClassifyLoadedTypes"/>) and
        /// logs a warning naming the failed count, so a thin snapshot is never silent. Assemblies
        /// whose <see cref="Assembly.GetTypes"/> fails outright are skipped, also with a warning.
        /// </summary>
        public static IReadOnlyList<ToolDescriptor> Scan(IEnumerable<Assembly> assemblies)
        {
            var descriptors = new List<ToolDescriptor>();
            foreach (var assembly in assemblies)
            {
                if (assembly == null)
                    continue;

                IReadOnlyList<Type> types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException rtle)
                {
                    types = ClassifyLoadedTypes(rtle.Types, rtle, out var failedTypeCount);
                    if (failedTypeCount > 0)
                        UnityEngine.Debug.LogWarning(
                            $"[UnityMCP] AttributeScanner: assembly '{assembly.GetName().Name}' partially loaded — " +
                            $"{failedTypeCount} type(s) failed to load and were skipped. The tool registry snapshot may be thin.");
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[UnityMCP] AttributeScanner: assembly '{assembly.GetName().Name}' could not be scanned and was skipped " +
                        $"({ex.GetType().Name}: {ex.Message}). The tool registry snapshot may be thin.");
                    continue;
                }

                foreach (var type in types)
                {
                    if (type == null)
                        continue;

                    if (!Attribute.IsDefined(type, typeof(McpToolTypeAttribute), inherit: false))
                        continue;

                    foreach (var method in type.GetMethods(MethodBindingFlags))
                    {
                        if (!Attribute.IsDefined(method, typeof(McpToolAttribute), inherit: false))
                            continue;

                        var attr = (McpToolAttribute?)Attribute.GetCustomAttribute(method, typeof(McpToolAttribute), inherit: false);
                        if (attr == null)
                            continue;

                        descriptors.Add(new ToolDescriptor(type, method, attr, attr.GetHints(), attr.MainThreadRequired));
                    }
                }
            }
            return descriptors;
        }

        /// <summary>
        /// Classifies the outcome of an <see cref="Assembly.GetTypes"/> call that threw
        /// <see cref="ReflectionTypeLoadException"/>. Returns the non-null types that loaded
        /// successfully and reports, via <paramref name="failedTypeCount"/>, how many entries
        /// failed to load (null slots in <paramref name="loadedTypes"/>, plus any extra failures
        /// recorded on the exception's <c>LoaderExceptions</c> that aren't represented by a null
        /// slot). Pure and assembly-free so it can be exercised in isolation by tests.
        /// </summary>
        internal static IReadOnlyList<Type> ClassifyLoadedTypes(Type?[]? loadedTypes, ReflectionTypeLoadException? loadException, out int failedTypeCount)
        {
            var loaded = new List<Type>();
            var nullSlots = 0;
            if (loadedTypes != null)
            {
                foreach (var type in loadedTypes)
                {
                    if (type == null)
                        nullSlots++;
                    else
                        loaded.Add(type);
                }
            }

            // A null slot in Types lines up with a LoaderException, but some runtimes report
            // loader failures without a matching null slot — take the larger of the two so the
            // partial-load signal never under-counts.
            var loaderFailures = loadException?.LoaderExceptions?.Length ?? 0;
            failedTypeCount = Math.Max(nullSlots, loaderFailures);
            return loaded;
        }
    }
}
