using UnityEngine;

namespace Light2D
{
    /// <summary>
    /// Pure light-camera geometry math shared by both backends. Promoted from the URP feature's private
    /// CalculateLightCameraGeometry/CalculateSnappedPosition so Built-in (which today mutates a helper
    /// camera) and URP (which builds matrices directly) can derive one identical result. Uses only
    /// UnityEngine types; the URP feature keeps its private copy until it is switched over in a later
    /// chunk, so the two definitions may coexist meanwhile.
    /// </summary>
    internal readonly struct Light2DCameraGeometry
    {
        public readonly Vector3 Position;
        public readonly Matrix4x4 View;
        public readonly Matrix4x4 Projection;
        public readonly float OrthographicSize;
        public readonly float Aspect;

        internal Light2DCameraGeometry(Vector3 position, Matrix4x4 view, Matrix4x4 projection,
            float orthographicSize, float aspect)
        {
            Position = position;
            View = view;
            Projection = projection;
            OrthographicSize = orthographicSize;
            Aspect = aspect;
        }

        /// <summary>
        /// Derives the light-camera view/projection for a source <paramref name="camera"/> snapped to
        /// <paramref name="position"/>. When <paramref name="extended"/> is true the projection covers the
        /// larger obstacle/ambient render target; otherwise it covers the small light-source target.
        /// Mirrors Light2DUniversalRendererFeature.CalculateLightCameraGeometry.
        /// </summary>
        internal static Light2DCameraGeometry Calculate(Camera camera, Vector3 position, Vector2Int smallSize,
            Vector2Int extendedSize, bool extended, float fovAdd, float obstaclesDistance)
        {
            var aspect = extended
                ? extendedSize.x / (float)extendedSize.y
                : smallSize.x / (float)smallSize.y;

            float orthographicSize;
            float fieldOfView;
            if (extended)
            {
                orthographicSize = camera.orthographicSize * extendedSize.y / smallSize.y;
                fieldOfView = camera.fieldOfView + fovAdd;
            }
            else
            {
                orthographicSize = camera.orthographic
                    ? camera.orthographicSize
                    : Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * obstaclesDistance;
                fieldOfView = camera.fieldOfView;
            }

            var view = camera.worldToCameraMatrix * Matrix4x4.Translate(camera.transform.position - position);
            var projection = camera.orthographic
                ? Matrix4x4.Ortho(-orthographicSize * aspect, orthographicSize * aspect,
                    -orthographicSize, orthographicSize, camera.nearClipPlane, camera.farClipPlane)
                : Matrix4x4.Perspective(fieldOfView, aspect, camera.nearClipPlane, camera.farClipPlane);
            return new Light2DCameraGeometry(position, view, projection, orthographicSize, aspect);
        }

        /// <summary>
        /// Snaps the camera position to the lighting pixel grid to keep low-resolution lighting stable
        /// under camera motion. Mirrors Light2DUniversalRendererFeature.CalculateSnappedPosition.
        /// </summary>
        internal static Vector3 CalculateSnappedPosition(Camera camera, float pixelsPerMeter)
        {
            var position = camera.transform.position;
            var rotation = camera.transform.rotation;
            var local = Quaternion.Inverse(rotation) * position;
            var snapped = new Vector2(Mathf.Round(local.x * pixelsPerMeter) / pixelsPerMeter,
                Mathf.Round(local.y * pixelsPerMeter) / pixelsPerMeter);
            return rotation * (snapped - (Vector2)local) + position;
        }
    }
}
