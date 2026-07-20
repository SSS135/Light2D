#nullable enable

using System.ComponentModel;

namespace EA
{
    public class AnimatorConditionData
    {
        [Description("Parameter name for the condition.")]
        public string? parameter;

        [Description("Condition mode: If, IfNot, Greater, Less, Equals, NotEqual.")]
        public string? mode;

        [Description("Threshold value for the condition.")]
        public float? threshold;
    }
}
