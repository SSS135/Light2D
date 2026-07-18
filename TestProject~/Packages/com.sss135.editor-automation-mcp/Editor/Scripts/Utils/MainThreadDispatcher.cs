#nullable enable
using System.Threading;
using UnityEditor;

namespace com.IvanMurzak.ReflectorNet.Utils
{
    // [InitializeOnLoad] forces the static constructor to run on the editor main thread
    // after every domain reload, so MainThreadId always captures the editor main thread.
    [InitializeOnLoad]
    public static class MainThreadDispatcher
    {
        public static readonly int MainThreadId = Thread.CurrentThread.ManagedThreadId;
        public static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == MainThreadId;
    }
}
