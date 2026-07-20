using System.Collections.Generic;

namespace com.SSS135.EditorAutomation.Recorder
{
    /// <summary>
    /// An instantaneous, free-form event. <see cref="Key"/> is null for global
    /// (object-less) events. <see cref="Props"/> is an arbitrary bag serialized
    /// as a JSON object.
    /// </summary>
    public class EventRecord
    {
        public int EventId;
        public int Frame;
        public string Key;
        public string Name;
        public Severity Severity = Severity.Info;
        public Dictionary<string, object> Props;
    }
}
