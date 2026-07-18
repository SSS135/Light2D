#nullable enable
using System;

namespace com.SSS135.EditorAutomation.Editor.Framework
{
    /// <summary>
    /// Per-parameter override carrying a description and an optional <see cref="Required"/> override.
    /// Per-parameter descriptions are still readable from <see cref="System.ComponentModel.DescriptionAttribute"/>
    /// for parity with C# convention; <see cref="McpParamAttribute"/> wins when both are present.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class McpParamAttribute : Attribute
    {
        /// <summary>
        /// Human-readable parameter description used in the schema and the wiki.
        /// </summary>
        public string? Description { get; }

        private bool _required;
        private bool _requiredSet;

        /// <summary>
        /// Optional override for the required-flag. When unset the dispatcher infers required-ness
        /// from the parameter's default value (no default ⇒ required).
        /// </summary>
        public bool Required
        {
            get => _required;
            set { _required = value; _requiredSet = true; }
        }

        /// <summary>The <see cref="Required"/> value, or null if it was not explicitly set.</summary>
        public bool? RequiredValue => _requiredSet ? _required : (bool?)null;

        public McpParamAttribute(string? description = null)
        {
            Description = description;
        }
    }
}
