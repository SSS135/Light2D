#nullable enable
using System;
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
        public const string PackageRemoveToolId = "package-remove";
        [McpTool
        (
            PackageRemoveToolId,
            Title = "Package Manager / Remove",
            DestructiveHint = true
        )]
        [Description("Remove (uninstall) a package from the Unity project. " +
            "This removes the package from the project's manifest.json and triggers package resolution. " +
            "Note: Built-in packages and packages that are dependencies of other installed packages cannot be removed. " +
            "Note: Package removal may trigger a domain reload. Returns a Processing response carrying a job ticket id; " +
            "poll 'system-job-status' with that id for the final outcome.")]
        public static McpResponse Remove
        (
            [Description("The ID of the package to remove. Example: 'com.unity.textmeshpro'. Do not include version number.")]
            string packageId
        )
        {
            if (string.IsNullOrWhiteSpace(packageId))
                return McpResponse.Error(Error.PackageNameIsEmpty());

            // Remove version suffix if accidentally included
            var cleanPackageId = packageId.Contains("@", StringComparison.OrdinalIgnoreCase)
                ? packageId.Substring(0, packageId.IndexOf('@', StringComparison.OrdinalIgnoreCase))
                : packageId;

            return JobHelpers.StartJob(
                method: "package.remove",
                startedPayload: new JsonObject
                {
                    ["package_id"] = cleanPackageId,
                    ["operation"] = "remove",
                },
                bind: (ticketId, complete) =>
                {
                    MainThread.Instance.RunAsync(async () =>
                    {
                        await Task.Yield();

                        // First verify the package is installed
                        var listRequest = Client.List(offlineMode: true);
                        while (!listRequest.IsCompleted)
                            await Task.Yield();

                        if (listRequest.Status == StatusCode.Success)
                        {
                            var isInstalled = false;
                            foreach (var pkg in listRequest.Result)
                            {
                                if (pkg.name.Equals(cleanPackageId, StringComparison.OrdinalIgnoreCase))
                                {
                                    isInstalled = true;
                                    break;
                                }
                            }

                            if (!isInstalled)
                            {
                                complete(McpResponse.Error(Error.PackageNotFound(cleanPackageId)));
                                return;
                            }
                        }

                        var removeRequest = Client.Remove(cleanPackageId);
                        while (!removeRequest.IsCompleted)
                            await Task.Yield();

                        if (removeRequest.Status == StatusCode.Failure)
                        {
                            var errorMessage = Error.PackageOperationFailed("remove", cleanPackageId, removeRequest.Error?.message ?? "Unknown error");
                            complete(McpResponse.Error(errorMessage));
                            return;
                        }

                        // Schedule notification across the post-reload boundary; PackageUtils
                        // persists the ticket id in SessionState and calls JobRegistry.Complete
                        // from ProcessPendingNotifications after the editor finishes reloading.
                        PackageUtils.SchedulePostDomainReloadNotification(
                            ticketId,
                            cleanPackageId,
                            "remove",
                            expectedResult: true);
                    });
                });
        }
    }
}
