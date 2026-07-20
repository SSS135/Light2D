#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using com.IvanMurzak.ReflectorNet.Model;
using EA;

namespace EA
{
    public class PassData
    {
        [Description("Index of this pass within the subshader.")]
        public int Index { get; set; }

        [Description("Name of the pass. Null if unnamed.")]
        public string? Name { get; set; }

        [Description("Source code of the pass. Null if unavailable.")]
        public string? SourceCode { get; set; }
    }
}
