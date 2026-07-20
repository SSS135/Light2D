#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Editor.Framework.Runtime;
using com.SSS135.EditorAutomation.Editor.Framework.Wiki;

namespace com.SSS135.EditorAutomation.Editor.Framework.Builder
{
    /// <summary>
    /// Static, single-collection tool registry. Replaces the legacy
    /// <c>ToolRunnerCollection</c> + <c>SystemToolRunnerCollection</c> pair — every tool
    /// scanned by <see cref="AttributeScanner"/> joins one map regardless of category.
    /// API surface is intentionally aligned with the existing
    /// <c>com.SSS135.EditorAutomation.Editor.Http.ToolCatalog</c> so the Wave 2 dispatcher
    /// port-over is mechanical.
    /// </summary>
    public static class ToolRegistry
    {
        const string SystemNamespace = "system";

        static readonly object _lock = new object();

        // Dotted method name (e.g. "gameobject.find") → resolved tool. Populated by Rebuild().
        static Dictionary<string, IMcpTool> _dottedToTool = new Dictionary<string, IMcpTool>(StringComparer.OrdinalIgnoreCase);
        // Dotted method name → original kebab-case tool id ("gameobject-find").
        static Dictionary<string, string> _dottedToToolId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // namespace → ordered list of dotted method names in that namespace.
        static Dictionary<string, List<string>> _namespaceToDotted = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        // Standalone topic-page slugs discovered under <package>/Docs~/WikiSource/ that don't
        // collide with any registered tool / namespace. Snapshotted by Rebuild() and stable until
        // the next Rebuild() — adding a new topic file requires an Editor restart per the
        // Docs~/WikiSource/README.md "Topic Pages" rule.
        static HashSet<string> _topicPageSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyCollection<string> AllDottedMethods
        {
            get
            {
                lock (_lock)
                    return _dottedToTool.Keys.ToArray();
            }
        }

        /// <summary>
        /// Snapshot of every registered tool's original kebab-case id (e.g. "gameobject-find").
        /// </summary>
        public static IReadOnlyCollection<string> AllKebabIds
        {
            get
            {
                lock (_lock)
                    return _dottedToToolId.Values.ToArray();
            }
        }

        /// <summary>
        /// Snapshot of every registered namespace name (the segment before the first '.' in a
        /// dotted method id; dot-less ids land in <c>"system"</c>).
        /// </summary>
        public static IReadOnlyCollection<string> AllNamespaces
        {
            get
            {
                lock (_lock)
                    return _namespaceToDotted.Keys.ToArray();
            }
        }

        /// <summary>
        /// Returns the dotted names of other methods in the same namespace as
        /// <paramref name="dotted"/>, excluding <paramref name="dotted"/> itself. Empty list if
        /// the tool is alone in its namespace or unknown.
        /// </summary>
        public static IReadOnlyList<string> SiblingsInNamespace(string dotted)
        {
            if (string.IsNullOrEmpty(dotted))
                return Array.Empty<string>();
            var ns = ExtractNamespace(dotted);
            lock (_lock)
            {
                if (!_namespaceToDotted.TryGetValue(ns, out var list))
                    return Array.Empty<string>();
                var result = new List<string>(list.Count);
                foreach (var sibling in list)
                {
                    if (!string.Equals(sibling, dotted, StringComparison.OrdinalIgnoreCase))
                        result.Add(sibling);
                }
                return result;
            }
        }

        /// <summary>
        /// Re-scan every loaded assembly via <see cref="AttributeScanner.Scan()"/> and snapshot
        /// the result into the lookup maps. Safe to call repeatedly (idempotent) and from any
        /// thread.
        /// </summary>
        /// <remarks>
        /// Wired exactly once per domain reload by <see cref="FrameworkBootstrap"/>. There is no
        /// framework-level <c>OnToolsChanged</c> event because the registry is attribute-driven:
        /// new tools enter the build via class+method <c>[McpTool]</c> annotations, which only
        /// become live after a C# recompile (which itself triggers a domain reload and a fresh
        /// bootstrap call). At runtime no new tools can be added, so a pub/sub layer would be
        /// dead code. If a future change introduces runtime tool registration, subscribe
        /// callers to a new event here rather than polling.
        /// </remarks>
        public static void Rebuild()
        {
            var descriptors = AttributeScanner.Scan();

            var nextDottedToTool = new Dictionary<string, IMcpTool>(StringComparer.OrdinalIgnoreCase);
            var nextDottedToToolId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var nextNamespaceToDotted = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            // Maps a dotted id back to the descriptor that claimed it first, so a duplicate
            // collision can name both colliding declaring-type+method sites in the throw.
            var nextDottedToDescriptor = new Dictionary<string, ToolDescriptor>(StringComparer.OrdinalIgnoreCase);

            // Fail loud: a tool that can't register is a programming error in the plugin, not a
            // condition to silently route around. A dropped tool yields a confusing "method not
            // found" at call time; throwing here surfaces it at domain-reload with the exact site.
            foreach (var descriptor in descriptors)
            {
                var toolId = descriptor.Attribute.ToolId;
                if (string.IsNullOrEmpty(toolId))
                {
                    throw new InvalidOperationException(
                        $"[UnityMCP] ToolRegistry.Rebuild: [McpTool] on {DescribeSite(descriptor)} has an empty tool id. " +
                        "Pass a non-empty dotted kebab-case id (e.g. \"assets-copy\") to the [McpTool] attribute.");
                }

                var dotted = ToDottedName(toolId);
                if (nextDottedToDescriptor.TryGetValue(dotted, out var existing))
                {
                    throw new InvalidOperationException(
                        $"[UnityMCP] ToolRegistry.Rebuild: duplicate tool id \"{toolId}\" (dotted \"{dotted}\") declared on both " +
                        $"{DescribeSite(existing)} and {DescribeSite(descriptor)}. Tool ids must be unique — rename one.");
                }

                IMcpTool tool;
                try
                {
                    tool = RunTool.Create(descriptor);
                }
                catch (Exception ex)
                {
                    // A malformed tool method (unsupported parameter / return type, instance/static
                    // mismatch, etc.) is a hard build error. Wrap with the tool id + declaring site
                    // and rethrow the inner exception type+message so the offending signature is named.
                    throw new InvalidOperationException(
                        $"[UnityMCP] ToolRegistry.Rebuild: failed to construct tool \"{toolId}\" on {DescribeSite(descriptor)} — " +
                        $"{ex.GetType().Name}: {ex.Message}", ex);
                }

                nextDottedToTool[dotted] = tool;
                nextDottedToToolId[dotted] = toolId;
                nextDottedToDescriptor[dotted] = descriptor;

                var ns = ExtractNamespace(dotted);
                if (!nextNamespaceToDotted.TryGetValue(ns, out var list))
                    nextNamespaceToDotted[ns] = list = new List<string>();
                list.Add(dotted);
            }

            foreach (var list in nextNamespaceToDotted.Values)
                list.Sort(StringComparer.OrdinalIgnoreCase);

            // Eager scan of Docs~/WikiSource/*.md. Filter out slugs already owned by a
            // registered tool or namespace — those are namespace overlays, not topic pages.
            var nextTopicPages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var slug in WikiOverlay.EnumerateOverlaySlugs())
            {
                if (string.IsNullOrEmpty(slug))
                    continue;
                if (nextDottedToTool.ContainsKey(slug))
                    continue; // tool method wins on collision
                if (nextNamespaceToDotted.ContainsKey(slug))
                    continue; // namespace overlay, not a topic page
                nextTopicPages.Add(slug);
            }

            lock (_lock)
            {
                _dottedToTool = nextDottedToTool;
                _dottedToToolId = nextDottedToToolId;
                _namespaceToDotted = nextNamespaceToDotted;
                _topicPageSlugs = nextTopicPages;
            }

            // Mirror the freshly committed registry to disk under WikiGenerated~/ so the checked-in full
            // pages track the live wiki. Best-effort and intentionally outside the lock above: the
            // generator reads only the public enumerators, so file I/O never holds the registry lock.
            // It swallows its own failures, but the extra try/catch here guarantees Rebuild never
            // throws on a wiki hiccup even if that contract were ever broken.
            try
            {
                WikiDiskGenerator.Generate();
            }
            catch
            {
                // A wiki-generation failure must not abort a successful registry rebuild.
            }
        }

        /// <summary>
        /// "Namespace.Type.Method" for a descriptor, used in fail-loud Rebuild messages so an
        /// operator can jump straight to the offending [McpTool] declaration.
        /// </summary>
        static string DescribeSite(ToolDescriptor descriptor)
        {
            var typeName = descriptor.ClassType?.FullName ?? descriptor.ClassType?.Name ?? "<unknown type>";
            var methodName = descriptor.MethodInfo?.Name ?? "<unknown method>";
            return $"{typeName}.{methodName}";
        }

        /// <summary>
        /// Snapshot of every standalone topic-page slug discovered under
        /// <c>Docs~/WikiSource/</c> that doesn't collide with a registered tool or namespace.
        /// </summary>
        public static IReadOnlyCollection<string> TopicPages
        {
            get
            {
                lock (_lock)
                    return _topicPageSlugs.ToArray();
            }
        }

        /// <summary>
        /// True when <paramref name="slug"/> resolves to a standalone topic page (and not a
        /// registered tool or namespace). Used by wiki routing to dispatch to
        /// <c>WikiOverlay.LoadGroupPrelude</c> instead of the namespace renderer.
        /// </summary>
        public static bool IsTopicPage(string? slug)
        {
            if (string.IsNullOrEmpty(slug))
                return false;
            lock (_lock)
                return _topicPageSlugs.Contains(slug!);
        }

        /// <summary>
        /// "gameobject-find" → "gameobject.find"; tool ids without '-' are routed to
        /// namespace <c>"system"</c> (their dotted form is just the name, with no dot).
        /// </summary>
        public static string ToDottedName(string toolId)
        {
            if (string.IsNullOrEmpty(toolId))
                return string.Empty;
            var idx = toolId.IndexOf('-');
            if (idx < 0)
                return toolId;
            return toolId.Substring(0, idx) + "." + toolId.Substring(idx + 1);
        }

        /// <summary>
        /// Inverse of <see cref="ToDottedName"/> using the cache built in <see cref="Rebuild"/>.
        /// Falls back to a structural conversion when the cache lacks the entry.
        /// </summary>
        public static string FromDottedName(string dotted)
        {
            if (string.IsNullOrEmpty(dotted))
                return string.Empty;
            lock (_lock)
            {
                if (_dottedToToolId.TryGetValue(dotted, out var id))
                    return id;
            }
            var idx = dotted.IndexOf('.');
            if (idx < 0) return dotted;
            return dotted.Substring(0, idx) + "-" + dotted.Substring(idx + 1);
        }

        public static IMcpTool? FindTool(string dotted)
        {
            if (string.IsNullOrEmpty(dotted))
                return null;
            lock (_lock)
            {
                return _dottedToTool.TryGetValue(dotted, out var tool) ? tool : null;
            }
        }

        /// <summary>
        /// Returns the dotted method names registered under the given namespace, in sorted
        /// order. Empty list if the namespace is unknown.
        /// </summary>
        public static IReadOnlyList<string> MethodsInNamespace(string ns)
        {
            if (string.IsNullOrEmpty(ns))
                return Array.Empty<string>();
            lock (_lock)
            {
                if (!_namespaceToDotted.TryGetValue(ns, out var list))
                    return Array.Empty<string>();
                return list.ToArray();
            }
        }

        /// <summary>
        /// Snapshot of every namespace + how many methods live under it. Sorted by namespace
        /// name. Used by wiki / discovery emitters.
        /// </summary>
        public static IReadOnlyList<KeyValuePair<string, int>> NamespaceSummary()
        {
            List<KeyValuePair<string, int>> entries;
            lock (_lock)
            {
                entries = _namespaceToDotted
                    .Select(kvp => new KeyValuePair<string, int>(kvp.Key, kvp.Value.Count))
                    .ToList();
            }
            entries.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase));
            return entries;
        }

        /// <summary>
        /// Pool of strings the "did you mean" fuzzy ranker matches against. Includes every
        /// dotted method name plus every namespace name. When <paramref name="includeTopicPages"/>
        /// is true, topic-page slugs discovered under <c>Docs~/WikiSource/</c> are merged
        /// into the same pool so the fuzzy ranker can suggest workflows alongside tools.
        /// </summary>
        public static IReadOnlyList<string> BuildSuggestionPool(bool includeTopicPages = false)
        {
            lock (_lock)
            {
                var capacity = _dottedToTool.Count + _namespaceToDotted.Count;
                if (includeTopicPages)
                    capacity += _topicPageSlugs.Count;
                var pool = new List<string>(capacity);
                pool.AddRange(_dottedToTool.Keys);
                pool.AddRange(_namespaceToDotted.Keys);
                if (includeTopicPages)
                    pool.AddRange(_topicPageSlugs);
                return pool;
            }
        }

        /// <summary>Extract the leading namespace segment from a dotted method id.</summary>
        public static string ExtractNamespace(string dotted)
        {
            if (string.IsNullOrEmpty(dotted))
                return SystemNamespace;
            var idx = dotted.IndexOf('.');
            return idx < 0 ? SystemNamespace : dotted.Substring(0, idx);
        }
    }
}
