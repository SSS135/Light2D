#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using com.IvanMurzak.ReflectorNet.Model;
using EA;

namespace EA
{
    public class SubshaderData
    {
        [Description("Index of this subshader within the shader.")]
        public int Index { get; set; }

        [Description("Number of passes in this subshader.")]
        public int PassCount { get; set; }

        [Description("List of passes in this subshader. Null if no passes.")]
        public List<PassData>? Passes { get; set; }
    }
}
