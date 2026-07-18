#nullable enable
using System;
using System.ComponentModel;

namespace EA
{
    public sealed class DuplicatedObjectsResult
    {
        [Description("References to the duplicated GameObjects.")]
        public GameObjectRef[] GameObjects { get; set; } = Array.Empty<GameObjectRef>();

        [Description("Number of GameObjects duplicated.")]
        public int Count { get; set; }
    }
}
