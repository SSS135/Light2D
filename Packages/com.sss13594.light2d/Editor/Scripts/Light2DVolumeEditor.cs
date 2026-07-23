using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Light2D
{
    /// <summary>
    /// Adds interactive, draggable Scene-view handles to a <see cref="Light2DVolume"/> so its region can be
    /// resized directly in the viewport like a 2D collider, complementing the component's non-interactive
    /// <c>OnDrawGizmosSelected</c>. A Box volume gets a <see cref="BoxBoundsHandle"/> that edits
    /// <see cref="Light2DVolume.Size"/> and — when a single face is dragged — moves the transform so the
    /// opposite edge stays put (collider-style). A Circle volume gets four cardinal dot handles that edit
    /// <see cref="Light2DVolume.Radius"/>. Sprite/Global shapes have no resize handle. All edits are
    /// Undo-recorded, handles are drawn in the volume's local space, and the default inspector is preserved.
    /// </summary>
    [CustomEditor(typeof(Light2DVolume))]
    [CanEditMultipleObjects]
    internal sealed class Light2DVolumeEditor : Editor
    {
        // Matches Light2DVolume.OnDrawGizmosSelected's warm fill colour so the interactive handles read as the
        // same volume the gizmo outlines.
        private static readonly Color HandleColor = new Color(1f, 0.85f, 0.4f, 0.9f);

        // Cardinal directions for the Circle radius dots, in the volume's local XY plane.
        private static readonly Vector3[] CircleHandleDirections =
        {
            Vector3.right, Vector3.up, Vector3.left, Vector3.down
        };

        private BoxBoundsHandle _boxHandle;

        private void OnEnable()
        {
            _boxHandle = new BoxBoundsHandle
            {
                // Keep the handle strictly 2D: only the four X/Y faces are draggable, the Z faces are hidden.
                axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Y
            };
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
        }

        private void OnSceneGUI()
        {
            var volume = target as Light2DVolume;
            if (volume == null)
                return;

            switch (volume.Shape)
            {
                case Light2DVolume.VolumeShape.Box:
                    DrawBoxHandle(volume);
                    break;
                case Light2DVolume.VolumeShape.Circle:
                    DrawCircleHandle(volume);
                    break;
                // Sprite/Global have no meaningful resize handle; the runtime gizmo still outlines them.
            }
        }

        private void DrawBoxHandle(Light2DVolume volume)
        {
            var t = volume.transform;
            // Draw in the volume's full local-to-world space (including lossyScale) so the handle shares the
            // exact space of the coverage mesh and gizmo. A unit-scale matrix desyncs the handle from the cursor
            // whenever the transform (or a parent) is scaled — the dragged edge outruns the mouse.
            using (new Handles.DrawingScope(HandleColor, t.localToWorldMatrix))
            {
                _boxHandle.center = Vector3.zero;
                _boxHandle.size = new Vector3(volume.Size.x, volume.Size.y, 0f);

                EditorGUI.BeginChangeCheck();
                _boxHandle.DrawHandle();
                if (!EditorGUI.EndChangeCheck())
                    return;

                var center = _boxHandle.center;
                var newSize = new Vector2(Mathf.Abs(_boxHandle.size.x), Mathf.Abs(_boxHandle.size.y));

                // Dragging one face keeps the opposite edge fixed (collider-style): the handle reports the
                // shifted local-space center, which we push into the transform so the box grows from that edge.
                if (center != Vector3.zero)
                {
                    Undo.RecordObjects(new Object[] { volume, t }, "Resize Light Volume");
                    // center is a local-space offset; TransformVector maps it to world (rotation + lossyScale)
                    // so the transform shifts by the same world amount the dragged face moved.
                    t.position += t.TransformVector(new Vector3(center.x, center.y, 0f));
                }
                else
                {
                    Undo.RecordObject(volume, "Resize Light Volume");
                }

                volume.Size = newSize;
                EditorUtility.SetDirty(volume);
            }
        }

        private void DrawCircleHandle(Light2DVolume volume)
        {
            var t = volume.transform;
            // Full local-to-world space (including lossyScale) so the dots sit on the same edge the coverage
            // mesh and gizmo draw; a unit-scale matrix would let the radius outrun the cursor when scaled.
            using (new Handles.DrawingScope(HandleColor, t.localToWorldMatrix))
            {
                Handles.DrawWireDisc(Vector3.zero, Vector3.forward, volume.Radius);

                // Screen-constant dot size: GetHandleSize returns a world size, so divide by the planar scale to
                // express it in the (now scaled) local space and keep every dot the same on-screen size.
                var lossy = t.lossyScale;
                var planarScale = Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y), 1e-4f);
                var dotSize = HandleUtility.GetHandleSize(t.position) * 0.08f / planarScale;
                var newRadius = volume.Radius;

                EditorGUI.BeginChangeCheck();
                foreach (var dir in CircleHandleDirections)
                {
                    var moved = Handles.Slider(dir * volume.Radius, dir, dotSize, Handles.DotHandleCap, 0f);
                    var candidate = Vector3.Dot(moved, dir);
                    // Only the dragged dot deviates from the current radius; pick the largest deviation.
                    if (Mathf.Abs(candidate - volume.Radius) > Mathf.Abs(newRadius - volume.Radius))
                        newRadius = candidate;
                }

                if (!EditorGUI.EndChangeCheck())
                    return;

                Undo.RecordObject(volume, "Resize Light Volume");
                volume.Radius = Mathf.Max(0f, newRadius);
                EditorUtility.SetDirty(volume);
            }
        }
    }
}
