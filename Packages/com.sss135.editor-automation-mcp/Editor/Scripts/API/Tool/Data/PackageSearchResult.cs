#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using com.IvanMurzak.ReflectorNet.Model;
using EA;

namespace EA
{
    [Description("Package search result with available versions.")]
    public class PackageSearchResult
    {
        [Description("The official Unity name of the package used as the package ID.")]
        public string Name { get; set; } = string.Empty;

        [Description("The display name of the package.")]
        public string DisplayName { get; set; } = string.Empty;

        [Description("The latest version available in the registry.")]
        public string LatestVersion { get; set; } = string.Empty;

        [Description("A brief description of the package.")]
        public string Description { get; set; } = string.Empty;

        [Description("Whether this package is already installed in the project.")]
        public bool IsInstalled { get; set; } = false;

        [Description("The currently installed version (if installed).")]
        public string? InstalledVersion { get; set; }

        [Description("Available versions of this package (up to 5 most recent).")]
        public List<string> AvailableVersions { get; set; } = new();
    }
}
