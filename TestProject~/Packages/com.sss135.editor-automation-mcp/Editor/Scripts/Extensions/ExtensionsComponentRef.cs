#nullable enable
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.ReflectorNet.Utils;
using EA;

namespace com.SSS135.EditorAutomation.Runtime.Extensions
{
    public static class ExtensionsComponentRef
    {
        public static bool Matches(this ComponentRef componentRef, UnityEngine.Component component, int? index = null)
        {
            if (componentRef.InstanceID != UnityEngine.EntityId.None)
            {
                return componentRef.InstanceID == component?.GetEntityId();
            }
            if (componentRef.Index >= 0 && index != null)
            {
                return componentRef.Index == index.Value;
            }
            if (!StringUtils.IsNullOrEmpty(componentRef.TypeName))
            {
                var type = component?.GetType() ?? typeof(UnityEngine.Component);
                return type.IsMatch(componentRef.TypeName);
            }
            if (componentRef.InstanceID == UnityEngine.EntityId.None && component == null)
            {
                return true; // Matches null component
            }
            return false;
        }
    }
}
