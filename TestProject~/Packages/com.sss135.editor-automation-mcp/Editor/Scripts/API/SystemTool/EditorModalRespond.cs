#nullable enable
using System;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Editor.Utils;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_EditorModal
    {
        public const string EditorModalRespondToolId = "system-editor-modal-respond";

        // Destructive keywords matched against button text (lowercased).
        // Caller must pass acknowledgeDestructive=true to click a button whose text contains any of these.
        static readonly string[] _destructiveKeywords =
        {
            "delete",
            "don't save",
            "discard",
            "reset",
            "remove",
            "overwrite",
            "abandon",
        };

        [McpTool
        (
            EditorModalRespondToolId,
            Title = "System / Editor / Modal Respond",
            DestructiveHint = true,
            MainThreadRequired = false
        )]
        [Description("Programmatically clicks a button on a native modal dialog previously surfaced by system-editor-modal-status. " +
            "Refuses to click obviously-destructive buttons unless acknowledgeDestructive=true.")]
        public static McpResponse Respond
        (
            [Description("hwnd of the dialog as returned by system-editor-modal-status (stringified Int64).")]
            string hwnd,
            [Description("Either a button text (case-insensitive) or stringified Int64 button hwnd. Text is matched against the dialog's currently-enumerated buttons.")]
            string button,
            [Description("Set true to confirm clicking buttons whose text matches a destructive keyword (delete, don't save, discard, reset, remove, overwrite, abandon). Default false.")]
            bool acknowledgeDestructive = false
        )
        {
            if (string.IsNullOrEmpty(hwnd))
                return McpResponse.Error("hwnd is required.");
            if (string.IsNullOrEmpty(button))
                return McpResponse.Error("button is required.");

            if (!TryParseHwnd(hwnd, out var dialogHandle))
                return McpResponse.Error("hwnd is not a valid integer.");

            // Re-enumerate so we confirm the same dialog is still up before clicking.
            // Without this, a stale hwnd could collide with a different modal that opened in the meantime.
            var dialogs = EditorWin32.TryEnumModalDialogs();
            EditorWin32.ModalDialog? match = null;
            foreach (var d in dialogs)
            {
                if (d.Hwnd == dialogHandle)
                {
                    match = d;
                    break;
                }
            }

            if (match == null)
            {
                return McpResponse.SuccessStructured(new JsonObject
                {
                    ["status"] = "modal_gone",
                    ["hint"] = "The dialog identified by hwnd is no longer present — it may have been dismissed manually.",
                });
            }

            // Match button: prefer hwnd parse, fall back to case-insensitive text equality.
            EditorWin32.ModalButton? buttonMatch = null;
            if (TryParseHwnd(button, out var buttonHandle))
            {
                foreach (var b in match.Buttons)
                {
                    if (b.Hwnd == buttonHandle)
                    {
                        buttonMatch = b;
                        break;
                    }
                }
            }

            if (buttonMatch == null)
            {
                foreach (var b in match.Buttons)
                {
                    if (string.Equals(b.Text, button, StringComparison.OrdinalIgnoreCase))
                    {
                        buttonMatch = b;
                        break;
                    }
                }
            }

            if (buttonMatch == null)
            {
                var available = new StringBuilder();
                available.Append('[');
                for (var i = 0; i < match.Buttons.Count; i++)
                {
                    if (i > 0) available.Append(", ");
                    available.Append('"').Append(match.Buttons[i].Text).Append('"');
                }
                available.Append(']');
                return McpResponse.Error($"button '{button}' not found on dialog; available: {available}");
            }

            if (!acknowledgeDestructive && IsDestructive(buttonMatch.Text))
            {
                return McpResponse.Error("Button text matches destructive heuristic. Pass acknowledgeDestructive:true to confirm.");
            }

            var success = EditorWin32.TryClickButton(buttonMatch.Hwnd);
            return McpResponse.SuccessStructured(new JsonObject
            {
                ["status"] = "clicked",
                ["button"] = buttonMatch.Text,
                ["success"] = success,
            });
        }

        static bool TryParseHwnd(string s, out IntPtr hwnd)
        {
            if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var asLong))
            {
                hwnd = new IntPtr(asLong);
                return true;
            }
            hwnd = IntPtr.Zero;
            return false;
        }

        static bool IsDestructive(string buttonText)
        {
            if (string.IsNullOrEmpty(buttonText)) return false;
            var lowered = buttonText.ToLowerInvariant();
            foreach (var keyword in _destructiveKeywords)
            {
                if (lowered.Contains(keyword)) return true;
            }
            return false;
        }
    }
}
