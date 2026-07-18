#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using com.IvanMurzak.ReflectorNet.Model;
using EA;

namespace EA
{
    public class ComponentListResult
    {
        [Description("Array of component type names for the current page.")]
        public string[] Items { get; set; } = Array.Empty<string>();

        [Description("Current page number (0-based).")]
        public int Page { get; set; }

        [Description("Number of items per page.")]
        public int PageSize { get; set; }

        [Description("Total number of matching components.")]
        public int TotalCount { get; set; }

        [Description("Total number of pages available.")]
        public int TotalPages { get; set; }
    }
}
