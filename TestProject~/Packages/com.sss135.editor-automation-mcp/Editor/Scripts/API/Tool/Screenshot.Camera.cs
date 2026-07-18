#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using com.IvanMurzak.ReflectorNet.Utils;
using EA;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Runtime.Extensions;
using UnityEngine;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Screenshot
    {
        public const string ScreenshotCameraToolId = "screenshot-camera";
        [McpTool
        (
            ScreenshotCameraToolId,
            Title = "Screenshot / Camera",
            ReadOnlyHint = true,
            IdempotentHint = true
        )]
        [Description("Captures a screenshot from a camera and returns it as an image. " +
            "If no camera is specified, uses the Main Camera. " +
            "Returns the image directly for visual inspection by the LLM.")]
        public McpResponse ScreenshotCamera
        (
            [Description("Reference to the camera GameObject. If not specified, uses the Main Camera.")]
            GameObjectRef? cameraRef = null,
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
                var camera = cameraRef?.FindGameObject()?.GetComponent<Camera>()
                    ?? Camera.main
                    ?? Camera.allCameras.FirstOrDefault();

                if (camera == null)
                {
                    var availableCameras = Camera.allCameras.Select(c => c.name).ToArray();
                    var msg = availableCameras.Length > 0
                        ? $"Camera not found. Available cameras: {string.Join(", ", availableCameras)}"
                        : "No cameras found in the scene.";
                    return McpResponse.Error(msg);
                }

                var rt = new RenderTexture(width, height, 24);
                var prevTarget = camera.targetTexture;
                var prevActive = RenderTexture.active;

                byte[] pngBytes;
                try
                {
                    camera.targetTexture = rt;
                    camera.Render();

                    RenderTexture.active = rt;
                    var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                    try
                    {
                        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                        tex.Apply();
                        pngBytes = tex.EncodeToPNG();
                    }
                    finally
                    {
                        Object.DestroyImmediate(tex);
                    }
                }
                finally
                {
                    camera.targetTexture = prevTarget;
                    RenderTexture.active = prevActive;
                    Object.DestroyImmediate(rt);
                }

                return new McpResponse(
                    status: McpStatus.Success,
                    content: new List<McpContent>
                    {
                        McpContent.CreateImage(pngBytes, "image/png"),
                        McpContent.CreateText($"Screenshot from camera '{camera.name}' ({width}x{height})"),
                    });
            });
        }
    }
}
