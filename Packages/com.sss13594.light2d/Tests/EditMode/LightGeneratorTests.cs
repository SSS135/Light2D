using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Light2D.Tests
{
    public class LightGeneratorTests
    {
        [Test]
        public void SameKeyReturnsCachedInstance()
        {
            var a = Light2DLightTextureGenerator.GetSprite(LightSprite.LightType.Point, 0.5f, 45f);
            var b = Light2DLightTextureGenerator.GetSprite(LightSprite.LightType.Point, 0.5f, 45f);
            Assert.That(a, Is.Not.Null);
            Assert.That(b, Is.SameAs(a));
            Assert.That(b.texture, Is.SameAs(a.texture));
        }

        [Test]
        public void DistinctFalloffYieldsDistinctTexture()
        {
            var soft = Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Point, 0.2f, 45f);
            var hard = Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Point, 0.9f, 45f);
            Assert.That(soft, Is.Not.Null);
            Assert.That(hard, Is.Not.SameAs(soft));
        }

        [Test]
        public void DistinctSpotAngleYieldsDistinctTexture()
        {
            var narrow = Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Spot, 0.5f, 20f);
            var wide = Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Spot, 0.5f, 120f);
            Assert.That(wide, Is.Not.SameAs(narrow));
        }

        [Test]
        public void DistinctTypeYieldsDistinctTexture()
        {
            var point = Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Point, 0.5f, 45f);
            var line = Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Line, 0.5f, 45f);
            var spot = Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Spot, 0.5f, 45f);
            Assert.That(line, Is.Not.SameAs(point));
            Assert.That(spot, Is.Not.SameAs(point));
            Assert.That(spot, Is.Not.SameAs(line));
        }

        [Test]
        public void SpotAngleIsIgnoredForPointType()
        {
            // Non-spot types ignore the spot angle, so the two requests share one cached entry.
            var a = Light2DLightTextureGenerator.GetSprite(LightSprite.LightType.Point, 0.5f, 20f);
            var b = Light2DLightTextureGenerator.GetSprite(LightSprite.LightType.Point, 0.5f, 120f);
            Assert.That(b, Is.SameAs(a));
        }

        [Test]
        public void GeneratedSpriteIsFullRectAndReadable()
        {
            var sprite = Light2DLightTextureGenerator.GetSprite(LightSprite.LightType.Point, 0.5f, 45f);
            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.vertices.Length, Is.EqualTo(4), "Full-rect sprites carry a 4-vertex quad.");
            // Canonical 1 world unit base so LightSprite.Radius scales it directly.
            Assert.That(sprite.bounds.size.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(sprite.bounds.size.y, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void PointTextureIsBrightAtCenterAndRadiallySymmetric()
        {
            var tex = Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Point, 0.5f, 45f);
            var center = tex.GetPixelBilinear(0.5f, 0.5f).a;
            var rim = tex.GetPixelBilinear(0.5f, 0.98f).a;
            Assert.That(center, Is.GreaterThan(rim));

            var up = tex.GetPixelBilinear(0.5f, 0.75f).a;
            var down = tex.GetPixelBilinear(0.5f, 0.25f).a;
            var left = tex.GetPixelBilinear(0.25f, 0.5f).a;
            var right = tex.GetPixelBilinear(0.75f, 0.5f).a;
            Assert.That(up, Is.EqualTo(down).Within(0.02f));
            Assert.That(left, Is.EqualTo(right).Within(0.02f));
            Assert.That(up, Is.EqualTo(left).Within(0.02f), "A radial gradient is symmetric in every direction.");
        }

        [Test]
        public void SpotTextureIsAConeFromTheBottomApex()
        {
            var tex = Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Spot, 0.5f, 60f);

            // Brighter closer to the bottom-center apex → a cone, not a center-radial gradient.
            var nearApex = tex.GetPixelBilinear(0.5f, 0.25f).a;
            var farApex = tex.GetPixelBilinear(0.5f, 0.75f).a;
            Assert.That(nearApex, Is.GreaterThan(farApex));

            // Confined to the cone: on-axis is far brighter than well outside the 60-degree wedge.
            var onAxis = tex.GetPixelBilinear(0.5f, 0.5f).a;
            var offAxis = tex.GetPixelBilinear(0.92f, 0.5f).a;
            Assert.That(onAxis, Is.GreaterThan(offAxis));
        }

        [Test]
        public void LineTextureIsUniformAlongHorizontalExtent()
        {
            var tex = Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Line, 0.5f, 45f);

            var leftMid = tex.GetPixelBilinear(0.2f, 0.5f).a;
            var rightMid = tex.GetPixelBilinear(0.8f, 0.5f).a;
            Assert.That(leftMid, Is.EqualTo(rightMid).Within(0.02f), "A strip is uniform along its length.");

            var mid = tex.GetPixelBilinear(0.5f, 0.5f).a;
            var edge = tex.GetPixelBilinear(0.5f, 0.98f).a;
            Assert.That(mid, Is.GreaterThan(edge), "A strip fades across its perpendicular profile.");
        }

        [Test]
        public void DistinctLineSidesYieldDistinctTextures()
        {
            var both = Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Line, 0.5f, 0f,
                LightSprite.LightSides.Both);
            var front = Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Line, 0.5f, 0f,
                LightSprite.LightSides.Front);
            var back = Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Line, 0.5f, 0f,
                LightSprite.LightSides.Back);
            Assert.That(front, Is.Not.SameAs(both));
            Assert.That(back, Is.Not.SameAs(both));
            Assert.That(front, Is.Not.SameAs(back));
        }

        [Test]
        public void OneSidedLineTexturesAreBrightAtTheirSegmentEdge()
        {
            // Front places the emitting segment (bright edge) at v == 0 and fades toward v == 1; Back mirrors it.
            var front = Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Line, 0.5f, 0f,
                LightSprite.LightSides.Front);
            Assert.That(front.GetPixelBilinear(0.5f, 0.02f).a,
                Is.GreaterThan(front.GetPixelBilinear(0.5f, 0.98f).a),
                "A Front strip is brightest at its v==0 segment edge.");

            var back = Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Line, 0.5f, 0f,
                LightSprite.LightSides.Back);
            Assert.That(back.GetPixelBilinear(0.5f, 0.98f).a,
                Is.GreaterThan(back.GetPixelBilinear(0.5f, 0.02f).a),
                "A Back strip is brightest at its v==1 segment edge.");
        }

        [Test]
        public void CacheIsBoundedByLruCapAndProtectsRecentlyUsedEntries()
        {
            Light2DLightTextureGenerator.ClearCache();
            var cap = Light2DLightTextureGenerator.MaxCacheEntries;

            // A "live" key we keep re-touching so it stays most-recently-used across the overflow.
            var kept = Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Point, 0f, 0f);
            Assert.That(kept, Is.Not.Null);

            // Fill exactly to capacity with distinct falloff buckets (1..cap-1) plus the kept key.
            for (var i = 1; i < cap; i++)
                Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Point, i / 100f, 0f);
            Assert.That(Light2DLightTextureGenerator.CacheCount, Is.EqualTo(cap));

            // Re-touch the kept key right before overflow → it becomes most-recently-used and must survive.
            Assert.That(Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Point, 0f, 0f),
                Is.SameAs(kept));

            // Overflow with more distinct keys; the cache stays bounded at the cap.
            for (var i = cap; i < cap + 20; i++)
                Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Point, i / 100f, 0f);
            Assert.That(Light2DLightTextureGenerator.CacheCount, Is.LessThanOrEqualTo(cap));

            // MRU protection: the kept key was re-touched just before overflow, so it was not evicted and the
            // same texture instance is returned (a rebuild would return a different instance).
            Assert.That(Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Point, 0f, 0f),
                Is.SameAs(kept));

            Light2DLightTextureGenerator.ClearCache();
        }

        [Test]
        public void SpotAt360EqualsPointFalloff()
        {
            // At 360 the Spot origin sits at the centre, the radial normalization matches Point's, and the
            // angular mask fully opens, so the falloff must equal the Point radial falloff. The morph math is
            // constructed to be bit-identical here, so the max byte delta is expected to be 0.
            for (var f = 0; f <= 4; f++)
            {
                var falloff = f / 4f;
                var spot = Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Spot, falloff, 360f);
                var point = Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Point, falloff, 45f);
                var maxDelta = MaxAlphaDelta(spot, point);
                Assert.That(maxDelta, Is.LessThanOrEqualTo(1),
                    "A 360-degree Spot must equal the Point falloff (falloff " + falloff + ", max byte delta " +
                    maxDelta + ").");
            }
        }

        [Test]
        public void DistinctSpotSoftnessYieldsDistinctTexture()
        {
            var hard = Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Spot, 0.5f, 90f,
                LightSprite.LightSides.Both, 0f);
            var soft = Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Spot, 0.5f, 90f,
                LightSprite.LightSides.Both, 120f);
            var auto = Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Spot, 0.5f, 90f);
            Assert.That(soft, Is.Not.SameAs(hard), "Distinct SpotSoftness must not share a cache entry.");
            Assert.That(auto, Is.Not.SameAs(hard), "Auto softness must not share the hard-edge entry.");
            Assert.That(auto, Is.Not.SameAs(soft));

            // Inside the 90-degree wedge but near its 45-degree edge (~35 deg from +Y, ~0.35 from the apex):
            // a hard edge is still fully lit there while a broad penumbra has already feathered it down.
            const float u = 0.701f;
            const float v = 0.287f;
            Assert.That(hard.GetPixelBilinear(u, v).a, Is.GreaterThan(soft.GetPixelBilinear(u, v).a + 0.2f),
                "A hard angular edge stays brighter near the aperture edge than a broad penumbra.");
        }

        [Test]
        public void LegacySpotFalloffIsByteIdenticalToPreChange()
        {
            // Regression guard: the default (Auto) Spot falloff must reproduce the pre-change formula exactly
            // for angles <= 180 across falloff values. The old pixel formula is recomputed independently below
            // and compared through the public GetTexture path.
            foreach (var falloff in new[] { 0.2f, 0.5f, 0.9f })
            foreach (var angle in new[] { 30f, 60f, 120f, 179f })
                AssertLegacySpotUnchanged(falloff, angle);
        }

        [Test]
        public void LegacyRadialFalloffIsByteIdenticalToPreChange()
        {
            // Regression guard for the [-1, 1] extension: every Falloff >= 0 must reproduce the pre-change
            // Point radial formula byte-for-byte. The old pixel formula is recomputed independently and
            // compared through the public GetTexture path across the full legacy range (0, 0.3, 0.7, 1).
            foreach (var falloff in new[] { 0f, 0.3f, 0.7f, 1f })
                AssertLegacyRadialUnchanged(falloff);
        }

        [Test]
        public void NegativeFalloffIsSofterThanZero()
        {
            // -1 is the fully inverse-square tail; it must be strictly dimmer than the softest legacy curve
            // (Falloff 0) both at a mid-tail sample (radial distance d == 0.75, on the vertical axis at
            // v == 0.875) and in total integrated alpha across the whole disc.
            var soft = Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Point, -1f, 45f);
            var baseline = Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Point, 0f, 45f);

            Assert.That(soft.GetPixelBilinear(0.5f, 0.875f).a,
                Is.LessThan(baseline.GetPixelBilinear(0.5f, 0.875f).a),
                "Falloff -1 must be dimmer than Falloff 0 at d == 0.75.");
            Assert.That(IntegratedAlpha(soft), Is.LessThan(IntegratedAlpha(baseline)),
                "Falloff -1 must carry less total light than Falloff 0.");
        }

        [Test]
        public void NegativeFalloffMonotonicallySoftens()
        {
            // Softness increases monotonically as Falloff walks 0 -> -1: each step lowers the integrated alpha.
            var previous = long.MaxValue;
            foreach (var falloff in new[] { 0f, -0.25f, -0.5f, -0.75f, -1f })
            {
                var sum = IntegratedAlpha(
                    Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Point, falloff, 45f));
                Assert.That(sum, Is.LessThan(previous),
                    "Integrated alpha must strictly decrease as Falloff moves negative (falloff " + falloff + ").");
                previous = sum;
            }
        }

        [Test]
        public void NegativeFalloffIsContinuousAtZero()
        {
            // C0 continuity across the 0 boundary: a hair below zero (-0.01, one quantization step) differs
            // from exactly 0 by at most a rounding byte, so sweeping the slider through 0 shows no visible jump.
            var justBelow = Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Point, -0.01f, 45f);
            var atZero = Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Point, 0f, 45f);
            Assert.That(MaxAlphaDelta(justBelow, atZero), Is.LessThanOrEqualTo(3),
                "Falloff -0.01 must be within a rounding byte of Falloff 0 (C0-continuous at the boundary).");
        }

        [Test]
        public void FalloffIsZeroBeyondTheRadialEdge()
        {
            // Edge cleanliness for every Falloff: the quad corners (radial distance d >= 1) must be fully
            // transparent so the light never clips or brightens at the quad border.
            const int res = Light2DLightTextureGenerator.DefaultResolution;
            var inv = 1f / res;
            foreach (var falloff in new[] { 1f, 0.5f, 0f, -0.5f, -1f })
            {
                var px = Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Point, falloff, 45f)
                    .GetPixels32();
                for (var y = 0; y < res; y++)
                {
                    var v = (y + 0.5f) * inv;
                    for (var x = 0; x < res; x++)
                    {
                        var u = (x + 0.5f) * inv;
                        var dx = (u - 0.5f) * 2f;
                        var dy = (v - 0.5f) * 2f;
                        if (Mathf.Sqrt(dx * dx + dy * dy) < 1f)
                            continue;
                        Assert.That(px[y * res + x].a, Is.EqualTo(0),
                            "d >= 1 must be transparent (falloff " + falloff + ", pixel " + x + "," + y + ").");
                    }
                }
            }
        }

        [Test]
        public void DistinctNegativeFalloffYieldsDistinctTexture()
        {
            var half = Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Point, -0.5f, 45f);
            var full = Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Point, -1f, 45f);
            var zero = Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Point, 0f, 45f);
            Assert.That(full, Is.Not.SameAs(half), "Distinct negative Falloff must not share a cache entry.");
            Assert.That(half, Is.Not.SameAs(zero), "A negative Falloff must not collapse onto the Falloff-0 entry.");
        }

        [Test]
        public void NegativeFalloffAlsoSoftensLineAndSpot()
        {
            // The tail blend is shared, so a negative Falloff softens the Line perpendicular fade and the Spot
            // radial fade coherently, not just the Point disc.
            var lineSoft = Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Line, -1f, 0f);
            var lineBase = Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Line, 0f, 0f);
            Assert.That(lineSoft.GetPixelBilinear(0.5f, 0.75f).a,
                Is.LessThan(lineBase.GetPixelBilinear(0.5f, 0.75f).a),
                "A negative Falloff dims the Line perpendicular fade at d == 0.5.");

            var spotSoft = Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Spot, -1f, 60f);
            var spotBase = Light2DLightTextureGenerator.GetTexture(LightSprite.LightType.Spot, 0f, 60f);
            Assert.That(IntegratedAlpha(spotSoft), Is.LessThan(IntegratedAlpha(spotBase)),
                "A negative Falloff dims the Spot radial fade overall.");
        }

        private static long IntegratedAlpha(Texture2D tex)
        {
            long sum = 0;
            foreach (var p in tex.GetPixels32())
                sum += p.a;
            return sum;
        }

        private static void AssertLegacyRadialUnchanged(float falloff)
        {
            const int res = 128;   // Light2DLightTextureGenerator.DefaultResolution
            var actual = Light2DLightTextureGenerator
                .GetTexture(LightSprite.LightType.Point, falloff, 45f).GetPixels32();

            // Match the generator's quantization: falloff -> round(clamp(-1,1)*100)/100 (identical to the old
            // clamp01 form for the non-negative values under test).
            var qFalloff = Mathf.RoundToInt(Mathf.Clamp(falloff, -1f, 1f) * 100f) / 100f;
            var core = Mathf.Clamp01(qFalloff) * 0.85f;
            var inv = 1f / res;

            for (var y = 0; y < res; y++)
            {
                var v = (y + 0.5f) * inv;
                for (var x = 0; x < res; x++)
                {
                    var u = (x + 0.5f) * inv;
                    var expected = (byte)(Mathf.Clamp01(OldRadialAlpha(u, v, core)) * 255f);
                    Assert.That(actual[y * res + x].a, Is.EqualTo(expected),
                        "Legacy Point pixel changed at (" + x + "," + y + ") for falloff " + falloff + ".");
                }
            }
        }

        // The pre-change RadialAlpha, copied verbatim, as the regression oracle for Falloff >= 0.
        private static float OldRadialAlpha(float u, float v, float core)
        {
            var dx = (u - 0.5f) * 2f;
            var dy = (v - 0.5f) * 2f;
            var d = Mathf.Sqrt(dx * dx + dy * dy);
            return 1f - OldSmoothStep01(core, 1f, d);
        }

        private static int MaxAlphaDelta(Texture2D a, Texture2D b)
        {
            var pa = a.GetPixels32();
            var pb = b.GetPixels32();
            Assert.That(pa.Length, Is.EqualTo(pb.Length));
            var max = 0;
            for (var i = 0; i < pa.Length; i++)
            {
                var d = Mathf.Abs(pa[i].a - pb[i].a);
                if (d > max)
                    max = d;
            }
            return max;
        }

        private static void AssertLegacySpotUnchanged(float falloff, float angle)
        {
            const int res = 128;   // Light2DLightTextureGenerator.DefaultResolution
            var actual = Light2DLightTextureGenerator
                .GetTexture(LightSprite.LightType.Spot, falloff, angle).GetPixels32();

            // Match the generator's quantization: falloff -> round(clamp01*100)/100, angle -> round(clamp 1..360).
            var qFalloff = Mathf.RoundToInt(Mathf.Clamp01(falloff) * 100f) / 100f;
            var qAngle = Mathf.RoundToInt(Mathf.Clamp(angle, 1f, 360f));
            var core = Mathf.Clamp01(qFalloff) * 0.85f;
            var inv = 1f / res;

            for (var y = 0; y < res; y++)
            {
                var v = (y + 0.5f) * inv;
                for (var x = 0; x < res; x++)
                {
                    var u = (x + 0.5f) * inv;
                    var expected = (byte)(Mathf.Clamp01(OldSpotAlpha(u, v, core, qFalloff, qAngle)) * 255f);
                    Assert.That(actual[y * res + x].a, Is.EqualTo(expected),
                        "Legacy Spot pixel changed at (" + x + "," + y + ") for falloff " + falloff +
                        ", angle " + angle + ".");
                }
            }
        }

        // The pre-change SpotAlpha, copied verbatim, as the regression oracle for angles <= 180 at Auto softness.
        private static float OldSpotAlpha(float u, float v, float core, float falloff, float spotAngleDeg)
        {
            var dx = u - 0.5f;
            var dy = v;
            var dist = Mathf.Sqrt(dx * dx + dy * dy);
            var radial = 1f - OldSmoothStep01(core, 1f, dist);

            var halfAngle = Mathf.Deg2Rad * spotAngleDeg * 0.5f;
            var angle = Mathf.Atan2(Mathf.Abs(dx), Mathf.Max(dy, 1e-5f));
            var softBand = Mathf.Lerp(0.5f, 0.05f, Mathf.Clamp01(falloff));
            var angular = 1f - OldSmoothStep01(halfAngle * (1f - softBand), halfAngle, angle);
            return radial * angular;
        }

        private static float OldSmoothStep01(float edge0, float edge1, float x)
        {
            var t = Mathf.Clamp01((x - edge0) / Mathf.Max(edge1 - edge0, 1e-5f));
            return t * t * (3f - 2f * t);
        }
    }

    public class LightSpriteBehaviorTests
    {
        [Test]
        public void VertexColorFoldsIntensityIntoRgbUnclamped()
        {
            var light = CreateLight();
            try
            {
                light.Color = new Color(0.75f, 0.5f, 0.25f, 1f);
                light.Intensity = 2f;
                Rebuild(light);

                var mesh = light.Mesh;
                var colors = mesh.colors;
                Assert.That(colors.Length, Is.GreaterThan(0));

                var c = colors[0];
                // rgb = Color.rgb * Intensity; the Float32 stream keeps r = 1.5 overbright (would clamp to
                // 1 on the Color32 path). Alpha is the authored texture alpha carrier, left untouched.
                Assert.That(c.r, Is.EqualTo(1.5f).Within(0.01f));
                Assert.That(c.g, Is.EqualTo(1.0f).Within(0.01f));
                Assert.That(c.b, Is.EqualTo(0.5f).Within(0.01f));
                Assert.That(c.a, Is.EqualTo(1.0f).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(light.gameObject);
            }
        }

        [Test]
        public void SwitchingToSpotDoesNotMutateLightOrigin()
        {
            var light = CreateLight();
            try
            {
                // The redesign makes LightOrigin a pure user-owned correction: a Type switch must never
                // rewrite it (the old model forced LightOrigin.y to -0.5 for Spot).
                light.LightOrigin = new Vector2(0.1f, 0.2f);
                light.Type = LightSprite.LightType.Spot;
                Rebuild(light);
                Assert.That(light.LightOrigin, Is.EqualTo(new Vector2(0.1f, 0.2f)));

                light.Type = LightSprite.LightType.Line;
                Rebuild(light);
                Assert.That(light.LightOrigin, Is.EqualTo(new Vector2(0.1f, 0.2f)));
            }
            finally
            {
                Object.DestroyImmediate(light.gameObject);
            }
        }

        [Test]
        public void SpotApexOriginatesAtTheTransform()
        {
            var light = CreateLight();
            try
            {
                // With the default (0,0) correction the cone apex (the mesh's bottom edge) sits at the
                // transform origin, so the local mesh spans y in [0, Radius] and the gizmo/render coincide.
                light.Type = LightSprite.LightType.Spot;
                light.Radius = 4f;
                light.LightOrigin = Vector2.zero;
                Rebuild(light);

                Bounds bounds = light.Mesh.bounds;
                Assert.That(bounds.min.y, Is.EqualTo(0f).Within(light.Radius * 0.05f),
                    "The Spot cone apex row should sit at the transform origin.");
                Assert.That(bounds.max.y, Is.EqualTo(light.Radius).Within(light.Radius * 0.05f),
                    "The Spot cone should reach one Radius past its apex.");
            }
            finally
            {
                Object.DestroyImmediate(light.gameObject);
            }
        }

        [Test]
        public void LineMeshIsSizedByLengthAndThrow()
        {
            var light = CreateLight();
            try
            {
                light.Type = LightSprite.LightType.Line;
                light.Length = 6f;
                light.Throw = 2f;
                light.Sides = LightSprite.LightSides.Both;
                Rebuild(light);

                Bounds both = light.Mesh.bounds;
                Assert.That(both.size.x, Is.EqualTo(6f).Within(0.05f), "Length sizes the bar along X.");
                Assert.That(both.size.y, Is.EqualTo(4f).Within(0.05f),
                    "A two-sided Line reaches Throw above and below the segment.");

                // A one-sided Line keeps the segment at the origin and only extends to +Throw (Front).
                light.Sides = LightSprite.LightSides.Front;
                Rebuild(light);
                Bounds front = light.Mesh.bounds;
                Assert.That(front.size.y, Is.EqualTo(2f).Within(0.05f), "Front reaches only Throw on one side.");
                Assert.That(front.min.y, Is.EqualTo(0f).Within(0.05f), "Front keeps the emitting segment at the origin.");
                Assert.That(front.max.y, Is.EqualTo(2f).Within(0.05f));
            }
            finally
            {
                Object.DestroyImmediate(light.gameObject);
            }
        }

        [Test]
        public void RadiusScalesRendererBoundsWithoutTransformScale()
        {
            var light = CreateLight();
            try
            {
                light.Radius = 2f;
                Rebuild(light);
                var size2 = light.WorldBounds.size.x;

                light.Radius = 4f;
                Rebuild(light);
                var size4 = light.WorldBounds.size.x;

                Assert.That(size2, Is.GreaterThan(0f));
                Assert.That(size4, Is.EqualTo(size2 * 2f).Within(size2 * 0.05f));
                Assert.That(light.transform.localScale, Is.EqualTo(Vector3.one),
                    "Radius must size the mesh, not the Transform.");
            }
            finally
            {
                Object.DestroyImmediate(light.gameObject);
            }
        }

        [Test]
        public void SpotSoftnessDefaultsToAuto()
        {
            var light = CreateLight();
            try
            {
                // The Auto sentinel (< 0) makes the angular edge softness derive from Falloff exactly as
                // before, so existing content deserializing this new field is byte-identical.
                Assert.That(light.SpotSoftness, Is.LessThan(0f),
                    "SpotSoftness must default to the Auto sentinel for byte-identical existing content.");
            }
            finally
            {
                Object.DestroyImmediate(light.gameObject);
            }
        }

        [Test]
        public void SpotOriginMigratesToPointConfigurationAt360()
        {
            var spot = CreateLight();
            var point = CreateLight();
            try
            {
                spot.Type = LightSprite.LightType.Spot;
                spot.Radius = 4f;
                spot.LightOrigin = Vector2.zero;
                spot.SpotAngle = 360f;
                Rebuild(spot);

                point.Type = LightSprite.LightType.Point;
                point.Radius = 4f;
                point.LightOrigin = Vector2.zero;
                Rebuild(point);

                var s = spot.Mesh.bounds;
                var p = point.Mesh.bounds;
                // At 360 the Spot quad is centred exactly like Point (the apex lift fades to zero): identical
                // local mesh bounds, spanning [-Radius/2, +Radius/2] about the origin.
                Assert.That(s.min.y, Is.EqualTo(p.min.y).Within(1e-4f), "360 Spot centres its quad like Point.");
                Assert.That(s.max.y, Is.EqualTo(p.max.y).Within(1e-4f));
                Assert.That(s.center.y, Is.EqualTo(p.center.y).Within(1e-4f));
                Assert.That(s.min.y, Is.EqualTo(-2f).Within(0.05f));
                Assert.That(s.max.y, Is.EqualTo(2f).Within(0.05f));
            }
            finally
            {
                Object.DestroyImmediate(spot.gameObject);
                Object.DestroyImmediate(point.gameObject);
            }
        }

        [Test]
        public void SpotBelow180KeepsApexAtTheTransform()
        {
            var light = CreateLight();
            try
            {
                // A non-default cone angle in the legacy range keeps the apex row at the transform origin.
                light.Type = LightSprite.LightType.Spot;
                light.Radius = 4f;
                light.LightOrigin = Vector2.zero;
                light.SpotAngle = 90f;
                Rebuild(light);

                var b = light.Mesh.bounds;
                Assert.That(b.min.y, Is.EqualTo(0f).Within(light.Radius * 0.05f),
                    "SpotAngle <= 180 keeps the apex at the transform origin.");
                Assert.That(b.max.y, Is.EqualTo(light.Radius).Within(light.Radius * 0.05f),
                    "The cone reaches one Radius past its apex.");
            }
            finally
            {
                Object.DestroyImmediate(light.gameObject);
            }
        }

        private static LightSprite CreateLight()
        {
            var go = new GameObject("Behavior Light");
            return go.AddComponent<LightSprite>();
        }

        // Forces a synchronous mesh rebuild with the current field values (no editor tick fires in a
        // batch EditMode run). Mirrors the enable-toggle used by the PlayMode alignment fixture.
        private static void Rebuild(LightSprite light)
        {
            light.enabled = false;
            light.enabled = true;
        }
    }
}
