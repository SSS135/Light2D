using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using Light2D.Examples;
using Light2D.TestProject.Editor;
using UnityEditor.SceneManagement;
#endif

namespace Light2D.TestProject.Tests
{
    // Shared plumbing for the sample smoke and visual tests: scene selection, error capture,
    // deterministic frame advancement, and render-target statistics.
    internal static class SampleTestSupport
    {
        internal const int FixedRandomSeed = 851203466;
        internal const int HueBucketCount = 12;
        internal const float BrightLuminanceThreshold = 0.5f;
        internal const float LitValueThreshold = 0.15f;
        internal const float LitSaturationThreshold = 0.25f;

#if UNITY_EDITOR
        private static int _teardownSceneCounter;
#endif

        internal static IEnumerator WaitForBuiltInPipeline()
        {
            const int frameLimit = 30;
            var frames = 0;
            while (RenderPipelineManager.currentPipeline != null && frames < frameLimit)
            {
                frames++;
                yield return null;
            }

            Assert.That(RenderPipelineManager.currentPipeline, Is.Null,
                "The active scriptable render pipeline did not shut down within " + frameLimit + " player frames.");
            Assert.That(GraphicsSettings.currentRenderPipeline, Is.Null);
        }

#if UNITY_EDITOR
        internal sealed class SampleSceneRunContext
        {
            internal LightingSystem Lighting;
            internal Camera SourceCamera;
            internal RenderTexture Target;
            internal SampleSceneLogGuard Guard;
        }

        // Full sample-scene run: force the Built-in pipeline, load the imported sample scene,
        // advance frames deterministically, run the caller's optional scenario coroutine (for
        // staging work such as relocating the viewpoint), run the caller's assertions, then
        // always tear the scene down so later package tests do not observe leftover sample
        // content.
        internal static IEnumerator RunSampleScene(string sceneFileName, int targetWidth, int targetHeight,
            int frameCount, Action<SampleSceneRunContext> assertPhase,
            Func<SampleSceneRunContext, IEnumerator> scenarioPhase = null)
        {
            RenderPipelineAsset previousDefaultPipeline = GraphicsSettings.defaultRenderPipeline;
            RenderPipelineAsset previousQualityPipeline = QualitySettings.renderPipeline;
            float previousCaptureDeltaTime = Time.captureDeltaTime;
            float previousTimeScale = Time.timeScale;
            var context = new SampleSceneRunContext { Guard = new SampleSceneLogGuard() };
            Scene sampleScene = default;
            AsyncOperation unloadOperation = null;
            try
            {
                QualitySettings.renderPipeline = null;
                GraphicsSettings.defaultRenderPipeline = null;
                yield return WaitForBuiltInPipeline();

                Time.captureDeltaTime = 1f / 60f;
                UnityEngine.Random.InitState(FixedRandomSeed);

                string scenePath = GetImportedScenePath(sceneFileName);
                sampleScene = EditorSceneManager.LoadSceneInPlayMode(
                    scenePath, new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                yield return null;

                LightingSystem[] lightingSystems =
                    UnityEngine.Object.FindObjectsByType<LightingSystem>(FindObjectsInactive.Exclude);
                Assert.That(lightingSystems, Has.Length.EqualTo(1),
                    $"Expected exactly one active LightingSystem in '{sceneFileName}'.");
                context.Lighting = lightingSystems[0];
                context.SourceCamera = context.Lighting.GetComponent<Camera>();
                Assert.That(context.SourceCamera, Is.Not.Null,
                    $"The LightingSystem in '{sceneFileName}' has no Camera on its GameObject.");
                context.Target = CreateSourceTarget(targetWidth, targetHeight);
                context.SourceCamera.targetTexture = context.Target;

                for (var frame = 0; frame < frameCount; frame++)
                    yield return null;

                if (scenarioPhase != null)
                {
                    IEnumerator scenario = scenarioPhase(context);
                    try
                    {
                        while (scenario.MoveNext())
                            yield return scenario.Current;
                    }
                    finally
                    {
                        (scenario as IDisposable)?.Dispose();
                    }
                }

                assertPhase(context);
                context.Guard.AssertNoUnexpectedErrors();
            }
            finally
            {
                if (context.SourceCamera != null)
                    context.SourceCamera.targetTexture = null;
                if (context.Target != null)
                {
                    context.Target.Release();
                    UnityEngine.Object.Destroy(context.Target);
                }

                Time.captureDeltaTime = previousCaptureDeltaTime;
                Time.timeScale = previousTimeScale;
                GraphicsSettings.defaultRenderPipeline = previousDefaultPipeline;
                QualitySettings.renderPipeline = previousQualityPipeline;
                unloadOperation = BeginSampleSceneTeardown(sampleScene);
                context.Guard.Dispose();
            }

            while (unloadOperation != null && !unloadOperation.isDone)
                yield return null;
        }

        internal static string GetImportedScenePath(string sceneFileName)
        {
            string[] scenePaths = SampleAutomation.GetImportedScenePaths();
            foreach (string scenePath in scenePaths)
            {
                if (scenePath.EndsWith("/" + sceneFileName, StringComparison.Ordinal))
                    return scenePath;
            }

            throw new InvalidOperationException(
                $"No imported sample scene path ends with '{sceneFileName}'. " +
                $"Known scenes: {string.Join(", ", scenePaths)}");
        }

        // Activating a fresh empty scene before unloading the sample scene guarantees the
        // sample content is gone before any later test scans materials or instances.
        private static AsyncOperation BeginSampleSceneTeardown(Scene sampleScene)
        {
            Scene emptyScene = SceneManager.CreateScene("Light2D Sample Teardown " + ++_teardownSceneCounter);
            SceneManager.SetActiveScene(emptyScene);
            return sampleScene.IsValid() && sampleScene.isLoaded
                ? SceneManager.UnloadSceneAsync(sampleScene)
                : null;
        }

        // Shared Rocket-scene relocation machinery. A pocket must fit the craft (wall-free disk)
        // and have a floor close below so the craft lands quickly instead of free-falling.
        internal const int LandingPocketRadius = 4;
        internal const int LandingMinFloorDepth = 3;
        internal const int LandingMaxFloorDepth = 12;

        // Teleports the whole articulated craft (hull plus jointed landing legs) by a shared
        // delta so no joint is stretched, and zeroes all velocities.
        internal static void TeleportSpacecraft(Spacecraft spacecraft, Vector2 target)
        {
            Vector2 delta = target - spacecraft.MainRigidbody.position;
            Rigidbody2D[] bodies = spacecraft.GetComponentsInChildren<Rigidbody2D>();
            var targets = new Vector2[bodies.Length];
            for (var index = 0; index < bodies.Length; index++)
                targets[index] = bodies[index].position + delta;

            for (var index = 0; index < bodies.Length; index++)
            {
                Rigidbody2D body = bodies[index];
                body.position = targets[index];
                Vector3 transformPosition = body.transform.position;
                body.transform.position = new Vector3(targets[index].x, targets[index].y, transformPosition.z);
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }

        // CameraFollower's per-frame lerp is negligible (0.02 * deltaTime), so a direct snap
        // sticks; the follower only jumps on its own when the craft is over 50 units away.
        internal static void SnapCamera(Transform cameraTransform, Vector2 focus)
        {
            Vector3 position = cameraTransform.position;
            cameraTransform.position = new Vector3(focus.x, focus.y, position.z);
        }

        internal static bool TryFindNearestLandingPocket(MapProbe map, Vector2 origin, int searchExtent,
            out Vector2 pocket)
        {
            int originX = Mathf.RoundToInt(origin.x);
            int originY = Mathf.RoundToInt(origin.y);
            var found = false;
            var best = Vector2.zero;
            float bestSquareDistance = float.MaxValue;
            for (int dy = -searchExtent; dy <= searchExtent; dy += 8)
            {
                for (int dx = -searchExtent; dx <= searchExtent; dx += 8)
                {
                    float squareDistance = dx * dx + dy * dy;
                    if (squareDistance >= bestSquareDistance)
                        continue;

                    int x = originX + dx;
                    int y = originY + dy;
                    if (!map.IsOpenPocket(x, y, LandingPocketRadius) ||
                        !map.HasFloorBelow(x, y, LandingMinFloorDepth, LandingMaxFloorDepth))
                        continue;

                    found = true;
                    best = new Vector2(x, y);
                    bestSquareDistance = squareDistance;
                }
            }

            pocket = best;
            return found;
        }

        // Reproduces the MapGenerator block field for the live seed through public sample APIs
        // only. GetNoise mirrors MapGenerator.InitRandom/GetNoise, and GetBlockInfo mirrors
        // GetCollMapPoint/SelectBlockInfo including their exact SimpleRNG consumption, so wall
        // prediction matches the generated colliders exactly and PredictChunkCrystals matches
        // the chunk RNG that spawns LightingCrystals.
        internal sealed class MapProbe
        {
            internal enum CellKind
            {
                Sky,
                Cave,
                Wall
            }

            // Mirrors MapGenerator.ChunkSize.
            internal const int ChunkSize = 32;

            private readonly int _seed;
            private readonly float _firstAddX;
            private readonly float _firstAddY;
            private readonly float _secondAddX;
            private readonly float _secondAddY;
            private readonly BlockSetProfile _blockSet;
            private readonly Dictionary<Vector2Int, BlockSetProfile.BlockInfo> _cellCache =
                new Dictionary<Vector2Int, BlockSetProfile.BlockInfo>();
            private readonly Dictionary<Vector2Int, List<Vector2>> _chunkCrystalCache =
                new Dictionary<Vector2Int, List<Vector2>>();

            internal MapProbe(MapGenerator generator)
            {
                _seed = generator.Seed;
                var rand = new SimpleRNG(_seed);
                _firstAddX = (rand.value - 0.5f) * 1000f;
                _firstAddY = (rand.value - 0.5f) * 10000f;
                _secondAddX = (rand.value - 0.5f) * 1000f;
                _secondAddY = (rand.value - 0.5f) * 1000f;
                _blockSet = generator.BlockSet;
            }

            internal CellKind Classify(int x, int y)
            {
                BlockSetProfile.BlockInfo info = GetBlockInfo(x, y);
                if (info == null || info.BlockType == BlockSetProfile.BlockType.CollidingWall)
                    return CellKind.Wall;
                return info.BlockType == BlockSetProfile.BlockType.Empty ? CellKind.Sky : CellKind.Cave;
            }

            internal bool IsOpenPocket(int x, int y, int radius)
            {
                // The pocket core must be cave (not sky) and the craft needs a wall-free disk.
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (Classify(x + dx, y + dy) != CellKind.Cave)
                            return false;
                    }
                }

                int radiusSquared = radius * radius;
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        if (dx * dx + dy * dy <= radiusSquared && Classify(x + dx, y + dy) == CellKind.Wall)
                            return false;
                    }
                }

                return true;
            }

            internal bool HasFloorBelow(int x, int y, int minDepth, int maxDepth)
            {
                for (int column = x - 2; column <= x + 2; column += 2)
                {
                    var found = false;
                    for (int depth = minDepth; depth <= maxDepth; depth++)
                    {
                        if (Classify(column, y - depth) == CellKind.Wall)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                        return false;
                }

                return true;
            }

            // First colliding-wall depth below (x, y) in the given range, or -1 when none.
            internal int GetFloorDepthBelow(int x, int y, int minDepth, int maxDepth)
            {
                for (int depth = minDepth; depth <= maxDepth; depth++)
                {
                    if (Classify(x, y - depth) == CellKind.Wall)
                        return depth;
                }

                return -1;
            }

            // A sky cell is part of an EXPANSE when a 4-neighbor is also Sky; the cave band
            // randomly contains isolated single see-through cells (1-in-101 weight roll)
            // that read as tiny nebula glints in every view, so solitary dots must not
            // disqualify a framing.
            internal bool IsSkyExpanse(int x, int y)
            {
                return Classify(x, y) == CellKind.Sky &&
                       (Classify(x + 1, y) == CellKind.Sky ||
                        Classify(x - 1, y) == CellKind.Sky ||
                        Classify(x, y + 1) == CellKind.Sky ||
                        Classify(x, y - 1) == CellKind.Sky);
            }

            private static readonly Vector2Int[] DeepRockProbes =
            {
                new Vector2Int(5, 0), new Vector2Int(-5, 0), new Vector2Int(0, 5), new Vector2Int(0, -5),
                new Vector2Int(4, 4), new Vector2Int(4, -4), new Vector2Int(-4, 4), new Vector2Int(-4, -4)
            };

            // Wall cells whose surroundings within ~5 units are also wall; boundary rock
            // (non-deep) is additionally lit by ambient bleeding from adjacent open space.
            internal bool IsDeepWall(int x, int y)
            {
                foreach (Vector2Int probe in DeepRockProbes)
                {
                    if (Classify(x + probe.x, y + probe.y) != CellKind.Wall)
                        return false;
                }

                return true;
            }

            // Predicts where GenerateBlocksJoined spawns LightingCrystals in a chunk by
            // mirroring its SimpleRNG consumption: one rand.value per block that carries an
            // additional-object prefab, then one rand.Range per sprite candidate consumed by
            // RandomElement inside CreatePoint.
            internal IReadOnlyList<Vector2> PredictChunkCrystals(int chunkX, int chunkY)
            {
                var key = new Vector2Int(chunkX, chunkY);
                if (_chunkCrystalCache.TryGetValue(key, out List<Vector2> cached))
                    return cached;

                var crystals = new List<Vector2>();
                int xOffset = chunkX * ChunkSize;
                int yOffset = chunkY * ChunkSize;
                var rand = new SimpleRNG(HashCell(_seed, xOffset, yOffset));
                for (var x = 0; x < ChunkSize; x++)
                {
                    for (var y = 0; y < ChunkSize; y++)
                    {
                        BlockSetProfile.BlockInfo info = GetBlockInfo(x + xOffset, y + yOffset);
                        if (info == null)
                            continue;

                        if (info.AditionalObjectPrefab != null && info.AditionalObjectProbability >= rand.value)
                            crystals.Add(new Vector2(xOffset + x + 0.5f, yOffset + y + 0.5f));

                        // Only the RNG advance matters here, so the sprite weights collapse to
                        // one Range call per sprite entry.
                        for (var sprite = 0; sprite < info.SpriteInfo.Length; sprite++)
                            rand.Range(1);
                    }
                }

                _chunkCrystalCache[key] = crystals;
                return crystals;
            }

            // Exact copy of the samples' SampleUtilities.Hash. It is inlined here because that
            // helper is internal to the Light2D.Samples.Runtime assembly; MapGenerator seeds its
            // per-cell and per-chunk SimpleRNG with this same value, so the arithmetic must match
            // bit for bit to keep the wall and crystal predictions identical to the live map.
            private static int HashCell(int first, int second, int third)
            {
                unchecked
                {
                    var hash = 23;
                    hash = hash * 31 + first;
                    hash = hash * 31 + second;
                    return hash * 31 + third;
                }
            }

            private float GetNoise(int x, int y)
            {
                float noise1 = (Noise.Generate(
                    x * _blockSet.FirstNoiseScale + _firstAddX,
                    y * _blockSet.FirstNoiseScale + _firstAddY) - 0.5f) * 2f;
                float noise2 = (Noise.Generate(
                    x * _blockSet.SecondNoiseScale + _secondAddX,
                    y * _blockSet.SecondNoiseScale + _secondAddY) - 0.5f) * 2f;
                return Mathf.Clamp01((noise1 + noise2 * _blockSet.SecondNoiseMul) / 2f + 0.5f);
            }

            // Exact replica of MapGenerator.GetCollMapPoint's block selection for the live
            // seed, including SelectBlockInfo's per-eligible-candidate Range consumption.
            private BlockSetProfile.BlockInfo GetBlockInfo(int x, int y)
            {
                var key = new Vector2Int(x, y);
                if (_cellCache.TryGetValue(key, out BlockSetProfile.BlockInfo cached))
                    return cached;

                float noise = GetNoise(x, y);
                var rand = new SimpleRNG(HashCell(_seed, x, y));
                BlockSetProfile.BlockInfo selected = null;
                var totalWeight = 0;
                foreach (BlockSetProfile.BlockInfo candidate in _blockSet.BlockInfos)
                {
                    if (candidate.MinNoise > noise || candidate.MaxNoise < noise)
                        continue;

                    int weight = Mathf.Max(0, candidate.Weight);
                    totalWeight += weight;
                    if (weight > 0 && rand.Range(totalWeight) >= totalWeight - weight)
                        selected = candidate;
                }

                _cellCache[key] = selected;
                return selected;
            }
        }
#endif

        internal static RenderTexture CreateSourceTarget(int width, int height)
        {
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "Light2D Sample Test Source Target"
            };
            target.Create();
            return target;
        }

        internal static Texture2D CaptureTexture(RenderTexture source)
        {
            RenderTexture previousActive = RenderTexture.active;
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = source;
                texture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                texture.Apply();
                return texture;
            }
            finally
            {
                RenderTexture.active = previousActive;
            }
        }

        internal static VisualStatistics ComputeStatistics(Color[] pixels, int width, int height)
        {
            if (pixels == null || pixels.Length != width * height)
                throw new ArgumentException("The pixel buffer does not match the reported dimensions.", nameof(pixels));

            double luminanceSum = 0;
            double luminanceSquareSum = 0;
            var minLuminance = float.MaxValue;
            var maxLuminance = float.MinValue;
            var brightPixels = 0;
            var litSaturatedPixels = 0;
            var centralThirdMaxLuminance = 0f;
            var hueBucketCounts = new int[HueBucketCount];
            int centralXStart = width / 3, centralXEnd = 2 * width / 3;
            int centralYStart = height / 3, centralYEnd = 2 * height / 3;

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    Color pixel = pixels[y * width + x];
                    float luminance = pixel.grayscale;
                    luminanceSum += luminance;
                    luminanceSquareSum += (double)luminance * luminance;
                    if (luminance < minLuminance)
                        minLuminance = luminance;
                    if (luminance > maxLuminance)
                        maxLuminance = luminance;
                    if (luminance > BrightLuminanceThreshold)
                        brightPixels++;

                    Color.RGBToHSV(pixel, out float hue, out float saturation, out float value);
                    if (value > LitValueThreshold && saturation > LitSaturationThreshold)
                    {
                        litSaturatedPixels++;
                        hueBucketCounts[Mathf.Min(HueBucketCount - 1, (int)(hue * HueBucketCount))]++;
                    }

                    if (x >= centralXStart && x < centralXEnd && y >= centralYStart && y < centralYEnd &&
                        luminance > centralThirdMaxLuminance)
                        centralThirdMaxLuminance = luminance;
                }
            }

            int totalPixels = width * height;
            var meanLuminance = (float)(luminanceSum / totalPixels);
            var variance = (float)(luminanceSquareSum / totalPixels) - meanLuminance * meanLuminance;
            float luminanceStdDev = Mathf.Sqrt(Mathf.Max(0f, variance));
            var hueBucketFractions = new float[HueBucketCount];
            for (var bucket = 0; bucket < HueBucketCount; bucket++)
            {
                hueBucketFractions[bucket] = litSaturatedPixels == 0
                    ? 0f
                    : hueBucketCounts[bucket] / (float)litSaturatedPixels;
            }

            return new VisualStatistics(width, height, meanLuminance, luminanceStdDev, minLuminance, maxLuminance,
                brightPixels / (float)totalPixels, centralThirdMaxLuminance, litSaturatedPixels, hueBucketFractions);
        }

        // Log-only diagnostic: number of 4-connected regions of pixels brighter than the threshold.
        internal static int CountBrightRegions(Color[] pixels, int width, int height, float luminanceThreshold)
        {
            if (pixels == null || pixels.Length != width * height)
                throw new ArgumentException("The pixel buffer does not match the reported dimensions.", nameof(pixels));

            var visited = new bool[pixels.Length];
            var pending = new Queue<int>();
            var regions = 0;
            for (var index = 0; index < pixels.Length; index++)
            {
                if (visited[index] || pixels[index].grayscale <= luminanceThreshold)
                    continue;

                regions++;
                visited[index] = true;
                pending.Enqueue(index);
                while (pending.Count > 0)
                {
                    int current = pending.Dequeue();
                    int x = current % width;
                    int y = current / width;
                    VisitBrightNeighbor(pixels, visited, pending, x - 1, y, width, height, luminanceThreshold);
                    VisitBrightNeighbor(pixels, visited, pending, x + 1, y, width, height, luminanceThreshold);
                    VisitBrightNeighbor(pixels, visited, pending, x, y - 1, width, height, luminanceThreshold);
                    VisitBrightNeighbor(pixels, visited, pending, x, y + 1, width, height, luminanceThreshold);
                }
            }

            return regions;
        }

        private static void VisitBrightNeighbor(Color[] pixels, bool[] visited, Queue<int> pending,
            int x, int y, int width, int height, float luminanceThreshold)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
                return;

            int index = y * width + x;
            if (visited[index] || pixels[index].grayscale <= luminanceThreshold)
                return;

            visited[index] = true;
            pending.Enqueue(index);
        }
    }

    // Captures Error/Exception/Assert log messages for the duration of a sample run while
    // LogAssert.ignoreFailingMessages keeps the Unity Test Framework from failing early.
    // Only the known URP global-settings postprocessor noise of this host is tolerated.
    internal sealed class SampleSceneLogGuard : IDisposable
    {
        private readonly object _sync = new object();
        private readonly List<string> _unexpectedMessages = new List<string>();
        private readonly bool _previousIgnoreFailingMessages;
        private int _toleratedKnownUrpErrors;
        private bool _disposed;

        internal SampleSceneLogGuard()
        {
            _previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceivedThreaded += OnLogMessageReceived;
        }

        internal void AssertNoUnexpectedErrors()
        {
            string[] unexpectedMessages;
            int toleratedKnownUrpErrors;
            lock (_sync)
            {
                unexpectedMessages = _unexpectedMessages.ToArray();
                toleratedKnownUrpErrors = _toleratedKnownUrpErrors;
            }

            if (toleratedKnownUrpErrors > 0)
            {
                Debug.Log("Tolerated " + toleratedKnownUrpErrors +
                          " known URP global-settings postprocessor error(s) during the sample run.");
            }

            Assert.That(unexpectedMessages, Is.Empty,
                "The sample run logged unexpected errors:\n" + string.Join("\n", unexpectedMessages));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Application.logMessageReceivedThreaded -= OnLogMessageReceived;
            LogAssert.ignoreFailingMessages = _previousIgnoreFailingMessages;
        }

        private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
                return;

            var tolerated = false;
#if UNITY_EDITOR
            // The known-noise fingerprint (postprocessor/utils type names) usually appears in the
            // stack trace rather than the exception message, so both parts are checked.
            tolerated = WindowsBuildAutomation.IsUrpGlobalSettingsPostprocessorError(condition) ||
                        WindowsBuildAutomation.IsUrpGlobalSettingsPostprocessorError(stackTrace);
#endif
            lock (_sync)
            {
                if (tolerated)
                    _toleratedKnownUrpErrors++;
                else
                    _unexpectedMessages.Add("[" + type + "] " + condition);
            }
        }
    }

    // Luminance and hue statistics of a captured frame. Luminance uses Color.grayscale; the hue
    // histogram spans 12 buckets over pixels that are both lit and saturated per Color.RGBToHSV.
    internal readonly struct VisualStatistics
    {
        internal readonly int Width;
        internal readonly int Height;
        internal readonly float MeanLuminance;
        internal readonly float LuminanceStdDev;
        internal readonly float MinLuminance;
        internal readonly float MaxLuminance;
        internal readonly float BrightFraction;
        internal readonly float CentralThirdMaxLuminance;
        internal readonly int LitSaturatedPixelCount;
        private readonly float[] _hueBucketFractions;

        internal VisualStatistics(int width, int height, float meanLuminance, float luminanceStdDev,
            float minLuminance, float maxLuminance, float brightFraction, float centralThirdMaxLuminance,
            int litSaturatedPixelCount, float[] hueBucketFractions)
        {
            Width = width;
            Height = height;
            MeanLuminance = meanLuminance;
            LuminanceStdDev = luminanceStdDev;
            MinLuminance = minLuminance;
            MaxLuminance = maxLuminance;
            BrightFraction = brightFraction;
            CentralThirdMaxLuminance = centralThirdMaxLuminance;
            LitSaturatedPixelCount = litSaturatedPixelCount;
            _hueBucketFractions = hueBucketFractions;
        }

        internal float LuminanceRange => MaxLuminance - MinLuminance;

        internal int CountHueBucketsAtOrAbove(float fraction)
        {
            var buckets = 0;
            foreach (float bucketFraction in _hueBucketFractions)
            {
                if (bucketFraction >= fraction)
                    buckets++;
            }

            return buckets;
        }

        internal string FormatHueHistogram()
        {
            var parts = new string[_hueBucketFractions.Length];
            for (var bucket = 0; bucket < _hueBucketFractions.Length; bucket++)
                parts[bucket] = _hueBucketFractions[bucket].ToString("F4", CultureInfo.InvariantCulture);

            return "[" + string.Join(", ", parts) + "]";
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "size={0}x{1}, meanLum={2:F4}, stdDevLum={3:F4}, minLum={4:F4}, maxLum={5:F4}, range={6:F4}, " +
                "brightFraction={7:F5}, centralThirdMaxLum={8:F4}, litSaturatedPixels={9}, hueHistogram={10}",
                Width, Height, MeanLuminance, LuminanceStdDev, MinLuminance, MaxLuminance, LuminanceRange,
                BrightFraction, CentralThirdMaxLuminance, LitSaturatedPixelCount, FormatHueHistogram());
        }
    }
}
