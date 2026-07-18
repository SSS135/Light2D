#nullable enable
#if UNITY_EDITOR
using System;
using System.Linq;
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.ReflectorNet.Model;
using Microsoft.Extensions.Logging;

namespace com.SSS135.EditorAutomation.Reflection.Converter
{
    public partial class UnityEngine_Sprite_ReflectionConverter : UnityEngine_Asset_ReflectionConverter<UnityEngine.Sprite>
    {
        protected override bool TryDeserializeValueInternal(
            Reflector reflector,
            SerializedMember data,
            out object? result,
            Type type,
            int depth = 0,
            Logs? logs = null,
            ILogger? logger = null)
        {
            var baseResult = base.TryDeserializeValueInternal(
                reflector: reflector,
                data: data,
                result: out result,
                type: type,
                depth: depth,
                logs: logs,
                logger: logger);

            if (result is UnityEngine.Sprite)
                return baseResult;

            if (result is UnityEngine.Texture2D texture)
            {
                var path = UnityEditor.AssetDatabase.GetAssetPath(texture);
                result = UnityEditor.AssetDatabase.LoadAllAssetRepresentationsAtPath(path)
                    .OfType<UnityEngine.Sprite>()
                    .FirstOrDefault();
                return result != null;
            }
            return baseResult;
        }

        protected override UnityEngine.Sprite? LoadFromEntityId(UnityEngine.EntityId entityId)
        {
            var textureOrSprite = UnityEditor.EditorUtility.EntityIdToObject(entityId);
            if (textureOrSprite == null) return null;

            if (textureOrSprite is UnityEngine.Sprite sprite)
                return sprite;

            if (textureOrSprite is UnityEngine.Texture2D texture)
            {
                var path = UnityEditor.AssetDatabase.GetAssetPath(texture);
                return UnityEditor.AssetDatabase.LoadAllAssetRepresentationsAtPath(path)
                    .OfType<UnityEngine.Sprite>()
                    .FirstOrDefault();
            }
            return null;
        }

        protected override UnityEngine.Sprite? LoadFromAssetPath(string path)
        {
            var allAssets = UnityEditor.AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
            return allAssets
               .OfType<UnityEngine.Sprite>()
               .FirstOrDefault();
        }
    }
}
#endif
