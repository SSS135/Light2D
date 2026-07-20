#nullable enable
using System;
using System.ComponentModel;

namespace EA
{
    public sealed class OpenedScenesResult
    {
        [Description("Currently opened scenes.")]
        public SceneDataShallow[] Scenes { get; set; } = Array.Empty<SceneDataShallow>();

        [Description("Number of opened scenes returned.")]
        public int Count { get; set; }
    }
}
