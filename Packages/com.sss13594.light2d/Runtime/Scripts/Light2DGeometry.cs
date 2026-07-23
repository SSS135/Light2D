using UnityEngine;

namespace Light2D
{
    /// <summary>
    /// Small geometry helpers shared by the register-and-draw components. Kept internal: these back the
    /// world-space bounds each component hands to the registry now that they no longer own a MeshRenderer
    /// (whose <c>bounds</c> used to supply the culling AABB).
    /// </summary>
    internal static class Light2DGeometry
    {
        /// <summary>
        /// Transforms a local-space AABB by an arbitrary matrix and returns the enclosing world-space AABB
        /// (the same value a MeshRenderer's <c>bounds</c> reports for the same mesh + transform).
        /// </summary>
        internal static Bounds TransformBounds(Bounds local, Matrix4x4 matrix)
        {
            var center = matrix.MultiplyPoint3x4(local.center);
            var ext = local.extents;
            var axisX = matrix.MultiplyVector(new Vector3(ext.x, 0f, 0f));
            var axisY = matrix.MultiplyVector(new Vector3(0f, ext.y, 0f));
            var axisZ = matrix.MultiplyVector(new Vector3(0f, 0f, ext.z));
            var worldExtents = new Vector3(
                Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
                Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
                Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));
            return new Bounds(center, worldExtents * 2f);
        }
    }
}
