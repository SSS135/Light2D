#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using com.IvanMurzak.ReflectorNet.Model;
using EA;

namespace EA
{
    public class ModifyComponentResponse
    {
        [Description("Whether the modification was successful.")]
        public bool Success { get; set; } = false;

        [Description("Reference to the modified component.")]
        public ComponentRef? Reference { get; set; }

        [Description("Index of the component in the GameObject's component list.")]
        public int Index { get; set; }

        [Description("Updated component information after modification.")]
        public ComponentDataShallow? Component { get; set; }
        [Description("Log of modifications made and any warnings/errors encountered.")]
        public string[]? Logs { get; set; }
    }
}
