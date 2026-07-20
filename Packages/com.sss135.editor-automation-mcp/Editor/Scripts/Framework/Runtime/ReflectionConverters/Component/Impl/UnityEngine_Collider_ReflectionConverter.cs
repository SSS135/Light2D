#nullable enable
using com.IvanMurzak.ReflectorNet.Converter;

namespace com.SSS135.EditorAutomation.Reflection.Converter
{
    public partial class UnityEngine_Collider_ReflectionConverter : LazyGenericReflectionConverter<UnityEngine.Component>
    {
        public UnityEngine_Collider_ReflectionConverter(UnityEngine_GenericComponent_ReflectionConverter<UnityEngine.Component> backingConverter)
            : base(
                targetTypeName: "UnityEngine.Collider",
                ignoredProperties: new string[]
                {
                    "material" // nameof(UnityEngine.Collider.material)
                },
                ignoredFields: new string[]
                {
                    "material" // nameof(UnityEngine.Collider.material)
                },
                backingConverter: backingConverter)
        {
            // empty
        }
    }
}
