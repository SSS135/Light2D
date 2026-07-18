#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using com.IvanMurzak.ReflectorNet.Model;
using EA;

namespace EA
{
    public class DestroyComponentsResponse
    {
        [Description("List of destroyed components.")]
        public ComponentRefList? DestroyedComponents { get; set; }
    }
}
