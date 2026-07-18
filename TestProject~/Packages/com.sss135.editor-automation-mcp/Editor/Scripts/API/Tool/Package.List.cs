#nullable enable
using EA;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.Framework;
using UnityEditor.PackageManager;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Package
    {
        [Description("Filter for package source in listing operations.")]
        public enum PackageSourceFilter
        {
            [Description("Show all packages regardless of source.")]
            All,

            [Description("Packages from the Unity Registry (official Unity packages).")]
            Registry,

            [Description("Packages embedded directly in the project's Packages folder.")]
            Embedded,

            [Description("Packages referenced from a local folder path.")]
            Local,

            [Description("Packages installed from a Git repository URL.")]
            Git,

            [Description("Built-in Unity packages that come with the editor.")]
            BuiltIn,

            [Description("Packages installed from a local tarball (.tgz) file.")]
            LocalTarball
        }

        public const string PackageListToolId = "package-list";
        [McpTool
        (
            PackageListToolId,
            Title = "Package Manager / List Installed",
            ReadOnlyHint = true,
            IdempotentHint = true
        )]
        [Description("List all packages installed in the Unity project (UPM packages). " +
            "Returns information about each installed package including name, version, source, and description. " +
            "Use this to check which packages are currently installed before adding or removing packages.")]
        public async Task<List<PackageData>> List
        (
            [Description("Filter packages by source.")]
            PackageSourceFilter sourceFilter = PackageSourceFilter.All,
            [Description("Filter packages by name, display name, or description (case-insensitive). Results are prioritized: exact name match, exact display name match, name substring, display name substring, description substring.")]
            string? nameFilter = null,
            [Description("Include only direct dependencies (packages in manifest.json). If false, includes all resolved packages. Default: false")]
            bool directDependenciesOnly = false
        )
        {
            return await MainThread.Instance.RunAsync(async () =>
            {
                var listRequest = Client.List(directDependenciesOnly);

                while (!listRequest.IsCompleted)
                    await Task.Yield();

                if (listRequest.Status == StatusCode.Failure)
                    throw new Exception(Error.PackageListFailed(listRequest.Error?.message ?? "Unknown error"));

                var packages = listRequest.Result.AsEnumerable();

                // Apply source filter
                if (sourceFilter != PackageSourceFilter.All)
                {
                    var unitySource = sourceFilter switch
                    {
                        PackageSourceFilter.Registry => PackageSource.Registry,
                        PackageSourceFilter.Embedded => PackageSource.Embedded,
                        PackageSourceFilter.Local => PackageSource.Local,
                        PackageSourceFilter.Git => PackageSource.Git,
                        PackageSourceFilter.BuiltIn => PackageSource.BuiltIn,
                        PackageSourceFilter.LocalTarball => PackageSource.LocalTarball,
                        _ => PackageSource.Unknown
                    };
                    if (unitySource != PackageSource.Unknown)
                        packages = packages.Where(p => p.source == unitySource);
                }

                // Apply name filter with priority ordering
                if (!string.IsNullOrEmpty(nameFilter))
                {
                    return packages
                        .Select(p => (pkg: p, priority: GetSearchPriority(p.name, p.displayName, p.description, nameFilter!)))
                        .Where(x => x.priority > 0)
                        .OrderBy(x => x.priority)
                        .ThenBy(x => x.pkg.name)
                        .Select(x => PackageData.FromPackageInfo(x.pkg))
                        .ToList();
                }

                return packages
                    .OrderBy(p => p.name)
                    .Select(PackageData.FromPackageInfo)
                    .ToList();
            }).Unwrap();
        }
    }
}
