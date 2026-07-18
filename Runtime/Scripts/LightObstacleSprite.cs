#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Light2D
{
    /// <summary>
    /// Sprite with dual color support. Grabs sprite from GameSpriteRenderer field.
    /// </summary>
    [ExecuteInEditMode]
    public class LightObstacleSprite : CustomSprite
    {
        /// <summary>
        /// Renderer from which sprite will be used.
        /// </summary>
        public Renderer GameSpriteRenderer;

        /// <summary>
        /// Color is packed in mesh UV1.
        /// </summary>
        public Color AdditiveColor;

        public LightingSystem Owner;

        private Color _oldSecondaryColor;
        private Renderer _oldGameSpriteRenderer;
        private SpriteRenderer _oldUnitySprite;
        private CustomSprite _oldCustomSprite;

        protected override void OnEnable()
        {
#if UNITY_EDITOR
            if (Material == null)
            {
                var path = AssetDatabase.GUIDToAssetPath("0a8c132f813f5514b9f5b48ff3956482");
                Material = AssetDatabase.LoadAssetAtPath<Material>(path);
            }
#endif

            base.OnEnable();

            if (GameSpriteRenderer == null && transform.parent != null)
                GameSpriteRenderer = transform.parent.gameObject.GetComponent<Renderer>();

            var lightingSystem = Owner != null ? Owner : LightingSystem.Instance;
            if (lightingSystem != null)
                gameObject.layer = lightingSystem.LightObstaclesLayer;

            UpdateMeshData(true);
        }

        protected override bool UseNormalTexcoordMaterial
        {
            get { return true; }
        }

        private void UpdateSecondaryColor()
        {
            var uv1 = new Vector2(
                Util.DecodeFloatRGBA((Vector4)AdditiveColor),
                Util.DecodeFloatRGBA(new Vector4(AdditiveColor.a, 0, 0)));
            for (int i = 0; i < _uv1.Length; i++)
            {
                _uv1[i] = uv1;
            }
        }

        protected override void UpdateMeshData(bool forceUpdate = false)
        {
            if (_meshRenderer == null || _meshFilter == null || IsPartOfStaticBatch)
                return;

            if (GameSpriteRenderer != null && (GameSpriteRenderer != _oldGameSpriteRenderer || forceUpdate ||
                (_oldUnitySprite != null && _oldUnitySprite.sprite != null && _oldUnitySprite.sprite != Sprite) ||
                (_oldCustomSprite != null && _oldCustomSprite.Sprite != null && _oldCustomSprite.Sprite != Sprite)))
            {
                _oldGameSpriteRenderer = GameSpriteRenderer;

                _oldCustomSprite = GameSpriteRenderer.GetComponent<CustomSprite>();
                if (_oldCustomSprite != null)
                {
                    Sprite = _oldCustomSprite.Sprite;
                }
                else
                {
                    _oldUnitySprite = GameSpriteRenderer.GetComponent<SpriteRenderer>();
                    if (_oldUnitySprite != null)
                        Sprite = _oldUnitySprite.sprite;
                }

            }

            if (_oldSecondaryColor != AdditiveColor || forceUpdate)
            {
                UpdateSecondaryColor();
                _isMeshDirty = true;
                _oldSecondaryColor = AdditiveColor;
            }

            base.UpdateMeshData(forceUpdate);
        }
    }
}

