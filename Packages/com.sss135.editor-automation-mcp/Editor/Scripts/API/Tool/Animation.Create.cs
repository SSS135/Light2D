#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public static partial class Tool_Animation
    {
        public const string AnimationCreateToolId = "animation-create";
        [McpTool
        (
            AnimationCreateToolId,
            Title = "Animation / Create",
            ReadOnlyHint = false,
            DestructiveHint = false,
            IdempotentHint = false,
            OpenWorldHint = false
        )]
        [Description(@"Create Unity's Animation asset files (AnimationClip). Creates folders recursively if they do not exist. Each path should start with 'Assets/' and end with '.anim'.")]
        public static CreateAnimationResponse CreateAnimationClips
        (
            [Description("The paths of the animation assets to create. Each path should start with 'Assets/' and end with '.anim'.")]
            string[] sourcePaths
        )
        {
            if (sourcePaths == null)
                throw new ArgumentNullException(nameof(sourcePaths));

            if (sourcePaths.Length == 0)
                throw new ArgumentException("Array is empty.", nameof(sourcePaths));

            return MainThread.Instance.Run(() =>
            {
                var response = new CreateAnimationResponse();

                foreach (var assetPath in sourcePaths)
                {
                    if (string.IsNullOrEmpty(assetPath))
                    {
                        response.errors ??= new();
                        response.errors.Add("Asset path is empty or null.");
                        continue;
                    }

                    if (!assetPath.StartsWith("Assets/"))
                    {
                        response.errors ??= new();
                        response.errors.Add($"Asset path '{assetPath}' must start with 'Assets/'.");
                        continue;
                    }

                    if (!assetPath.EndsWith(".anim"))
                    {
                        response.errors ??= new();
                        response.errors.Add($"Asset path '{assetPath}' must end with '.anim'.");
                        continue;
                    }

                    // Create all folders in the path if they do not exist
                    var directory = Path.GetDirectoryName(assetPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    }

                    // Create a new AnimationClip
                    var animationClip = new AnimationClip
                    {
                        name = Path.GetFileNameWithoutExtension(assetPath)
                    };

                    AssetDatabase.CreateAsset(animationClip, assetPath);

                    response.createdAssets ??= new();
                    response.createdAssets.Add(new CreatedAnimationInfo
                    {
                        path = assetPath,
                        instanceId = animationClip.GetEntityId(),
                        name = animationClip.name
                    });
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                EditorUtils.RepaintAllEditorWindows();

                return response;
            });
        }

        public class CreatedAnimationInfo
        {
            public string path = string.Empty;
            public UnityEngine.EntityId instanceId = UnityEngine.EntityId.None;
            public string name = string.Empty;
        }

        public class CreateAnimationResponse
        {
            public List<CreatedAnimationInfo>? createdAssets;
            public List<string>? errors;
        }
    }
}
