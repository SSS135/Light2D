using System;
using System.Collections.Generic;
using UnityEngine;

namespace Light2D
{
    /// <summary>
    /// Builds procedural light falloff textures (and matching full-rect runtime sprites) so lights need no
    /// shipped bitmaps. A texture is computed once per distinct key (type + falloff shape + spot angle +
    /// spot angular softness + line sides + resolution) and shared across every light that requests it,
    /// mirroring the material cache in
    /// <see cref="CustomSprite"/>. Works in edit mode; the cache is cleared and its native objects destroyed
    /// on enter-play-mode reload, like <c>CustomSprite.ResetMaterialCache</c>.
    ///
    /// The alpha channel carries the falloff profile (the light shader multiplies by <c>tex.a</c>); RGB is
    /// white so the vertex color alone tints the light.
    ///
    /// Lifetime is bounded by a small LRU cap so sweeping the Falloff/SpotAngle sliders in the editor does
    /// not accumulate textures without limit: every cache hit (and insert) bumps the entry's recency, and a
    /// fresh insert past the cap evicts and destroys the least-recently-used entry. Live lights re-fetch each
    /// frame, keeping themselves most-recently-used, so only abandoned slider-exploration textures are ever
    /// evicted. Each (re)generation runs inside the <see cref="Light2DProfiling.LightTextureGen"/> marker so
    /// it is visible in the profiler.
    /// </summary>
    internal static class Light2DLightTextureGenerator
    {
        internal const int DefaultResolution = 128;

        // LRU cap on distinct cached (type, falloff, spot-angle, resolution) entries.
        internal const int MaxCacheEntries = 64;

        private static readonly Dictionary<Key, Entry> Cache = new Dictionary<Key, Entry>();
        private static long _recencyCounter;

        // Live cache size, for tests.
        internal static int CacheCount { get { return Cache.Count; } }

        // The generated sprite used by a light with no assigned Sprite override. spotSoftness < 0 is Auto
        // (angular edge softness derived from falloff, the legacy behaviour).
        internal static Sprite GetSprite(LightSprite.LightType type, float falloff, float spotAngle,
            LightSprite.LightSides sides = LightSprite.LightSides.Both, float spotSoftness = -1f)
        {
            return GetEntry(type, falloff, spotAngle, sides, spotSoftness, DefaultResolution).Sprite;
        }

        // The generated falloff texture (RGBA32, readable) for the same key. Exposed for tooling/tests.
        internal static Texture2D GetTexture(LightSprite.LightType type, float falloff, float spotAngle,
            LightSprite.LightSides sides = LightSprite.LightSides.Both, float spotSoftness = -1f)
        {
            return GetEntry(type, falloff, spotAngle, sides, spotSoftness, DefaultResolution).Texture;
        }

        private static Entry GetEntry(LightSprite.LightType type, float falloff, float spotAngle,
            LightSprite.LightSides sides, float spotSoftness, int resolution)
        {
            var key = new Key(type, falloff, spotAngle, sides, spotSoftness, resolution);
            if (Cache.TryGetValue(key, out var entry) && entry.Texture != null && entry.Sprite != null)
            {
                entry.Recency = ++_recencyCounter;   // bump to most-recently-used on a hit
                return entry;
            }

            Cache.Remove(key);
            using (Light2DProfiling.LightTextureGen.Auto())
                entry = Build(type, key.Falloff / 100f, key.SpotAngle, sides, key.Softness, resolution);
            entry.Recency = ++_recencyCounter;
            Cache[key] = entry;
            EvictLeastRecentlyUsed();
            return entry;
        }

        // Enforces the LRU cap after an insert: while over capacity, destroy and remove the entry with the
        // lowest recency stamp. Live lights re-touch their entry every frame, so this only reclaims textures
        // left behind by editor slider exploration.
        private static void EvictLeastRecentlyUsed()
        {
            while (Cache.Count > MaxCacheEntries)
            {
                Key lruKey = default;
                Entry lruEntry = null;
                foreach (var pair in Cache)
                {
                    if (lruEntry == null || pair.Value.Recency < lruEntry.Recency)
                    {
                        lruKey = pair.Key;
                        lruEntry = pair.Value;
                    }
                }

                if (lruEntry == null)
                    return;
                Cache.Remove(lruKey);
                Light2DObjectUtility.Destroy(lruEntry.Sprite);
                Light2DObjectUtility.Destroy(lruEntry.Texture);
            }
        }

        private static Entry Build(LightSprite.LightType type, float falloff, float spotAngleDeg,
            LightSprite.LightSides sides, float spotSoftnessDeg, int resolution)
        {
            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, false)
            {
                name = "Generated Light Texture (DONT change it)",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };

            var pixels = new Color32[resolution * resolution];
            FillPixels(pixels, type, falloff, spotAngleDeg, spotSoftnessDeg, sides, resolution);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            // Full-rect sprite, 1 world unit across (PPU == resolution), centered pivot: LightSprite.Radius
            // scales the mesh from this canonical 1-unit base.
            var sprite = Sprite.Create(texture, new Rect(0, 0, resolution, resolution),
                new Vector2(0.5f, 0.5f), resolution, 0, SpriteMeshType.FullRect);
            sprite.name = "Generated Light Sprite (DONT change it)";
            sprite.hideFlags = HideFlags.DontSave;

            return new Entry(texture, sprite);
        }

        private static void FillPixels(Color32[] pixels, LightSprite.LightType type, float falloff,
            float spotAngleDeg, float spotSoftnessDeg, LightSprite.LightSides sides, int resolution)
        {
            // Core radius grows with hardness: a larger near-solid center plus a tighter rim reads as a
            // harder edge; falloff == 0 is an all-the-way-in soft gradient. Clamp01 keeps core at 0 for
            // negative falloff, where FalloffProfile instead blends the falloff==0 curve toward an
            // inverse-square tail for a progressively longer, dimmer edge.
            var core = Mathf.Clamp01(falloff) * 0.85f;
            var inv = 1f / resolution;

            for (var y = 0; y < resolution; y++)
            {
                var v = (y + 0.5f) * inv;                 // 0..1
                for (var x = 0; x < resolution; x++)
                {
                    var u = (x + 0.5f) * inv;             // 0..1
                    float alpha;
                    switch (type)
                    {
                        case LightSprite.LightType.Line:
                            alpha = LineAlpha(v, core, falloff, sides);
                            break;
                        case LightSprite.LightType.Spot:
                            alpha = SpotAlpha(u, v, core, falloff, spotAngleDeg, spotSoftnessDeg);
                            break;
                        default:
                            alpha = RadialAlpha(u, v, core, falloff);
                            break;
                    }

                    pixels[y * resolution + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha) * 255f));
                }
            }
        }

        // Shared radial-distance -> alpha mapping used by Point, Line, and the Spot radial term, so a single
        // Falloff control shapes every light type coherently across the extended [-1, 1] range.
        //   Falloff >= 0 (the legacy [0, 1] range): byte-identical to the original 1 - SmoothStep01(core, 1, d),
        //     with core = Clamp01(falloff) * 0.85 supplied by the caller. tail is never evaluated on this path.
        //   Falloff <  0: core is 0 (Clamp01), so `legacy` here is exactly the softest legacy curve (Falloff 0);
        //     blend it toward an inverse-square-style tail by weight -Falloff for a progressively longer, dimmer,
        //     softer falloff. The blend is C0-continuous at 0 (weight 0 returns the Falloff-0 curve, matching the
        //     >= 0 branch) and stays exactly 0 for d >= 1 (both endpoints are 0 there), so a negative Falloff
        //     never brightens the quad corners nor clips the light at its border.
        private static float FalloffProfile(float d, float core, float falloff)
        {
            var legacy = 1f - SmoothStep01(core, 1f, d);
            if (falloff >= 0f)
                return legacy;
            return Mathf.Lerp(legacy, InverseSquareTail(d), -falloff);
        }

        // Inverse-square-flavoured radial tail on d in [0, 1]: 1 at the centre, exactly 0 at d >= 1. The
        // Clamp01(1 - d)^2 numerator windows the profile to a clean zero at the rim (and keeps the sqrt(2) quad
        // corners dark); the (1 + a*d)^2 denominator is the 1/dist^2 concentration that gives the long dim tail.
        // Denominator >= 1 (no divide-by-zero) and the squared base is non-negative (no NaN) - cheap and stable.
        private const float TailFocus = 1f;   // 'a': larger concentrates the core and lengthens the dim tail
        private static float InverseSquareTail(float d)
        {
            var window = Mathf.Clamp01(1f - d);
            var denom = 1f + TailFocus * d;
            return (window * window) / (denom * denom);
        }

        // Point: symmetric radial gradient, bright center smoothly fading to a transparent rim.
        private static float RadialAlpha(float u, float v, float core, float falloff)
        {
            var dx = (u - 0.5f) * 2f;
            var dy = (v - 0.5f) * 2f;
            var d = Mathf.Sqrt(dx * dx + dy * dy);        // 0 center .. ~1 edge
            return FalloffProfile(d, core, falloff);
        }

        // Line: 1-D vertical profile, uniform along the horizontal extent. Sides places the emitting segment
        // (the bright edge) and the direction of the fade: Both is symmetric about the mid-row (two-sided),
        // Front puts the segment at v == 0 (the mesh's +normal edge) and fades toward v == 1, Back puts it
        // at v == 1 (the -normal edge) and fades toward v == 0.
        private static float LineAlpha(float v, float core, float falloff, LightSprite.LightSides sides)
        {
            float d;
            switch (sides)
            {
                case LightSprite.LightSides.Front:
                    d = v;                                // 0 at the segment (v == 0) .. 1 at the far edge
                    break;
                case LightSprite.LightSides.Back:
                    d = 1f - v;                           // 0 at the segment (v == 1) .. 1 at the far edge
                    break;
                default:
                    d = Mathf.Abs(v - 0.5f) * 2f;         // 0 mid-row .. 1 top/bottom
                    break;
            }

            return FalloffProfile(d, core, falloff);
        }

        // Spot: soft-edged wedge emanating from the (possibly migrated) origin, opening along +Y (v), with a
        // radial fade along the cone length.
        //
        // Origin migration. t goes 0 for spotAngleDeg <= 180 to 1 at 360. At t == 0 the origin sits at the
        // bottom-centre apex (originV 0) and the radial normalization is x1 (distScale 1), so this reduces
        // EXACTLY to the legacy cone. As t rises the origin migrates to the centre (originV -> 0.5) and the
        // radial normalization grows to the Point's x2 (distScale -> 2); at t == 1 the origin is the centre,
        // distScale is 2, and the angular mask opens fully, so the result equals RadialAlpha bit-for-bit (the
        // multiplies/adds by 0/1 are exact and 2*sqrt(r) == sqrt(4r) under round-to-nearest for these values).
        //
        // Angular edge softness. spotSoftnessDeg < 0 is Auto: derive the softness from the radial falloff
        // hardness, kept textually identical to the old code so default content is byte-identical.
        // spotSoftnessDeg >= 0 is an explicit angular penumbra in degrees that insets the fade from each
        // aperture edge (0 hard .. half-aperture full), independent of the radial falloff. The same mask
        // softness applies to the >180 masked-radial regime.
        private static float SpotAlpha(float u, float v, float core, float falloff, float spotAngleDeg,
            float spotSoftnessDeg)
        {
            var t = LightSprite.SpotOriginBlend(spotAngleDeg);
            var originV = 0.5f * t;
            var distScale = 1f + t;

            var dx = u - 0.5f;
            var dy = v - originV;                          // apex at v == 0 for the legacy cone (t == 0)
            var dist = distScale * Mathf.Sqrt(dx * dx + dy * dy);
            var radial = FalloffProfile(dist, core, falloff);

            var halfAngle = Mathf.Deg2Rad * spotAngleDeg * 0.5f;
            var angle = Mathf.Atan2(Mathf.Abs(dx), Mathf.Max(dy, 1e-5f));
            float innerEdge;
            if (spotSoftnessDeg < 0f)
            {
                // Auto: legacy falloff-derived angular softness (this expression is unchanged from before).
                // Clamp01 pins a negative Falloff to the falloff==0 angular band, so the wider [-1, 0) tail
                // softens only the radial fade and never inverts or narrows the angular mask.
                var softBand = Mathf.Lerp(0.5f, 0.05f, Mathf.Clamp01(falloff));
                innerEdge = halfAngle * (1f - softBand);
            }
            else
            {
                // Explicit penumbra: the fade spans spotSoftnessDeg degrees inward from the aperture edge,
                // clamped to the half-aperture. 0 => hard edge; >= half-aperture => fade across the whole cone.
                var innerDeg = Mathf.Max(0f, spotAngleDeg * 0.5f - spotSoftnessDeg);
                innerEdge = Mathf.Deg2Rad * innerDeg;
            }
            var angular = 1f - SmoothStep01(innerEdge, halfAngle, angle);
            angular = angular * (1f - t) + t;              // mask opens fully at 360 -> exact Point

            return radial * angular;
        }

        // GLSL-style smoothstep: 0 at x <= edge0, 1 at x >= edge1, smooth in between. NOT the same as
        // UnityEngine.Mathf.SmoothStep, which interpolates between two values by a [0,1] parameter.
        private static float SmoothStep01(float edge0, float edge1, float x)
        {
            var t = Mathf.Clamp01((x - edge0) / Mathf.Max(edge1 - edge0, 1e-5f));
            return t * t * (3f - 2f * t);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            ClearCache();
        }

        // Destroys every cached texture/sprite and empties the cache. Called on enter-play-mode reload and
        // exposed for tests that need a clean cache to reason about the LRU cap.
        internal static void ClearCache()
        {
            foreach (var entry in Cache.Values)
            {
                Light2DObjectUtility.Destroy(entry.Sprite);
                Light2DObjectUtility.Destroy(entry.Texture);
            }
            Cache.Clear();
            _recencyCounter = 0;
        }

        // Reference type so the LRU recency stamp can be bumped in place without reinserting into the cache.
        private sealed class Entry
        {
            public readonly Texture2D Texture;
            public readonly Sprite Sprite;
            public long Recency;

            public Entry(Texture2D texture, Sprite sprite)
            {
                Texture = texture;
                Sprite = sprite;
            }
        }

        // Falloff/spot angle/spot softness/sides are quantized so continuous slider values share a bounded set
        // of textures. Spot angle and softness only participate for Spot lights and Sides only for Line lights
        // (others collapse to a shared entry).
        private readonly struct Key : IEquatable<Key>
        {
            public readonly LightSprite.LightType Type;
            public readonly int Falloff;      // round(clamp(falloff, -1, 1) * 100); < 0 selects the inverse-square tail
            public readonly int SpotAngle;    // round(clamp(degrees, 1, 360)), 0 for non-spot
            public readonly int Softness;     // spot angular penumbra: -1 == Auto (from falloff), else round(clamp(deg, 0, 180)); 0 for non-spot
            public readonly int Sides;        // (int)sides for Line, 0 otherwise
            public readonly int Resolution;

            public Key(LightSprite.LightType type, float falloff, float spotAngle,
                LightSprite.LightSides sides, float spotSoftness, int resolution)
            {
                Type = type;
                Falloff = Mathf.RoundToInt(Mathf.Clamp(falloff, -1f, 1f) * 100f);
                SpotAngle = type == LightSprite.LightType.Spot
                    ? Mathf.RoundToInt(Mathf.Clamp(spotAngle, 1f, 360f))
                    : 0;
                Softness = type == LightSprite.LightType.Spot
                    ? (spotSoftness < 0f ? -1 : Mathf.RoundToInt(Mathf.Clamp(spotSoftness, 0f, 180f)))
                    : 0;
                Sides = type == LightSprite.LightType.Line ? (int)sides : 0;
                Resolution = resolution;
            }

            public bool Equals(Key other)
            {
                return Type == other.Type && Falloff == other.Falloff && SpotAngle == other.SpotAngle &&
                       Softness == other.Softness && Sides == other.Sides && Resolution == other.Resolution;
            }

            public override bool Equals(object obj)
            {
                return obj is Key other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = (int)Type;
                    hash = (hash * 397) ^ Falloff;
                    hash = (hash * 397) ^ SpotAngle;
                    hash = (hash * 397) ^ Softness;
                    hash = (hash * 397) ^ Sides;
                    return (hash * 397) ^ Resolution;
                }
            }
        }
    }
}
