#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Light2D
{
    /// <summary>
    /// Mirrors sprite geometry into the obstacle render target and stores additive color in UV1.
    /// </summary>
    [AddComponentMenu("Light2D/Light Obstacle Sprite")]
    [HelpURL(Light2DDocs.HelpUrl)]
    public class LightObstacleSprite : CustomSprite, ILight2DObstacle
    {
        [Tooltip("Renderer whose sprite geometry is mirrored into this obstacle. Auto-filled from the " +
                 "parent renderer when left empty.")]
        public Renderer GameSpriteRenderer;

        [Tooltip("Additive obstacle color encoded into UV1; brightens light passing through the obstacle " +
                 "(default transparent black adds nothing).")]
        public Color AdditiveColor;

        [Tooltip("LightingSystem that owns this obstacle. Leave empty to draw for any system, or set it to " +
                 "scope the obstacle to one system when several exist.")]
        public LightingSystem Owner;

        private Color _oldSecondaryColor;
        private Renderer _oldGameSpriteRenderer;
        private Sprite _oldSourceSprite;
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
            // B.4: builds compile out the editor asset load, so fall back to the obstacle shader here
            // instead of inheriting CustomSprite's Sprites/Default fallback (wrong light absorption).
            if (Material == null)
                Material = CustomSprite.GetRuntimeDefaultObstacleMaterial();

            if (GameSpriteRenderer == null && transform.parent != null)
                GameSpriteRenderer = transform.parent.GetComponent<Renderer>();

            base.OnEnable();
            UpdateMeshData(true);
            Light2DRenderRegistry.RegisterObstacle(this);
        }

        protected override void OnDisable()
        {
            Light2DRenderRegistry.DeregisterObstacle(this);
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            Light2DRenderRegistry.DeregisterObstacle(this);
            base.OnDestroy();
        }

#if UNITY_EDITOR
        // Seeds the texture-conditional obstacle defaults when the component is first added/reset in the
        // editor. Sprites are always textured, so the multiplicative default is the textured color.
        private void Reset()
        {
            var settings = Light2DObstacleSettings.GetOrCreate();
            Color = settings.TexturedMultiplicativeColor;
            AdditiveColor = settings.AdditiveColor;
            SeedDefaultObstacleMaterial();
        }

        // The Material slot is hidden in the normal inspector (see LightObstacleEditors) because it only
        // supplies the obstacle-writing shader. Seed it from the packaged default when unset; existing
        // assignments are left untouched, and a custom-shader material can still be set via the Debug
        // inspector. OnEnable keeps the build-safe runtime fallback for programmatic adds (no Reset).
        private void SeedDefaultObstacleMaterial()
        {
            if (Material != null)
                return;
            var defaults = Light2DDefaultResources.Load();
            if (defaults != null)
                Material = defaults.ObstacleMaterial;
        }
#endif

        bool ILight2DDrawable.TryGetDrawEntry(out Light2DDrawEntry entry)
        {
            entry = default;
            if (!isActiveAndEnabled)
                return false;
            if (Light2DObjectUtility.IsInPrefabStage(gameObject))
                return false;

            var mesh = Mesh;
            var material = DrawMaterial;
            if (mesh == null || material == null)
                return false;

            entry = new Light2DDrawEntry(mesh, material, transform.localToWorldMatrix, WorldBounds,
                Mathf.Max(1, mesh.subMeshCount), SortingOrder, Owner);
            return true;
        }

        private void UpdateSecondaryColor()
        {
            var uv = Light2DColorEncoding.EncodeAdditiveColorToUv(AdditiveColor);
            for (var i = 0; i < _uv1.Length; i++)
                _uv1[i] = uv;
        }

        protected override void UpdateMeshData(bool forceUpdate = false)
        {
            if (_mesh == null)
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
