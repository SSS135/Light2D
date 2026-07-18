#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using com.IvanMurzak.ReflectorNet.Model;
using EA;

namespace EA
{
    public class ToolToggleInput
    {
        [Description("Name of the MCP tool to enable or disable.")]
        public string Name { get; set; } = string.Empty;

        [Description("Whether the tool should be enabled (true) or disabled (false).")]
        public bool Enabled { get; set; }
    }
}
