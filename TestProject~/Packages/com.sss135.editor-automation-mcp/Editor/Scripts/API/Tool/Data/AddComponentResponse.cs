#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using com.IvanMurzak.ReflectorNet.Model;
using EA;

namespace EA
{
    public class AddComponentResponse
    {
        [Description("List of successfully added components.")]
        public List<ComponentDataShallow> AddedComponents { get; set; } = new List<ComponentDataShallow>();

        [Description("List of success messages for added components.")]
        public List<string>? Messages { get; set; }

        [Description("List of warnings encountered during component addition.")]
        public List<string>? Warnings { get; set; }

        [Description("List of errors encountered during component addition.")]
        public List<string>? Errors { get; set; }
    }
}
