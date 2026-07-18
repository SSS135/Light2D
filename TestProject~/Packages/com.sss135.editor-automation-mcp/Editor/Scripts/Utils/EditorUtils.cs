#nullable enable

namespace com.SSS135.EditorAutomation.Editor.Utils
{
    /// <summary>
    /// Utility class for Unity Editor operations.
    /// </summary>
    public static class EditorUtils
    {
        /// <summary>
        /// Repaints all editor windows including Project, Hierarchy, Animation, and all views.
        /// </summary>
        public static void RepaintAllEditorWindows()
        {
            UnityEditor.EditorApplication.RepaintProjectWindow();
            UnityEditor.EditorApplication.RepaintHierarchyWindow();
            UnityEditor.EditorApplication.RepaintAnimationWindow();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
    }
}
