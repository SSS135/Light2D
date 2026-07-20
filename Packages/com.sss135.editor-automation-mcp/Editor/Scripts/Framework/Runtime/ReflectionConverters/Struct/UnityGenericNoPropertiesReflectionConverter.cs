#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using com.IvanMurzak.ReflectorNet;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace com.SSS135.EditorAutomation.Reflection.Converter
{
    public partial class UnityGenericNoPropertiesReflectionConverter<T> : UnityGenericReflectionConverter<T>
    {
        protected override IEnumerable<PropertyInfo>? GetSerializablePropertiesInternal(
            Reflector reflector,
            Type objType,
            BindingFlags flags,
            ILogger? logger = null)
        {
            return null;
        }
    }
}
