#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.Framework.Builder;
using com.SSS135.EditorAutomation.Utils;
using Microsoft.Extensions.Logging;

namespace com.SSS135.EditorAutomation.Editor.Framework.Runtime
{
    /// <summary>
    /// Canonical <see cref="IMcpTool"/> implementation. Wraps a <see cref="ToolDescriptor"/>
    /// and binds named JSON parameters onto the underlying method via ReflectorNet's
    /// <see cref="MethodWrapper.InvokeDict"/>. The cached input schema is computed by
    /// <see cref="MethodWrapper.CreateInputSchema"/> at construction (via the inherited base).
    /// </summary>
    /// <remarks>
    /// Inheriting from <see cref="MethodWrapper"/> mirrors the upstream MCP-Plugin-dotnet
    /// <c>RunTool</c>: it gives the framework access to the protected
    /// <see cref="MethodWrapper.CreateInputSchema"/> hook plus the binder field
    /// <c>_reflector</c> for the structured-result serializer.
    /// </remarks>
    public sealed class RunTool : MethodWrapper, IMcpTool
    {
        // Assigned once via Init() from each binding-shaped constructor; not readonly because the
        // assignment happens in a shared helper method rather than directly in the constructor body.
        ToolDescriptor _descriptor = null!;

        // Set once by Init() from the shared constructor body. Private setters (rather than
        // get-only init) because the two binding-shaped constructors both delegate the assignment
        // to Init() after their respective base() call.
        public string Name { get; private set; } = string.Empty;
        public string? Title { get; private set; }
        // Description is inherited from MethodWrapper (sourced from [Description] on the method).
        // No [McpSkillDescription] equivalent in the new attribute set — emitters fall back to Description.
        public string? SkillDescription => null;
        public McpToolHints ToolHints { get; private set; }
        public bool MainThreadRequired => _descriptor.MainThreadRequired;

        // Per-call request id storage. Read by GetParameterValue overrides to substitute
        // for parameters marked with [RequestID]. Reassigned on every InvokeAsync call.
        // RunTool instances are not designed to be reused across concurrent invocations;
        // the dispatcher serializes calls per tool.
        string? _currentRequestId;

        // MethodWrapper exposes three mutually-exclusive base constructors, and a C# constructor
        // initializer (`: base(...)`) is a single fixed compile-time target that cannot branch at
        // runtime. So the two binding shapes are split across two private constructors, each
        // calling the matching base overload, and callers go through the Create() factory:
        //
        //   (reflector, logger, methodInfo)            — STATIC methods only (the base throws
        //                                                "must be static" for instance methods).
        //   (reflector, logger, classType, methodInfo) — INSTANCE methods. The declaring class is
        //                                                instantiated lazily inside MethodWrapper
        //                                                .InvokeDict via Reflector.CreateInstance,
        //                                                so the tool method runs against a real
        //                                                receiver. The base throws for static
        //                                                methods, hence the explicit split.
        //
        // Most [McpTool] methods are instance methods, so this factory routes them through the
        // instance overload; the static overload serves the handful of methods on static tool
        // classes. Without this split every instance tool throws "must be static" in MethodWrapper's
        // constructor — and, now that ToolRegistry.Rebuild is fail-loud, that aborts the whole rebuild.
        public static RunTool Create(ToolDescriptor descriptor)
        {
            if (descriptor == null)
                throw new ArgumentNullException(nameof(descriptor));
            if (descriptor.MethodInfo == null)
                throw new ArgumentException("ToolDescriptor.MethodInfo is null.", nameof(descriptor));

            return descriptor.MethodInfo.IsStatic
                ? new RunTool(descriptor, forStatic: true)
                : new RunTool(descriptor.ClassType, descriptor);
        }

        // STATIC-method base constructor. The bool parameter only disambiguates the overload from
        // the instance one below; callers reach this through Create().
        RunTool(ToolDescriptor descriptor, bool forStatic)
            : base(
                reflector: ResolveReflector(descriptor),
                logger: UnityLoggerFactory.LoggerFactory.CreateLogger("EditorAutomation.RunTool"),
                methodInfo: descriptor.MethodInfo)
        {
            Init(descriptor);
        }

        // INSTANCE-method base constructor. classType is passed first to bind MethodWrapper's
        // (reflector, logger, Type, MethodInfo) overload, which instantiates the receiver lazily
        // at invoke time. classType leads the parameter list purely to keep the overloads distinct.
        RunTool(Type classType, ToolDescriptor descriptor)
            : base(
                reflector: ResolveReflector(descriptor),
                logger: UnityLoggerFactory.LoggerFactory.CreateLogger("EditorAutomation.RunTool"),
                classType: classType,
                methodInfo: descriptor.MethodInfo)
        {
            Init(descriptor);
        }

        void Init(ToolDescriptor descriptor)
        {
            _descriptor = descriptor;
            Name = descriptor.Attribute.ToolId;
            Title = descriptor.Attribute.Title;
            ToolHints = descriptor.Hints;
        }

        static Reflector ResolveReflector(ToolDescriptor descriptor)
        {
            if (descriptor == null)
                throw new ArgumentNullException(nameof(descriptor));
            // Live, process-wide Reflector. Relocated from Runtime/EditorAutomationPlugin.Converters.cs
            // into Editor/Scripts/Framework/Runtime/ReflectorFactory.cs in Wave 2.
            return ReflectorFactory.Instance;
        }

        /// <summary>
        /// Per-parameter <see cref="RequestIDAttribute"/> substitution for positional invocation.
        /// </summary>
        protected override object? GetParameterValue(Reflector reflector, ParameterInfo paramInfo, object? value)
        {
            if (paramInfo.GetCustomAttribute<RequestIDAttribute>() != null)
                return _currentRequestId;
            return FixEnumConversion(paramInfo, base.GetParameterValue(reflector, paramInfo, value));
        }

        /// <summary>
        /// Per-parameter <see cref="RequestIDAttribute"/> substitution for named-dictionary invocation.
        /// </summary>
        protected override object? GetParameterValue(
            Reflector reflector,
            ParameterInfo paramInfo,
            IReadOnlyDictionary<string, object?>? namedParameters)
        {
            if (paramInfo.GetCustomAttribute<RequestIDAttribute>() != null)
                return _currentRequestId;
            return FixEnumConversion(paramInfo, base.GetParameterValue(reflector, paramInfo, namedParameters));
        }

        protected override object? GetDefaultParameterValue(Reflector reflector, ParameterInfo methodParameter)
        {
            if (methodParameter.GetCustomAttribute<RequestIDAttribute>() != null)
                return _currentRequestId;
            return FixEnumConversion(methodParameter, base.GetDefaultParameterValue(reflector, methodParameter));
        }

        // Mirrors upstream RunTool: ReflectorNet hands enum-typed args back as their underlying
        // integral type; coerce them to the actual enum before invocation.
        static object? FixEnumConversion(ParameterInfo paramInfo, object? value)
        {
            if (value == null)
                return null;
            var paramType = paramInfo.ParameterType;
            var underlyingType = Nullable.GetUnderlyingType(paramType) ?? paramType;
            if (underlyingType.IsEnum && value.GetType() != underlyingType)
                return Enum.ToObject(underlyingType, value);
            return value;
        }

        public async Task<McpResponse> InvokeAsync(
            string requestId,
            IReadOnlyDictionary<string, JsonElement> namedParams,
            CancellationToken ct)
        {
            _currentRequestId = requestId;
            try
            {
                // Strip params that should be replaced by [RequestID] substitution before binding.
                // Case-insensitive name match against the method's actual parameters.
                var finalParameters = StripRequestIdParams(namedParams);

                // Marshal onto the Unity main thread. UnityMainThread.RunAsync short-circuits when
                // already on the main thread, so this is a no-op cost in the common case.
                var invocationTask = InvokeDict(finalParameters, ct);
                var result = await MainThread.Instance.RunAsync(invocationTask).ConfigureAwait(false);

                return ConvertResult(result);
            }
            catch (TargetInvocationException tie)
            {
                var inner = tie.InnerException ?? tie;
                return McpResponse.Error($"[Error] {inner.Message}\n{inner.StackTrace}");
            }
            catch (ArgumentException ex)
            {
                return McpResponse.Error(
                    $"Parameter validation failed for tool '{Title ?? _descriptor.MethodInfo.Name}': {ex.Message}");
            }
            catch (JsonException ex)
            {
                return McpResponse.Error(
                    $"JSON parameter parsing failed for tool '{Title ?? _descriptor.MethodInfo.Name}': {ex.Message}");
            }
            catch (TargetParameterCountException ex)
            {
                return McpResponse.Error(
                    $"Parameter count mismatch for tool '{Title ?? _descriptor.MethodInfo.Name}': {ex.Message}");
            }
            catch (Exception ex)
            {
                return McpResponse.Error(
                    $"Tool execution failed for '{Title ?? _descriptor.MethodInfo.Name}': {(ex.InnerException ?? ex).Message}");
            }
            finally
            {
                _currentRequestId = null;
            }
        }

        // MethodWrapper.InvokeDict takes IReadOnlyDictionary<string, object?>; converting from
        // JsonElement keeps the existing ReflectorNet deserializer in charge of JSON-to-CLR
        // binding (including the Unity-type ReflectionConverters wired through ReflectorFactory).
        Dictionary<string, object?> StripRequestIdParams(IReadOnlyDictionary<string, JsonElement> namedParams)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (namedParams.Count == 0)
                return dict;

            var requestIdNames = CollectRequestIdParameterNames();

            foreach (var kvp in namedParams)
            {
                if (requestIdNames.Contains(kvp.Key))
                    continue;
                dict[kvp.Key] = kvp.Value;
            }
            return dict;
        }

        HashSet<string> CollectRequestIdParameterNames()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in _descriptor.MethodInfo.GetParameters())
            {
                if (p.GetCustomAttribute<RequestIDAttribute>() != null && p.Name != null)
                    set.Add(p.Name);
            }
            return set;
        }

        McpResponse ConvertResult(object? result)
        {
            if (result is McpResponse response)
                return response;
            if (result == null)
                return McpResponse.Success();

            return McpResponse.SuccessStructured(
                System.Text.Json.JsonSerializer.SerializeToNode(result, ReflectorFactory.JsonOptions));
        }
    }
}
