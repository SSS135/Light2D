#nullable enable

using System.Collections.Generic;

namespace EA
{
    public class AnimatorStateInfo
    {
        public string name = string.Empty;
        public string tag = string.Empty;
        public float speed;
        public bool speedParameterActive;
        public string speedParameter = string.Empty;
        public float cycleOffset;
        public bool cycleOffsetParameterActive;
        public string cycleOffsetParameter = string.Empty;
        public bool mirror;
        public bool mirrorParameterActive;
        public string mirrorParameter = string.Empty;
        public bool writeDefaultValues;
        public string? motionName;
        public List<AnimatorTransitionInfo>? transitions;
    }
}
