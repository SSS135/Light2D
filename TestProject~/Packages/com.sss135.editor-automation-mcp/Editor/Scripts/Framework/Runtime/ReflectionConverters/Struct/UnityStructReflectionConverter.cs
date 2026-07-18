#nullable enable

namespace com.SSS135.EditorAutomation.Reflection.Converter
{
    public partial class UnityStructReflectionConverter<T> : UnityGenericNoPropertiesReflectionConverter<T>
    {
        public override bool AllowCascadeSerialization => false;
    }
}
