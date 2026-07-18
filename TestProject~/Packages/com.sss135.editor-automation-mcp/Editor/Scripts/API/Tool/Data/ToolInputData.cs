#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using com.IvanMurzak.ReflectorNet.Model;
using EA;

namespace EA
{
    [Description("MCP tool input argument.")]
    public class ToolInputData
    {
        [JsonInclude, JsonPropertyName("name")]
        [Description("Argument name.")]
        public string Name { get; set; } = string.Empty;

        [JsonInclude, JsonPropertyName("description")]
        [Description("Argument description.")]
        public string? Description { get; set; }
    }
}
