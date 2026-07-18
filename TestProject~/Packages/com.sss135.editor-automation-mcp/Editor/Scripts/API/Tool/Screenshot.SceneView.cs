#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.Framework;
using UnityEditor;
using UnityEngine;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Screenshot
    {
        public const string ScreenshotSceneViewToolId = "screenshot-scene-view";
        [McpTool
        (
            ScreenshotSceneViewToolId,
            Title = "Screenshot / Scene View",
            ReadOnlyHint = true,
            IdempotentHint = true
        )]
        [Description("Captures a screenshot from the Unity Editor Scene View and returns it as an image. " +
            "Returns the image directly for visual inspection by the LLM.")]
        public McpResponse ScreenshotSceneView
        (
            [Description("Width of the screenshot in pixels.")]
            int width = 1920,
            [Description("Height of the screenshot in pixels.")]
            int height = 1080
        )
        {
            if (width <= 0 || height <= 0)
                return McpResponse.Error($"Width and height must be greater than 0. Got {width}x{height}.");
            if (width > MaxDimension || height > MaxDimension)
                return McpResponse.Error($"Width and height must not exceed {MaxDimension} pixels. Got {width}x{height}.");

            return MainThread.Instance.Run(() =>
            {
                var sceneView = SceneView.lastActiveSceneView;
                if (sceneView == null && SceneView.sceneViews.Count > 0)
                    sceneView = SceneView.sceneViews[0] as SceneView;

                if (sceneView == null)
                    return McpResponse.Error("No Scene View window is open.");

                var sceneCamera = sceneView.camera;
                if (sceneCamera == null)
                    return McpResponse.Error("Scene View camera is not available.");

                var rt = new RenderTexture(width, height, 24);
                var prevTarget = sceneCamera.targetTexture;
                var prevActive = RenderTexture.active;
                Texture2D? tex = null;
                byte[]? pngBytes = null;
                try
                {
                    sceneCamera.targetTexture = rt;
                    sceneCamera.Render();

                    RenderTexture.active = rt;
                    tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    tex.Apply();

                    pngBytes = tex.EncodeToPNG();
                }
                finally
                {
                    sceneCamera.targetTexture = prevTarget;
                    RenderTexture.active = prevActive;
                    Object.DestroyImmediate(rt);
                    if (tex != null)
                        Object.DestroyImmediate(tex);
                }

                return new McpResponse(
                    status: McpStatus.Success,
                    content: new List<McpContent>
                    {
                        McpContent.CreateImage(pngBytes!, "image/png"),
                        McpContent.CreateText($"Screenshot from Scene View ({width}x{height})"),
                    });
            });
        }
    }
}
