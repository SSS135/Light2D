#nullable enable

using System;
using System.Collections.Generic;
#if UNITY_EDITOR_WIN
using System.Diagnostics;
using System.Runtime.InteropServices;
#endif

namespace com.SSS135.EditorAutomation.Editor.Utils
{
    // Best-effort wake of the running Unity Editor window. Safe to call from any thread.
    // On non-Windows platforms this is a no-op.
    public static class EditorWin32
    {
#if UNITY_EDITOR_WIN
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] static extern uint keybd_event(byte vk, byte scan, uint flags, UIntPtr extraInfo);

        [DllImport("user32.dll")] static extern bool EnumThreadWindows(uint threadId, EnumWindowsProc cb, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowTextW(IntPtr hwnd, [Out] char[] buf, int max);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassNameW(IntPtr hwnd, [Out] char[] buf, int max);
        [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc cb, IntPtr lParam);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hwnd, int idx);
        [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

        delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        const byte VK_MENU = 0x12;
        const uint KEYEVENTF_KEYUP = 0x0002;
        const int SW_RESTORE = 9;

        const int GWL_STYLE = -16;
        const long WS_POPUP = 0x80000000L;
        const long WS_VISIBLE = 0x10000000L;
        const long WS_DISABLED = 0x08000000L;
        const uint GA_ROOTOWNER = 3;
        const uint BM_CLICK = 0x00F5;
#endif

        public sealed class ModalDialog
        {
            // Opaque token returned to caller; pass back verbatim to TryClickButton via ModalButton.Hwnd.
            public IntPtr Hwnd;
            public string Title = string.Empty;
            public List<ModalButton> Buttons = new();
        }

        public sealed class ModalButton
        {
            // Button window handle for click.
            public IntPtr Hwnd;
            public string Text = string.Empty;
        }

        public static bool TryWakeEditor()
        {
#if UNITY_EDITOR_WIN
            try
            {
                var hwnd = Process.GetCurrentProcess().MainWindowHandle;
                if (hwnd == IntPtr.Zero) return false;
                // ALT pulse defeats Windows' SetForegroundWindow restriction.
                keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
                keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
                return SetForegroundWindow(hwnd);
            }
            catch { return false; }
#else
            return false;
#endif
        }

        /// <summary>
        /// Enumerates native Win32 modal dialogs owned by the running Unity Editor's main window.
        /// Detects native Win32 modal dialogs (e.g. <c>EditorUtility.DisplayDialog</c>). Does NOT detect
        /// IMGUI-rendered modal <c>EditorWindow</c>s — those have no Win32 identity. Returns an empty list
        /// on non-Windows platforms or on enumeration failure. Safe to call from any thread.
        /// </summary>
        public static List<ModalDialog> TryEnumModalDialogs()
        {
            var result = new List<ModalDialog>();
#if UNITY_EDITOR_WIN
            try
            {
                var mainHwnd = Process.GetCurrentProcess().MainWindowHandle;
                if (mainHwnd == IntPtr.Zero) return result;

                var threadId = GetWindowThreadProcessId(mainHwnd, out _);
                if (threadId == 0) return result;

                EnumThreadWindows(threadId, (hwnd, _) =>
                {
                    try
                    {
                        var style = (long)(uint)GetWindowLong(hwnd, GWL_STYLE);
                        if ((style & WS_VISIBLE) == 0) return true;
                        if ((style & WS_POPUP) == 0) return true;
                        if ((style & WS_DISABLED) != 0) return true;

                        var rootOwner = GetAncestor(hwnd, GA_ROOTOWNER);
                        if (rootOwner != mainHwnd) return true;

                        var dialog = new ModalDialog
                        {
                            Hwnd = hwnd,
                            Title = ReadWindowText(hwnd),
                            Buttons = new List<ModalButton>(),
                        };

                        EnumChildWindows(hwnd, (childHwnd, _) =>
                        {
                            try
                            {
                                var className = ReadClassName(childHwnd);
                                if (string.Equals(className, "Button", StringComparison.Ordinal))
                                {
                                    dialog.Buttons.Add(new ModalButton
                                    {
                                        Hwnd = childHwnd,
                                        Text = ReadWindowText(childHwnd),
                                    });
                                }
                            }
                            catch { /* swallow per-child failures */ }
                            return true;
                        }, IntPtr.Zero);

                        result.Add(dialog);
                    }
                    catch { /* swallow per-window failures */ }
                    return true;
                }, IntPtr.Zero);
            }
            catch { /* enumeration failed — return whatever we collected (likely empty) */ }
#endif
            return result;
        }

        public static bool TryClickButton(IntPtr buttonHwnd)
        {
#if UNITY_EDITOR_WIN
            try
            {
                if (buttonHwnd == IntPtr.Zero) return false;
                return PostMessage(buttonHwnd, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
            }
            catch { return false; }
#else
            return false;
#endif
        }

#if UNITY_EDITOR_WIN
        static string ReadWindowText(IntPtr hwnd)
        {
            var buf = new char[512];
            var len = GetWindowTextW(hwnd, buf, buf.Length);
            return len > 0 ? new string(buf, 0, len) : string.Empty;
        }

        static string ReadClassName(IntPtr hwnd)
        {
            var buf = new char[256];
            var len = GetClassNameW(hwnd, buf, buf.Length);
            return len > 0 ? new string(buf, 0, len) : string.Empty;
        }
#endif
    }
}
