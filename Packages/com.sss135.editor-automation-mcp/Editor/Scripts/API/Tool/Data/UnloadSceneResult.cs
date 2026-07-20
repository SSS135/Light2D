#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using com.IvanMurzak.ReflectorNet.Model;
using EA;

namespace EA
{
    public class UnloadSceneResult
    {
        [Description("Name of the unloaded scene.")]
        public string? Name { get; set; }
        [Description("Reference to the unloaded scene asset.")]
        public AssetObjectRef? AssetObjectRef { get; set; } = null!;
    }
}
