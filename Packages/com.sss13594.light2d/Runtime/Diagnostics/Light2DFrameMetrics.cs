using System.Threading;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Light2D
{
    public readonly struct Light2DFrameMetrics
    {
        public Light2DFrameMetrics(
            int textureWidth,
            int textureHeight,
            long estimatedOwnedTextureBytes,
            int visibleLights,
            int ambientCycles,
            int reallocations,
            int contextCount)
        {
            TextureWidth = textureWidth;
            TextureHeight = textureHeight;
            EstimatedOwnedTextureBytes = estimatedOwnedTextureBytes;
            VisibleLights = visibleLights;
            AmbientCycles = ambientCycles;
            Reallocations = reallocations;
            ContextCount = contextCount;
        }

        public int TextureWidth { get; }
        public int TextureHeight { get; }
        public long EstimatedOwnedTextureBytes { get; }
        public int VisibleLights { get; }
        public int AmbientCycles { get; }
        public int Reallocations { get; }
        public int ContextCount { get; }
    }

    public static class Light2DDiagnostics
    {
        private static Light2DFrameMetrics lastFrame;
        private static long ownedTextureBytes;
        private static int contextCount;
        private static int generation;

        public static Light2DFrameMetrics LastFrame => lastFrame;
        public static long OwnedTextureBytes => Interlocked.Read(ref ownedTextureBytes);
        public static int ContextCount => Volatile.Read(ref contextCount);

        internal static void ReportFrame(in Light2DFrameMetrics metrics)
        {
            lastFrame = metrics;
        }

        internal static int RegisterContext()
        {
            int currentGeneration = Volatile.Read(ref generation);
            Interlocked.Increment(ref contextCount);
            return currentGeneration;
        }

        internal static void UnregisterContext(int ownerGeneration)
        {
            if (ownerGeneration == Volatile.Read(ref generation))
                Interlocked.Decrement(ref contextCount);
        }

        internal static void AddOwnedTextureBytes(int ownerGeneration, long bytes)
        {
            if (ownerGeneration == Volatile.Read(ref generation))
                Interlocked.Add(ref ownedTextureBytes, bytes);
        }

        internal static void RemoveOwnedTextureBytes(int ownerGeneration, long bytes)
        {
            if (ownerGeneration == Volatile.Read(ref generation))
                Interlocked.Add(ref ownedTextureBytes, -bytes);
        }

        // Shared byte estimate for package-owned textures; both backends account with the same formula.
        internal static long EstimateTextureBytes(int width, int height, int volumeDepth, GraphicsFormat format)
        {
            return (long)width * height * Mathf.Max(1, volumeDepth) * GraphicsFormatUtility.GetBlockSize(format);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Reset()
        {
            Interlocked.Increment(ref generation);
            lastFrame = default;
            Interlocked.Exchange(ref ownedTextureBytes, 0);
            Interlocked.Exchange(ref contextCount, 0);
        }
    }
}
