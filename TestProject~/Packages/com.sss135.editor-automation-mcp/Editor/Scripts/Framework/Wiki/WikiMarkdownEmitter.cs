#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace com.SSS135.EditorAutomation.Editor.Framework.Wiki
{
    /// <summary>
    /// Per-namespace summary entry used by <see cref="WikiMarkdownEmitter.RenderRootIndex"/>.
    /// Carries the namespace name plus its dotted method ids for the index listing.
    /// </summary>
    public readonly struct NamespaceInfo
    {
        public string Name { get; }
        public IReadOnlyList<IMcpTool> Tools { get; }

        public NamespaceInfo(string name, IReadOnlyList<IMcpTool> tools)
        {
            Name = name;
            Tools = tools;
        }
    }

    /// <summary>
    /// Canonical wiki renderer for the in-Editor HTTP gateway. Emits markdown per tool, per
    /// namespace, and a root index. The output is consumed by <c>tools/call</c> wiki-mode
    /// responses — there is no on-disk emitter; all wiki content is served live.
    /// Loopback-only gateway: the authorization example block is skipped entirely.
    /// </summary>
    public class WikiMarkdownEmitter
    {
        // TypeInfoResolver is required on netstandard2.1/net8+: JsonNode.ToJsonString(options)
        // calls options.MakeReadOnly(), which throws if no resolver is set.
        static readonly JsonSerializerOptions _prettyJsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };

        // Detects a kebab-case tool id, either bare or wrapped in single quotes / backticks.
        // Captured groups: 1 = optional opening delim, 2 = the id, 3 = closing delim (re-matched).
        // The id shape is restricted to lowercase letters/digits with at least one hyphen — this
        // avoids matching plain English words while still scanning all known ids.
        static readonly Regex _toolIdMentionRegex = new Regex(
            @"(['`]?)\b([a-z][a-z0-9]*(?:-[a-z0-9]+)+)\b\1",
            RegexOptions.Compiled);

        const int SummaryTruncateLength = 200;

        protected readonly ILogger? _logger;

        public WikiMarkdownEmitter(ILogger? logger = null)
        {
            _logger = logger;
        }

        // ── Customization properties (mirror SkillFileGenerator) ─────────────

        /// <summary>
        /// When true, the description paragraph is rendered after the H1 heading.
        /// The HTTP gateway emitter renders a linkified description itself, so this is false.
        /// </summary>
        public virtual bool IncludeDescriptionBody { get; } = false;

        /// <summary>
        /// When true, the parameter table is included in the Input section.
        /// </summary>
        public virtual bool IncludeParameterTable { get; } = true;

        /// <summary>
        /// When true, the raw Input JSON Schema code block is included after the parameter table.
        /// </summary>
        public virtual bool IncludeInputJsonSchema { get; } = true;

        /// <summary>
        /// When true, description fields inside the schema's <c>properties</c> are preserved.
        /// The parameter table already shows descriptions, so the schema block strips them.
        /// </summary>
        public virtual bool IncludeInputSchemaPropertyDescriptions { get; } = false;

        /// <summary>
        /// When true, the Output section is included.
        /// </summary>
        public virtual bool IncludeOutputSection { get; } = true;

        /// <summary>
        /// Maximum length, in characters, allowed for the SKILL.md YAML <c>description:</c> field.
        /// Codex / Anthropic Agent Skills cap this at 1024. Values &lt;= 0 disable truncation.
        /// </summary>
        public virtual int MaxSkillDescriptionLength { get; } = 1024;

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Render the full per-tool wiki markdown for <paramref name="tool"/>.
        /// </summary>
        public virtual string Render(IMcpTool tool)
        {
            if (tool == null) throw new ArgumentNullException(nameof(tool));
            return BuildMarkdown(tool, SanitizeSkillName(tool.Name));
        }

        /// <summary>
        /// Render the root index page listing every namespace plus a per-namespace method list.
        /// Caller passes a snapshot of every namespace's tools — the emitter does not call into
        /// any registry itself.
        /// </summary>
        public virtual string RenderRootIndex(IEnumerable<NamespaceInfo> namespaces)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Editor Automation — Method Reference");
            sb.AppendLine();
            sb.AppendLine("Auto-generated from the live tool registry.");
            sb.AppendLine();
            sb.AppendLine("Call any method without `args` to read its wiki page; include `args` (even `{}`) to execute.");
            sb.AppendLine();

            // Inject the root-page prelude overlay. Looked up via the conventional
            // "wiki" slug — author Docs~/WikiSource/wiki.md to add a root-page intro;
            // absent that the block is skipped silently.
            var rootPrelude = LoadGroupPreludeSafe("wiki");
            if (!string.IsNullOrWhiteSpace(rootPrelude))
            {
                sb.AppendLine(rootPrelude);
                sb.AppendLine();
            }

            var snapshot = (namespaces ?? Array.Empty<NamespaceInfo>())
                .Where(n => !string.IsNullOrEmpty(n.Name) && n.Tools != null)
                .OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            sb.AppendLine("## Namespaces");
            sb.AppendLine();
            foreach (var entry in snapshot)
            {
                var count = entry.Tools.Count;
                sb.AppendLine($"- `{entry.Name}` ({count} method{(count == 1 ? string.Empty : "s")})");
            }
            sb.AppendLine();

            foreach (var entry in snapshot)
            {
                sb.AppendLine($"## `{entry.Name}`");
                sb.AppendLine();

                var nsPrelude = LoadGroupPreludeSafe(entry.Name);
                if (!string.IsNullOrWhiteSpace(nsPrelude))
                {
                    sb.AppendLine(nsPrelude);
                    sb.AppendLine();
                }

                foreach (var tool in entry.Tools)
                {
                    var dotted = ToDottedName(tool.Name);
                    var summary = GetSummary(tool, truncate: true);
                    if (string.IsNullOrEmpty(summary))
                        sb.AppendLine($"- `{dotted}`");
                    else
                        sb.AppendLine($"- `{dotted}` — {summary}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Render a standalone topic page (cross-namespace walkthrough) keyed by slug.
        /// The body is the overlay prelude from <c>Docs~/WikiSource/&lt;slug&gt;.md</c> —
        /// topic pages stop at the first <c>### </c> line, so any method sections inside
        /// the file are intentionally ignored. Returns a plain "not found" page when no
        /// overlay file exists for <paramref name="slug"/>.
        /// </summary>
        public virtual string RenderTopicPage(string slug)
        {
            var prelude = LoadGroupPreludeSafe(slug);
            var sb = new StringBuilder();
            sb.AppendLine($"# `{slug}`");
            sb.AppendLine();
            if (string.IsNullOrWhiteSpace(prelude))
            {
                sb.AppendLine($"No overlay body found for topic `{slug}`. Add `Docs~/WikiSource/{slug}.md` to author one.");
            }
            else
            {
                sb.AppendLine(prelude);
            }
            sb.AppendLine();
            return sb.ToString();
        }

        /// <summary>
        /// Render a namespace-scoped page listing every method in that namespace.
        /// </summary>
        public virtual string RenderNamespacePage(string ns, IEnumerable<IMcpTool> tools)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# `{ns}`");
            sb.AppendLine();

            var prelude = LoadGroupPreludeSafe(ns);
            if (!string.IsNullOrWhiteSpace(prelude))
            {
                sb.AppendLine(prelude);
                sb.AppendLine();
            }

            sb.AppendLine("To read a method's wiki, call `tools/call` with `arguments = { method: \"<dotted>\" }` (omit `args`). Include `args` (even `{}`) to execute.");
            sb.AppendLine();
            sb.AppendLine("## Methods");
            sb.AppendLine();
            foreach (var tool in tools ?? Array.Empty<IMcpTool>())
            {
                var dotted = ToDottedName(tool.Name);
                var summary = GetSummary(tool, truncate: true);
                if (string.IsNullOrEmpty(summary))
                    sb.AppendLine($"- `{dotted}`");
                else
                    sb.AppendLine($"- `{dotted}` — {summary}");
            }
            sb.AppendLine();
            return sb.ToString();
        }

        // ── Per-tool page assembly ───────────────────────────────────────────

        /// <summary>
        /// Override to replace or extend the entire per-tool document structure.
        /// </summary>
        protected virtual string BuildMarkdown(IMcpTool tool, string skillName)
        {
            var sb = new StringBuilder();
            var dotted = ToDottedName(tool.Name);
            var title = tool.Title ?? tool.Name;
            var description = tool.Description ?? string.Empty;
            var yamlDescription = ResolveYamlDescription(tool.SkillDescription, description, tool.Name);

            // YAML front-matter
            sb.AppendLine("---");
            sb.AppendLine($"name: {EscapeYaml(skillName)}");
            sb.AppendLine($"description: {EscapeYaml(yamlDescription)}");
            sb.AppendLine("---");
            sb.AppendLine();

            // Title
            sb.AppendLine($"# {title}");
            sb.AppendLine();

            // Linkified description — even when IncludeDescriptionBody is false, the wiki page wants
            // the prose right after the title so siblings get backticked.
            var linkified = LinkifyDescription(description, EmptyKebabPool, tool.Name);
            if (IncludeDescriptionBody && !string.IsNullOrWhiteSpace(description))
            {
                sb.AppendLine(description);
                sb.AppendLine();
            }
            else if (!string.IsNullOrWhiteSpace(linkified))
            {
                sb.AppendLine(linkified);
                sb.AppendLine();
            }

            // How to Call
            sb.AppendLine("## How to Call");
            sb.AppendLine();
            BuildHowToCallHeading(sb);

            var inputExample = BuildInputExample(tool.InputSchema);
            BuildToolCommand(sb, tool, inputExample, dotted);

            // Input section
            sb.AppendLine("## Input");
            sb.AppendLine();

            if (IncludeParameterTable)
            {
                AppendParameterTable(sb, tool.InputSchema);
                sb.AppendLine();
            }

            if (IncludeInputJsonSchema)
            {
                BuildInputJsonSchemaBlock(sb, tool);
            }

            // Output section
            if (IncludeOutputSection)
            {
                sb.AppendLine("## Output");
                sb.AppendLine();
                BuildOutputSchemaBlock(sb, tool);
                sb.AppendLine();
            }

            // Per-method overlay: pulled from Docs~/WikiSource/<namespace>.md's
            // `### <full.method.name>` section via WikiOverlay. Injected as a `## Notes`
            // block before any "Related Methods" tail so authored guidance sits next to
            // the generated parameter/output blocks.
            var notes = LoadMethodSectionSafe(ToDottedName(tool.Name));
            if (!string.IsNullOrWhiteSpace(notes))
            {
                sb.AppendLine("## Notes");
                sb.AppendLine();
                sb.AppendLine(notes);
                sb.AppendLine();
            }

            // Related-methods tail block, when the host provided a sibling list.
            var related = BuildRelatedMethodsBlock(tool);
            if (!string.IsNullOrEmpty(related))
            {
                sb.AppendLine(related);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Sub-heading at the top of the <c>## How to Call</c> section. Override to replace.
        /// </summary>
        protected virtual void BuildHowToCallHeading(StringBuilder sb)
        {
            sb.AppendLine("Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.");
            sb.AppendLine();
        }

        /// <summary>
        /// Emit the execute and discovery examples under <c>## How to Call</c> as MCP
        /// <c>tools/call</c> argument payloads. Clients route these through their own MCP
        /// transport — the wiki does not advertise the HTTP endpoint.
        /// </summary>
        protected virtual void BuildToolCommand(StringBuilder sb, IMcpTool tool, string inputExample, string dotted)
        {
            // Execute mode: arguments carry method + args.
            sb.AppendLine("### Execute");
            sb.AppendLine();
            sb.AppendLine("```json");
            sb.AppendLine($"{{ \"method\": \"{dotted}\", \"args\": {inputExample} }}");
            sb.AppendLine("```");
            sb.AppendLine();

            sb.AppendLine("### Discovery (read this wiki)");
            sb.AppendLine();
            sb.AppendLine("Omit `args` to fetch this wiki page instead of executing:");
            sb.AppendLine("```json");
            sb.AppendLine($"{{ \"method\": \"{dotted}\" }}");
            sb.AppendLine("```");
            sb.AppendLine();
        }

        /// <summary>
        /// One row's worth of metadata parsed from an MCP tool's <see cref="IMcpTool.InputSchema"/>
        /// <c>properties</c> map: name, JSON-Schema type string (or <c>"any"</c>), the JSON-Schema
        /// <c>description</c> (or null), and whether the property is listed in <c>required</c>.
        /// Shared by the wiki parameter-table renderer and any caller that wants the same view of
        /// a tool's inputs (e.g. <c>Tool.List</c>) — single source of truth, no divergence.
        /// </summary>
        public readonly struct ParameterRow
        {
            public string Name { get; }
            public string Type { get; }
            public string? Description { get; }
            public bool Required { get; }

            public ParameterRow(string name, string type, string? description, bool required)
            {
                Name = name;
                Type = type;
                Description = description;
                Required = required;
            }
        }

        /// <summary>
        /// Parse an MCP tool's input schema into an ordered list of <see cref="ParameterRow"/>s
        /// (one per top-level property, in declared order). Returns an empty list when the schema
        /// is null, lacks <c>properties</c>, or declares zero properties — caller can branch on
        /// emptiness to print "no input parameters". Required-set membership is matched
        /// case-insensitively, matching the dispatcher's auto-param validation.
        /// </summary>
        public static IReadOnlyList<ParameterRow> ParseParameterRows(JsonNode? inputSchema)
        {
            if (inputSchema is not JsonObject schemaObject)
                return Array.Empty<ParameterRow>();

            if (schemaObject["properties"] is not JsonObject properties || properties.Count == 0)
                return Array.Empty<ParameterRow>();

            var required = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (schemaObject["required"] is JsonArray requiredArray)
            {
                foreach (var item in requiredArray)
                    if (item?.GetValue<string>() is string r)
                        required.Add(r);
            }

            var rows = new List<ParameterRow>(properties.Count);
            foreach (var prop in properties)
            {
                var propName = prop.Key;
                var propNode = prop.Value as JsonObject;
                var propType = propNode?["type"]?.GetValue<string>() ?? "any";
                var propDesc = propNode?["description"]?.GetValue<string>();
                rows.Add(new ParameterRow(propName, propType, propDesc, required.Contains(propName)));
            }
            return rows;
        }

        /// <summary>
        /// Emit the parameter table inside the <c>## Input</c> section.
        /// </summary>
        protected virtual void AppendParameterTable(StringBuilder sb, JsonNode? inputSchema)
        {
            var rows = ParseParameterRows(inputSchema);
            if (rows.Count == 0)
            {
                sb.AppendLine("This tool takes no input parameters.");
                return;
            }

            sb.AppendLine("| Name | Type | Required | Description |");
            sb.AppendLine("|------|------|----------|-------------|");

            foreach (var row in rows)
            {
                var isRequired = row.Required ? "Yes" : "No";
                sb.AppendLine($"| `{row.Name}` | `{row.Type}` | {isRequired} | {row.Description ?? string.Empty} |");
            }
        }

        /// <summary>
        /// Emit the <c>### Input JSON Schema</c> code block. Strips per-property descriptions
        /// when <see cref="IncludeInputSchemaPropertyDescriptions"/> is false.
        /// </summary>
        protected virtual void BuildInputJsonSchemaBlock(StringBuilder sb, IMcpTool tool)
        {
            var schema = tool.InputSchema;

            if (!IncludeInputSchemaPropertyDescriptions && schema != null)
                schema = StripPropertyDescriptions(schema);

            sb.AppendLine("### Input JSON Schema");
            sb.AppendLine();
            sb.AppendLine("```json");
            sb.AppendLine(PrettyPrintJson(schema));
            sb.AppendLine("```");
            sb.AppendLine();
        }

        /// <summary>
        /// Emit the output schema block, or a "no structured output" line when the tool has none.
        /// </summary>
        protected virtual void BuildOutputSchemaBlock(StringBuilder sb, IMcpTool tool)
        {
            sb.AppendLine("This tool does not return a structured schema; results are delivered as JSON-RPC result content.");
        }

        /// <summary>
        /// Build the per-tool "Related Methods" trailing block.
        /// Subclasses may override to inject siblings discovered from the registry.
        /// The default emits nothing; the live wiki call site enriches via subclass.
        /// </summary>
        protected virtual string? BuildRelatedMethodsBlock(IMcpTool tool) => null;

        // ── Wiki overlay seams ───────────────────────────────────────────────

        /// <summary>
        /// Returns the per-namespace prelude markdown for <paramref name="ns"/>, or null if absent.
        /// Forwards to <see cref="WikiOverlay.LoadGroupPrelude"/>; missing or empty overlays
        /// return null so the renderer skips the prelude block entirely. Tests / specialised
        /// emitters may override to supply fixture-backed overlays.
        /// </summary>
        protected virtual string? LoadGroupPreludeSafe(string? ns) => WikiOverlay.LoadGroupPrelude(ns);

        /// <summary>
        /// Returns the per-method overlay markdown for <paramref name="method"/>, or null if absent.
        /// Forwards to <see cref="WikiOverlay.LoadMethodSection"/>; missing sections / methods
        /// without a namespace prefix return null so the renderer skips the <c>## Notes</c>
        /// block entirely. Tests / specialised emitters may override.
        /// </summary>
        protected virtual string? LoadMethodSectionSafe(string method) => WikiOverlay.LoadMethodSection(method);

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// "gameobject-find" → "gameobject.find"; ids without '-' stay as-is.
        /// </summary>
        protected static string ToDottedName(string toolId)
        {
            if (string.IsNullOrEmpty(toolId))
                return string.Empty;
            var idx = toolId.IndexOf('-');
            if (idx < 0)
                return toolId;
            return toolId.Substring(0, idx) + "." + toolId.Substring(idx + 1);
        }

        /// <summary>
        /// Wrap every recognised tool-id mention in backticks. Already-backticked or unknown ids
        /// are left untouched. The mention regex is reused by subclasses for sibling discovery.
        /// </summary>
        protected static string LinkifyDescription(string text, IReadOnlyCollection<string>? allKebabIds, string? selfKebabId)
        {
            if (string.IsNullOrEmpty(text) || allKebabIds == null || allKebabIds.Count == 0)
                return text;
            var known = new HashSet<string>(allKebabIds, StringComparer.OrdinalIgnoreCase);

            return _toolIdMentionRegex.Replace(text, match =>
            {
                var delim = match.Groups[1].Value;
                var id = match.Groups[2].Value;

                if (delim == "`")
                    return match.Value;
                if (!known.Contains(id))
                    return match.Value;
                if (selfKebabId != null && string.Equals(id, selfKebabId, StringComparison.OrdinalIgnoreCase))
                    return match.Value;

                return "`" + id + "`";
            });
        }

        /// <summary>
        /// Compact JSON example built from the input schema's <c>properties</c> + per-type
        /// placeholder values. Returns <c>"{}"</c> when no properties are declared.
        /// </summary>
        protected virtual string BuildInputExample(JsonNode? inputSchema)
        {
            if (inputSchema == null)
                return "{}";

            var properties = inputSchema["properties"] as JsonObject;
            if (properties == null || properties.Count == 0)
                return "{}";

            var example = new JsonObject();
            foreach (var prop in properties)
            {
                var propNode = prop.Value as JsonObject;
                var propType = propNode?["type"]?.GetValue<string>() ?? "string";
                example[prop.Key] = CreateExampleValue(propType, propNode);
            }

            return example.ToJsonString(_prettyJsonOptions);
        }

        /// <summary>
        /// Pretty-print a JsonNode with the renderer's shared serialization options.
        /// </summary>
        protected virtual string PrettyPrintJson(JsonNode? node)
        {
            if (node == null)
                return "null";
            try
            {
                return node.ToJsonString(_prettyJsonOptions);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "{class}.{method}: Failed to pretty-print JSON schema.",
                    nameof(WikiMarkdownEmitter), nameof(PrettyPrintJson));
                return node.ToString();
            }
        }

        /// <summary>
        /// Placeholder value for the given JSON Schema type string.
        /// </summary>
        protected static JsonNode CreateExampleValue(string type, JsonObject? schema)
        {
            return type switch
            {
                "integer" => JsonValue.Create(0)!,
                "number" => JsonValue.Create(0.0)!,
                "boolean" => JsonValue.Create(false)!,
                "array" => new JsonArray(),
                "object" => new JsonObject(),
                "null" => JsonValue.Create((string?)null)!,
                _ => JsonValue.Create("string_value")!,
            };
        }

        /// <summary>
        /// Convert a tool name into a valid Agent Skills directory name (lowercase alphanumeric and
        /// hyphens only, no leading/trailing/consecutive hyphens). Falls back to a stable hash when
        /// the sanitised result would be empty.
        /// </summary>
        protected static string SanitizeSkillName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "tool-" + StableShortHash(name ?? string.Empty);

            var sb = new StringBuilder();
            var lastWasHyphen = false;

            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(char.ToLowerInvariant(c));
                    lastWasHyphen = false;
                }
                else if (sb.Length > 0 && !lastWasHyphen)
                {
                    sb.Append('-');
                    lastWasHyphen = true;
                }
            }

            while (sb.Length > 0 && sb[sb.Length - 1] == '-')
                sb.Length--;

            return sb.Length > 0 ? sb.ToString() : "tool-" + StableShortHash(name);
        }

        /// <summary>
        /// FNV-1a 32-bit hash over UTF-8 bytes, formatted as 8 lowercase hex chars.
        /// </summary>
        protected static string StableShortHash(string value)
        {
            uint hash = 2166136261u;
            foreach (byte b in Encoding.UTF8.GetBytes(value ?? string.Empty))
            {
                hash ^= b;
                hash *= 16777619u;
            }
            return hash.ToString("x8");
        }

        /// <summary>
        /// Escape a string for safe use as a YAML scalar value. Multi-line values become a
        /// YAML literal block scalar (<c>|-</c>) with two-space indentation.
        /// </summary>
        protected static string EscapeYaml(string value)
        {
            value ??= string.Empty;
            if (value.Contains('\n'))
            {
                var indent = "  ";
                var normalized = value.Replace("\r\n", "\n").Replace("\r", "\n");
                var indented = string.Join("\n" + indent, normalized.TrimEnd('\n').Split('\n'));
                return $"|-\n{indent}{indented}";
            }
            if (value.Contains(':') || value.Contains('"'))
                return $"\"{value.Replace("\"", "\\\"")}\"";
            return value;
        }

        /// <summary>
        /// Pick the text for the SKILL.md YAML <c>description:</c> field, applying
        /// <see cref="MaxSkillDescriptionLength"/>. <see cref="IMcpTool.SkillDescription"/> wins
        /// when set; otherwise the full description is used, truncated with an ellipsis.
        /// </summary>
        protected virtual string ResolveYamlDescription(string? skillDescription, string fullDescription, string toolOrSkillName)
        {
            var cap = MaxSkillDescriptionLength;

            if (!string.IsNullOrEmpty(skillDescription))
            {
                if (cap > 0 && skillDescription!.Length > cap)
                {
                    _logger?.LogWarning(
                        "{class}: SkillDescription for '{name}' is {len} chars, exceeding the YAML cap of {cap}. Truncating.",
                        nameof(WikiMarkdownEmitter), toolOrSkillName, skillDescription.Length, cap);
                    return TruncateForYaml(skillDescription, cap);
                }
                return skillDescription!;
            }

            if (cap > 0 && fullDescription.Length > cap)
            {
                _logger?.LogWarning(
                    "{class}: Description for '{name}' is {len} chars, exceeding the YAML cap of {cap}. Truncating for the YAML scalar.",
                    nameof(WikiMarkdownEmitter), toolOrSkillName, fullDescription.Length, cap);
                return TruncateForYaml(fullDescription, cap);
            }

            return fullDescription ?? string.Empty;
        }

        /// <summary>
        /// Truncate to at most <paramref name="maxLength"/> characters; pull the cut back to the
        /// previous whitespace within the last 32 chars; pull the cut back to the last
        /// <c>```</c> fence when truncation would land inside an unbalanced fence block.
        /// </summary>
        protected static string TruncateForYaml(string value, int maxLength)
        {
            if (maxLength <= 0 || value.Length <= maxLength)
                return value;

            const string ellipsis = "…";
            var budget = maxLength - ellipsis.Length;
            if (budget <= 0)
                return value.Substring(0, maxLength);

            var cut = budget;

            var minBoundary = Math.Max(0, budget - 32);
            for (var i = budget; i >= minBoundary; i--)
            {
                if (char.IsWhiteSpace(value[i]))
                {
                    cut = i;
                    break;
                }
            }

            var fenceCount = 0;
            var lastFenceStart = -1;
            for (var i = 0; i + 2 < cut; i++)
            {
                if (value[i] == '`' && value[i + 1] == '`' && value[i + 2] == '`')
                {
                    fenceCount++;
                    lastFenceStart = i;
                    i += 2;
                }
            }
            if ((fenceCount % 2) == 1 && lastFenceStart >= 0)
                cut = lastFenceStart;

            if (cut <= 0)
                cut = budget;

            return value.Substring(0, cut).TrimEnd() + ellipsis;
        }

        /// <summary>
        /// Deep-copy the schema and strip <c>description</c> fields from each top-level property.
        /// </summary>
        protected static JsonNode StripPropertyDescriptions(JsonNode schema)
        {
            var copy = JsonNode.Parse(schema.ToJsonString())!;
            if (copy["properties"] is JsonObject properties)
            {
                foreach (var kvp in properties)
                {
                    if (kvp.Value is JsonObject propObj)
                        propObj.Remove("description");
                }
            }
            return copy;
        }

        /// <summary>
        /// Pick the description text used in summary listings; truncated at a fixed cap when long.
        /// </summary>
        protected static string GetSummary(IMcpTool? tool, bool truncate)
        {
            if (tool == null)
                return string.Empty;
            var summary = !string.IsNullOrEmpty(tool.SkillDescription)
                ? tool.SkillDescription!
                : (tool.Description ?? string.Empty);
            if (truncate && summary.Length > SummaryTruncateLength)
                summary = summary.Substring(0, SummaryTruncateLength).TrimEnd() + "…";
            return summary;
        }

        /// <summary>
        /// Compiled tool-id mention regex exposed to subclasses that scan descriptions for sibling refs.
        /// </summary>
        protected static Regex ToolIdMentionRegex => _toolIdMentionRegex;

        // Subclasses that have access to the live registry can pass a non-empty pool; the base emitter
        // does not depend on the registry directly to keep wiki rendering testable in isolation.
        static readonly IReadOnlyCollection<string> EmptyKebabPool = Array.Empty<string>();
    }
}
