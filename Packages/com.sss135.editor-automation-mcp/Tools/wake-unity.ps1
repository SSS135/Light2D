# Wake-Unity (one-shot).
#
# Solves the "Unity needs focus to finish recompiling" problem that the
# Editor Automation plugin itself cannot solve from inside the editor process. Once
# `AssemblyReloadEvents.beforeAssemblyReload` fires, no plugin code runs
# until the new AppDomain is live; the new AppDomain needs
# `EditorApplication.update` to tick to finish compile, which Unity
# throttles while unfocused. The in-plugin wake watchdog (RpcDispatcher's
# StartWakeWatchdog) can only fire while the gateway is alive — it cannot
# help during the reload window itself. This external script fills the gap
# by calling SetForegroundWindow on the Unity process on demand.
#
# Usage:
#   .\wake-unity.ps1
#   .\wake-unity.ps1 -ProjectFilter "OtherProject"
#
# -ProjectFilter matches Unity processes whose main-window title contains
# the given substring. Defaults to "Unity" which matches any Unity editor
# window. On a host running multiple Unity editors, pass the project name
# (e.g. -ProjectFilter "MyGame") so the script only wakes the right one.

[CmdletBinding()]
param(
    [string]$ProjectFilter = "Unity"
)

$ErrorActionPreference = "Stop"

Add-Type @'
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

public static class WakeUnityWin32
{
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] static extern uint keybd_event(byte vk, byte scan, uint flags, UIntPtr extraInfo);

    const byte VK_MENU = 0x12;
    const uint KEYEVENTF_KEYUP = 0x0002;
    const int SW_RESTORE = 9;

    public static bool TryWake(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        // ALT pulse defeats Windows' foreground-set restriction when our
        // process is not currently the foreground process.
        keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
        keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
        return SetForegroundWindow(hwnd);
    }
}
'@

function Get-UnityHwnd {
    param([string]$Filter)
    $candidates = Get-Process Unity -ErrorAction SilentlyContinue |
        Where-Object { $_.MainWindowHandle -ne [IntPtr]::Zero -and $_.MainWindowTitle -match $Filter }
    if ($null -eq $candidates -or $candidates.Count -eq 0) { return [IntPtr]::Zero }
    return ($candidates | Sort-Object StartTime -Descending | Select-Object -First 1).MainWindowHandle
}

$hwnd = Get-UnityHwnd -Filter $ProjectFilter
$stamp = Get-Date -Format HH:mm:ss
if ($hwnd -eq [IntPtr]::Zero) {
    Write-Warning "[$stamp] No Unity window found matching '$ProjectFilter'."
    exit 1
}

$ok = [WakeUnityWin32]::TryWake($hwnd)
if ($ok) {
    Write-Host "[$stamp] Woke Unity (hwnd=$hwnd)."
    exit 0
} else {
    Write-Warning "[$stamp] SetForegroundWindow returned false (hwnd=$hwnd)."
    exit 2
}
