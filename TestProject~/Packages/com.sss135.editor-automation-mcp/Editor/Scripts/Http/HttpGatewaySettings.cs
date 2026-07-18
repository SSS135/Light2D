#nullable enable
using System;
using UnityEditor;
using UnityEngine;

namespace com.SSS135.EditorAutomation.Editor.Http
{
    // Controls the dispatcher's behavior when a tool dispatch must be marshalled onto the
    // Editor's main thread but the Editor is unfocused (and therefore ticking slowly or not
    // at all). The watchdog pulses Win32 SetForegroundWindow to restore normal tick rate.
    public enum AutoWakeMode
    {
        Never,    // legacy behavior: dispatcher never calls SetForegroundWindow
        OnStall,  // call SetForegroundWindow only if a main-thread marshal hasn't completed in AutoWakeStallMilliseconds
        Always    // call SetForegroundWindow on every main-thread tool dispatch
    }

    [FilePath("ProjectSettings/EditorAutomationHttpGateway.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class HttpGatewaySettings : ScriptableSingleton<HttpGatewaySettings>
    {
        [SerializeField] public bool EnableHttpGateway = true;
        [SerializeField] public int  HttpMaxRequestBodyBytes = 1_048_576;
        [SerializeField] public int  HttpDefaultTimeoutMs    = 120_000;
        [SerializeField] public int  HttpMaxTimeoutMs        = 300_000;
        [SerializeField] public int  HttpResponseSpillThresholdCharacters = 10_000;
        [SerializeField] public bool EnableInCi = false;

        // ── Auto-wake watchdog (focus-stall mitigation) ───────────────────
        // When a main-thread tool marshal stalls (typical cause: Unity Editor unfocused +
        // pending recompile), the dispatcher can pulse the Editor's window to the
        // foreground via Win32 to resume normal tick rate. See EditorWin32.TryWakeEditor.
        [SerializeField] public AutoWakeMode AutoWakeMode = AutoWakeMode.OnStall;
        [SerializeField] public int AutoWakeStallMilliseconds = 500;

        // When a main-thread tool dispatch is requested while a native Win32 modal dialog
        // is blocking the Editor (e.g. EditorUtility.DisplayDialog), short-circuit the
        // marshal and return an "editor_modal_blocked" payload instead of hanging. Callers
        // can then invoke system-editor-modal-respond to dismiss it. Disable to restore
        // legacy hang-on-modal behavior.
        [SerializeField] public bool DispatcherModalPreCheckEnabled = true;

        // ── Job-ticket layer (Wave 4) ─────────────────────────────────────
        // Completed tickets age out of the in-memory registry after this many seconds.
        // The JSONL monitor retains the full history on disk regardless.
        [SerializeField] public int CompletedTicketTtlSeconds = 600;

        // Compatibility setting retained for existing assets. The active job monitor is pruned
        // to a fixed line count by JobMonitorLog.
        [SerializeField] public int MonitorFileMaxBytes = 10 * 1024 * 1024;

        // Compatibility setting retained for cleanup of old jobs.jsonl.N siblings.
        [SerializeField] public int MonitorFileRotationKeep = 3;

        // Lower bound on the gap between two progress events on the SAME ticket; events
        // arriving faster than this are dropped. Keeps the monitor log readable when a
        // tool emits per-iteration progress.
        [SerializeField] public int ProgressEventMinIntervalMs = 250;

        public static bool IsCi => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"));

        public void SaveAsText() => Save(true);
    }
}
