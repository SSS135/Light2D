#nullable enable
namespace com.SSS135.EditorAutomation.Editor.Framework
{
    /// <summary>
    /// Lifecycle of a single tool invocation, mirroring the legacy <c>ResponseStatus</c>
    /// plus a dedicated <see cref="Cancelled"/> state for the new job-ticket layer.
    /// </summary>
    public enum McpStatus
    {
        /// <summary>The call completed successfully.</summary>
        Success = 0,

        /// <summary>
        /// The call has been accepted and is running in the background.
        /// The body carries an immediate ticket payload; final result is delivered via
        /// the <c>system.job_status</c> RPCs.
        /// </summary>
        Processing = 1,

        /// <summary>The call failed in the handler domain.</summary>
        Error = 2,

        /// <summary>The call was cancelled (typically via <c>system.job_cancel</c>).</summary>
        Cancelled = 3,
    }
}
