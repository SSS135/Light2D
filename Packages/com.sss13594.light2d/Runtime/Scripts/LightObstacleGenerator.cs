using UnityEngine;

namespace Light2D
{
    /// <summary>
    /// Creates the matching sprite or mesh obstacle for this renderer at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public class LightObstacleGenerator : MonoBehaviour
    {
        public Color MultiplicativeColor = new Color(0, 0, 0, 1);
        public Color AdditiveColor;
        public Material Material;
        public float LightObstacleScale = 1;
        public LightingSystem Owner;

        private void Start()
        {
#if UNITY_EDITOR
            if (Material == null)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath("0a8c132f813f5514b9f5b48ff3956482");
                Material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
            }
#endif
            if (!Application.isPlaying || Light2DObjectUtility.IsInPrefabStage(gameObject))
                return;

            var lightingSystem = Light2DObjectUtility.ResolveOwner(Owner);
            if (lightingSystem == null)
            {
                Debug.LogWarning(
                    "LightObstacleGenerator requires an Owner unless exactly one LightingSystem is active.", this);
                return;
            }

            var obstacleObject = new GameObject(gameObject.name + " Light Obstacle");
            obstacleObject.transform.SetParent(transform, false);
            obstacleObject.transform.localScale = Vector3.one * LightObstacleScale;

            if (GetComponent<SpriteRenderer>() != null || GetComponent<CustomSprite>() != null)
            {
                var obstacle = obstacleObject.AddComponent<LightObstacleSprite>();
                obstacle.GameSpriteRenderer = GetComponent<Renderer>();
                obstacle.Color = MultiplicativeColor;
                obstacle.AdditiveColor = AdditiveColor;
                obstacle.Material = Material;
                obstacle.Owner = lightingSystem;
            }
            else if (GetComponent<MeshRenderer>() != null && GetComponent<MeshFilter>() != null)
            {
                var obstacle = obstacleObject.AddComponent<LightObstacleMesh>();
                obstacle.MultiplicativeColor = MultiplicativeColor;
                obstacle.AdditiveColor = AdditiveColor;
                obstacle.Material = Material;
                obstacle.Owner = lightingSystem;
            }
            else
            {
                Debug.LogWarning("LightObstacleGenerator requires a SpriteRenderer, CustomSprite, or MeshRenderer with MeshFilter.", this);
                Light2DObjectUtility.Destroy(obstacleObject);
                return;
            }

            Destroy(this);
        }
    }
}
