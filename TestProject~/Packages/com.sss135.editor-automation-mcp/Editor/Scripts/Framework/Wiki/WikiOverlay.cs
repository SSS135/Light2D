#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor.PackageManager;

namespace com.SSS135.EditorAutomation.Editor.Framework.Wiki
{
    /// <summary>
    /// Editorial overlay loader for the wiki. Reads per-namespace markdown files at
    /// <c>&lt;package&gt;/Docs~/WikiSource/&lt;dotted-path&gt;.md</c> and extracts:
    /// <list type="bullet">
    /// <item>the prelude body (everything between the H1 / leading HTML comments and the
    /// first <c>### </c> heading), used for branch / leaf-namespace / hybrid / topic pages;</item>
    /// <item>per-method H3 sections keyed by the full method name, used for method pages.</item>
    /// </list>
    /// Files are cached by absolute path; cache entries are invalidated when the file
    /// mtime changes, so editing an overlay file picks up on the next call without an
    /// editor restart.
    ///
    /// Mirrors Unreal's <c>WikiOverlay.h</c> API shape (<c>LoadGroupPrelude</c> +
    /// <c>LoadMethodSection</c>) so the renderer integration is symmetric across projects.
    /// </summary>
    public static class WikiOverlay
    {
        const string PackageName = "com.sss135.editor-automation-mcp";
        const string WikiSubDirectory = "Docs~/WikiSource";

        // Per-file cache entry. Prelude + Sections are derived from the file body
        // (see ParseInto) and stay valid as long as the file's mtime is unchanged.
        sealed class CachedOverlay
        {
            public DateTime FileTimeStamp;
            public string Prelude = string.Empty;
            // Method full name → section body (everything below `### <method>` until the next `### ` or EOF).
            public Dictionary<string, string> Sections = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        // Cache keyed by absolute file path (case-insensitive on Windows; we lower-case the key
        // before lookup so divergent casings collapse to a single entry).
        static readonly object _lock = new object();
        static readonly Dictionary<string, CachedOverlay> _cache = new Dictionary<string, CachedOverlay>(StringComparer.Ordinal);

        // Resolved once. The package install path is stable for the lifetime of the Editor
        // process — Unity restarts on package add/remove/move — so a one-shot lookup is safe.
        static string? _wikiRootCached;
        static bool _wikiRootResolved;
        static string? _packageRootCached;
        static bool _packageRootResolved;

        /// <summary>
        /// Absolute filesystem path to the package install root (the directory that holds
        /// <c>package.json</c>), or null when the package cannot be resolved (cooked /
        /// non-editor builds). The wiki overlay source and the generated wiki output are both
        /// sub-directories of this root; the disk generator resolves its output dir from here so
        /// it shares this single source of truth instead of re-running the package lookup.
        /// </summary>
        public static string? PackageRoot
        {
            get
            {
                if (_packageRootResolved)
                    return _packageRootCached;
                lock (_lock)
                {
                    if (!_packageRootResolved)
                    {
                        _packageRootCached = ResolvePackageRoot();
                        _packageRootResolved = true;
                    }
                    return _packageRootCached;
                }
            }
        }

        /// <summary>
        /// Absolute filesystem path to the overlay directory
        /// (<c>&lt;package&gt;/Docs~/WikiSource</c>), or null when the package cannot be
        /// resolved (cooked / non-editor builds).
        /// </summary>
        public static string? WikiRoot
        {
            get
            {
                if (_wikiRootResolved)
                    return _wikiRootCached;
                lock (_lock)
                {
                    if (!_wikiRootResolved)
                    {
                        _wikiRootCached = ResolveWikiRoot();
                        _wikiRootResolved = true;
                    }
                    return _wikiRootCached;
                }
            }
        }

        /// <summary>
        /// Returns the prelude body for the overlay file at
        /// <c>&lt;package&gt;/Docs~/WikiSource/&lt;Namespace&gt;.md</c>.
        /// <list type="bullet">
        /// <item>The file's leading H1 line (<c># ...</c>) and any leading HTML comments / blank lines are stripped.</item>
        /// <item>Reading stops at the first <c>### </c> heading.</item>
        /// <item>Trailing whitespace is trimmed.</item>
        /// <item>Missing file / null namespace / empty body returns null.</item>
        /// </list>
        /// </summary>
        public static string? LoadGroupPrelude(string? ns)
        {
            if (string.IsNullOrEmpty(ns))
                return null;
            var abs = OverlayFilePath(ns!);
            if (abs == null)
                return null;
            var entry = GetOrLoad(abs);
            if (entry == null)
                return null;
            return string.IsNullOrEmpty(entry.Prelude) ? null : entry.Prelude;
        }

        /// <summary>
        /// Returns the body of the H3 section whose heading line is exactly
        /// <c>### &lt;FullMethodName&gt;</c> (case-sensitive on the method name; trailing whitespace tolerated).
        /// The enclosing file is derived from <paramref name="fullMethodName"/> by stripping the
        /// trailing leaf segment to yield the Category, then reading
        /// <c>&lt;package&gt;/Docs~/WikiSource/&lt;Category&gt;.md</c>.
        /// Returns null when the file is missing, the section is absent, the method has no
        /// namespace segment, or the body is empty.
        /// </summary>
        public static string? LoadMethodSection(string? fullMethodName)
        {
            if (string.IsNullOrEmpty(fullMethodName))
                return null;
            var dotIdx = fullMethodName!.LastIndexOf('.');
            if (dotIdx <= 0)
                return null; // un-namespaced method names have no overlay file
            var category = fullMethodName.Substring(0, dotIdx);
            var abs = OverlayFilePath(category);
            if (abs == null)
                return null;
            var entry = GetOrLoad(abs);
            if (entry == null)
                return null;
            return entry.Sections.TryGetValue(fullMethodName, out var body) && !string.IsNullOrEmpty(body)
                ? body
                : null;
        }

        /// <summary>
        /// Enumerate every overlay file's slug (filename without extension) under the wiki root.
        /// Slugs beginning with <c>README</c> (case-insensitive) are skipped. Returns an empty
        /// list when the wiki root cannot be resolved. Used by <c>ToolRegistry.Rebuild()</c> to
        /// build the topic-page registry — files whose slug matches a registered namespace or
        /// dotted method id are namespace overlays; the rest are standalone topic pages.
        /// </summary>
        public static IReadOnlyList<string> EnumerateOverlaySlugs()
        {
            var root = WikiRoot;
            if (root == null || !Directory.Exists(root))
                return Array.Empty<string>();

            try
            {
                var files = Directory.GetFiles(root, "*.md", SearchOption.TopDirectoryOnly);
                var slugs = new List<string>(files.Length);
                foreach (var file in files)
                {
                    var slug = Path.GetFileNameWithoutExtension(file);
                    if (string.IsNullOrEmpty(slug))
                        continue;
                    if (slug.StartsWith("README", StringComparison.OrdinalIgnoreCase))
                        continue;
                    slugs.Add(slug);
                }
                slugs.Sort(StringComparer.Ordinal);
                return slugs;
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Discard every cached overlay. Tests use this to start from a clean slate; the live
        /// wiki path doesn't need it because cache entries self-invalidate on mtime change.
        /// </summary>
        public static void ClearCache()
        {
            lock (_lock)
                _cache.Clear();
        }

        // ── Internals ────────────────────────────────────────────────────────

        static string? OverlayFilePath(string slug)
        {
            var root = WikiRoot;
            if (root == null)
                return null;
            // Slug carries the dotted form ("asset.dump-quickstart" → file name "asset.dump-quickstart.md").
            return Path.Combine(root, slug + ".md");
        }

        static string? ResolveWikiRoot()
        {
            var root = PackageRoot;
            if (root == null)
                return null;
            return Path.Combine(root, WikiSubDirectory.Replace('/', Path.DirectorySeparatorChar));
        }

        // Resolve the package install root via PackageInfo, falling back to the project-relative
        // Packages/<name>/ path when the assembly isn't package-resolved. Both the wiki overlay
        // source and the generated wiki output hang off this single resolution.
        static string? ResolvePackageRoot()
        {
            try
            {
                var pkg = PackageInfo.FindForAssembly(typeof(WikiOverlay).Assembly);
                if (pkg != null && !string.IsNullOrEmpty(pkg.resolvedPath))
                    return pkg.resolvedPath;
            }
            catch
            {
                // Fall through to the project-relative fallback.
            }

            // Fallback: project-relative path. Works when the plugin lives under
            // Packages/<name>/ in the consumer project.
            var rel = Path.Combine("Packages", PackageName);
            try
            {
                return Path.GetFullPath(rel);
            }
            catch
            {
                return null;
            }
        }

        static CachedOverlay? GetOrLoad(string absPath)
        {
            var key = absPath.ToLowerInvariant();
            DateTime currentStamp;
            try
            {
                if (!File.Exists(absPath))
                {
                    lock (_lock)
                        _cache.Remove(key);
                    return null;
                }
                currentStamp = File.GetLastWriteTimeUtc(absPath);
            }
            catch
            {
                lock (_lock)
                    _cache.Remove(key);
                return null;
            }

            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var existing) && existing.FileTimeStamp == currentStamp)
                    return existing;
            }

            string fileBody;
            try
            {
                fileBody = File.ReadAllText(absPath, Encoding.UTF8);
            }
            catch
            {
                lock (_lock)
                    _cache.Remove(key);
                return null;
            }

            var fresh = new CachedOverlay { FileTimeStamp = currentStamp };
            ParseInto(fileBody, fresh);

            lock (_lock)
                _cache[key] = fresh;
            return fresh;
        }

        // Walk the file's lines once, populating out.Prelude and out.Sections.
        // Lines are split on \r\n / \n; trailing \r is stripped per line.
        static void ParseInto(string fileBody, CachedOverlay outEntry)
        {
            var lines = fileBody.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            // Phase 1: skip leading H1, blank lines, and HTML comment blocks.
            int idx = 0;
            bool h1Skipped = false;
            while (idx < lines.Length)
            {
                var l = lines[idx];
                if (!h1Skipped && IsH1Line(l))
                {
                    h1Skipped = true;
                    idx++;
                    continue;
                }
                if (string.IsNullOrEmpty(l.Trim()))
                {
                    idx++;
                    continue;
                }
                if (IsCommentOpenLine(l))
                {
                    if (LineContainsCommentClose(l))
                    {
                        idx++;
                        continue;
                    }
                    idx++;
                    while (idx < lines.Length && !LineContainsCommentClose(lines[idx]))
                        idx++;
                    if (idx < lines.Length)
                        idx++; // skip the closing-comment line itself
                    continue;
                }
                break; // first non-skipped content line
            }

            // Phase 2: collect prelude up to (not including) the first `### ` line.
            int preludeStart = idx;
            while (idx < lines.Length && !lines[idx].StartsWith("### ", StringComparison.Ordinal))
                idx++;
            var sb = new StringBuilder();
            for (int i = preludeStart; i < idx; i++)
            {
                sb.Append(lines[i]);
                sb.Append('\n');
            }
            outEntry.Prelude = sb.ToString().Trim();

            // Phase 3: scan H3 sections. Heading line is `### <full method name>`
            // (case-sensitive on the name). Section ends at next `### ` or EOF.
            while (idx < lines.Length)
            {
                var heading = lines[idx];
                if (!heading.StartsWith("### ", StringComparison.Ordinal))
                {
                    idx++;
                    continue;
                }
                var methodName = heading.Substring(4).Trim();
                idx++;

                var body = new StringBuilder();
                while (idx < lines.Length && !lines[idx].StartsWith("### ", StringComparison.Ordinal))
                {
                    body.Append(lines[idx]);
                    body.Append('\n');
                    idx++;
                }

                if (methodName.Length > 0)
                    outEntry.Sections[methodName] = body.ToString().Trim();
            }
        }

        // `# something`, but not `## ` / `### `.
        static bool IsH1Line(string line) =>
            line.StartsWith("# ", StringComparison.Ordinal) && !line.StartsWith("## ", StringComparison.Ordinal);

        static bool IsCommentOpenLine(string line) =>
            line.TrimStart().StartsWith("<!--", StringComparison.Ordinal);

        static bool LineContainsCommentClose(string line) =>
            line.Contains("-->");
    }
}
