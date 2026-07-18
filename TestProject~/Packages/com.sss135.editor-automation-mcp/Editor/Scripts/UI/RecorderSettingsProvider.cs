#nullable enable
#if UNITY_EDITOR
using com.SSS135.EditorAutomation.Recorder;
using UnityEditor;
using UnityEngine;

namespace com.SSS135.EditorAutomation.Editor.UI
{
    // The runtime namespace shares its final segment with the static API class, so from this
    // editor code the bare name 'Recorder' would bind to the sibling namespace. This alias
    // must live INSIDE the namespace block (where it wins lookup) to map it to the class.
    using Recorder = com.SSS135.EditorAutomation.Recorder.Recorder;

    /// <summary>
    /// Project Settings page (Project / Editor Automation) exposing the gameplay recorder
    /// on/off switch. The toggle is backed by <see cref="Recorder.Enabled"/>, a
    /// PlayerPrefs flag shared between editor and runtime, so the runtime assembly
    /// can read it without reaching editor-only settings. The MCP query tools stay
    /// registered regardless of this toggle; turning recording off only makes
    /// <see cref="Recorder.BeginSession"/> no-op with zero overhead.
    /// </summary>
    static class RecorderSettingsProvider
    {
        const string SettingsPath = "Project/Editor Automation";

        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return new SettingsProvider(SettingsPath, SettingsScope.Project)
            {
                label = "Editor Automation",
                guiHandler = _ =>
                {
                    EditorGUILayout.Space();

                    EditorGUI.BeginChangeCheck();
                    var enabled = EditorGUILayout.Toggle(
                        new GUIContent(
                            "Enable Gameplay Recorder",
                            "When off, recording no-ops with zero overhead. The MCP query tools stay registered regardless."),
                        Recorder.Enabled);
                    if (EditorGUI.EndChangeCheck())
                        Recorder.Enabled = enabled;

                    EditorGUILayout.HelpBox(
                        "Off = recording no-ops with zero overhead. The MCP recorder query tools stay registered regardless.",
                        MessageType.Info);
                },
                keywords = new[] { "MCP", "Recorder", "Gameplay", "Recording", "Debug" }
            };
        }
    }
}
#endif
