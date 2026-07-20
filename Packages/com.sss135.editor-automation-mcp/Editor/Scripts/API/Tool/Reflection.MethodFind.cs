#nullable enable
using System;
using System.ComponentModel;
using System.Linq;
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.ReflectorNet.Model;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.Framework;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_Reflection
    {
        public const string ReflectionMethodFindToolId = "reflection-method-find";
        [McpTool
        (
            ReflectionMethodFindToolId,
            Title = "Method C# / Find",
            ReadOnlyHint = true,
            IdempotentHint = true
        )]
        [Description("Find method in the project using C# Reflection. " +
            "It looks for all assemblies in the project and finds method by its name, class name and parameters. " +
            "Even private methods are available. " +
            "Use '" + ReflectionMethodCallToolId + "' to call the method after finding it.")]
        public string MethodFind
        (
            MethodRef filter,

            [Description("Set to true if 'Namespace' is known and full namespace name is specified in the 'filter.Namespace' property. Otherwise, set to false.")]
            bool knownNamespace = false,

            [Description("Minimal match level for 'typeName'. " +
                "0 - ignore 'filter.typeName', " +
                "1 - contains ignoring case (default value), " +
                "2 - contains case sensitive, " +
                "3 - starts with ignoring case, " +
                "4 - starts with case sensitive, " +
                "5 - equals ignoring case, " +
                "6 - equals case sensitive.")]
            int typeNameMatchLevel = 1,

            [Description("Minimal match level for 'MethodName'. " +
                "0 - ignore 'filter.MethodName', " +
                "1 - contains ignoring case (default value), " +
                "2 - contains case sensitive, " +
                "3 - starts with ignoring case, " +
                "4 - starts with case sensitive, " +
                "5 - equals ignoring case, " +
                "6 - equals case sensitive.")]
            int methodNameMatchLevel = 1,

            [Description("Minimal match level for 'Parameters'. " +
                "0 - ignore 'filter.Parameters' (default value), " +
                "1 - parameters count is the same, " +
                "2 - equals.")]
            int parametersMatchLevel = 0
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var methodEnumerable = FindMethods(
                    filter: filter,
                    knownNamespace: knownNamespace,
                    typeNameMatchLevel: typeNameMatchLevel,
                    methodNameMatchLevel: methodNameMatchLevel,
                    parametersMatchLevel: parametersMatchLevel);

                var methods = methodEnumerable.ToList();
                if (methods.Count == 0)
                    return $"[Success] Method not found. With request:\n{filter}";

                var reflector = ReflectorFactory.Instance ?? throw new Exception("Reflector is not available.");

                var methodRefs = methods
                    .Select(method => new MethodData(reflector, method, justRef: false))
                    .ToList();

                return $@"[Success] Found {methods.Count} method(s):
```json
{methodRefs.ToJson(reflector)}
```";
            });
        }
    }
}
