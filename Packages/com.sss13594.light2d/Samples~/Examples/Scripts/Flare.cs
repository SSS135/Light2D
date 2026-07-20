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


            var alpha = Mathf.Lerp(0, _startColor.a, Mathf.Min(_lifetimeElapsed, AlphaGrowTime)/AlphaGrowTime);
            Light.Color = Color.Lerp(
                SampleUtilities.WithAlpha(_startColor, alpha),
                SampleUtilities.WithAlpha(_startColor, 0),
                _lifetimeElapsed/Lifetime);
        }
    }
}
