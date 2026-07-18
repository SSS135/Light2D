#nullable enable
#if UNITY_EDITOR
using com.SSS135.EditorAutomation.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace com.SSS135.EditorAutomation.Editor.UI
{
    public static class MenuItems
    {
        [MenuItem("Tools/Editor Automation/Check for Updates", priority = 999)]
        public static void CheckForUpdates() => _ = UpdateChecker.CheckForUpdatesAsync(forceCheck: true);

        [MenuItem("Tools/Editor Automation/Debug/Reset Update Preferences", priority = 2001)]
        public static void ResetUpdatePreferences()
        {
            UpdateChecker.ClearPreferences();
            Debug.Log("Update preferences have been reset.");
        }
    }
}
#endif
