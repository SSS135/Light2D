#nullable enable
using System.ComponentModel;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.Utils;
using UnityEditor;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Assets
    {
        public const string AssetsRefreshToolId = "assets-refresh";

        // EditorPrefs key backing Preferences → General → Script Changes While Playing. Value 1 ==
        // RecompileAfterFinishedPlaying — the only setting that silently defers a script recompile
        // while in Play mode (0 recompiles immediately, 2 stops play then recompiles). Under value 1
        // a just-edited script is NOT reflected by AssetDatabase.Refresh() and isCompiling stays false,
        // so a "refreshed" result cannot be trusted for code verification.
        const string kScriptCompilationDuringPlayKey = "ScriptCompilationDuringPlay";
        [McpTool
        (
            AssetsRefreshToolId,
            Title = "Assets / Refresh",
            IdempotentHint = true
        )]
        [Description("Refreshes the AssetDatabase. " +
            "Use it if any file was added or updated in the project outside of Unity API. " +
            "Use it if need to force scripts recompilation when '.cs' file changed.")]
        public async Task<McpResponse> Refresh
        (
            [Description("Asset import options.")]
            ImportAssetOptions? options = ImportAssetOptions.ForceSynchronousImport,
            [RequestID]
            string? requestId = null
        )
        {
            if (requestId == null || string.IsNullOrWhiteSpace(requestId))
                return McpResponse.Error("Original request with valid RequestID must be provided.");

            return await MainThread.Instance.RunAsync<McpResponse>(() =>
            {
                AssetDatabase.Refresh(options ?? ImportAssetOptions.ForceSynchronousImport);

                // AssetDatabase.Refresh() returns void and exposes no "a .cs changed" signal, so we
                // cannot tell an asset-only refresh from one that touched scripts. Under "Script Changes
                // While Playing = Recompile After Finished Playing" (pref value 1) ANY script edit made
                // before this call was silently deferred while isCompiling stays false — a "refreshed"
                // result would falsely look like the code is live. Conservatively fail with a hard error
                // (not a soft warning) so callers exit Play mode and re-refresh before verifying code.
                if (EditorApplication.isPlaying
                    && EditorPrefs.GetInt(kScriptCompilationDuringPlayKey, 0) == 1
                    && !EditorApplication.isCompiling)
                {
                    var deferred = McpResponse.Error(
                        "AssetDatabase refreshed, but script recompilation is deferred: the Editor is in Play mode " +
                        "with \"Script Changes While Playing = Recompile After Finished Playing\". Any edited '.cs' file " +
                        "will not take effect until you exit Play mode. Exit Play mode and re-run assets-refresh before " +
                        "verifying code changes.");
                    deferred.Metadata ??= new JsonObject();
                    deferred.Metadata["statusReason"] = "compilation_deferred_play_mode";
                    deferred.Metadata["recompiled"] = false;
                    deferred.Metadata["isPlaying"] = true;
                    deferred.Metadata["scriptCompilationDuringPlay"] = 1;
                    return AttachAutoRefreshWarning(deferred);
                }

                if (EditorApplication.isCompiling)
                {
                    ScriptUtils.SchedulePostCompilationNotification(requestId, "AssetDatabase", "Assets refresh");
                    return AttachAutoRefreshWarning(McpResponse.Processing("AssetDatabase refreshed. Compilation in progress, waiting for completion..."));
                }

                if (EditorUtility.scriptCompilationFailed)
                {
                    var errorDetails = ScriptUtils.GetCompilationErrorDetails();
                    var response = AttachAutoRefreshWarning(McpResponse.Success($"[Warning] AssetDatabase refreshed, but compilation errors exist:\n\n{errorDetails}"));
                    // Keep the human-readable joined string above for back-compat, and additionally expose
                    // the structured errors[] (shape shared with CompileRequestState) for machine consumers.
                    response.Metadata ??= new JsonObject();
                    response.Metadata["errors"] = ScriptUtils.GetCompilationErrorList();
                    return response;
                }

                return AttachAutoRefreshWarning(McpResponse.Success("AssetDatabase refreshed successfully."));
            });
        }

        // The Editor's Auto Refresh preference (Preferences → Asset Pipeline → Auto Refresh) is
        // persisted in EditorPrefs under the key `kAutoRefreshMode`. Value 0 = Disabled; -1 means
        // the user has not configured the pref yet (treat as default-on). When disabled, background
        // script reload / asset import does not run on its own once the editor loses focus — so
        // callers chaining assets.refresh with subsequent calls that depend on the import settling
        // need a heads-up. The warning is advisory only; the call itself still succeeds.
        static McpResponse AttachAutoRefreshWarning(McpResponse response)
        {
            const string kAutoRefreshModeKey = "kAutoRefreshMode";
            if (EditorPrefs.GetInt(kAutoRefreshModeKey, -1) != 0)
                return response;

            response.Metadata ??= new JsonObject();
            if (response.Metadata["warnings"] is not JsonArray warnings)
            {
                warnings = new JsonArray();
                response.Metadata["warnings"] = warnings;
            }
            warnings.Add(JsonValue.Create("Editor Auto Refresh is disabled (Preferences → Asset Pipeline → Auto Refresh = Disabled). Background script reload will not run until you manually focus the Editor or trigger a recompile."));
            return response;
        }
    }
}
