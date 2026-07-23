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
