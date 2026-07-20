#nullable enable
using System.Text.Json;
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.ReflectorNet.Converter;
using com.SSS135.EditorAutomation.JsonConverters;
using com.SSS135.EditorAutomation.Reflection.Converter;
using com.SSS135.EditorAutomation.Utils;
using Microsoft.Extensions.Logging;

namespace com.SSS135.EditorAutomation.Editor.Framework
{
    /// <summary>
    /// Static, process-wide <see cref="Reflector"/> factory. Replaces the instance-bound
    /// <c>EditorAutomationPlugin.CreateDefaultReflector()</c> (deleted with the Runtime/ tree). The
    /// <see cref="Instance"/> is built lazily on first access and reused for the lifetime of
    /// the editor session — every tool invocation, schema build, and JSON serialize routes
    /// through it.
    /// </summary>
    public static class ReflectorFactory
    {
        static readonly object _lock = new();
        static Reflector? _instance;
        static JsonSerializerOptions? _jsonOptions;

        /// <summary>The live <see cref="Reflector"/>. Lazy-built on first access.</summary>
        public static Reflector Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                lock (_lock)
                {
                    _instance ??= Build();
                    return _instance;
                }
            }
        }

        /// <summary>
        /// Snapshot of <see cref="Reflector.JsonSerializerOptions"/> after all 16 Unity-type
        /// <see cref="System.Text.Json.Serialization.JsonConverter{T}"/>s have been registered. Used by
        /// <see cref="RunTool.ConvertResult"/> to serialize raw tool return values into
        /// structured JSON.
        /// </summary>
        public static JsonSerializerOptions JsonOptions
        {
            get
            {
                if (_jsonOptions != null)
                    return _jsonOptions;

                // Force the Reflector to be built first so its options have all converters registered.
                _ = Instance;
                _jsonOptions ??= Instance.JsonSerializerOptions;
                return _jsonOptions;
            }
        }

        static Reflector Build()
        {
            var logger = UnityLoggerFactory.LoggerFactory.CreateLogger(nameof(ReflectorFactory));
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var reflector = new Reflector();
            reflector.JsonSerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals;

            // Remove converters that are not needed in Unity
            reflector.Converters.Remove<GenericReflectionConverter<object>>();
            reflector.Converters.Remove<ArrayReflectionConverter>();

            // Add Unity-specific converters
            reflector.Converters.Add(new UnityGenericReflectionConverter<object>());
            reflector.Converters.Add(new UnityArrayReflectionConverter());

            // Unity types
            reflector.Converters.Add(new UnityEngine_Color32_ReflectionConverter());
            reflector.Converters.Add(new UnityEngine_Color_ReflectionConverter());
            reflector.Converters.Add(new UnityEngine_Matrix4x4_ReflectionConverter());
            reflector.Converters.Add(new UnityEngine_Quaternion_ReflectionConverter());
            reflector.Converters.Add(new UnityEngine_Vector2_ReflectionConverter());
            reflector.Converters.Add(new UnityEngine_Vector2Int_ReflectionConverter());
            reflector.Converters.Add(new UnityEngine_Vector3_ReflectionConverter());
            reflector.Converters.Add(new UnityEngine_Vector3Int_ReflectionConverter());
            reflector.Converters.Add(new UnityEngine_Vector4_ReflectionConverter());
            reflector.Converters.Add(new UnityEngine_Bounds_ReflectionConverter());
            reflector.Converters.Add(new UnityEngine_BoundsInt_ReflectionConverter());
            reflector.Converters.Add(new UnityEngine_Rect_ReflectionConverter());
            reflector.Converters.Add(new UnityEngine_RectInt_ReflectionConverter());
            reflector.Converters.Add(new UnityEngine_Gradient_ReflectionConverter());
            reflector.Converters.Add(new UnityEngine_Scene_ReflectionConverter());
            reflector.Converters.Add(new UnityEngine_GradientColorKey_ReflectionConverter());
            reflector.Converters.Add(new UnityEngine_GradientAlphaKey_ReflectionConverter());
            reflector.Converters.Add(new UnityEngine_EntityId_ReflectionConverter());

            // Unity objects
            reflector.Converters.Add(new UnityEngine_Object_ReflectionConverter());
            reflector.Converters.Add(new UnityEngine_GameObject_ReflectionConverter());

            // Components
            var componentConverter = new UnityEngine_Component_ReflectionConverter();
            reflector.Converters.Add(componentConverter);
            reflector.Converters.Add(new UnityEngine_Transform_ReflectionConverter());
            reflector.Converters.Add(new UnityEngine_Renderer_ReflectionConverter());
            reflector.Converters.Add(new UnityEngine_MeshFilter_ReflectionConverter());
            reflector.Converters.Add(new UnityEngine_Collider_ReflectionConverter(componentConverter));

            // Assets
            reflector.Converters.Add(new UnityEngine_Material_ReflectionConverter());
            reflector.Converters.Add(new UnityEngine_Texture_ReflectionConverter());
            reflector.Converters.Add(new UnityEngine_Sprite_ReflectionConverter());
            reflector.Converters.Add(new UnityEngine_TextAsset_ReflectionConverter());

            // Blacklist types
            // ---------------------------------------------------------

            // System types
            reflector.Converters.BlacklistType(typeof(System.Delegate));
            reflector.Converters.BlacklistType(typeof(System.EventHandler));
            reflector.Converters.BlacklistType(typeof(System.EventHandler<>));
            reflector.Converters.BlacklistType(typeof(System.MulticastDelegate));
            reflector.Converters.BlacklistType(typeof(System.IntPtr));
            reflector.Converters.BlacklistType(typeof(System.UIntPtr));
            reflector.Converters.BlacklistType(typeof(System.Reflection.FieldInfo));
            reflector.Converters.BlacklistType(typeof(System.Reflection.PropertyInfo));
            reflector.Converters.BlacklistType(typeof(System.Threading.CancellationToken));
            reflector.Converters.BlacklistType(typeof(System.Span<>));
            reflector.Converters.BlacklistType(typeof(System.ReadOnlySpan<>));

#if UNITY_2023_1_OR_NEWER
            reflector.Converters.BlacklistType(typeof(UnityEngine.LowLevelPhysics.GeometryHolder));
#endif
            // Redundant text data
#pragma warning disable CS0618 // Type or member is obsolete
            reflector.Converters.BlacklistType(typeof(UnityEngine.TextCore.Text.FontFeatureTable));
#pragma warning restore CS0618 // Type or member is obsolete
            reflector.Converters.BlacklistType(typeof(UnityEngine.TextCore.Glyph));
            reflector.Converters.BlacklistType(typeof(UnityEngine.TextCore.GlyphRect));
            reflector.Converters.BlacklistType(typeof(UnityEngine.TextCore.GlyphMetrics));

            // Redundant TextMeshPro data
            reflector.Converters.BlacklistTypesInAssembly(
                assemblyNamePrefix: "Unity.TextMeshPro",
                typeFullNames: new[]
                {
                    "TMPro.TMP_TextInfo",         // Heavy text data
                    "TMPro.TMP_TextElement",      // Heavy text data
                    "TMPro.TMP_FontFeatureTable", // Heavy font data
                    "TMPro.TMP_FontWeightPair",   // Heavy font data
                    "TMPro.FaceInfo_Legacy"       // Heavy font data
                }
            );

            // Redundant RenderPipeline data
            reflector.Converters.BlacklistTypeInAssembly(
                assemblyNamePrefix: "Unity.RenderPipelines.Core.Runtime",
                typeFullName: "UnityEngine.Rendering.RTHandle" // Can't be utilized
            );

            // Photon IL-weaved types
            reflector.Converters.BlacklistTypeInAssembly(
                assemblyNamePrefix: "Fusion.Runtime",
                typeFullName: "Fusion.NetworkBehaviourBuffer"
            );

            // Addressables
            reflector.Converters.BlacklistTypeInAssembly(
                assemblyNamePrefix: "Unity.ResourceManager",
                typeFullName: "UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle"
            );

            // Json Converters
            // ---------------------------------------------------------

            // Unity types
            reflector.JsonSerializer.AddConverter(new Color32Converter());
            reflector.JsonSerializer.AddConverter(new ColorConverter());
            reflector.JsonSerializer.AddConverter(new Matrix4x4Converter());
            reflector.JsonSerializer.AddConverter(new QuaternionConverter());
            reflector.JsonSerializer.AddConverter(new Vector2Converter());
            reflector.JsonSerializer.AddConverter(new Vector2IntConverter());
            reflector.JsonSerializer.AddConverter(new Vector3Converter());
            reflector.JsonSerializer.AddConverter(new Vector3IntConverter());
            reflector.JsonSerializer.AddConverter(new Vector4Converter());
            reflector.JsonSerializer.AddConverter(new BoundsConverter());
            reflector.JsonSerializer.AddConverter(new BoundsIntConverter());
            reflector.JsonSerializer.AddConverter(new RectConverter());
            reflector.JsonSerializer.AddConverter(new RectIntConverter());
            reflector.JsonSerializer.AddConverter(new GradientConverter());
            reflector.JsonSerializer.AddConverter(new GradientColorKeyConverter());
            reflector.JsonSerializer.AddConverter(new GradientAlphaKeyConverter());
            reflector.JsonSerializer.AddConverter(new EntityIdConverter());

            // Reference types
            reflector.JsonSerializer.AddConverter(new ObjectRefConverter());
            reflector.JsonSerializer.AddConverter(new AssetObjectRefConverter());
            reflector.JsonSerializer.AddConverter(new GameObjectRefConverter());
            reflector.JsonSerializer.AddConverter(new ComponentRefConverter());
            reflector.JsonSerializer.AddConverter(new SceneRefConverter());

            stopwatch.Stop();
            logger.LogDebug("Created default reflector in {elapsedMilliseconds} ms.",
                stopwatch.ElapsedMilliseconds);

            return reflector;
        }
    }
}
