#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.ReflectorNet.Converter;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace com.SSS135.EditorAutomation.Reflection.Converter
{
    public partial class UnityArrayReflectionConverter : ArrayReflectionConverter
    {
        protected override IEnumerable<FieldInfo>? GetSerializableFieldsInternal(Reflector reflector, Type objType, BindingFlags flags, ILogger? logger = null)
        {
            return objType.GetFields(flags)
                .Where(field => field.GetCustomAttribute<ObsoleteAttribute>() == null)
                .Where(field => field.GetCustomAttribute<NonSerializedAttribute>() == null)
                .Where(field => field.IsPublic || field.IsPrivate && field.GetCustomAttribute<SerializeField>() != null);
        }
    }
}
