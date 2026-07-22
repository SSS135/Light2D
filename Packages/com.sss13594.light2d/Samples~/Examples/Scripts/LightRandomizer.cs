using UnityEngine;
using Random = UnityEngine.Random;

namespace Light2D.Examples
{
    [RequireComponent(typeof(LightSprite))]
    public class LightRandomizer : MonoBehaviour
    {
        public float MinRadius = 5;
        public float MaxRadius = 35;
        public float MinLightAlpha = 0.3f;
        public float MaxLightAlpha = 0.8f;
        private LightSprite _light;

        private void Awake()
        {
            _light = GetComponent<LightSprite>();
        }

        private void Start()
        {
            _light.Radius = Random.Range(MinRadius, MaxRadius);
            var c = new Vector4(Random.value, Random.value, Random.value, Random.Range(MinLightAlpha, MaxLightAlpha));
            var maxc = Mathf.Max(c.x, c.y, c.z);
            _light.Color = new Color(c.x/maxc, c.y/maxc, c.z/maxc, c.w);
        }
    }
}
