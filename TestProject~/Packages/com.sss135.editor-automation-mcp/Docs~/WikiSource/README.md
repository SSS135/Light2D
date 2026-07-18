# Wiki Source Directory (`Docs~/WikiSource/`)

This directory holds the **authored** wiki source: editorial overlays and
standalone topic pages. These are the committed files you hand-edit. They are
read live by `WikiOverlay` at request time — every `POST /mcp` body whose
`tools/call` `arguments` carry a `method` field but no `args` field is routed
to `WikiMarkdownEmitter`, which consults `WikiOverlay` for the per-namespace
prelude and per-method `### <full.method.name>` sections.

The authored overlays live here under
`<package>/Docs~/WikiSource/<dotted-path>.md`. The sibling `WikiGenerated~/` tree is
the **generated** output: full merged pages (overlay prelude + generated
signatures + method detail sections) written on every domain reload. It is
committed so the merged pages are diffable and greppable, but it is generated —
**do not hand-edit `WikiGenerated~/`**; edit the source here in `Docs~/WikiSource/`
and let the next reload regenerate it.

The authored `WikiSource/` tree lives inside `Docs~/`; the generated
`WikiGenerated~/` tree is a sibling at the package root. Both are excluded from
Unity asset import by a `~` suffix — `Docs~` carries it for the source tree and
`WikiGenerated~` carries its own — so no `.meta` sidecars are written and the
files do not appear in the Project window.

## Live Wiki Navigation

The MCP surface exposes one tool: `call`.

| Call shape | Meaning |
|---|---|
| no `method`, no `args` | Return a `{page:<path>}` reference to the generated root wiki index. |
| `method`, no `args` | Return a `{page:<path>}` reference to the wiki page for a namespace, a registered tool, or a standalone topic page. |
| `method` plus `args` | Execute the RPC named by `method`; use `args={}` for parameterless methods. |

### Model-facing workflow

A wiki-mode `call` (no `args`) no longer streams page markdown back through the
tool result. It returns a `{page:<path>}` reference pointing at the committed
generated page under `WikiGenerated~/`. To read the content:

- **`Read` the returned path** — for example
  `WikiGenerated~/assets.md` — to load the full merged page.
- **`Grep` the committed `WikiGenerated~/` tree** to find a method, namespace, or
  topic across all pages without round-tripping through the gateway.
- **`Grep` the C# source** for grep-based tool discovery: every handler carries
  a `[McpTool]` attribute that names the method and, with its parameter
  attributes, describes the argument schema — so the tool names and shapes are
  discoverable straight from the source without any wiki read.

Bare namespace pages such as the one referenced by `call("assets")` are
caller-facing indexes that combine generated method listings with the prelude
in `assets.md`. They are not static full-reference dumps.

## Overlay Shape

Each namespace overlay file has three structural parts:

- H1 title: `# <dotted.path>`. The merger strips it before rendering.
- Prelude body: content after the H1 and leading HTML comments until the
  first `## ` or `### ` heading. Rendered above the generated namespace
  content on the namespace page, and (for top-level namespaces) seeded into
  the root-index one-liner.
- Optional method detail sections: `### <full.method.name>` sections appended
  as a `## Notes` block to the matching method page, above any "Related
  Methods" tail.

Branch pages (no methods of their own — namespace prelude only) use only a
prelude. Leaf or hybrid pages may also include method detail sections.

## Authoring Rules

- Do not duplicate generated signatures, parameter lists, or one-line
  summaries — the renderer emits those from the JSON schema.
- Use overlays for workflow, gotchas, cross-references, and examples.
- Write cross-refs using `call("path")` or the explicit `method` / `args`
  shape so the prose matches the live invocation surface.
- Add short "Key methods" lists only where they improve navigation.
- Put longer category-page guidance under `##` headings so it stays off the
  root-index one-liner.
- Keep a non-empty intro paragraph before the first `##` or `###` — a
  top-level overlay with no intro renders as a bare root entry.
- Add method `### ` sections only when generated content alone would mislead.

Missing files fall back to generated content only. The merger reads files
lazily and caches by mtime, so edits to existing files are picked up by the
next wiki read without restarting the Editor.

## Topic Pages

Standalone topic pages are cross-namespace walkthroughs rather than overlays
for registered tool namespaces. They live beside namespace overlays as
`<package>/Docs~/WikiSource/<slug>.md` and are reachable directly through
`call("<slug>")`, for example `call("workflows")`.

The router discovers topic pages when `ToolRegistry.Rebuild()` builds its
cache by enumerating `<package>/Docs~/WikiSource/*.md`. It skips
`README*.md` files and skips any slug already owned by a registered
namespace or dotted method id. Registered tools and namespaces win on
collisions, so do not reuse namespace or method names for topic slugs.

Topic pages use the same H1 + prelude parser as namespace overlays, but they
should not use `### ` headings. `### ` is reserved for method detail
sections in namespace overlays; standalone topic rendering stops at the
first `### ` line.

Adding a brand-new topic file requires an Editor restart because the
topic-node set is built once with the wiki cache during
`ToolRegistry.Rebuild()`. Edits to an existing topic file are picked up by
mtime revalidation on the next wiki read.
