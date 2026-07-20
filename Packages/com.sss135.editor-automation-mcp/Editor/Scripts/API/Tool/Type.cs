#nullable enable
using com.SSS135.EditorAutomation.Editor.Framework;

namespace com.SSS135.EditorAutomation.Editor.API
{
    [McpToolType]
    public partial class Tool_Type
    {
        public enum DescriptionMode
        {
            /// <summary>
            /// Include descriptions on the target type and its direct properties/items.
            /// Descriptions inside <c>$defs</c> entries are excluded.
            /// </summary>
            Include,

            /// <summary>
            /// Include descriptions everywhere: the target type, its properties/items,
            /// and recursively inside all <c>$defs</c> entries.
            /// </summary>
            IncludeRecursively,

            /// <summary>
            /// Omit all <c>description</c> fields from the schema output.
            /// </summary>
            Ignore
        }
    }
}
