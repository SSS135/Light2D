#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace EA
{
    [Description("Scene reference. Used to find a Scene.")]
    public class SceneRef : ObjectRef
    {
        public static partial class SceneRefProperty
        {
            public const string Path = "path";
            public const string BuildIndex = "buildIndex";

            public static IEnumerable<string> All => new[] { Path, BuildIndex };
        }

        [JsonInclude, JsonPropertyName(SceneRefProperty.Path)]
        [Description("Path to the Scene within the project. Starts with 'Assets/'")]
        public string Path { get; set; } = string.Empty;

        [JsonInclude, JsonPropertyName(SceneRefProperty.BuildIndex)]
        [Description("Build index of the Scene in the Build Settings.")]
        public int BuildIndex { get; set; } = -1;

        public SceneRef() { }
        public SceneRef(UnityEngine.EntityId entityId)
        {
            this.InstanceID = entityId;
        }
        public SceneRef(UnityEngine.SceneManagement.Scene scene)
        {
            this.InstanceID = UnityEngine.EntityId.FromULong((ulong)scene.GetHashCode());
            this.Path = scene.path;
            this.BuildIndex = scene.buildIndex;
        }
    }
}
