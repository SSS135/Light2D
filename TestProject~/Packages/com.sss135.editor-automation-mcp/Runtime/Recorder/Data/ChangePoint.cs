namespace com.SSS135.EditorAutomation.Recorder
{
    /// <summary>
    /// One emitted change in a (key, tag) series: the frame it occurred and the
    /// value it changed to. Series are stored as sorted lists of these for
    /// as-of (last frame ≤ N) lookups.
    /// </summary>
    public struct ChangePoint
    {
        public int Frame;
        public RecordedValue V;
    }
}
