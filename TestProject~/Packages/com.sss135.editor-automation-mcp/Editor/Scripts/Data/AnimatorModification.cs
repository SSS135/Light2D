#nullable enable

using System.ComponentModel;

namespace EA
{
    public class AnimatorModification
    {
        [Description("Modification type. Properties below are used conditionally based on this value.")]
        public AnimatorModificationType type;

        // Parameter fields (AddParameter, RemoveParameter)
        [Description("Parameter name. Required for: AddParameter, RemoveParameter.")]
        public string? parameterName;

        [Description("Parameter type: Float, Int, Bool, Trigger. Required for: AddParameter.")]
        public string? parameterType;

        [Description("Default float value. Optional for: AddParameter (Float type).")]
        public float? defaultFloat;

        [Description("Default int value. Optional for: AddParameter (Int type).")]
        public int? defaultInt;

        [Description("Default bool value. Optional for: AddParameter (Bool type).")]
        public bool? defaultBool;

        // Layer fields (AddLayer, RemoveLayer, AddState, RemoveState, SetDefaultState, transitions)
        [Description("Layer name. Required for: AddLayer, RemoveLayer, AddState, RemoveState, SetDefaultState, AddTransition, RemoveTransition, AddAnyStateTransition, SetStateMotion, SetStateSpeed.")]
        public string? layerName;

        // State fields (AddState, RemoveState, SetDefaultState, SetStateMotion, SetStateSpeed)
        [Description("State name. Required for: AddState, RemoveState, SetDefaultState, SetStateMotion, SetStateSpeed.")]
        public string? stateName;

        [Description("Asset path to AnimationClip. Optional for: AddState. Required for: SetStateMotion.")]
        public string? motionAssetPath;

        [Description("Speed multiplier. Required for: SetStateSpeed.")]
        public float? speed;

        // Transition fields (AddTransition, RemoveTransition, AddAnyStateTransition)
        [Description("Source state name. Required for: AddTransition, RemoveTransition.")]
        public string? sourceStateName;

        [Description("Destination state name. Required for: AddTransition, RemoveTransition, AddAnyStateTransition.")]
        public string? destinationStateName;

        [Description("Whether transition waits for exit time. Optional for: AddTransition, AddAnyStateTransition.")]
        public bool? hasExitTime;

        [Description("Normalized exit time (0-1). Optional for: AddTransition, AddAnyStateTransition.")]
        public float? exitTime;

        [Description("Transition blend duration. Optional for: AddTransition, AddAnyStateTransition.")]
        public float? duration;

        [Description("Whether duration is in seconds (true) or normalized (false). Optional for: AddTransition, AddAnyStateTransition.")]
        public bool? hasFixedDuration;

        [Description("Transition conditions. Optional for: AddTransition, AddAnyStateTransition.")]
        public AnimatorConditionData[]? conditions;
    }
}
