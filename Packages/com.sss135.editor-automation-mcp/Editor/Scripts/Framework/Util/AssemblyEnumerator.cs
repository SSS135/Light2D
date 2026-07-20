#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;

namespace com.SSS135.EditorAutomation.Editor.Framework.Util
{
    /// <summary>
    /// Walks every loaded assembly in <see cref="AppDomain.CurrentDomain"/> and yields the ones
    /// the framework cares about — i.e. user / package assemblies that may carry
    /// <c>[McpToolType]</c>-marked classes. The ignore list mirrors the legacy
    /// <c>EditorAutomationPlugin.Build.cs:128-143</c> prefix list so post-migration behavior matches
    /// pre-migration scanning behavior.
    /// </summary>
    public static class AssemblyEnumerator
    {
        // Default ignore-prefix list. Matches the legacy EditorAutomationPlugin.Build.cs:128-143 list.
        // BCL + Unity engine / editor + Microsoft.Extensions + R3 + the vendored framework DLLs
        // + every test asmdef the plugin ships.
        static readonly List<string> _defaultIgnorePrefixes = new List<string>
        {
            "mscorlib",
            "Mono.",
            "Mono.Security",
            "netstandard",
            "System",
            "UnityEngine",
            "UnityEditor",
            "Unity.",
            "Microsoft",
            "R3",
            "McpPlugin",
            "ReflectorNet",
            "nunit.",
            "JetBrains.",
            "com.SSS135.EditorAutomation.TestFiles",
            "com.SSS135.EditorAutomation.Editor.Tests",
            "com.SSS135.EditorAutomation.Tests",
        };

        /// <summary>
        /// Returns every loaded assembly whose name does NOT start with one of the configured
        /// ignore prefixes. Iteration order matches <see cref="AppDomain.GetAssemblies"/>.
        /// </summary>
        public static IEnumerable<Assembly> Enumerate()
            => Enumerate(_defaultIgnorePrefixes);

        /// <summary>
        /// Returns every loaded assembly whose name does NOT start with one of the supplied
        /// ignore prefixes. Pass a custom prefix list to exclude additional assemblies (or to
        /// include something the default list filters out).
        /// </summary>
        public static IEnumerable<Assembly> Enumerate(IReadOnlyList<string> ignorePrefixes)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var asm in assemblies)
            {
                if (asm == null)
                    continue;

                var name = asm.GetName().Name;
                if (string.IsNullOrEmpty(name))
                    continue;

                if (StartsWithAny(name!, ignorePrefixes))
                    continue;

                yield return asm;
            }
        }

        /// <summary>
        /// Snapshot of the default ignore-prefix list. Callers can copy this list, append extra
        /// prefixes, and pass the result back to <see cref="Enumerate(IReadOnlyList{string})"/>.
        /// </summary>
        public static IReadOnlyList<string> DefaultIgnorePrefixes => _defaultIgnorePrefixes;

        static bool StartsWithAny(string assemblyName, IReadOnlyList<string> prefixes)
        {
            for (var i = 0; i < prefixes.Count; i++)
            {
                var prefix = prefixes[i];
                if (string.IsNullOrEmpty(prefix))
                    continue;
                if (assemblyName.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
    }
}
