#nullable enable
using System;

namespace com.SSS135.EditorAutomation.Editor.Framework
{
    /// <summary>
    /// Class-level marker. The framework's <c>AttributeScanner</c> only walks methods on classes
    /// carrying this attribute. Replaces <c>[McpPluginToolType]</c>; carries no fields.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class McpToolTypeAttribute : Attribute
    {
        public McpToolTypeAttribute() { }
    }
}
