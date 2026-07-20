#nullable enable
using System.ComponentModel;
using System.Text.Json.Nodes;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Editor.Utils;

namespace com.SSS135.EditorAutomation.Editor.API
{
    [McpToolType]
    public partial class Tool_EditorModal
    {
        public const string EditorModalStatusToolId = "system-editor-modal-status";

        [McpTool
        (
            EditorModalStatusToolId,
            Title = "System / Editor / Modal Status",
            ReadOnlyHint = true,
            IdempotentHint = true,
            MainThreadRequired = false
        )]
        [Description("Returns metadata for any native Win32 modal dialog currently blocking Unity's main thread. " +
            "Use this to detect why a main-thread tool is hanging. " +
            "Hwnd values are stringified Int64; pass them back verbatim to 'system-editor-modal-respond'. " +
            "Does NOT detect IMGUI-rendered modal EditorWindows — those have no Win32 identity.")]
        public static McpResponse Status()
        {
            var dialogs = EditorWin32.TryEnumModalDialogs();

            var dialogArray = new JsonArray();
            foreach (var dialog in dialogs)
            {
                var buttonArray = new JsonArray();
                foreach (var button in dialog.Buttons)
                {
                    buttonArray.Add(new JsonObject
                    {
                        ["hwnd"] = button.Hwnd.ToInt64().ToString(),
                        ["text"] = button.Text,
                    });
                }

                dialogArray.Add(new JsonObject
                {
                    ["hwnd"] = dialog.Hwnd.ToInt64().ToString(),
                    ["title"] = dialog.Title,
                    ["buttons"] = buttonArray,
                });
            }

            return McpResponse.SuccessStructured(new JsonObject
            {
                ["active"] = dialogs.Count > 0,
                ["dialogs"] = dialogArray,
            });
        }
    }
}
