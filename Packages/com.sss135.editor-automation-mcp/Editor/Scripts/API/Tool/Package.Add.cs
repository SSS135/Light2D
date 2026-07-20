#nullable enable
using System.ComponentModel;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Editor.Framework.Jobs;
using com.SSS135.EditorAutomation.Editor.Utils;
using UnityEditor.PackageManager;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Package
    {
        public const string PackageAddToolId = "package-add";
        [McpTool
        (
            PackageAddToolId,
            Title = "Package Manager / Add",
            OpenWorldHint = true
        )]
        [Description("Install a package from the Unity Package Manager registry, Git URL, or local path. " +
            "This operation modifies the project's manifest.json and triggers package resolution. " +
            "Note: Package installation may trigger a domain reload. The tool returns a Processing response " +
            "carrying a job ticket id; poll 'system-job-status' with that id for the final outcome.")]
        public static McpResponse Add
        (
            [Description("The package ID to install. " +
                "Formats: Package ID 'com.unity.textmeshpro' (installs latest compatible version), " +
                "Package ID with version 'com.unity.textmeshpro@3.0.6', " +
                "Git URL 'https://github.com/user/repo.git', " +
                "Git URL with branch/tag 'https://github.com/user/repo.git#v1.0.0', " +
                "Local path 'file:../MyPackage'.")]
            string packageId
        )
        {
            if (string.IsNullOrWhiteSpace(packageId))
                return McpResponse.Error(Error.PackageIdentifierIsEmpty());

            return JobHelpers.StartJob(
                method: "package.add",
                startedPayload: new JsonObject
                {
                    ["package_id"] = packageId,
                    ["operation"] = "add",
                },
                bind: (ticketId, complete) =>
                {
                    MainThread.Instance.RunAsync(async () =>
                    {
                        await Task.Yield();

                        var addRequest = Client.Add(packageId);
                        while (!addRequest.IsCompleted)
                            await Task.Yield();

                        var success = addRequest.Status == StatusCode.Success;
                        if (!success)
                        {
                            var errorMessage = Error.PackageOperationFailed("add", packageId, addRequest.Error?.message ?? "Unknown error");
                            complete(McpResponse.Error(errorMessage));
                            return;
                        }

                        var packageName = addRequest.Result.name;
                        var version = addRequest.Result.version;
                        var displayName = addRequest.Result.displayName ?? packageName;

                        // The terminal notification may arrive on the OTHER side of a domain reload that
                        // wipes the in-memory ticket. PackageUtils persists the ticket id to SessionState
                        // and calls JobRegistry.Complete from the post-reload ProcessPendingNotifications.
                        PackageUtils.SchedulePostDomainReloadNotification(
                            ticketId,
                            $"{displayName} v{version}",
                            "add",
                            expectedResult: true);
                    });
                });
        }
    }
}
