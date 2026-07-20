#nullable enable

namespace com.SSS135.EditorAutomation.Editor
{
    /// <summary>
    /// Single source of truth for the plugin version string used at runtime by
    /// the update checker and any future version-aware components. Must be kept
    /// in sync with <c>package.json</c>'s <c>version</c> field; a Play Mode test
    /// (<c>StartupVersionTest</c>) asserts equality between the two.
    /// </summary>
    public static class EditorAutomationVersion
    {
        public const string Current = "0.72.1";
    }
}
