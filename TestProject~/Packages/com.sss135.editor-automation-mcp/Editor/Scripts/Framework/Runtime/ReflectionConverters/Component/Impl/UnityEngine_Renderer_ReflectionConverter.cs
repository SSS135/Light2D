#nullable enable
using System.Collections.Generic;

namespace com.SSS135.EditorAutomation.Reflection.Converter
{
    public partial class UnityEngine_Renderer_ReflectionConverter : UnityEngine_GenericComponent_ReflectionConverter<UnityEngine.Renderer>
    {
        protected override IEnumerable<string> GetIgnoredProperties()
        {
            foreach (var property in base.GetIgnoredProperties())
                yield return property;

            yield return nameof(UnityEngine.Renderer.material);
            yield return nameof(UnityEngine.Renderer.materials);
        }
    }
}
