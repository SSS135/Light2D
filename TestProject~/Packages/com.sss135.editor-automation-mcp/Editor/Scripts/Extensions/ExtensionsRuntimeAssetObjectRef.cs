#nullable enable
using System.Linq;
using EA;
using UnityEngine;

namespace com.SSS135.EditorAutomation.Runtime.Extensions
{
    public static class ExtensionsRuntimeAssetObjectRef
    {
        public static UnityEngine.Object? FindAssetObject(this AssetObjectRef? assetObjectRef)
        {
            return FindAssetObject<UnityEngine.Object>(assetObjectRef);
        }

        public static UnityEngine.Object? FindAssetObject(this AssetObjectRef? assetObjectRef, System.Type type)
        {
            if (assetObjectRef == null)
                return null;

            if (type == null)
                throw new System.ArgumentNullException(nameof(type));

#if UNITY_EDITOR
            if (assetObjectRef.InstanceID != UnityEngine.EntityId.None)
            {
                var obj = UnityEditor.EditorUtility.EntityIdToObject(assetObjectRef.InstanceID);
                if (obj != null && type.IsAssignableFrom(obj.GetType()))
                    return obj;

                var assetPath = UnityEditor.AssetDatabase.GetAssetPath(assetObjectRef.InstanceID);
                var asset = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath)
                    .FirstOrDefault(asset => asset != null && type.IsAssignableFrom(asset.GetType()));
                if (asset != null)
                    return asset;
            }

            if (!string.IsNullOrEmpty(assetObjectRef.AssetPath))
            {
                var result = UnityEditor.AssetDatabase.LoadAssetAtPath(assetObjectRef.AssetPath, type);
                if (result == null)
                {
                    // Fallback: Try loading all assets and finding the one of the correct type
                    var asset = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetObjectRef.AssetPath)
                        .FirstOrDefault(asset => asset != null && type.IsAssignableFrom(asset.GetType()));
                    if (asset != null)
                        return asset;
                }
                return result;
            }

            if (!string.IsNullOrEmpty(assetObjectRef.AssetGuid))
            {
                var assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(assetObjectRef.AssetGuid);
                var asset = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath)
                    .FirstOrDefault(asset => asset != null && type.IsAssignableFrom(asset.GetType()));
                if (asset != null)
                    return asset;
            }
#endif

            return null;
        }

        public static T? FindAssetObject<T>(this AssetObjectRef? assetObjectRef) where T : UnityEngine.Object
        {
            if (assetObjectRef == null)
                return null;

#if UNITY_EDITOR
            if (assetObjectRef.InstanceID != UnityEngine.EntityId.None)
            {
                var obj = UnityEditor.EditorUtility.EntityIdToObject(assetObjectRef.InstanceID);
                return obj as T;
            }

            if (!string.IsNullOrEmpty(assetObjectRef.AssetPath))
            {
                var result = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(assetObjectRef.AssetPath);
                if (result == null)
                {
                    // Fallback: Try loading all assets and finding the one of the correct type
                    var allAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetObjectRef.AssetPath);
                    foreach (var asset in allAssets)
                    {
                        if (asset is T typedAsset)
                        {
                            result = typedAsset;
                            break;
                        }
                    }
                }
                return result;
            }

            if (!string.IsNullOrEmpty(assetObjectRef.AssetGuid))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(assetObjectRef.AssetGuid);
                if (!string.IsNullOrEmpty(path))
                    return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
            }
#endif

            return null;
        }
        public static AssetObjectRef? ToAssetObjectRef(this UnityEngine.Object? obj)
        {
            if (obj == null)
                return new AssetObjectRef();

            return new AssetObjectRef(obj);
        }
    }
}
