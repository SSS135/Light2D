#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using com.IvanMurzak.ReflectorNet;
using EA;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Utils;
using Microsoft.Extensions.Logging;

namespace com.SSS135.EditorAutomation.Editor.API
{
    [McpToolType]
    public partial class Tool_GameObject
    {
        public static class Error
        {
            public static string NotFoundComponent(UnityEngine.EntityId componentEntityID, IEnumerable<UnityEngine.Component> allComponents)
            {
                var reflector = ReflectorFactory.Instance ?? throw new Exception("Reflector is not available.");
                var availableComponentsPreview = allComponents
                    .Select((c, i) => reflector.Serialize(
                        c,
                        name: $"[{i}]",
                        recursive: false,
                        logger: UnityLoggerFactory.LoggerFactory.CreateLogger<Tool_GameObject>()
                    ))
                    .ToList();
                var previewJson = availableComponentsPreview.ToJson(reflector);

                var instanceIdSample = new { componentData = availableComponentsPreview[0] }.ToJson(reflector);
                var helpMessage = $"Use 'name=[index]' to specify the component. Or use 'entityId' to specify the component.\n{instanceIdSample}";

                return $"No component with entityId '{componentEntityID}' found in GameObject.\n{helpMessage}\nAvailable components preview:\n{previewJson}";
            }
            public static string NotFoundComponents(ComponentRefList componentRefs, IEnumerable<UnityEngine.Component> allComponents)
            {
                var reflector = ReflectorFactory.Instance ?? throw new Exception("Reflector is not available.");
                var componentInstanceIDsString = string.Join(", ", componentRefs.Select(cr => cr.ToString()));
                var availableComponentsPreview = allComponents
                    .Select((c, i) => reflector.Serialize(
                        obj: c,
                        fallbackType: typeof(UnityEngine.Component),
                        name: $"[{i}]",
                        recursive: false,
                        logger: UnityLoggerFactory.LoggerFactory.CreateLogger<Tool_GameObject>()
                    ))
                    .ToList();
                var previewJson = availableComponentsPreview.ToJson(reflector);

                return $"No components with instanceIDs [{componentInstanceIDsString}] found in GameObject.\nAvailable components preview:\n{previewJson}";
            }
            public static string InvalidInstanceID(Type holderType, string fieldName)
                => $"Invalid instanceID '{fieldName}' for '{holderType.GetTypeId()}'. It should be a valid field name.";
        }
    }
}