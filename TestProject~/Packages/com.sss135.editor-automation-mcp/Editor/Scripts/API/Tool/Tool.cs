#nullable enable
using com.SSS135.EditorAutomation.Editor.Framework;

namespace com.SSS135.EditorAutomation.Editor.API
{
    [McpToolType]
    public partial class Tool_Tool
    {
        // BuildToolLookup / ResolveToolName helpers + the IToolManager-dependent
        // ToolsArrayIsNullOrEmpty error existed solely for Tool.SetEnabledState; that
        // file was deleted in the Wave 2 follow-up fix (the new framework treats
        // [McpTool(Enabled = ...)] as an authoring-time decision with no runtime
        // mutation path). The remaining Error string supports Tool.List.cs.

        public static class Error
        {
            public static string ToolRegistryNotAvailable()
                => "Tool registry is not available. The framework bootstrap may not have completed.";
        }
    }
}
