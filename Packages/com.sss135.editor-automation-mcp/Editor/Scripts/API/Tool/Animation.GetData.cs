#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using EA;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Runtime.Extensions;
using UnityEditor;
using UnityEngine;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public static partial class Tool_Animation
    {
        public const string AnimationGetDataToolId = "animation-get-data";
        [McpTool
        (
            AnimationGetDataToolId,
            Title = "Animation / Get Data",
            ReadOnlyHint = true,
            DestructiveHint = false,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description(@"Get data about a Unity AnimationClip asset file. Returns information such as name, length, frame rate, wrap mode, animation curves, and events.")]
        public static GetDataResponse GetData
        (
            [Description("Reference to the animation asset. The path should start with 'Assets/' and end with '.anim'.")]
            AssetObjectRef animRef
        )
        {
            if (animRef == null)
                throw new ArgumentNullException(nameof(animRef));

            if (!animRef.IsValid(out var animRefValidationError))
                throw new ArgumentException(animRefValidationError, nameof(animRef));

            return MainThread.Instance.Run(() =>
            {
                var animation = animRef.FindAssetObject<AnimationClip>();
                if (animation == null)
                    throw new System.Exception($"AnimationClip not found.");

                var response = new GetDataResponse
                {
                    name = animation.name,
                    length = animation.length,
                    frameRate = animation.frameRate,
                    wrapMode = animation.wrapMode.ToString(),
                    isLooping = animation.isLooping,
                    hasGenericRootTransform = animation.hasGenericRootTransform,
                    hasMotionCurves = animation.hasMotionCurves,
                    hasMotionFloatCurves = animation.hasMotionFloatCurves,
                    hasRootCurves = animation.hasRootCurves,
                    humanMotion = animation.humanMotion,
                    legacy = animation.legacy,
                    localBounds = animation.localBounds.ToString(),
                    empty = animation.empty
                };

                // Get curve bindings
                var curveBindings = AnimationUtility.GetCurveBindings(animation);
                response.curveBindings = new List<CurveBindingInfo>();
                foreach (var binding in curveBindings)
                {
                    var curve = AnimationUtility.GetEditorCurve(animation, binding);
                    response.curveBindings.Add(new CurveBindingInfo
                    {
                        path = binding.path,
                        propertyName = binding.propertyName,
                        type = binding.type.FullName ?? binding.type.Name,
                        isPPtrCurve = binding.isPPtrCurve,
                        isDiscreteCurve = binding.isDiscreteCurve,
                        keyframeCount = curve?.length ?? 0
                    });
                }

                // Get object reference curve bindings
                var objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(animation);
                response.objectReferenceBindings = new List<CurveBindingInfo>();
                foreach (var binding in objectBindings)
                {
                    var keyframes = AnimationUtility.GetObjectReferenceCurve(animation, binding);
                    response.objectReferenceBindings.Add(new CurveBindingInfo
                    {
                        path = binding.path,
                        propertyName = binding.propertyName,
                        type = binding.type.FullName ?? binding.type.Name,
                        isPPtrCurve = binding.isPPtrCurve,
                        isDiscreteCurve = binding.isDiscreteCurve,
                        keyframeCount = keyframes?.Length ?? 0
                    });
                }

                // Get animation events
                var events = AnimationUtility.GetAnimationEvents(animation);
                response.events = new List<AnimationEventInfo>();
                foreach (var evt in events)
                {
                    response.events.Add(new AnimationEventInfo
                    {
                        time = evt.time,
                        functionName = evt.functionName,
                        intParameter = evt.intParameter,
                        floatParameter = evt.floatParameter,
                        stringParameter = evt.stringParameter
                    });
                }

                return response;
            });
        }

        public class CurveBindingInfo
        {
            public string path = string.Empty;
            public string propertyName = string.Empty;
            public string type = string.Empty;
            public bool isPPtrCurve;
            public bool isDiscreteCurve;
            public int keyframeCount;
        }

        public class AnimationEventInfo
        {
            public float time;
            public string functionName = string.Empty;
            public int intParameter;
            public float floatParameter;
            public string stringParameter = string.Empty;
        }

        public class GetDataResponse
        {
            public string name = string.Empty;
            public float length;
            public float frameRate;
            public string wrapMode = string.Empty;
            public bool isLooping;
            public bool hasGenericRootTransform;
            public bool hasMotionCurves;
            public bool hasMotionFloatCurves;
            public bool hasRootCurves;
            public bool humanMotion;
            public bool legacy;
            public string localBounds = string.Empty;
            public bool empty;
            public List<CurveBindingInfo>? curveBindings;
            public List<CurveBindingInfo>? objectReferenceBindings;
            public List<AnimationEventInfo>? events;
        }
    }
}
