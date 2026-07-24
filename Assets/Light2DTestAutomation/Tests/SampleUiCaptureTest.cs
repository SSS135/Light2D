using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Light2D.Examples;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using Light2D.TestProject.Editor;
#endif

namespace Light2D.TestProject.Tests
{
    // After-capture for the uGUI -> UI Toolkit migration: loads each converted sample scene in Play mode
    // through the shared SampleTestSupport harness (which forces the Built-in pipeline and guards for
    // unexpected errors), lets the overlay bind, points the shared SampleUIPanelSettings.targetTexture at
    // an offscreen RenderTexture so the UI Toolkit panel renders into it, reads the pixels back, and
    // writes a PNG to TestResults/UiAfter/<scene>__<variant>.png for a human/orchestrator to compare
    // against TestResults/UiBaseline/.
    //
    // Every scene captures the 1280x720 landscape "default" view; the two Rocket scenes additionally
    // capture a 720x1280 portrait "touch_portrait" view with the touch cluster forced visible. These are
    // artifact-only smoke captures: there are NO luminance or content gates, so a batch host that renders
    // the panel weakly (or not at all) still passes as long as the capture and file write succeed. When
    // PanelSettings.targetTexture is set the panel leaves the on-screen overlay, so it is always restored
    // in a finally before the RenderTexture is released.
    public class SampleUiCaptureTest
    {
        // The harness renders the source camera into its own target; that image is irrelevant to the UI
        // capture (the panel renders into PanelSettings.targetTexture), so keep the camera target small.
        private const int CameraTargetWidth = 256;
        private const int CameraTargetHeight = 144;

        // Frames for the UIDocument to clone its tree, SampleUiController to bind, and any controller that
        // pushes an initial feature label (SimpleExamplesController.Start) to run before the capture.
        private const int WarmupFrames = 20;

        // Frames to let the panel repaint into a freshly assigned target texture.
        private const int RepaintFrames = 2;

        private const int LandscapeWidth = 1280;
        private const int LandscapeHeight = 720;
        private const int PortraitWidth = 720;
        private const int PortraitHeight = 1280;

        // Landscape desktop view: touch cluster hidden (matches the "default" baseline variant).
        private static readonly Variant DefaultVariant =
            new Variant("default", LandscapeWidth, LandscapeHeight, false);

        // Portrait mobile view with the touch cluster forced on (matches the "touch_portrait" baseline
        // variant). Only the Rocket scenes have a touch cluster.
        private static readonly Variant TouchPortraitVariant =
            new Variant("touch_portrait", PortraitWidth, PortraitHeight, true);

        [UnityTest, Timeout(240000)]
        public IEnumerator SmallExamplesUiCapture()
        {
            return RunCapture(SceneName_SmallExamples, new[] { DefaultVariant });
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator RocketUiCapture()
        {
            return RunCapture(SceneName_Rocket, new[] { DefaultVariant, TouchPortraitVariant });
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator RocketTilemapUiCapture()
        {
            return RunCapture(SceneName_RocketTilemap, new[] { DefaultVariant, TouchPortraitVariant });
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator LightVolumeUiCapture()
        {
            return RunCapture(SceneName_LightVolume, new[] { DefaultVariant });
        }

#if UNITY_EDITOR
        private const string SceneName_SmallExamples = SampleAutomation.SmallExamplesSceneName;
        private const string SceneName_Rocket = SampleAutomation.RocketSceneName;
        private const string SceneName_RocketTilemap = SampleAutomation.RocketTilemapSceneName;
        private const string SceneName_LightVolume = SampleAutomation.LightVolumeSceneName;
#else
        private const string SceneName_SmallExamples = "Small Examples.unity";
        private const string SceneName_Rocket = "Rocket.unity";
        private const string SceneName_RocketTilemap = "Rocket Tilemap.unity";
        private const string SceneName_LightVolume = "Light Volume.unity";
#endif

        private readonly struct Variant
        {
            internal readonly string Suffix;
            internal readonly int Width;
            internal readonly int Height;
            internal readonly bool TouchVisible;

            internal Variant(string suffix, int width, int height, bool touchVisible)
            {
                Suffix = suffix;
                Width = width;
                Height = height;
                TouchVisible = touchVisible;
            }
        }

        private static IEnumerator RunCapture(string sceneFileName, Variant[] variants)
        {
#if UNITY_EDITOR
            var written = new List<string>();
            IEnumerator run = SampleTestSupport.RunSampleScene(
                sceneFileName, CameraTargetWidth, CameraTargetHeight, WarmupFrames,
                context => AssertCapturesWritten(written, variants.Length),
                context => CaptureVariants(sceneFileName, variants, written));
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
            Assert.Ignore("The UI capture test loads scenes through EditorSceneManager and requires the Unity editor.");
            yield break;
#endif
        }

#if UNITY_EDITOR
        // Renders each requested variant into an offscreen RenderTexture via PanelSettings.targetTexture
        // and writes the PNG. Runs as the harness scenario phase (before the assert phase) so failures
        // still leave whatever artifacts were captured.
        private static IEnumerator CaptureVariants(string sceneFileName, Variant[] variants, List<string> written)
        {
            UIDocument document = Object.FindAnyObjectByType<UIDocument>(FindObjectsInactive.Include);
            Assert.That(document, Is.Not.Null,
                $"'{sceneFileName}' has no UIDocument after conversion; the Sample UI object is missing.");
            PanelSettings panel = document.panelSettings;
            Assert.That(panel, Is.Not.Null,
                $"The UIDocument in '{sceneFileName}' has no PanelSettings assigned.");

            SampleUiController controller = Object.FindAnyObjectByType<SampleUiController>(FindObjectsInactive.Include);

            string outputDirectory = GetOutputDirectory();
            Directory.CreateDirectory(outputDirectory);

            RenderTexture previousTarget = panel.targetTexture;
            bool previousClearColor = panel.clearColor;
            Camera sourceCamera = Camera.main;
            RenderTexture previousCameraTarget = sourceCamera != null ? sourceCamera.targetTexture : null;
            try
            {
                foreach (Variant variant in variants)
                {
                    // Force the touch cluster to match the variant regardless of platform gating.
                    if (controller != null)
                        controller.SetTouchControlsVisible(variant.TouchVisible);

                    var renderTexture = new RenderTexture(variant.Width, variant.Height, 24, RenderTextureFormat.ARGB32)
                    {
                        name = "Light2D UI After Capture"
                    };
                    renderTexture.Create();
                    try
                    {
                        // Composite capture: the game camera renders the lit world into the texture and
                        // the panel paints over it WITHOUT clearing, so the PNG shows the UI exactly as
                        // it reads over the real game background (the point of the readability check).
                        if (sourceCamera != null)
                            sourceCamera.targetTexture = renderTexture;
                        panel.clearColor = false;
                        panel.targetTexture = renderTexture;
                        for (var frame = 0; frame < RepaintFrames; frame++)
                            yield return null;

                        Texture2D readback = SampleTestSupport.CaptureTexture(renderTexture);
                        try
                        {
                            string fileName = Path.GetFileNameWithoutExtension(sceneFileName) + "__" + variant.Suffix + ".png";
                            string filePath = Path.Combine(outputDirectory, fileName);
                            File.WriteAllBytes(filePath, readback.EncodeToPNG());
                            written.Add(filePath);

                            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                                "[SampleUiCaptureTest] {0} {1}x{2} touch={3} -> {4}",
                                Path.GetFileNameWithoutExtension(sceneFileName), variant.Width, variant.Height,
                                variant.TouchVisible, filePath));
                        }
                        finally
                        {
                            Object.Destroy(readback);
                        }
                    }
                    finally
                    {
                        // Restore before releasing so the panel never references a destroyed texture.
                        panel.targetTexture = previousTarget;
                        panel.clearColor = previousClearColor;
                        if (sourceCamera != null)
                            sourceCamera.targetTexture = previousCameraTarget;
                        renderTexture.Release();
                        Object.Destroy(renderTexture);
                    }
                }
            }
            finally
            {
                panel.targetTexture = previousTarget;
                panel.clearColor = previousClearColor;
                if (sourceCamera != null)
                    sourceCamera.targetTexture = previousCameraTarget;
            }
        }

        private static void AssertCapturesWritten(List<string> written, int expectedCount)
        {
            Assert.That(written, Has.Count.EqualTo(expectedCount),
                $"Expected {expectedCount} UI capture PNG(s) but wrote {written.Count}.");
            foreach (string filePath in written)
                Assert.That(File.Exists(filePath), Is.True, $"Expected capture artifact was not written: {filePath}");
        }

        private static string GetOutputDirectory()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TestResults", "UiAfter"));
        }
#endif
    }
}
