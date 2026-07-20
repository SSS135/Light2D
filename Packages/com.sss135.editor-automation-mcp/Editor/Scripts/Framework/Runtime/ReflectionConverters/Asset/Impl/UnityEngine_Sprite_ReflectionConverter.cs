#nullable enable

namespace com.SSS135.EditorAutomation.Reflection.Converter
{
    public partial class UnityEngine_Sprite_ReflectionConverter : UnityEngine_Asset_ReflectionConverter<UnityEngine.Sprite>
    {
        public override bool AllowCascadeSerialization => false;
        public override bool AllowSetValue => false;
    }
}
