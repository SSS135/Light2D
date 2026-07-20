#nullable enable
using System.ComponentModel;
using System.Linq;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.IvanMurzak.ReflectorNet.Utils;
using EA;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Scene
    {
        public const string SceneListOpenedToolId = "scene-list-opened";
        [McpTool
        (
            SceneListOpenedToolId,
            Title = "Scene / List Opened",
            ReadOnlyHint = true,
            IdempotentHint = true
        )]
        [Description("Returns the list of currently opened scenes in Unity Editor. " +
            "Use '" + SceneGetDataToolId + "' tool to get detailed information about a specific scene.")]
        public OpenedScenesResult ListOpened(string? nothing = null)
        {
            return MainThread.Instance.Run(() =>
            {
                var scenes = OpenedScenes
                    .Select(scene => scene.ToSceneDataShallow())
                    .ToArray();
                return new OpenedScenesResult { Scenes = scenes, Count = scenes.Length };
            });
        }
    }
}
