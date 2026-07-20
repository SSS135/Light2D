#nullable enable

using System.Collections.Generic;

namespace EA
{
    public class AnimatorTransitionInfo
    {
        public string? destinationStateName;
        public bool hasExitTime;
        public float exitTime;
        public bool hasFixedDuration;
        public float duration;
        public float offset;
        public bool canTransitionToSelf;
        public List<AnimatorConditionInfo>? conditions;
    }
}
