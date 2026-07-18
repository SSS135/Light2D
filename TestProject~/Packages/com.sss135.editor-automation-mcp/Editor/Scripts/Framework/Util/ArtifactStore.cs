#nullable enable
using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace com.SSS135.EditorAutomation.Editor.Framework.Util
{
    /// <summary>
    /// Shared persistence helpers for the "compile user C# then run it" tools
    /// (<c>script.execute</c>, <c>recorder-query</c>, <c>profiler-query</c>). Each persists a
    /// caller-named, durable <c>.cs</c> snippet under <c>Library/</c> (outside Unity's compiled
    /// paths; survives Editor restart) so callers can edit-in-place and re-invoke via a file path
    /// instead of resubmitting the full source on every iteration.
    /// </summary>
    public static class ArtifactStore
    {
        // Cross-platform invalid file name characters.
        // Path.GetInvalidFileNameChars() is OS-dependent (Linux/Mac only returns '/' and '\0'),
        // but Unity projects must be portable across all platforms. Lives here (Framework) rather
        // than on the API-layer tools so lower layers can reference it without backwards layering.
        public static readonly char[] InvalidFileNameChars = new[]
        {
            '/', '\\', '<', '>', ':', '"', '|', '?', '*',
            '\0', '\x01', '\x02', '\x03', '\x04', '\x05', '\x06', '\x07',
            '\x08', '\x09', '\x0A', '\x0B', '\x0C', '\x0D', '\x0E', '\x0F',
            '\x10', '\x11', '\x12', '\x13', '\x14', '\x15', '\x16', '\x17',
            '\x18', '\x19', '\x1A', '\x1B', '\x1C', '\x1D', '\x1E', '\x1F'
        };

        /// <summary>
        /// The Unity project root — the directory containing <c>Assets/</c> and <c>Library/</c>.
        /// </summary>
        public static string ProjectRoot => Path.GetDirectoryName(Application.dataPath)!;

        /// <summary>
        /// Validates a caller-supplied artifact file name and returns the canonical
        /// <c>&lt;name&gt;&lt;requiredExtension&gt;</c>. Normalizes the extension so "Foo", "Foo.cs",
        /// and "Foo.CS" all map to "Foo.cs" (when requiredExtension is ".cs"), and rejects directory
        /// separators / other invalid characters via the shared portable list — that single check also
        /// blocks path traversal, since "../" and "..\" both contain a separator.
        /// </summary>
        public static string SanitizeFileName(string name, string requiredExtension)
        {
            var trimmed = name.Trim();
            var bare = trimmed.EndsWith(requiredExtension, StringComparison.OrdinalIgnoreCase)
                ? trimmed.Substring(0, trimmed.Length - requiredExtension.Length)
                : trimmed;
            if (string.IsNullOrWhiteSpace(bare) || bare == "." || bare == "..")
                throw new Exception($"file name is not valid: '{name}'.");
            var invalidIndex = bare.IndexOfAny(InvalidFileNameChars);
            if (invalidIndex >= 0)
                throw new Exception($"file name contains invalid character '{bare[invalidIndex]}': '{name}'.");
            return bare + requiredExtension;
        }

        /// <summary>
        /// Builds the <c>//</c>-comment header from the caller's description. One comment line per
        /// input line followed by a blank separator line; intended to be prepended to the top of the
        /// saved file (above the usings) — a leading comment block is legal C# and inert to Roslyn.
        /// </summary>
        public static string BuildDescriptionHeader(string description)
        {
            var sb = new StringBuilder();
            foreach (var line in description.Split('\n'))
                sb.AppendLine($"// {line.TrimEnd()}");
            sb.AppendLine();
            return sb.ToString();
        }

        /// <summary>
        /// Persists <paramref name="contents"/> to <c>{projectRoot}/Library/{subdir}/{fileName}</c>
        /// and returns the absolute path, overwriting any existing file with that name. Library/
        /// survives Editor restarts (unlike Temp/), so named artifacts persist as a reusable library.
        /// </summary>
        public static string PersistNamedArtifact(string subdir, string fileName, string contents)
        {
            var dir = Path.Combine(ProjectRoot, "Library", subdir);
            Directory.CreateDirectory(dir);

            var fullPath = Path.Combine(dir, fileName);
            File.WriteAllText(fullPath, contents);
            return fullPath;
        }
    }
}
