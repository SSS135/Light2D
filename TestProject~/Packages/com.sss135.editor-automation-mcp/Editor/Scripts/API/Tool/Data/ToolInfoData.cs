#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using com.IvanMurzak.ReflectorNet.Model;
using EA;

namespace EA
{
    [Description("MCP tool information.")]
    public class ToolInfoData
    {
        [JsonInclude, JsonPropertyName("name")]
        [Description("Tool name.")]
        public string Name { get; set; } = string.Empty;

        [JsonInclude, JsonPropertyName("description")]
        [Description("Tool description.")]
        public string? Description { get; set; }

        [JsonInclude, JsonPropertyName("inputs")]
        [Description("Tool input arguments.")]
        public ToolInputData[]? Inputs { get; set; }
    }
}
