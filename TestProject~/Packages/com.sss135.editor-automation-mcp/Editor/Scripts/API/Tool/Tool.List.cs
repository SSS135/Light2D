#nullable enable
using EA;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Editor.Framework.Builder;
using com.SSS135.EditorAutomation.Editor.Framework.Wiki;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Tool
    {
        public const string ToolListId = "tool-list";

        static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(200);

        [Description("Specifies what to include for tool input arguments.")]
        public enum InputRequest
        {
            [Description("Do not include input arguments.")]
            None = 0,

            [Description("Include input argument names only.")]
            Inputs = 1,

            [Description("Include input argument names and descriptions.")]
            InputsWithDescription = 2
        }

        [McpTool
        (
            ToolListId,
            Title = "Tool / List",
            ReadOnlyHint = true,
            IdempotentHint = true
        )]
        [Description("List all available MCP tools. " +
            "Optionally filter by regex across tool names, descriptions, and arguments.")]
        public ToolInfoData[] List
        (
            [Description("Regex pattern to filter tools. " +
                "Matches against tool name, description, and argument names and descriptions.")]
            string? regexSearch = null,

            [Description("Include tool descriptions in the result. Default: false")]
            bool? includeDescription = false,

            [Description("Include input arguments in the result. Default: None")]
            InputRequest? includeInputs = InputRequest.None
        )
        {
            return MainThread.Instance.Run(() =>
            {
                // The legacy IToolManager was deleted with the SignalR bridge in Wave 2.
                // ToolRegistry (populated by FrameworkBootstrap on domain reload) is the
                // post-bridge source of truth for every registered [McpTool].
                var dottedNames = ToolRegistry.AllDottedMethods;
                if (dottedNames == null || dottedNames.Count == 0)
                    return Array.Empty<ToolInfoData>();

                Regex? regex = null;
                if (!string.IsNullOrWhiteSpace(regexSearch))
                {
                    try { regex = new Regex(regexSearch!, RegexOptions.IgnoreCase, RegexTimeout); }
                    catch (ArgumentException ex)
                    {
                        throw new ArgumentException($"Invalid regex pattern '{regexSearch}': {ex.Message}", nameof(regexSearch));
                    }
                }

                var inputsRequested = includeInputs ?? InputRequest.None;
                var descriptionRequested = includeDescription == true;
                var needInputs = regex != null || inputsRequested != InputRequest.None;

                var results = new List<ToolInfoData>();

                foreach (var dotted in dottedNames)
                {
                    var tool = ToolRegistry.FindTool(dotted);
                    if (tool == null)
                        continue;

                    List<ToolInputData>? inputs = null;

                    if (regex != null)
                    {
                        if (!MatchesTool(regex, tool, needInputs ? tool.InputSchema : null, out inputs))
                            continue;
                    }

                    var info = new ToolInfoData { Name = tool.Name };

                    if (descriptionRequested)
                        info.Description = tool.Description;

                    if (inputsRequested != InputRequest.None)
                    {
                        inputs ??= ParseInputs(tool.InputSchema);
                        info.Inputs = BuildInputResults(inputs, inputsRequested);
                    }

                    results.Add(info);
                }

                return results.ToArray();
            });
        }

        static bool MatchesTool(Regex regex, IMcpTool tool, JsonNode? schema, out List<ToolInputData>? parsedInputs)
        {
            parsedInputs = null;

            if (regex.IsMatch(tool.Name ?? string.Empty))
                return true;

            if (tool.Description != null && regex.IsMatch(tool.Description))
                return true;

            if (schema == null)
                return false;

            parsedInputs = ParseInputs(schema);
            foreach (var input in parsedInputs)
            {
                if (regex.IsMatch(input.Name))
                    return true;

                if (input.Description != null && regex.IsMatch(input.Description))
                    return true;
            }

            return false;
        }

        static ToolInputData[] BuildInputResults(List<ToolInputData> inputs, InputRequest request)
        {
            var result = new ToolInputData[inputs.Count];
            for (int i = 0; i < inputs.Count; i++)
            {
                result[i] = new ToolInputData
                {
                    Name = inputs[i].Name,
                    Description = request == InputRequest.InputsWithDescription
                        ? inputs[i].Description
                        : null
                };
            }
            return result;
        }

        // Forward to the canonical parser on WikiMarkdownEmitter so the wiki parameter table and
        // tool.list see the exact same view of every tool's inputs (name, description). Previously
        // this file rolled its own walk over `properties` and `description`, which diverged from
        // the wiki emitter when one side learned a new schema-shape detail and the other did not.
        static List<ToolInputData> ParseInputs(JsonNode? schema)
        {
            var rows = WikiMarkdownEmitter.ParseParameterRows(schema);
            var result = new List<ToolInputData>(rows.Count);
            foreach (var row in rows)
                result.Add(new ToolInputData { Name = row.Name, Description = row.Description });
            return result;
        }
    }
}
