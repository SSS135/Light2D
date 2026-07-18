#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using com.IvanMurzak.ReflectorNet.Model;
using com.SSS135.EditorAutomation.Editor.Utils;
using EA;
using UnityEditor;

namespace EA
{
    [Description("Available information about 'UnityEditor.EditorApplication'.")]
    public class EditorStatsData
    {
        [Description("Whether the Editor is in Play mode.")]
        public bool IsPlaying { get; set; } = false;

        [Description("Whether the Editor is paused.")]
        public bool IsPaused { get; set; } = false;

        [Description("Is editor currently compiling scripts? (Read Only)")]
        public bool IsCompiling { get; set; } = false;

        [Description("Editor application state which is true only when the Editor is currently in or about to enter Play mode. (Read Only)")]
        public bool IsPlayingOrWillChangePlaymode { get; set; } = false;

        [Description("True if the Editor is currently refreshing the AssetDatabase. (Read Only)")]
        public bool IsUpdating { get; set; } = false;

        [Description("Path to the Unity editor contents folder. (Read Only)")]
        public string ApplicationContentsPath { get; set; } = string.Empty;

        [Description("Gets the path to the Unity Editor application. (Read Only)")]
        public string ApplicationPath { get; set; } = string.Empty;

        [Description("The time since the editor was started. (Read Only)")]
        public double TimeSinceStartup { get; set; } = 0;

        [Description("UTC timestamp (ISO 8601) when the last script compilation finished this editor " +
            "session. Empty if no compilation has been observed since the editor started. (Read Only)")]
        public string LastCompilationFinishedUtc { get; set; } = string.Empty;

        [Description("Seconds elapsed since the last script compilation finished, on the editor's " +
            "monotonic clock. Null if no compilation has been observed this session. (Read Only)")]
        public double? SecondsSinceLastCompilation { get; set; }

        public static EditorStatsData FromEditor()
        {
            return new EditorStatsData
            {
                IsPlaying = EditorApplication.isPlaying,
                IsPaused = EditorApplication.isPaused,
                IsCompiling = EditorApplication.isCompiling,
                IsPlayingOrWillChangePlaymode = EditorApplication.isPlayingOrWillChangePlaymode,
                IsUpdating = EditorApplication.isUpdating,
                ApplicationContentsPath = EditorApplication.applicationContentsPath,
                ApplicationPath = EditorApplication.applicationPath,
                TimeSinceStartup = EditorApplication.timeSinceStartup,
                LastCompilationFinishedUtc = CompileTimeTracker.LastCompilationFinishedUtc,
                SecondsSinceLastCompilation = CompileTimeTracker.SecondsSinceLastCompilation
            };
        }
    }
}
