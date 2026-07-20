#nullable enable
using System;
using System.ComponentModel;

namespace EA
{
    public sealed class ShaderListResult
    {
        [Description("Names of all available shaders in the project assets and packages.")]
        public string[] Shaders { get; set; } = Array.Empty<string>();

        [Description("Number of shaders returned.")]
        public int Count { get; set; }
    }
}
