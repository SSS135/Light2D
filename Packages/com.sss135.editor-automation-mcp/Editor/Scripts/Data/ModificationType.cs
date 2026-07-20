#nullable enable

using System.ComponentModel;

namespace EA
{
    public enum ModificationType
    {
        [Description("Add or modify an animation curve.")]
        SetCurve,

        [Description("Remove a specific animation curve.")]
        RemoveCurve,

        [Description("Remove all curves from the clip.")]
        ClearCurves,

        [Description("Set the frame rate of the clip.")]
        SetFrameRate,

        [Description("Set the wrap mode of the clip.")]
        SetWrapMode,

        [Description("Set whether the clip uses legacy animation system.")]
        SetLegacy,

        [Description("Add an animation event.")]
        AddEvent,

        [Description("Remove all animation events.")]
        ClearEvents
    }
}
