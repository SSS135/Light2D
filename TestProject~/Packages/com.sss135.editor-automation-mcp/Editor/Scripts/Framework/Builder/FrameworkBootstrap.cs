#nullable enable
using UnityEditor;

namespace com.SSS135.EditorAutomation.Editor.Framework.Builder
{
    /// <summary>
    /// Sole entry point for the framework. On editor load, schedules a single
    /// <see cref="ToolRegistry.Rebuild"/> via <see cref="EditorApplication.delayCall"/> so the
    /// rebuild runs after every domain assembly has finished loading. Replaces the role of
    /// the legacy bridge-owned build hook (deleted in Wave 2) — no builder chain,
    /// no DI container, no <c>IMcpPlugin</c>. Just a static registry.
    /// </summary>
    [InitializeOnLoad]
    public static class FrameworkBootstrap
    {
        static FrameworkBootstrap()
        {
            EditorApplication.delayCall += SafeRebuild;
            // Re-snapshot once every domain assembly has finished loading. The lone delayCall above
            // can race the warm-up window and commit a partial scan; afterAssemblyReload fires after
            // the load settles, so the registry self-corrects. SafeRebuild is idempotent — a double
            // fire is harmless.
            AssemblyReloadEvents.afterAssemblyReload += SafeRebuild;
        }

        static void SafeRebuild()
        {
            try
            {
                ToolRegistry.Rebuild();
            }
            catch (System.Exception ex)
            {
                // ToolRegistry.Rebuild is fail-loud: an empty/duplicate id or a tool whose method
                // signature the binder rejects aborts the whole rebuild. Surface it as a console
                // error (red, loud) with the actionable site/message, but don't kill the editor
                // session — the operator fixes the offending [McpTool] and the next reload retries.
                UnityEngine.Debug.LogError($"[UnityMCP] ToolRegistry.Rebuild failed: {ex.Message}\n{ex}");
            }
        }
    }
}
