#nullable enable
using System;
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.ReflectorNet.Model;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Runtime.Extensions;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace com.SSS135.EditorAutomation.Reflection.Converter
{
    public partial class UnityEngine_Object_ReflectionConverter<T>
    {
        public override object? Deserialize(
            Reflector reflector,
            SerializedMember data,
            Type? fallbackType = null,
            string? fallbackName = null,
            int depth = 0,
            Logs? logs = null,
            ILogger? logger = null,
            DeserializationContext? context = null)
        {
            var targetType = fallbackType ?? typeof(T);
            var padding = StringUtils.GetPadding(depth);
            if (logger?.IsEnabled(LogLevel.Information) == true)
                logger.LogInformation($"{padding}[UnityEngine_Object_ReflectionConverter] Deserialize called for {targetType.GetTypeId()}. Converter: {GetType().GetTypeShortName()}");

            logs?.Info($"Deserialize called for '{targetType.GetTypeId()}'. Converter: {GetType().GetTypeShortName()}", depth);

            if (!TryDeserializeValue(
                reflector,
                data: data,
                result: out var result,
                type: out var type,
                fallbackType: fallbackType,
                depth: depth,
                logs: logs,
                logger: logger))
            {
                return result;
            }
            // Register the object early (before deserializing children) so child references can resolve
            if (result != null && context != null)
                context.Register(result);

            return data.valueJsonElement
                .ToAssetObjectRef(
                    reflector: reflector,
                    depth: depth,
                    logs: logs,
                    logger: logger)
                .FindAssetObject(targetType);
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
                .ToAssetObjectRef(
                    reflector: reflector,
                    depth: depth,
                    logs: logs,
                    logger: logger)
                .FindAssetObject(type);
        }
    }
}
