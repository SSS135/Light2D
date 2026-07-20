using UnityEngine;

namespace Light2D.Examples
{
    public class Wisp : MonoBehaviour
    {
        public float Force;
        public GameObject LineLight;
        private Rigidbody2D _rigidbody;
        private Camera _camera;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _camera = Camera.main;
        }

        void Update()
        {
            var moveVec = Vector2.zero;

            if (SampleInput.GetKey(KeyCode.UpArrow)) moveVec += new Vector2(0, 1);
            if (SampleInput.GetKey(KeyCode.DownArrow)) moveVec += new Vector2(0, -1);
            if (SampleInput.GetKey(KeyCode.RightArrow)) moveVec += new Vector2(1, 0);
            if (SampleInput.GetKey(KeyCode.LeftArrow)) moveVec += new Vector2(-1, 0);

            if (moveVec.sqrMagnitude > 0.01f*0.01f)
            {
                var force = moveVec.normalized*Force;
                _rigidbody.AddForce(force, ForceMode2D.Force);
            }

            if (_camera != null)
            {
                var lightAngle = SampleUtilities.AngleZ(SampleUtilities.PointerWorldPosition(_camera) - (Vector2)transform.position);
                LineLight.transform.rotation = Quaternion.Euler(0, 0, lightAngle);
            }
        }
    }
}
