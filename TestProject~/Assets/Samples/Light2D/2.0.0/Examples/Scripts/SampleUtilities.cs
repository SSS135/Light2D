using UnityEngine;

namespace Light2D.Examples
{
    internal static class SampleUtilities
    {
        public static float AngleZ(Vector2 direction)
        {
            return direction == Vector2.zero ? 0f : Mathf.Atan2(-direction.x, direction.y) * Mathf.Rad2Deg;
        }

        public static Vector3 WithXY(Vector3 value, Vector2 xy)
        {
            value.x = xy.x;
            value.y = xy.y;
            return value;
        }

        public static Vector3 WithXY(Vector3 value, float x, float y)
        {
            value.x = x;
            value.y = y;
            return value;
        }

        public static Vector3 WithY(Vector3 value, float y)
        {
            value.y = y;
            return value;
        }

        public static Color WithAlpha(Color value, float alpha)
        {
            value.a = alpha;
            return value;
        }

        public static Vector2 PointerWorldPosition(Camera camera)
        {
            var screenPosition = SampleInput.TouchCount > 0
                ? (Vector3)SampleInput.GetTouchPosition(0)
                : SampleInput.MousePosition;
            screenPosition.z = Mathf.Abs(camera.transform.position.z);
            return camera.ScreenToWorldPoint(screenPosition);
        }

        public static bool PointerPressedThisFrame()
        {
            return SampleInput.GetMouseButtonDown(0) ||
                   SampleInput.TouchCount > 0 && SampleInput.GetTouchPhase(0) == TouchPhase.Began;
        }

        public static bool PointerHeld()
        {
            var touchHeld = false;
            if (SampleInput.TouchCount > 0)
            {
                var phase = SampleInput.GetTouchPhase(0);
                touchHeld = phase == TouchPhase.Began || phase == TouchPhase.Moved || phase == TouchPhase.Stationary;
            }

            return SampleInput.GetMouseButton(0) ||
                   touchHeld;
        }

        public static int Hash(int first, int second, int third)
        {
            unchecked
            {
                var hash = 23;
                hash = hash * 31 + first;
                hash = hash * 31 + second;
                return hash * 31 + third;
            }
        }
    }
}
