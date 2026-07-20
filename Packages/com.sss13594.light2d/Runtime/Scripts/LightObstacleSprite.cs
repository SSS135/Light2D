#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Light2D
{
    /// <summary>
    /// Mirrors sprite geometry into the obstacle layer and stores additive color in UV1.
    /// </summary>
    public class LightObstacleSprite : CustomSprite
    {
        public Renderer GameSpriteRenderer;
        public Color AdditiveColor;
        public LightingSystem Owner;

        private Color _oldSecondaryColor;
        private Renderer _oldGameSpriteRenderer;
        private Sprite _oldSourceSprite;
        private LightingSystem _oldResolvedOwner;
        private int _oldObstacleLayer = -1;
        private Renderer _sourceRenderer;
        private CustomSprite _sourceCustomSprite;
        private SpriteRenderer _sourceSpriteRenderer;

        protected override void OnEnable()
        {
#if UNITY_EDITOR
            if (Material == null)
            {
                var path = AssetDatabase.GUIDToAssetPath("0a8c132f813f5514b9f5b48ff3956482");
                Material = AssetDatabase.LoadAssetAtPath<Material>(path);
            }
#endif
            if (GameSpriteRenderer == null && transform.parent != null)
                GameSpriteRenderer = transform.parent.GetComponent<Renderer>();

            base.OnEnable();
            UpdateMeshData(true);
        }

        protected override bool UseNormalTexcoordMaterial { get { return true; } }

        private void UpdateSecondaryColor()
        {
            var uv = Light2DColorEncoding.EncodeAdditiveColorToUv(AdditiveColor);
            for (var i = 0; i < _uv1.Length; i++)
                _uv1[i] = uv;
        }

        protected override void UpdateMeshData(bool forceUpdate = false)
        {
            if (_meshRenderer == null || _meshFilter == null || IsPartOfStaticBatch)
                return;

            var sourceSprite = ResolveSourceSprite(GameSpriteRenderer);
            if (GameSpriteRenderer != _oldGameSpriteRenderer || sourceSprite != _oldSourceSprite || forceUpdate)
            {
                Sprite = sourceSprite;
                _oldGameSpriteRenderer = GameSpriteRenderer;
                _oldSourceSprite = sourceSprite;
            }

            base.UpdateMeshData(forceUpdate);

            if (_oldSecondaryColor != AdditiveColor || forceUpdate)
            {
                UpdateSecondaryColor();
                _oldSecondaryColor = AdditiveColor;
                _isMeshDirty = true;
                base.UpdateMeshData();
            }

            var resolvedOwner = Light2DObjectUtility.ResolveOwner(Owner);
            var obstacleLayer = resolvedOwner != null ? resolvedOwner.LightObstaclesLayer : -1;
            if (!Light2DObjectUtility.IsInPrefabStage(gameObject) &&
                (resolvedOwner != _oldResolvedOwner || obstacleLayer != _oldObstacleLayer) && obstacleLayer >= 0)
                gameObject.layer = obstacleLayer;
            _oldResolvedOwner = resolvedOwner;
            _oldObstacleLayer = obstacleLayer;
        }

        private Sprite ResolveSourceSprite(Renderer source)
        {
            if (source == null)
            {
                _sourceRenderer = null;
                _sourceCustomSprite = null;
                _sourceSpriteRenderer = null;
                return null;
            }

            // Component lookup runs only when the source renderer changes; the cached source stays live-read.
            if (source != _sourceRenderer)
            {
                _sourceRenderer = source;
                var customSprite = source.GetComponent<CustomSprite>();
                _sourceCustomSprite = customSprite != this ? customSprite : null;
                _sourceSpriteRenderer = _sourceCustomSprite != null ? null : source as SpriteRenderer;
            }

            if (_sourceCustomSprite != null)
                return _sourceCustomSprite.Sprite;
            return _sourceSpriteRenderer != null ? _sourceSpriteRenderer.sprite : null;
        }
    }
}
