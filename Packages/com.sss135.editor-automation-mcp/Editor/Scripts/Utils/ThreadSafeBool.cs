#nullable enable
using System.Threading;

namespace com.SSS135.EditorAutomation.Utils
{
    /// <summary>
    /// Minimal in-package replacement for the McpPlugin.Common type of the same name —
    /// the upstream DLL was deleted in Wave 2 but a handful of survivors
    /// (<c>InMemoryLogStorage</c>, <c>FileLogStorage</c>, <c>UnityLogCollector</c>) still
    /// need an Interlocked-backed boolean for their dispose / disposed-bit toggle.
    /// </summary>
    public sealed class ThreadSafeBool
    {
        // 0 = false, 1 = true. Interlocked operates on int, so we store the bool as int.
        int _value;

        public ThreadSafeBool(bool initialValue)
        {
            _value = initialValue ? 1 : 0;
        }

        public bool Value
        {
            get => Volatile.Read(ref _value) != 0;
            set => Interlocked.Exchange(ref _value, value ? 1 : 0);
        }

        /// <summary>
        /// Atomically sets the value to true. Returns true if the value changed
        /// (i.e. it was previously false); returns false if already true.
        /// Mirrors the upstream API used by surviving callers.
        /// </summary>
        public bool TrySetTrue()
            => Interlocked.CompareExchange(ref _value, 1, 0) == 0;

        /// <summary>
        /// Atomically sets the value to false. Returns true if the value changed
        /// (i.e. it was previously true); returns false if already false.
        /// </summary>
        public bool TrySetFalse()
            => Interlocked.CompareExchange(ref _value, 0, 1) == 1;
    }
}
