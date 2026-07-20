using System.Collections;
using System.Collections.Generic;
using System.IO;
using Light2D.Examples;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using Light2D.TestProject.Editor;
#endif

namespace Light2D.TestProject.Tests
{
    // Statistical visual regression for the Rocket scene against the README screenshot
    // (Documentation~/Images/overall.png): a mostly dark cave, a small fraction of bright
    // pixels, high contrast, at least two distinct hue clusters, and a bright glow near the
    // frame center.
    //
    // The craft spawns in an open region where the bright nebula skybox dominates the frame,
    // open "Background" cave areas glow at ambient steady state (~0.33 mean; convergence was
    // verified, so settling is not the lever), and rendered luminance is dominated by
    // DISTANCE TO THE NEAREST LIGHT (craft, crystals): rock only reaches README-like
    // near-black well away from every light. The scenario reproduces the MapGenerator block
    // field exactly through public sample APIs (MapGenerator.Seed, SimpleRNG, Noise.Generate,
    // BlockSetProfile), picks a landing pocket adjacent to large rock masses, and — at the
    // scene-default orthographicSize 15, matching the README's view scale — frames a
    // sky-free view chosen to minimize a distance-aware predicted mean luminance while
    // keeping a predicted LightingCrystals spawn in frame (chunk RNG replicated too) and a
    // bright peak in the central third (crystal core or the craft's own glow). After landing,
    // the framing is re-derived around the actual rest position with live crystal lights
    // under the same scoring. A radial calibration log at capture time reports mean luminance
    // by distance band from the craft, split rock/open, so the falloff model is directly
    // recalibratable from any run's output. Map layout and crystal colors are procedural per
    // run, so only distribution statistics are asserted. The PNG artifact is written before
    // any assertion runs so a failing run still leaves the image for review.
    public class RocketVisualSmokeTest
    {
        private const int TargetWidth = 640;
        private const int TargetHeight = 360;
        // Map generation around spawn plus the scripted intro (the craft turns dynamic after
        // one second of game time, frame 60 at the fixed 1/60 s capture delta).
        private const int MapWarmupFrames = 90;
        // Chunk generation reaches the whole view (~1 chunk/frame) well within this window
        // after each teleport.
        private const int ChunkSettleFrames = 90;
        private const int MaxLandingFrames = 240;
        private const int RestFramesRequired = 30;
        private const float RestSpeedThreshold = 0.5f;
        // Ambient light converges iteratively; teleports shift its history out of the view,
        // so the final settle is adaptive: ambient cycles are accelerated through the
        // sample's LoopAmbientLight API (8 per frame) and the capture waits until the
        // rendered mean luminance stabilizes or a hard frame cap.
        private const int AmbientSampleIntervalFrames = 30;
        private const int MaxAmbientSettleFrames = 900;
        private const float AmbientConvergenceDelta = 0.005f;
        private const int AmbientCyclesPerFrameRequest = 8;
        private const int ConvergedSampleStreak = 2;
        private const int CandidateAttempts = 4;
        private const int SearchExtent = 288;
        private const int SearchStep = 8;
        // The camera may shift this far from the craft; the craft can sit toward the frame
        // edge but stays fully visible (craft half extents ~1.5 against view half extents
        // ~26.7 x 15 at the scene-default orthographicSize 15).
        private const int FramingOffsetHalfWidth = 20;
        private const int FramingOffsetHalfHeight = 12;
        private const int FramingOffsetStepX = 4;
        private const int FramingOffsetStepY = 3;
        private const float CrystalViewFactor = 0.9f;
        // Predicted crystals gathered around a pocket as light sources for the luminance
        // model; the margins cover every reachable framing offset.
        private const float CrystalGatherMarginX = 20f;
        private const float CrystalGatherMarginY = 12f;
        // Live-light sanity rect around a teleport target generously covering every framed
        // view reachable from it.
        private const float LiveLightAcceptHalfWidth = 46f;
        private const float LiveLightAcceptHalfHeight = 28f;
        // Sampled-view sky tolerance for the relaxed selection passes.
        private const float RelaxedSkyExpanseFraction = 0.011f;
        private const float MinCandidateSeparation = 40f;
        private const float MinHueBucketFraction = 0.005f;
        // Bounded wait for rock visuals to actually overlap the framed view before the final
        // ambient settle and capture.
        private const int RockVisualsFrameBudget = 120;
        private const int MinVisibleChunkRenderers = 2;
        // Distance-aware luminance model, provisionally fitted through measured points
        // (~0.9 at 5u from a light, ~0.15 at 18u, ~0.05 at 30+u):
        // luminance ~= Floor + Amplitude * exp(-distance / Falloff), floored by material
        // terms (glowing open cave, ambient-lit boundary rock). The radial calibration log
        // emitted at capture time yields the true falloff for recalibrating these constants.
        private const float LightBleedFloor = 0.03f;
        private const float LightBleedAmplitude = 1.86f;
        private const float LightBleedFalloff = 6.56f;
        private const float MaxPredictedLuminance = 0.95f;
        private const float OpenCaveLuminance = 0.33f;
        private const float BoundaryRockLuminance = 0.149f;
        private const float DarkLuminanceThreshold = 0.1f;

        [UnityTest, Timeout(240000)]
        public IEnumerator RocketSceneRendersDarkCaveWithColoredLights()
        {
#if UNITY_EDITOR
            IEnumerator run = SampleTestSupport.RunSampleScene(
                SampleAutomation.RocketSceneName, TargetWidth, TargetHeight, MapWarmupFrames,
                AssertRocketVisualState, RelocateIntoCaveAndSettle);
            try
            {
                while (run.MoveNext())
                    yield return run.Current;
            }
            finally
            {
                (run as System.IDisposable)?.Dispose();
            }
#else
            Assert.Ignore("The Rocket visual smoke test loads scenes through EditorSceneManager and requires the Unity editor.");
            yield break;
#endif
        }

#if UNITY_EDITOR
        private readonly struct FramePrediction
        {
            internal readonly float MeanLuminance;
            internal readonly float WallFraction;
            internal readonly float DarkFraction;
            internal readonly float SkyExpanseFraction;

            internal FramePrediction(float meanLuminance, float wallFraction, float darkFraction,
                float skyExpanseFraction)
            {
                MeanLuminance = meanLuminance;
                WallFraction = wallFraction;
                DarkFraction = darkFraction;
                SkyExpanseFraction = skyExpanseFraction;
            }
        }

        private readonly struct CaveCandidate
        {
            internal readonly Vector2 Position;
            internal readonly Vector2Int RestEstimate;
            internal readonly float Score;
            internal readonly float PredictedMeanLuminance;
            internal readonly float WallFraction;
            internal readonly float DarkFraction;
            internal readonly Vector2Int FramingOffset;
            internal readonly int CrystalsInView;
            internal readonly int CentralCrystals;
            internal readonly bool CraftCentral;
            internal readonly int NearbyLights;

            internal CaveCandidate(Vector2 position, Vector2Int restEstimate, float score,
                float predictedMeanLuminance, float wallFraction, float darkFraction,
                Vector2Int framingOffset, int crystalsInView, int centralCrystals, bool craftCentral,
                int nearbyLights)
            {
                Position = position;
                RestEstimate = restEstimate;
                Score = score;
                PredictedMeanLuminance = predictedMeanLuminance;
                WallFraction = wallFraction;
                DarkFraction = darkFraction;
                FramingOffset = framingOffset;
                CrystalsInView = crystalsInView;
                CentralCrystals = centralCrystals;
                CraftCentral = craftCentral;
                NearbyLights = nearbyLights;
            }

            public override string ToString()
            {
                return $"pocket={Position}, rest={RestEstimate}, framingOffset={FramingOffset}, " +
                       $"predictedMeanLum={PredictedMeanLuminance:F3}, wall={WallFraction:F3}, " +
                       $"darkFraction={DarkFraction:F3}, crystalsInView={CrystalsInView}, " +
                       $"centralCrystals={CentralCrystals}, craftCentral={CraftCentral}, " +
                       $"nearbyLights={NearbyLights}";
            }
        }

        private static IEnumerator RelocateIntoCaveAndSettle(SampleTestSupport.SampleSceneRunContext context)
        {
            MapGenerator generator = Object.FindAnyObjectByType<MapGenerator>();
            Assert.That(generator, Is.Not.Null, "The Rocket scene has no active MapGenerator.");
            Spacecraft spacecraft = Object.FindAnyObjectByType<Spacecraft>();
            Assert.That(spacecraft, Is.Not.Null, "The Rocket scene has no active Spacecraft.");

            // Scene-default orthographic size (15) — the README's view scale.
            Camera camera = context.SourceCamera;
            float viewHalfHeight = camera.orthographicSize;
            float viewHalfWidth = viewHalfHeight * ((float)TargetWidth / TargetHeight);

            var map = new SampleTestSupport.MapProbe(generator);
            Vector2 spawn = spacecraft.MainRigidbody.position;
            List<CaveCandidate> candidates = FindCaveCandidates(map, spawn, viewHalfWidth, viewHalfHeight);
            Assert.That(candidates, Is.Not.Empty,
                $"No landing pocket with a predicted-dark framed view found within {SearchExtent} units " +
                $"of spawn {spawn} for map seed {generator.Seed}.");

            // Try candidates until live cave lights confirm the predicted crystals; remember
            // the best fallback and log every rejection so the traversal is traceable.
            CaveCandidate best = candidates[0];
            CaveCandidate current = candidates[0];
            var bestLiveCount = -1;
            for (var attempt = 0; attempt < candidates.Count; attempt++)
            {
                current = candidates[attempt];
                SampleTestSupport.TeleportSpacecraft(spacecraft, current.Position);
                SampleTestSupport.SnapCamera(camera.transform, current.Position);
                for (var frame = 0; frame < ChunkSettleFrames; frame++)
                    yield return null;

                int liveCount = CountCaveLightsNear(current.Position,
                    LiveLightAcceptHalfWidth, LiveLightAcceptHalfHeight);
                Debug.Log($"Rocket visual candidate {attempt}: {current}, live cave lights nearby={liveCount}.");
                if (liveCount > bestLiveCount)
                {
                    bestLiveCount = liveCount;
                    best = current;
                }

                if (liveCount >= 1)
                {
                    Debug.Log($"Rocket visual candidate {attempt} accepted.");
                    break;
                }

                Debug.Log($"Rocket visual candidate {attempt} rejected: no live cave light within " +
                          $"+-{LiveLightAcceptHalfWidth}x+-{LiveLightAcceptHalfHeight} of the pocket " +
                          $"(prediction expected {current.CrystalsInView}).");
            }

            if (bestLiveCount < 1)
                Debug.Log("Rocket visual: every candidate was rejected by the live-light check; " +
                          "falling back to the best-scored candidate anyway.");

            if (best.Position != current.Position)
            {
                // Chunks regenerate deterministically from the map seed, so returning to an
                // earlier candidate restores the same crystal layout.
                SampleTestSupport.TeleportSpacecraft(spacecraft, best.Position);
                SampleTestSupport.SnapCamera(camera.transform, best.Position);
                for (var frame = 0; frame < ChunkSettleFrames; frame++)
                    yield return null;
            }

            Debug.Log($"Rocket visual selected candidate: {best} " +
                      $"(map seed {generator.Seed}, {bestLiveCount} live cave lights nearby).");

            // Let the craft land on the cave floor guaranteed by the candidate filter.
            var restFrames = 0;
            for (var frame = 0; frame < MaxLandingFrames && restFrames < RestFramesRequired; frame++)
            {
                restFrames = spacecraft.MainRigidbody.linearVelocity.magnitude < RestSpeedThreshold
                    ? restFrames + 1
                    : 0;
                yield return null;
            }

            Debug.Log($"Rocket visual landing: craft position={spacecraft.MainRigidbody.position}, " +
                      $"resting frames={restFrames}.");

            Vector2 focus = ChooseFinalFraming(map, spacecraft.MainRigidbody.position,
                viewHalfWidth, viewHalfHeight);
            SampleTestSupport.SnapCamera(camera.transform, focus);

            // Bounded wait until chunk visuals actually overlap the framed view, so the
            // capture cannot happen against a rockless skybox.
            var visibleChunks = 0;
            for (var frame = 0; frame < RockVisualsFrameBudget; frame++)
            {
                visibleChunks = CountChunkRenderersInView(generator, focus, viewHalfWidth, viewHalfHeight);
                if (visibleChunks >= MinVisibleChunkRenderers)
                    break;
                yield return null;
            }

            Debug.Log($"Rocket visual rock visibility: {visibleChunks} chunk renderers overlap the framed view.");

            // Adaptive ambient convergence: sample the rendered mean luminance periodically
            // and capture only once it is stable (two consecutive small deltas) or at the
            // frame cap; the full curve is logged so a bright plateau proves steady-state
            // emission rather than insufficient settling.
            var previousMean = float.MaxValue;
            var stableSamples = 0;
            var settledFrames = 0;
            var curve = new System.Text.StringBuilder("Rocket visual ambient convergence (frame, meanLum):");
            while (settledFrames < MaxAmbientSettleFrames && stableSamples < ConvergedSampleStreak)
            {
                for (var frame = 0; frame < AmbientSampleIntervalFrames; frame++)
                {
                    context.Lighting.LoopAmbientLight(AmbientCyclesPerFrameRequest);
                    yield return null;
                }

                settledFrames += AmbientSampleIntervalFrames;
                float mean = MeasureTargetMeanLuminance(context.Target);
                curve.Append($" ({settledFrames}, {mean:F4})");
                stableSamples = Mathf.Abs(mean - previousMean) < AmbientConvergenceDelta ? stableSamples + 1 : 0;
                previousMean = mean;
            }

            Debug.Log(curve + " -> " +
                      (stableSamples >= ConvergedSampleStreak ? "converged" : "hit frame cap") +
                      $" after {settledFrames} frames.");
        }

        private static float MeasureTargetMeanLuminance(RenderTexture target)
        {
            Texture2D capture = SampleTestSupport.CaptureTexture(target);
            try
            {
                Color[] pixels = capture.GetPixels();
                var luminanceSum = 0f;
                foreach (Color pixel in pixels)
                    luminanceSum += pixel.grayscale;
                return pixels.Length > 0 ? luminanceSum / pixels.Length : 0f;
            }
            finally
            {
                Object.Destroy(capture);
            }
        }

        private static Vector2 ChooseFinalFraming(SampleTestSupport.MapProbe map, Vector2 craftPosition,
            float viewHalfWidth, float viewHalfHeight)
        {
            List<Vector2> liveLights = CollectCaveLightPositions();
            var lights = new List<Vector2>(liveLights.Count + 1) { craftPosition };
            lights.AddRange(liveLights);
            int craftX = Mathf.RoundToInt(craftPosition.x);
            int craftY = Mathf.RoundToInt(craftPosition.y);
            Vector2 focus = craftPosition;
            float bestScore = float.MinValue;
            var bestDescription = "craft-centered fallback";
            for (int offsetX = -FramingOffsetHalfWidth; offsetX <= FramingOffsetHalfWidth; offsetX += FramingOffsetStepX)
            {
                for (int offsetY = -FramingOffsetHalfHeight; offsetY <= FramingOffsetHalfHeight; offsetY += FramingOffsetStepY)
                {
                    int viewX = craftX + offsetX;
                    int viewY = craftY + offsetY;
                    FramePrediction prediction = PredictFrame(map, viewX, viewY,
                        viewHalfWidth, viewHalfHeight, lights);

                    var centralLight = false;
                    var anyLight = false;
                    foreach (Vector2 light in liveLights)
                    {
                        float absX = Mathf.Abs(light.x - viewX);
                        float absY = Mathf.Abs(light.y - viewY);
                        if (absX <= viewHalfWidth / 3f && absY <= viewHalfHeight / 3f)
                            centralLight = true;
                        if (absX <= viewHalfWidth * 0.9f && absY <= viewHalfHeight * 0.9f)
                            anyLight = true;
                    }

                    bool craftCentral = Mathf.Abs(offsetX) <= viewHalfWidth / 3f &&
                                        Mathf.Abs(offsetY) <= viewHalfHeight / 3f;
                    // The central-third gate needs a bright peak — a live crystal core or the
                    // craft's own glow; among gate-securing framings prefer predicted darkness.
                    float score = (centralLight || craftCentral ? 0f : -10f) - prediction.MeanLuminance +
                                  (anyLight ? 0.1f : 0f) - 10f * prediction.SkyExpanseFraction -
                                  0.0003f * (Mathf.Abs(offsetX) + Mathf.Abs(offsetY));
                    if (score > bestScore)
                    {
                        bestScore = score;
                        focus = new Vector2(viewX, viewY);
                        bestDescription = $"offset=({offsetX},{offsetY}), predictedMeanLum={prediction.MeanLuminance:F3}, " +
                                          $"wall={prediction.WallFraction:F3}, darkFraction={prediction.DarkFraction:F3}, " +
                                          $"skyExpanse={prediction.SkyExpanseFraction:F3}, centralLight={centralLight}, " +
                                          $"craftCentral={craftCentral}, anyLight={anyLight}";
                    }
                }
            }

            Debug.Log($"Rocket visual final framing: focus={focus}, {bestDescription}.");
            return focus;
        }

        private static void AssertRocketVisualState(SampleTestSupport.SampleSceneRunContext context)
        {
            Texture2D capture = SampleTestSupport.CaptureTexture(context.Target);
            try
            {
                string artifactPath = WriteArtifact(capture);
                Debug.Log($"Wrote the Rocket visual smoke artifact to '{artifactPath}'.");

                Color[] pixels = capture.GetPixels();
                VisualStatistics statistics =
                    SampleTestSupport.ComputeStatistics(pixels, capture.width, capture.height);
                Debug.Log("Rocket visual statistics: " + statistics);

                int brightRegions = SampleTestSupport.CountBrightRegions(
                    pixels, capture.width, capture.height, SampleTestSupport.BrightLuminanceThreshold);
                Debug.Log($"Rocket visual connected bright-region count (log-only): {brightRegions}");

                LogRadialCalibration(pixels, capture.width, capture.height, context.SourceCamera);

                // Thresholds are grounded in measurements of the README reference
                // (Documentation~/Images/overall.png) taken with the same statistics code:
                // meanLum=0.056, stdDevLum=0.071, range=0.996, brightFraction=0.00085,
                // centralThirdMaxLum=0.996, 6 hue buckets >= 0.5% of lit saturated pixels.
                // The rejected bright-bloom composition measured meanLum=0.310 and
                // brightFraction=0.177, which both bounds still exclude.
                Assert.That(statistics.MeanLuminance, Is.InRange(0.015f, 0.2f),
                    "The cave shot should stay dark like the README reference (measured 0.056); a mean " +
                    "above 0.2 indicates glowing open cave or nebula sky dominates the frame. " + statistics);
                Assert.That(statistics.BrightFraction, Is.GreaterThan(0.0001f).And.LessThan(0.12f),
                    "Bright pixels should exist (crystal cores, glow) but stay a limited fraction of the " +
                    "frame; the README reference measures 0.00085 and the framing may legitimately " +
                    "enlarge a nearby crystal core. " + statistics);
                Assert.That(statistics.LuminanceRange, Is.GreaterThan(0.4f),
                    "The frame should span from dark rock to bright light cores (README reference: 0.996). " +
                    statistics);
                Assert.That(statistics.LuminanceStdDev, Is.GreaterThan(0.03f),
                    "The frame should not be uniform (README reference: 0.071). " + statistics);
                Assert.That(statistics.CountHueBucketsAtOrAbove(MinHueBucketFraction), Is.GreaterThanOrEqualTo(2),
                    "At least two distinct hue clusters are expected among lit saturated pixels (README " +
                    "reference: 6 buckets). Histogram: " + statistics.FormatHueHistogram() + ". " + statistics);
                Assert.That(statistics.CentralThirdMaxLuminance, Is.GreaterThan(0.3f),
                    "A glow should keep a bright peak within the central third of the frame (README " +
                    "reference: 0.996). " + statistics);
            }
            finally
            {
                Object.Destroy(capture);
            }
        }

        // Permanent diagnostic: mean rendered luminance by distance band from the craft,
        // split by rock/open classification from the generator replica. This turns every run
        // into a calibration source for the distance-aware luminance model above.
        private static void LogRadialCalibration(Color[] pixels, int width, int height, Camera sourceCamera)
        {
            Spacecraft spacecraft = Object.FindAnyObjectByType<Spacecraft>();
            MapGenerator generator = Object.FindAnyObjectByType<MapGenerator>();
            if (spacecraft == null || generator == null)
            {
                Debug.Log("Rocket visual radial calibration: no spacecraft or generator found.");
                return;
            }

            var map = new SampleTestSupport.MapProbe(generator);
            Vector2 craft = spacecraft.MainRigidbody.position;
            Vector3 cameraPosition = sourceCamera.transform.position;
            float viewHalfHeight = sourceCamera.orthographicSize;
            float viewHalfWidth = viewHalfHeight * (width / (float)height);

            float[] bandEdges = { 5f, 10f, 15f, 20f, 25f, 30f };
            int bands = bandEdges.Length + 1;
            var rockSum = new float[bands];
            var rockCount = new int[bands];
            var openSum = new float[bands];
            var openCount = new int[bands];

            for (var py = 0; py < height; py += 2)
            {
                for (var px = 0; px < width; px += 2)
                {
                    float worldX = cameraPosition.x + ((px + 0.5f) / width - 0.5f) * 2f * viewHalfWidth;
                    float worldY = cameraPosition.y + ((py + 0.5f) / height - 0.5f) * 2f * viewHalfHeight;
                    float distance = Vector2.Distance(new Vector2(worldX, worldY), craft);
                    var band = 0;
                    while (band < bandEdges.Length && distance >= bandEdges[band])
                        band++;

                    bool rock = map.Classify(Mathf.RoundToInt(worldX), Mathf.RoundToInt(worldY)) ==
                                SampleTestSupport.MapProbe.CellKind.Wall;
                    float luminance = pixels[py * width + px].grayscale;
                    if (rock)
                    {
                        rockSum[band] += luminance;
                        rockCount[band]++;
                    }
                    else
                    {
                        openSum[band] += luminance;
                        openCount[band]++;
                    }
                }
            }

            var report = new System.Text.StringBuilder(
                "Rocket visual radial calibration (band: rock n/mean | open n/mean):");
            for (var band = 0; band < bands; band++)
            {
                string label = band == 0
                    ? $"0-{bandEdges[0]:F0}"
                    : band < bandEdges.Length
                        ? $"{bandEdges[band - 1]:F0}-{bandEdges[band]:F0}"
                        : $"{bandEdges[bandEdges.Length - 1]:F0}+";
                string rockPart = rockCount[band] > 0
                    ? $"{rockCount[band]}/{rockSum[band] / rockCount[band]:F4}"
                    : "0/-";
                string openPart = openCount[band] > 0
                    ? $"{openCount[band]}/{openSum[band] / openCount[band]:F4}"
                    : "0/-";
                report.Append($" [{label}: {rockPart} | {openPart}]");
            }

            Debug.Log(report.ToString());
        }

        private static string WriteArtifact(Texture2D capture)
        {
            string directory = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "TestResults", "Light2DSampleVisual"));
            Directory.CreateDirectory(directory);
            string artifactPath = Path.Combine(directory, "rocket-visual-smoke.png");
            File.WriteAllBytes(artifactPath, capture.EncodeToPNG());
            return artifactPath;
        }

        // Selection validated offline against the generator replica for the harness's
        // deterministic map seed -2630381: at the scene-default view scale the strict pass
        // finds pocket (-74, 9788) (framing offset (-4,-3), wall 0.803, darkFraction 0.171)
        // as the darkest gate-securing framing, with a provisional-model predicted mean of
        // 0.319. The provisional falloff constants demonstrably overestimate light bleed
        // (the README itself measures 0.056 mean WITH a glowing craft in frame), so the
        // absolute prediction is expected high; the RANKING is monotone in the
        // rock-far-from-light share and therefore robust, and the radial calibration log
        // recalibrates the constants from the run's own output. Candidates are ranked by
        // predicted mean; the passes only enforce structural validity so a selection always
        // exists.
        private static List<CaveCandidate> FindCaveCandidates(SampleTestSupport.MapProbe map, Vector2 spawn,
            float viewHalfWidth, float viewHalfHeight)
        {
            int spawnX = Mathf.RoundToInt(spawn.x);
            int spawnY = Mathf.RoundToInt(spawn.y);
            var scored = new List<CaveCandidate>();

            for (var pass = 0; pass < 3 && scored.Count == 0; pass++)
            {
                float maxSkyExpanse = pass < 2 ? 0f : RelaxedSkyExpanseFraction;
                float minFramedWall = pass == 0 ? 0.65f : pass == 1 ? 0.5f : 0.4f;

                for (int dy = -SearchExtent; dy <= SearchExtent; dy += SearchStep)
                {
                    for (int dx = -SearchExtent; dx <= SearchExtent; dx += SearchStep)
                    {
                        int x = spawnX + dx;
                        int y = spawnY + dy;
                        if (!map.IsOpenPocket(x, y, SampleTestSupport.LandingPocketRadius) ||
                            !map.HasFloorBelow(x, y,
                                SampleTestSupport.LandingMinFloorDepth, SampleTestSupport.LandingMaxFloorDepth))
                            continue;

                        int floorDepth = map.GetFloorDepthBelow(x, y,
                            SampleTestSupport.LandingMinFloorDepth, SampleTestSupport.LandingMaxFloorDepth);
                        if (floorDepth < 0)
                            continue;

                        // Estimated hull center once the craft has landed on the pocket floor.
                        var rest = new Vector2Int(x, y - floorDepth + 2);
                        if (QuickWallFraction(map, rest.x, rest.y, viewHalfWidth, viewHalfHeight) < 0.45f)
                            continue;

                        List<Vector2> lights = GatherPocketLights(map, rest, viewHalfWidth, viewHalfHeight);

                        var found = false;
                        float bestFramingScore = float.MinValue;
                        FramePrediction bestPrediction = default;
                        var bestOffset = Vector2Int.zero;
                        var bestCrystals = 0;
                        var bestCentral = 0;
                        var bestCraftCentral = false;
                        for (int offsetX = -FramingOffsetHalfWidth; offsetX <= FramingOffsetHalfWidth; offsetX += FramingOffsetStepX)
                        {
                            for (int offsetY = -FramingOffsetHalfHeight; offsetY <= FramingOffsetHalfHeight; offsetY += FramingOffsetStepY)
                            {
                                int viewX = rest.x + offsetX;
                                int viewY = rest.y + offsetY;
                                int crystals = CountLightsInView(lights, viewX, viewY,
                                    viewHalfWidth * CrystalViewFactor, viewHalfHeight * CrystalViewFactor,
                                    viewHalfWidth / 3f, viewHalfHeight / 3f, out int centralCrystals);
                                if (crystals == 0)
                                    continue;

                                bool craftCentral = Mathf.Abs(offsetX) <= viewHalfWidth / 3f &&
                                                    Mathf.Abs(offsetY) <= viewHalfHeight / 3f;
                                if (centralCrystals == 0 && !craftCentral)
                                    continue;

                                FramePrediction prediction = PredictFrame(map, viewX, viewY,
                                    viewHalfWidth, viewHalfHeight, lights);
                                if (prediction.SkyExpanseFraction > maxSkyExpanse)
                                    continue;

                                float framingScore = -prediction.MeanLuminance -
                                                     0.0003f * (Mathf.Abs(offsetX) + Mathf.Abs(offsetY));
                                if (framingScore > bestFramingScore)
                                {
                                    found = true;
                                    bestFramingScore = framingScore;
                                    bestPrediction = prediction;
                                    bestOffset = new Vector2Int(offsetX, offsetY);
                                    bestCrystals = crystals;
                                    bestCentral = centralCrystals;
                                    bestCraftCentral = craftCentral;
                                }
                            }
                        }

                        if (!found || bestPrediction.WallFraction < minFramedWall)
                            continue;

                        float spawnDistance = new Vector2(dx, dy).magnitude;
                        float score = -bestPrediction.MeanLuminance - spawnDistance * 0.00005f;
                        scored.Add(new CaveCandidate(new Vector2(x, y), rest, score,
                            bestPrediction.MeanLuminance, bestPrediction.WallFraction,
                            bestPrediction.DarkFraction, bestOffset, bestCrystals, bestCentral,
                            bestCraftCentral, lights.Count - 1));
                    }
                }
            }

            scored.Sort((left, right) => right.Score.CompareTo(left.Score));

            var candidates = new List<CaveCandidate>();
            foreach (CaveCandidate entry in scored)
            {
                var separated = true;
                foreach (CaveCandidate chosen in candidates)
                {
                    if (Vector2.Distance(chosen.Position, entry.Position) < MinCandidateSeparation)
                    {
                        separated = false;
                        break;
                    }
                }

                if (separated)
                    candidates.Add(entry);
                if (candidates.Count >= CandidateAttempts)
                    break;
            }

            return candidates;
        }

        // Distance-aware frame prediction: every sampled cell's luminance is the light-bleed
        // falloff toward the nearest light (craft plus crystals), floored by material terms —
        // glowing open cave and ambient-lit boundary rock. DarkFraction counts cells at or
        // below the dark threshold; only deep rock far from every light qualifies.
        private static FramePrediction PredictFrame(SampleTestSupport.MapProbe map, int viewX, int viewY,
            float viewHalfWidth, float viewHalfHeight, List<Vector2> lights)
        {
            var total = 0;
            var walls = 0;
            var dark = 0;
            var skyExpanse = 0;
            var luminanceSum = 0f;
            int halfWidth = (int)viewHalfWidth;
            int halfHeight = (int)viewHalfHeight;
            for (int dx = -halfWidth; dx <= halfWidth; dx += 2)
            {
                for (int dy = -halfHeight; dy <= halfHeight; dy += 2)
                {
                    total++;
                    int cellX = viewX + dx;
                    int cellY = viewY + dy;
                    var cell = new Vector2(cellX, cellY);
                    float nearest = float.MaxValue;
                    foreach (Vector2 light in lights)
                    {
                        float distance = Vector2.Distance(light, cell);
                        if (distance < nearest)
                            nearest = distance;
                    }

                    float luminance = Mathf.Min(MaxPredictedLuminance,
                        LightBleedFloor + LightBleedAmplitude * Mathf.Exp(-nearest / LightBleedFalloff));
                    SampleTestSupport.MapProbe.CellKind kind = map.Classify(cellX, cellY);
                    if (kind == SampleTestSupport.MapProbe.CellKind.Wall)
                    {
                        walls++;
                        if (!map.IsDeepWall(cellX, cellY))
                            luminance = Mathf.Max(luminance, BoundaryRockLuminance);
                    }
                    else
                    {
                        luminance = Mathf.Max(luminance, OpenCaveLuminance);
                        if (kind == SampleTestSupport.MapProbe.CellKind.Sky && map.IsSkyExpanse(cellX, cellY))
                            skyExpanse++;
                    }

                    if (luminance <= DarkLuminanceThreshold)
                        dark++;
                    luminanceSum += luminance;
                }
            }

            return new FramePrediction(luminanceSum / total, walls / (float)total,
                dark / (float)total, skyExpanse / (float)total);
        }

        private static float QuickWallFraction(SampleTestSupport.MapProbe map, int x, int y,
            float viewHalfWidth, float viewHalfHeight)
        {
            var total = 0;
            var walls = 0;
            for (int dx = -(int)viewHalfWidth; dx <= (int)viewHalfWidth; dx += 4)
            {
                for (int dy = -(int)viewHalfHeight; dy <= (int)viewHalfHeight; dy += 4)
                {
                    total++;
                    if (map.Classify(x + dx, y + dy) == SampleTestSupport.MapProbe.CellKind.Wall)
                        walls++;
                }
            }

            return walls / (float)total;
        }

        // The craft (index 0) plus every predicted crystal near enough to reach any framed
        // view around the pocket; these are the light sources of the luminance model.
        private static List<Vector2> GatherPocketLights(SampleTestSupport.MapProbe map, Vector2Int rest,
            float viewHalfWidth, float viewHalfHeight)
        {
            var lights = new List<Vector2> { new Vector2(rest.x, rest.y) };
            float gatherHalfWidth = viewHalfWidth + CrystalGatherMarginX;
            float gatherHalfHeight = viewHalfHeight + CrystalGatherMarginY;
            const int chunkSize = SampleTestSupport.MapProbe.ChunkSize;
            int minChunkX = Mathf.FloorToInt((rest.x - gatherHalfWidth) / chunkSize);
            int maxChunkX = Mathf.FloorToInt((rest.x + gatherHalfWidth) / chunkSize);
            int minChunkY = Mathf.FloorToInt((rest.y - gatherHalfHeight) / chunkSize);
            int maxChunkY = Mathf.FloorToInt((rest.y + gatherHalfHeight) / chunkSize);
            for (int chunkX = minChunkX; chunkX <= maxChunkX; chunkX++)
            {
                for (int chunkY = minChunkY; chunkY <= maxChunkY; chunkY++)
                {
                    foreach (Vector2 crystal in map.PredictChunkCrystals(chunkX, chunkY))
                    {
                        if (Mathf.Abs(crystal.x - rest.x) <= gatherHalfWidth &&
                            Mathf.Abs(crystal.y - rest.y) <= gatherHalfHeight)
                            lights.Add(crystal);
                    }
                }
            }

            return lights;
        }

        // Counts predicted crystal lights (skipping the craft at index 0) inside the view
        // rect and its central third.
        private static int CountLightsInView(List<Vector2> lights, int viewX, int viewY,
            float rectHalfWidth, float rectHalfHeight, float centralHalfWidth, float centralHalfHeight,
            out int centralLights)
        {
            centralLights = 0;
            var count = 0;
            for (var index = 1; index < lights.Count; index++)
            {
                float absX = Mathf.Abs(lights[index].x - viewX);
                float absY = Mathf.Abs(lights[index].y - viewY);
                if (absX > rectHalfWidth || absY > rectHalfHeight)
                    continue;

                count++;
                if (absX <= centralHalfWidth && absY <= centralHalfHeight)
                    centralLights++;
            }

            return count;
        }

        private static List<Vector2> CollectCaveLightPositions()
        {
            var positions = new List<Vector2>();
            foreach (LightSprite light in Object.FindObjectsByType<LightSprite>(FindObjectsSortMode.None))
            {
                if (light.isActiveAndEnabled && light.GetComponentInParent<Spacecraft>() == null)
                    positions.Add(light.transform.position);
            }

            return positions;
        }

        private static int CountCaveLightsNear(Vector2 center, float halfWidth, float halfHeight)
        {
            var count = 0;
            foreach (Vector2 light in CollectCaveLightPositions())
            {
                if (Mathf.Abs(light.x - center.x) <= halfWidth && Mathf.Abs(light.y - center.y) <= halfHeight)
                    count++;
            }

            return count;
        }

        private static int CountChunkRenderersInView(MapGenerator generator, Vector2 viewCenter,
            float viewHalfWidth, float viewHalfHeight)
        {
            Transform container = generator != null ? generator.Container : null;
            if (container == null)
                return 0;

            var count = 0;
            foreach (MeshRenderer meshRenderer in container.GetComponentsInChildren<MeshRenderer>())
            {
                // Chunk visual roots are named "Block Mesh { X = ...; Y = ... }" and live on
                // the default layer; obstacle and ambient children sit on the light layers.
                if (!meshRenderer.enabled || !meshRenderer.name.StartsWith("Block Mesh", System.StringComparison.Ordinal))
                    continue;

                Bounds bounds = meshRenderer.bounds;
                if (bounds.min.x <= viewCenter.x + viewHalfWidth && bounds.max.x >= viewCenter.x - viewHalfWidth &&
                    bounds.min.y <= viewCenter.y + viewHalfHeight && bounds.max.y >= viewCenter.y - viewHalfHeight)
                    count++;
            }

            return count;
        }
#endif
    }
}
