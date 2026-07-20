using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Light2D.Examples
{
    public class Flare : MonoBehaviour
    {
        public float Lifetime;
        public LightSprite Light;
        public float AlphaGrowTime = 0.5f;
        private float _lifetimeElapsed = 0;
        private Color _startColor;

        void Start()
        {
            // Light is a public field that may be left unassigned in the inspector; guard it
            // so Start (and every subsequent Update) does not NRE.
            if (Light == null)
            {
                enabled = false;
                return;
            }

            _startColor = Light.Color;
            Light.Color = SampleUtilities.WithAlpha(_startColor, 0);
        }

        void Update()
        {
            _lifetimeElapsed += Time.deltaTime;

            

            if (_lifetimeElapsed > Lifetime)
            {
                _lifetimeElapsed = Lifetime;
                Destroy(gameObject);
            }


            // Guard the divisors: AlphaGrowTime/Lifetime are serialized floats that can be 0,
            // which would feed NaN/Inf through the Lerps.
            var alpha = Mathf.Lerp(0, _startColor.a,
                AlphaGrowTime > 0 ? Mathf.Min(_lifetimeElapsed, AlphaGrowTime)/AlphaGrowTime : 1f);
            Light.Color = Color.Lerp(
                SampleUtilities.WithAlpha(_startColor, alpha),
                SampleUtilities.WithAlpha(_startColor, 0),
                Lifetime > 0 ? _lifetimeElapsed/Lifetime : 1f);
        }
    }
}
