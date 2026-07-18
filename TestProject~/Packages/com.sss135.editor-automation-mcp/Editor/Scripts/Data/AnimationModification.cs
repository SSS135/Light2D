#nullable enable

using System.ComponentModel;
using UnityEngine;

namespace EA
{
    public class AnimationModification
    {
        [Description("Modification type. Properties below are used conditionally based on this value.")]
        public ModificationType type;

        // Curve-related properties (SetCurve, RemoveCurve)
        [Description("Path to target GameObject relative to the root (empty for root). Used by: SetCurve, RemoveCurve.")]
        public string? relativePath;

        [Description("Component type name (e.g., 'Transform', 'SpriteRenderer'). Required for: SetCurve, RemoveCurve.")]
        public string? componentType;

        [Description("Property to animate (e.g., 'localPosition.x', 'm_LocalScale.y'). Required for: SetCurve, RemoveCurve.")]
        public string? propertyName;

        [Description("Keyframes for the curve. Required for: SetCurve.")]
        public AnimationKeyframe[]? keyframes;

        // Clip properties (SetFrameRate, SetWrapMode, SetLegacy)
        [Description("Frames per second. Required for: SetFrameRate.")]
        public float? frameRate;

        [Description("How animation behaves at boundaries. Required for: SetWrapMode.")]
        public WrapMode? wrapMode;

        [Description("Use legacy animation system. Required for: SetLegacy.")]
        public bool? legacy;

        // Event-related (AddEvent)
        [Description("Event trigger time in seconds. Required for: AddEvent.")]
        public float? time;

        [Description("Function to invoke. Required for: AddEvent.")]
        public string? functionName;

        [Description("String parameter passed to the function. Optional for: AddEvent.")]
        public string? stringParameter;

        [Description("Float parameter passed to the function. Optional for: AddEvent.")]
        public float? floatParameter;

        [Description("Integer parameter passed to the function. Optional for: AddEvent.")]
        public int? intParameter;
    }
}
