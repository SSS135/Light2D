using System;

namespace com.SSS135.EditorAutomation.Recorder
{
    /// <summary>
    /// Marks a field or property to be recorded by a sibling
    /// <see cref="RecordedFields"/> component each frame. <see cref="Tag"/>
    /// overrides the recorded tag (defaults to the member name);
    /// <see cref="Unit"/> annotates the variable manifest.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public sealed class RecordAttribute : Attribute
    {
        public string Tag { get; set; }
        public string Unit { get; set; }

        public RecordAttribute() { }

        public RecordAttribute(string tag)
        {
            Tag = tag;
        }
    }
}
