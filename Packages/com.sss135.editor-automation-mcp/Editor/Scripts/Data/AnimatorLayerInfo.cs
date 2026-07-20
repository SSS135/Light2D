#nullable enable

using System.Collections.Generic;

namespace EA
{
    public class AnimatorLayerInfo
    {
        public string name = string.Empty;
        public float defaultWeight;
        public string blendingMode = string.Empty;
        public int syncedLayerIndex;
        public bool iKPass;
        public string? defaultStateName;
        public List<AnimatorStateInfo>? states;
        public List<string>? subStateMachines;
        public List<AnimatorTransitionInfo>? anyStateTransitions;
    }
}
