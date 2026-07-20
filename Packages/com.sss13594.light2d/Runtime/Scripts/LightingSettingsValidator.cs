using System;
using UnityEngine;

namespace Light2D
{
    internal readonly struct LightingSizeEstimate
    {
        public readonly Vector2Int Small;
        public readonly Vector2Int Extended;
        public readonly long EstimatedTextureBytes;

        public LightingSizeEstimate(Vector2Int small, Vector2Int extended, long estimatedTextureBytes)
        {
            Small = small;
            Extended = extended;
            EstimatedTextureBytes = estimatedTextureBytes;
        }
    }

    internal static class LightingSettingsValidator
    {
        internal static bool TryCalculateSizes(Camera camera, float pixelSize, float orthographicSizeAdd,
            float fieldOfViewAdd, float obstacleDistance, bool hdr, bool ambient, bool antialiasing,
            bool normalMapping, out LightingSizeEstimate estimate, out string error)
        {
            estimate = default;
            error = null;
            if (camera == null)
                return Fail("A source camera is required.", out error);
            if (!IsFinitePositive(pixelSize))
                return Fail("Light Pixel Size must be finite and greater than zero.", out error);
            if (!IsFinite(camera.aspect) || camera.aspect <= 0f)
                return Fail("The source camera aspect ratio must be finite and greater than zero.", out error);

            double smallHeight;
            double extendedHeight;
            if (camera.orthographic)
            {
                if (!IsFinitePositive(camera.orthographicSize) || !IsFinite(orthographicSizeAdd) ||
                    camera.orthographicSize + orthographicSizeAdd <= 0f)
                    return Fail("Camera size and Light Camera Size Add must produce a finite positive size.", out error);

                smallHeight = camera.orthographicSize * 2d / pixelSize;
                extendedHeight = (camera.orthographicSize + orthographicSizeAdd) * 2d / pixelSize;
            }
            else
            {
                if (!IsFinitePositive(obstacleDistance) || !IsFinite(fieldOfViewAdd))
                    return Fail("Obstacle distance and Light Camera FOV Add must be finite, with distance greater than zero.", out error);
                double smallFov = camera.fieldOfView;
                double extendedFov = smallFov + fieldOfViewAdd;
                if (smallFov <= 0d || smallFov >= 180d || extendedFov <= 0d || extendedFov >= 180d)
                    return Fail("Camera FOV and Light Camera FOV Add must produce an angle between 0 and 180 degrees.", out error);

                smallHeight = Math.Tan(smallFov * Mathf.Deg2Rad * 0.5d) * obstacleDistance * 2d / pixelSize;
                extendedHeight = Math.Tan(extendedFov * Mathf.Deg2Rad * 0.5d) * obstacleDistance * 2d / pixelSize;
            }

            if (!TryDimension(smallHeight, out var smallY) ||
                !TryDimension(smallHeight * camera.aspect, out var smallX) ||
                !TryDimension(extendedHeight, out var extendedY) ||
                !TryDimension(extendedHeight * camera.aspect, out var extendedX))
                return Fail("Calculated light texture dimensions are not finite or exceed supported integer sizes.", out error);

            extendedX = MakeEven(extendedX);
            extendedY = MakeEven(extendedY);
            var maxTextureSize = SystemInfo.maxTextureSize;
            var largestScale = antialiasing ? 2 : 1;
            if ((long)extendedX * largestScale > maxTextureSize || (long)extendedY * largestScale > maxTextureSize ||
                smallX > maxTextureSize || smallY > maxTextureSize)
                return Fail($"Calculated textures exceed this device's {maxTextureSize} pixel texture-size limit.", out error);

            try
            {
                checked
                {
                    long bytesPerPixel = hdr ? 8L : 4L;
                    long extendedPixels = (long)extendedX * extendedY;
                    long smallPixels = (long)smallX * smallY;
                    long textureBytes = extendedPixels * bytesPerPixel;
                    textureBytes += extendedPixels * bytesPerPixel * (ambient ? 3L : 0L);
                    textureBytes += extendedPixels * bytesPerPixel * largestScale * largestScale;
                    textureBytes += smallPixels * bytesPerPixel * (normalMapping ? 3L : 2L);
                    estimate = new LightingSizeEstimate(new Vector2Int(smallX, smallY),
                        new Vector2Int(extendedX, extendedY), textureBytes);
                }
            }
            catch (OverflowException)
            {
                return Fail("Calculated texture memory exceeds supported limits.", out error);
            }

            return true;
        }

        private static bool TryDimension(double value, out int dimension)
        {
            dimension = 0;
            if (double.IsNaN(value) || double.IsInfinity(value) || value > int.MaxValue - 1d)
                return false;
            dimension = Math.Max(4, (int)Math.Round(value));
            return true;
        }

        private static int MakeEven(int value)
        {
            return value + (value & 1);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }
    }
}
