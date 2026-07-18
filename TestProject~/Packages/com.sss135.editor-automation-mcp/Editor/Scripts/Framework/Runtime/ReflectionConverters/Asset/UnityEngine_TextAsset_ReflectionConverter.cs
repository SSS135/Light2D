#nullable enable

using System.Collections.Generic;

namespace com.SSS135.EditorAutomation.Reflection.Converter
{
    public partial class UnityEngine_TextAsset_ReflectionConverter : UnityEngine_Asset_ReflectionConverter<UnityEngine.TextAsset>
    {
        protected override IEnumerable<string> GetIgnoredProperties()
        {
            foreach (var property in base.GetIgnoredProperties())
                yield return property;

            yield return nameof(UnityEngine.TextAsset.bytes);
        }
    }
}
