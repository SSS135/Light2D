#nullable enable

using System;
using System.Collections.Generic;
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.ReflectorNet.Model;
using com.SSS135.EditorAutomation.Runtime.Extensions;
using Microsoft.Extensions.Logging;

namespace com.SSS135.EditorAutomation.Reflection.Converter
{
    public abstract class UnityEngine_GenericComponent_ReflectionConverter<T> : UnityEngine_Object_ReflectionConverter<T> where T : UnityEngine.Component
    {
        public override bool AllowSetValue => false;

        protected override IEnumerable<string> GetIgnoredProperties()
        {
            foreach (var property in base.GetIgnoredProperties())
                yield return property;

            yield return nameof(UnityEngine.Component.gameObject);
            yield return nameof(UnityEngine.Component.transform);
#if UNITY_6000_3_OR_NEWER
            yield return nameof(UnityEngine.Component.transformHandle);
#endif
        }
        protected override object? DeserializeValueAsJsonElement(
            Reflector reflector,
            SerializedMember data,
            Type type,
            int depth = 0,
            Logs? logs = null,
            ILogger? logger = null)
        {
            return data.valueJsonElement
                .ToObjectRef(
                    reflector: reflector,
                    depth: depth,
                    logs: logs,
                    logger: logger)
                .FindObject() as UnityEngine.Component;
        }
    }
}
