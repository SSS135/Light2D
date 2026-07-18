namespace com.SSS135.EditorAutomation.Recorder
{
    /// <summary>
    /// Catalog entry for one recorded object, written once on first sight
    /// (and updated when the object is destroyed). <see cref="Key"/> is the
    /// stable identity; <see cref="Label"/>/<see cref="Path"/> are labels only.
    /// </summary>
    public class ObjectRecord
    {
        public string Key;
        public string Label;
        public string Path;
        public string ParentKey;
        public string Type;
        public int FrameFirst;
        public int? FrameDestroyed;
    }
}
