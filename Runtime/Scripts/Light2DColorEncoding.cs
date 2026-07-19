using UnityEngine;

namespace Light2D
{
    /// <summary>
    /// Packs obstacle additive colors into the UV1 channel format expected by the Light2D obstacle shaders.
    /// </summary>
    internal static class Light2DColorEncoding
    {
        /// <summary>
        /// Encodes an additive color into the two floats stored per vertex in UV1:
        /// x carries RGB, y carries alpha. Matches the legacy Util.DecodeFloatRGBA packing.
        /// </summary>
        internal static Vector2 EncodeAdditiveColorToUv(Color color)
        {
            return new Vector2(
                DecodeFloatRGBA(new Vector3(color.r, color.g, color.b)),
                DecodeFloatRGBA(new Vector3(color.a, 0, 0)));
        }

        private static float DecodeFloatRGBA(Vector3 enc)
        {
            enc = new Vector3((byte)(enc.x * 254f), (byte)(enc.y * 254f), (byte)(enc.z * 254f)) / 255f;
            var kDecodeDot = new Vector4(1f, 1 / 255f, 1 / 65025f);
            return Vector3.Dot(enc, kDecodeDot);
        }
    }
}
