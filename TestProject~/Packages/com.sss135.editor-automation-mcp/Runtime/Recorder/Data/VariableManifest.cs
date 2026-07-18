namespace com.SSS135.EditorAutomation.Recorder
{
    /// <summary>
    /// Per-tag manifest entry, written once on first sight of a tag. Records the
    /// observed value kind, optional unit/epsilon, and whether the tag's type
    /// drifted across rows (<see cref="Mixed"/>).
    /// </summary>
    public class VariableManifest
    {
        public string Tag;
        public ValueKind ValueKind;
        public string Unit;
        public float? Epsilon;
        public int FirstFrame;
        public bool Mixed;
    }
}
