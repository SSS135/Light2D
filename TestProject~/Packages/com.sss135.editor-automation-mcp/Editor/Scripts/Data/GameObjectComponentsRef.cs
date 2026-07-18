#nullable enable
using System.ComponentModel;
using System.Text.Json.Serialization;
using com.IvanMurzak.ReflectorNet.Model;

namespace EA
{
    [System.Serializable]
    [Description("GameObject reference. " +
        "Used to find GameObject in opened Prefab or in a Scene.")]
    public class GameObjectComponentsRef : GameObjectRef
    {
        [JsonInclude, JsonPropertyName("components")]
        [Description("GameObject 'components'.")]
        public SerializedMemberList? Components { get; set; }

        public GameObjectComponentsRef() { }

        public override string ToString()
        {
            var stringBuilder = new System.Text.StringBuilder();
            stringBuilder.AppendLine($"{base.ToString()}");

            if (Components != null && Components.Count > 0)
            {
                stringBuilder.AppendLine($"Components total amount: {Components.Count}");
                for (int i = 0; i < Components.Count; i++)
                    stringBuilder.AppendLine($"Component[{i}] {Components[i]}");
            }
            else
            {
                stringBuilder.AppendLine("No Components");
            }
            return stringBuilder.ToString();
        }
    }
}
