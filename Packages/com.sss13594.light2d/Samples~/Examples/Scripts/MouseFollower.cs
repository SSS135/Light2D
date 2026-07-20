using UnityEngine;

namespace Light2D.Examples
{
    public class MouseFollower : MonoBehaviour
    {
        public bool RightClickRotation = false;
        private Vector2 _pressPos;
        private Camera _camera;

        private void Awake()
        {
            _camera = Camera.main;
        }

        private void LateUpdate()
        {
            if (_camera == null)
                return;

            if (SampleUtilities.PointerPressedThisFrame())
                _pressPos = SampleUtilities.PointerWorldPosition(_camera);

            if (SampleUtilities.PointerHeld() && RightClickRotation)
            {
                var shift = SampleUtilities.PointerWorldPosition(_camera) - _pressPos;
                if (shift.sqrMagnitude > 0.1f*0.1f)
                {
                    var angle = SampleUtilities.AngleZ(shift);
                    transform.rotation = Quaternion.Euler(0, 0, angle);
                }
            }
            else
            {
                Vector3 pos = SampleUtilities.PointerWorldPosition(_camera);
                pos.z = transform.position.z;
                transform.position = pos;
            }
        }
    }
}
