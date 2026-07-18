#nullable enable
using com.IvanMurzak.ReflectorNet.Utils;
using UnityEditor;

namespace com.SSS135.EditorAutomation.Utils
{
    /// <summary>
    /// Process-wide owner of the in-memory log collector used by
    /// <c>console-get-logs</c> / <c>console-clear-logs</c>. Replaces the
    /// bridge-era log-collector accessor deleted in Wave 2.
    /// </summary>
    /// <remarks>
    /// Lazy-bootstrapped on first <see cref="Instance"/> read via
    /// <see cref="EditorApplication.delayCall"/> if accessed off the Unity main
    /// thread; main-thread callers construct the collector directly. The
    /// underlying <see cref="UnityLogCollector"/> attaches to
    /// <c>Application.logMessageReceivedThreaded</c>, so all background-thread
    /// log messages are captured the same way the legacy bridge buffered them.
    /// </remarks>
    public static class EditorLogCollectorService
    {
        static readonly object _lock = new();
        static UnityLogCollector? _instance;

        /// <summary>
        /// The lazy-bootstrapped log collector. Returns null only when invoked
        /// off the main thread before the collector has been constructed for
        /// the first time — surviving callers all run on the main thread (the
        /// tool body is wrapped in <see cref="MainThread"/>.<c>Run</c>).
        /// </summary>
        public static UnityLogCollector? Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                lock (_lock)
                {
                    if (_instance != null)
                        return _instance;

                    if (!MainThread.Instance.IsMainThread)
                    {
                        // UnityLogCollector's constructor requires the main thread.
                        // Defer the build; the next main-thread access will succeed.
                        EditorApplication.delayCall += () => _ = Instance;
                        return null;
                    }

                    var storage = new InMemoryLogStorage();
                    _instance = new UnityLogCollector(storage);
                }
                return _instance;
            }
        }
    }
}
