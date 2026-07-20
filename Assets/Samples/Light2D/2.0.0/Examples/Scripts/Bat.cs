using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Light2D.Examples
{
    public class Bat : MonoBehaviour
    {
        public Rect FlyRect;
        public float MoveSpeed;
        public float RotationLerpFactor;
        private Vector2 _flyTarget;
        private const float _targetSqDist = 1*1;

        void OnEnable()
        {
            FindNewFlyTarget();
        }

        void Update()
        {
            var pos = transform.position;
            var rot = transform.rotation;

            // A degenerate/unassigned FlyRect (size ~0, or smaller than the target distance)
            // can never yield a target far enough away, so the original unbounded while-loop
            // would freeze the main thread. Cap the retries and accept the last pick; skip
            // re-targeting entirely when the fly area is too small to satisfy the distance test.
            if (FlyRect.size.sqrMagnitude >= _targetSqDist)
            {
                for (int i = 0; i < 32 && ((Vector2) pos - _flyTarget).sqrMagnitude < _targetSqDist; i++)
                    FindNewFlyTarget();
            }

            var direction = rot*new Vector3(0, 1, 0);
            var targetRot = Quaternion.Euler(0, 0, SampleUtilities.AngleZ(_flyTarget - (Vector2)pos));
            transform.rotation = Quaternion.Lerp(rot, targetRot, RotationLerpFactor*Time.deltaTime);

            transform.position = pos + direction*MoveSpeed*Time.deltaTime;
        }

        void FindNewFlyTarget()
        {
            var x = Random.Range(FlyRect.xMin, FlyRect.xMax);
            var y = Random.Range(FlyRect.yMin, FlyRect.yMax);
            _flyTarget = new Vector2(x, y);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireCube(FlyRect.center, FlyRect.size);
        }
    }
}
