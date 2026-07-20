#nullable enable
using EA;

namespace com.SSS135.EditorAutomation.Runtime.Extensions
{
    public static class ExtensionsRuntimeObjectRef
    {
        public static UnityEngine.Object? FindObject(this ObjectRef? objectRef)
        {
            if (objectRef == null)
                return null;

#if UNITY_EDITOR
            if (objectRef.InstanceID != UnityEngine.EntityId.None)
            {
                return UnityEditor.EditorUtility.EntityIdToObject(objectRef.InstanceID);
            }
#endif
            return null;
        }
        public static ObjectRef? ToObjectRef(this UnityEngine.Object? obj)
        {
            return new ObjectRef(obj);
        }
    }
}
