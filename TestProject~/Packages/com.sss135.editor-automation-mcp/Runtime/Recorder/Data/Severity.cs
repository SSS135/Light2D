namespace com.SSS135.EditorAutomation.Recorder
{
    /// <summary>
    /// Event severity ordering. Numeric order is meaningful: query filters use
    /// <c>severityMin</c> as a <c>&gt;=</c> threshold against these values.
    /// </summary>
    public enum Severity : byte
    {
        Trace,
        Debug,
        Info,
        Warning,
        Error,
        Fatal
    }
}
