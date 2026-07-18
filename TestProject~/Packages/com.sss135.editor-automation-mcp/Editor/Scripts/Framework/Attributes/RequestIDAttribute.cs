#nullable enable
using System;

namespace com.SSS135.EditorAutomation.Editor.Framework
{
    /// <summary>
    /// Marker on a parameter. When present, the runtime substitutes the current request id
    /// (the dispatcher's per-call <c>requestId</c> string) for that parameter and strips the
    /// corresponding entry from the named-params bag before binding.
    /// Same shape and intent as the legacy <c>[RequestID]</c> attribute so Wave 1 migrations
    /// can keep parameter usages unchanged.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class RequestIDAttribute : Attribute
    {
    }
}
