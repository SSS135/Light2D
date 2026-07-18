#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using EA;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Editor.Utils;
using com.SSS135.EditorAutomation.Runtime.Extensions;
using UnityEditor;
using UnityEngine;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public static partial class Tool_Animation
    {
        public const string AnimationModifyToolId = "animation-modify";
        [McpTool
        (
            AnimationModifyToolId,
            Title = "Animation / Modify",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false,
            OpenWorldHint = false
        )]
        [Description("Modify Unity's AnimationClip asset. " +
            "Apply an array of modifications including setting curves, clearing curves, setting properties, and managing animation events. " +
            "Use '" + AnimationGetDataToolId + "' tool to get valid property names and existing curves for modifications.")]
        public static ModifyAnimationResponse ModifyAnimationClip
        (
            [Description("Reference to the AnimationClip asset to modify.")]
            AssetObjectRef animRef,

            [Description("Array of modifications to apply to the clip.")]
            AnimationModification[] modifications
        )
        {
            if (animRef == null)
                throw new ArgumentNullException(nameof(animRef));

            if (!animRef.IsValid(out var animRefValidationError))
                throw new ArgumentException(animRefValidationError, nameof(animRef));

            if (modifications == null)
                throw new ArgumentNullException(nameof(modifications));

            if (modifications.Length == 0)
                throw new ArgumentException("Array is empty.", nameof(modifications));

            return MainThread.Instance.Run(() =>
            {
                var animation = animRef.FindAssetObject<AnimationClip>();
                if (animation == null)
                    throw new Exception("AnimationClip not found.");

                if (modifications == null || modifications.Length == 0)
                    throw new Exception("Modifications array is empty or null.");

                var response = new ModifyAnimationResponse();
                var eventsList = new List<AnimationEvent>(AnimationUtility.GetAnimationEvents(animation));

                for (int i = 0; i < modifications.Length; i++)
                {
                    var mod = modifications[i];
                    try
                    {
                        ApplyModification(animation, mod, eventsList);
                    }
                    catch (Exception ex)
                    {
                        response.errors ??= new List<string>();
                        response.errors.Add($"[{i}] {mod.type}: {ex.Message}");
                    }
                }

                // Apply collected events
                AnimationUtility.SetAnimationEvents(animation, eventsList.ToArray());

                EditorUtility.SetDirty(animation);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                EditorUtils.RepaintAllEditorWindows();

                var assetPath = AssetDatabase.GetAssetPath(animation);
                response.modifiedAsset = new ModifyAnimationInfo
                {
                    path = assetPath,
                    instanceId = animation.GetEntityId(),
                    name = animation.name
                };

                return response;
            });
        }

        private static void ApplyModification(AnimationClip clip, AnimationModification mod, List<AnimationEvent> eventsList)
        {
            switch (mod.type)
            {
                case ModificationType.SetCurve:
                    ApplySetCurve(clip, mod);
                    break;

                case ModificationType.RemoveCurve:
                    ApplyRemoveCurve(clip, mod);
                    break;

                case ModificationType.ClearCurves:
                    clip.ClearCurves();
                    break;

                case ModificationType.SetFrameRate:
                    if (!mod.frameRate.HasValue)
                        throw new Exception("frameRate is required for SetFrameRate.");
                    clip.frameRate = mod.frameRate.Value;
                    break;

                case ModificationType.SetWrapMode:
                    if (!mod.wrapMode.HasValue)
                        throw new Exception("wrapMode is required for SetWrapMode.");
                    clip.wrapMode = mod.wrapMode.Value;
                    break;

                case ModificationType.SetLegacy:
                    if (!mod.legacy.HasValue)
                        throw new Exception("legacy is required for SetLegacy.");
                    clip.legacy = mod.legacy.Value;
                    break;

                case ModificationType.AddEvent:
                    ApplyAddEvent(eventsList, mod);
                    break;

                case ModificationType.ClearEvents:
                    eventsList.Clear();
                    break;

                default:
                    throw new Exception($"Unknown modification type: {mod.type}");
            }
        }

        private static void ApplySetCurve(AnimationClip clip, AnimationModification mod)
        {
            if (string.IsNullOrEmpty(mod.componentType))
                throw new Exception("componentType is required for setCurve.");
            if (string.IsNullOrEmpty(mod.propertyName))
                throw new Exception("propertyName is required for setCurve.");
            if (mod.keyframes == null || mod.keyframes.Length == 0)
                throw new Exception("keyframes array is required for setCurve.");

            var type = TypeUtils.GetType(mod.componentType);
            if (type == null)
                throw new Exception($"Could not resolve component type: {mod.componentType}");

            var curve = new AnimationCurve();
            foreach (var kf in mod.keyframes)
            {
                var keyframe = new Keyframe(kf.time, kf.value)
                {
                    inTangent = kf.inTangent ?? 0f,
                    outTangent = kf.outTangent ?? 0f,
                    inWeight = kf.inWeight ?? 0.33f,
                    outWeight = kf.outWeight ?? 0.33f,
                    weightedMode = kf.weightedMode ?? WeightedMode.None
                };
                curve.AddKey(keyframe);
            }

            clip.SetCurve(mod.relativePath ?? string.Empty, type, mod.propertyName, curve);
        }

        private static void ApplyRemoveCurve(AnimationClip clip, AnimationModification mod)
        {
            if (string.IsNullOrEmpty(mod.componentType))
                throw new Exception("componentType is required for removeCurve.");
            if (string.IsNullOrEmpty(mod.propertyName))
                throw new Exception("propertyName is required for removeCurve.");

            var type = TypeUtils.GetType(mod.componentType);
            if (type == null)
                throw new Exception($"Could not resolve component type: {mod.componentType}");

            var relativePath = mod.relativePath ?? string.Empty;

            // Get all curve bindings and curves before clearing
            var bindings = AnimationUtility.GetCurveBindings(clip);
            var curveData = new Dictionary<EditorCurveBinding, AnimationCurve>();
            foreach (var binding in bindings)
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve != null)
                    curveData[binding] = curve;
            }

            var objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            var objectData = new Dictionary<EditorCurveBinding, ObjectReferenceKeyframe[]>();
            foreach (var binding in objectBindings)
            {
                var keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                if (keyframes != null && keyframes.Length > 0)
                    objectData[binding] = keyframes;
            }

            clip.ClearCurves();

            // Re-add all curves except the one to remove
            foreach (var binding in bindings)
            {
                // Skip the binding we want to remove
                if (binding.path == relativePath && binding.type == type && binding.propertyName == mod.propertyName)
                    continue;

                if (curveData.TryGetValue(binding, out var curve))
                    AnimationUtility.SetEditorCurve(clip, binding, curve);
            }

            foreach (var binding in objectBindings)
            {
                // Skip the binding we want to remove
                if (binding.path == relativePath && binding.type == type && binding.propertyName == mod.propertyName)
                    continue;

                if (objectData.TryGetValue(binding, out var keyframes))
                    AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
            }
        }

        private static void ApplyAddEvent(List<AnimationEvent> eventsList, AnimationModification mod)
        {
            if (!mod.time.HasValue)
                throw new Exception("time is required for addEvent.");
            if (string.IsNullOrEmpty(mod.functionName))
                throw new Exception("functionName is required for addEvent.");

            var animEvent = new AnimationEvent
            {
                time = mod.time.Value,
                functionName = mod.functionName,
                stringParameter = mod.stringParameter ?? string.Empty,
                floatParameter = mod.floatParameter ?? 0f,
                intParameter = mod.intParameter ?? 0
            };
            eventsList.Add(animEvent);
        }

        #region Response Data Classes

        public class ModifyAnimationInfo
        {
            public string path = string.Empty;
            public UnityEngine.EntityId instanceId = UnityEngine.EntityId.None;
            public string name = string.Empty;
        }

        public class ModifyAnimationResponse
        {
            public ModifyAnimationInfo? modifiedAsset;
            public List<string>? errors;
        }

        #endregion
    }
}
