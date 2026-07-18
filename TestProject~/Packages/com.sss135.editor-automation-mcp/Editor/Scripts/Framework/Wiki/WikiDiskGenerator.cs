#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using com.SSS135.EditorAutomation.Editor.Framework.Builder;

namespace com.SSS135.EditorAutomation.Editor.Framework.Wiki
{
    /// <summary>
    /// Renders the full wiki tree to disk on every domain reload so the committed pages under
    /// <c>&lt;package&gt;/WikiGenerated~/</c> stay in lock-step with the live <c>call</c>-wiki output.
    /// Every page is produced by reusing <see cref="WikiMarkdownEmitter"/> against the snapshot in
    /// <see cref="ToolRegistry"/> — the generator owns no render logic of its own, so a generated
    /// file is byte-identical to what <c>RpcDispatcher.RenderWikiMarkdown</c> would serve live.
    ///
    /// The output dir (<c>WikiGenerated~</c>, a package-root sibling) is deliberately separate from
    /// the overlay source dir (<c>Docs~/WikiSource</c>): the source is hand-authored prose, the
    /// output is the assembled full pages. Both directories carry the <c>~</c> suffix so Unity never
    /// imports them — there is no <c>.meta</c>, no reimport, and no domain-reload feedback loop. Do
    /// not call <c>AssetDatabase.Refresh</c> here.
    ///
    /// Writes are deterministic and idempotent: content carries no timestamp / GUID / absolute
    /// machine path, an unchanged page is skipped (byte-compared against the file on disk), and
    /// orphaned <c>*.md</c> whose slug is no longer registered are pruned. The net effect is that an
    /// unchanged reload produces zero writes and zero git diff, so <see cref="ToolRegistry.Rebuild"/>
    /// firing two or three times per reload is a cheap no-op.
    /// </summary>
    public static class WikiDiskGenerator
    {
        // Output sub-directory under the package root. Intentionally NOT WikiOverlay.WikiSubDirectory
        // (that points at the overlay source, "Docs~/WikiSource"); the generated full pages land here.
        const string OutputSubDirectory = "WikiGenerated~";

        // Slug that owns the root index file. The live wiki serves the root index for the empty
        // method, and RenderRootIndex injects its overlay via the "wiki" slug, so "wiki.md" is the
        // canonical root page. A same-named topic overlay (Docs~/WikiSource/wiki.md) is the root
        // prelude, never a standalone topic page — the root index claims the filename.
        const string RootSlug = "wiki";

        // Header prepended to every generated file. Deterministic (no per-run data) so unchanged
        // reloads byte-match the on-disk content and skip the write. The trailing blank line keeps
        // the rendered body's leading H1 / front-matter visually separated from the marker.
        const string GeneratedHeaderPrefix =
            "<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. " +
            "Do not edit; edit Docs~/WikiSource/";
        const string GeneratedHeaderSuffix = ".md. -->\n\n";

        // One emitter for the whole pass — matches the single static instance RpcDispatcher uses
        // for the live wiki, so generated and live output share identical render configuration.
        static readonly WikiMarkdownEmitter _emitter = new WikiMarkdownEmitter();

        /// <summary>
        /// Absolute path to the generated wiki output directory (<c>&lt;package&gt;/WikiGenerated~</c>),
        /// or null when the package cannot be resolved. Shared with the dispatcher's wiki-mode path
        /// reference so both sides agree on where the generated pages live.
        /// </summary>
        public static string? OutputDirectory
        {
            get
            {
                var root = WikiOverlay.PackageRoot;
                if (string.IsNullOrEmpty(root))
                    return null;
                return Path.Combine(root!, OutputSubDirectory.Replace('/', Path.DirectorySeparatorChar));
            }
        }

        /// <summary>
        /// Slug that owns the root index page (<c>wiki.md</c>). Exposed so the dispatcher can build
        /// the root path reference without re-deriving the convention.
        /// </summary>
        public static string RootIndexSlug => RootSlug;

        /// <summary>
        /// Absolute path to the generated page for <paramref name="slug"/>
        /// (<c>&lt;package&gt;/WikiGenerated~/&lt;slug&gt;.md</c>), or null when the package cannot be
        /// resolved or the slug is empty. Does not check existence — the caller decides whether to
        /// fall back to a live render when the file is absent.
        /// </summary>
        public static string? PagePath(string? slug)
        {
            if (string.IsNullOrEmpty(slug))
                return null;
            var dir = OutputDirectory;
            if (dir == null)
                return null;
            return Path.Combine(dir, slug! + ".md");
        }

        /// <summary>
        /// Render the full wiki tree and reconcile it with <c>&lt;package&gt;/WikiGenerated~/</c>:
        /// write changed pages, skip unchanged ones, and prune orphans. Best-effort and total —
        /// any failure (unresolved package, I/O error, render exception) is swallowed so a wiki
        /// hiccup never aborts the registry rebuild. Must be called on the main thread, after the
        /// registry lock has been released (it reads only the public registry enumerators, so file
        /// I/O never holds the registry lock).
        /// </summary>
        public static void Generate()
        {
            try
            {
                var root = WikiOverlay.PackageRoot;
                if (string.IsNullOrEmpty(root))
                    return; // package unresolved (cooked / non-editor build) — nothing to write

                var outputDir = Path.Combine(root!, OutputSubDirectory.Replace('/', Path.DirectorySeparatorChar));

                // Build slug → rendered markdown for the entire tree first. Rendering reads only the
                // public registry enumerators, so this whole block runs outside the registry lock.
                var pages = BuildPages();

                Directory.CreateDirectory(outputDir);
                WriteChangedPages(outputDir, pages);
                PruneOrphans(outputDir, pages);
            }
            catch
            {
                // Swallow: see the method summary. Rebuild must never throw out of this call.
            }
        }

        // ── Page assembly ────────────────────────────────────────────────────

        /// <summary>
        /// Assemble the full slug → markdown map for every page the live wiki can serve: the root
        /// index, every namespace page, every method page, and every standalone topic page. Each
        /// value already carries the generated-file header for its slug.
        /// </summary>
        static Dictionary<string, string> BuildPages()
        {
            var pages = new Dictionary<string, string>(StringComparer.Ordinal);

            // Root index (slug "wiki"). Mirrors RpcDispatcher.RenderRootIndex: a NamespaceInfo per
            // namespace, each carrying that namespace's resolved tools in registry order.
            pages[RootSlug] = WithHeader(RootSlug, _emitter.RenderRootIndex(BuildNamespaceInfos()));

            // Namespace pages + per-method pages. Mirrors RpcDispatcher.RenderNamespacePage and the
            // per-tool Render(tool) branch in RenderWikiMarkdown.
            foreach (var ns in ToolRegistry.AllNamespaces)
            {
                if (string.IsNullOrEmpty(ns))
                    continue;

                var dottedNames = ToolRegistry.MethodsInNamespace(ns);
                var tools = ResolveTools(dottedNames);

                // A namespace slug never collides with the root slug — "wiki" is not a namespace
                // (it has no registered method) — so this write is unconditional.
                pages[ns] = WithHeader(ns, _emitter.RenderNamespacePage(ns, tools));

                foreach (var tool in tools)
                {
                    var dotted = ToolRegistry.ToDottedName(tool.Name);
                    if (string.IsNullOrEmpty(dotted))
                        continue;
                    pages[dotted] = WithHeader(dotted, _emitter.Render(tool));
                }
            }

            // Standalone topic pages. Mirrors the IsTopicPage(method) → RenderTopicPage branch.
            // Skip the root slug: a Docs~/WikiSource/wiki.md overlay is the root-index prelude, not a
            // topic page, and the root index already owns wiki.md above.
            foreach (var slug in ToolRegistry.TopicPages)
            {
                if (string.IsNullOrEmpty(slug))
                    continue;
                if (string.Equals(slug, RootSlug, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (pages.ContainsKey(slug))
                    continue; // tool / namespace page already claimed this slug
                pages[slug] = WithHeader(slug, _emitter.RenderTopicPage(slug));
            }

            return pages;
        }

        /// <summary>
        /// Build the per-namespace summary list the root index consumes, in the same shape
        /// <c>RpcDispatcher.RenderRootIndex</c> builds it: one <see cref="NamespaceInfo"/> per
        /// namespace carrying its resolved tools in registry (sorted dotted) order. The emitter
        /// re-sorts namespaces by name itself, so enumeration order here does not affect output.
        /// </summary>
        static List<NamespaceInfo> BuildNamespaceInfos()
        {
            var namespaces = new List<NamespaceInfo>();
            foreach (var ns in ToolRegistry.AllNamespaces)
            {
                if (string.IsNullOrEmpty(ns))
                    continue;
                var tools = ResolveTools(ToolRegistry.MethodsInNamespace(ns));
                namespaces.Add(new NamespaceInfo(ns, tools));
            }
            return namespaces;
        }

        /// <summary>Resolve a list of dotted method names to their tools, skipping any that the
        /// registry no longer knows (a name can vanish between enumeration and lookup only across a
        /// rebuild, which re-runs this generator anyway).</summary>
        static List<IMcpTool> ResolveTools(IReadOnlyList<string> dottedNames)
        {
            var tools = new List<IMcpTool>(dottedNames.Count);
            foreach (var dotted in dottedNames)
            {
                var tool = ToolRegistry.FindTool(dotted);
                if (tool != null)
                    tools.Add(tool);
            }
            return tools;
        }

        static string WithHeader(string slug, string body) =>
            GeneratedHeaderPrefix + slug + GeneratedHeaderSuffix + body;

        // ── Disk reconciliation ──────────────────────────────────────────────

        /// <summary>
        /// Write each page whose on-disk content differs (or is absent). Byte-identical files are
        /// left untouched so unchanged reloads touch no file and produce no git diff. UTF-8 without
        /// BOM is used for both the read and the write so the round-trip comparison is exact.
        /// </summary>
        static void WriteChangedPages(string outputDir, Dictionary<string, string> pages)
        {
            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            foreach (var kvp in pages)
            {
                var path = Path.Combine(outputDir, kvp.Key + ".md");
                try
                {
                    if (File.Exists(path) && File.ReadAllText(path, encoding) == kvp.Value)
                        continue; // unchanged — skip the write
                    File.WriteAllText(path, kvp.Value, encoding);
                }
                catch
                {
                    // Skip this page; a transient I/O failure on one file must not abort the rest.
                }
            }
        }

        /// <summary>
        /// Delete generated <c>*.md</c> whose slug is no longer registered, so removing a tool /
        /// namespace / topic file leaves no stale page behind. Only files in the top directory are
        /// considered, matching the flat one-file-per-slug layout the overlay loader uses.
        /// </summary>
        static void PruneOrphans(string outputDir, Dictionary<string, string> pages)
        {
            string[] existing;
            try
            {
                existing = Directory.GetFiles(outputDir, "*.md", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                return;
            }

            foreach (var file in existing)
            {
                var slug = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrEmpty(slug) || pages.ContainsKey(slug))
                    continue;
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // A locked / vanished orphan is harmless; leave it and retry next reload.
                }
            }
        }
    }
}
