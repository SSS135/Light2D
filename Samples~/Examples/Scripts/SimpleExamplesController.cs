using UnityEngine;

namespace Light2D.Examples
{
    public class SimpleExamplesController : MonoBehaviour
    {
        public LightSprite[] ColoredLights = new LightSprite[0];
        public GameObject[] Examples = new GameObject[0];
        public Color[] LightColors = {Color.white};
        private int _currExampleIndex;
        private int _currColorIndex;

        void Start()
        {
            UpdateExample();
            UpdateColors();
        }

        void Update()
        {
            if (Input.GetMouseButtonUp(0) || TouchEnded(0))
            {
                _currExampleIndex++;
                if (_currExampleIndex >= Examples.Length)
                    _currExampleIndex = 0;

                UpdateExample();
            }

            if (Input.GetMouseButtonUp(1) || TouchEnded(1))
            {
                _currColorIndex++;
                if (_currColorIndex >= LightColors.Length)
                    _currColorIndex = 0;

                UpdateColors();
            }
        }

        void UpdateColors()
        {
            var color = LightColors.Length == 0 ? Color.white : LightColors[_currColorIndex];
            for (int i = 0; i < ColoredLights.Length; i++)
                ColoredLights[i].Color = SampleUtilities.WithAlpha(color, ColoredLights[i].Color.a);
        }

        void UpdateExample()
        {
            for (int i = 0; i < Examples.Length; i++)
                Examples[i].SetActive(i == _currExampleIndex);
            if (LightingSystem.Instance != null)
                LightingSystem.Instance.LoopAmbientLight(20);
        }

        private static bool TouchEnded(int fingerIndex)
        {
            return Input.touchCount > fingerIndex && Input.GetTouch(fingerIndex).phase == TouchPhase.Ended;
        }
    }
}
