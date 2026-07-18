using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace com.SSS135.EditorAutomation.Recorder
{
    /// <summary>
    /// Derives stable string keys and human labels for recorded objects.
    /// Keys must stay distinct for same-named objects, so Unity objects key on
    /// <c>GetEntityId()</c> (the non-deprecated instance id); name/path are labels only.
    /// </summary>
    public static class Identity
    {
        /// <summary>
        /// Stable key for any logged object: Unity objects → <c>"iid:&lt;id&gt;"</c>,
        /// dev-supplied strings pass through verbatim, anything else →
        /// <c>"oid:&lt;runtime-hash&gt;"</c>.
        /// </summary>
        public static string KeyFor(object o)
        {
            switch (o)
            {
                case null:
                    return "oid:0";
                case string s:
                    return s;
                case UnityEngine.Object uo:
                    return "iid:" + uo.GetEntityId();
                default:
                    return "oid:" + RuntimeHelpers.GetHashCode(o);
            }
        }

        /// <summary>
        /// Label / hierarchy path / parent key for a Unity object. Components and
        /// GameObjects resolve a transform path; other Unity objects report a
        /// type-name label with no path/parent.
        /// </summary>
        public static (string label, string path, string parentKey) DescribeUnityObject(UnityEngine.Object o)
        {
            if (o == null)
                return (null, null, null);

            var transform = TransformOf(o);
            if (transform == null)
                return (o.name, null, null);

            var label = transform.name;
            var path = HierarchyPath(transform);
            var parentKey = transform.parent != null
                ? "iid:" + transform.parent.GetEntityId()
                : null;

            return (label, path, parentKey);
        }

        static Transform TransformOf(UnityEngine.Object o)
        {
            switch (o)
            {
                case GameObject go: return go.transform;
                case Component c: return c.transform;
                default: return null;
            }
        }

        static string HierarchyPath(Transform t)
        {
            var sb = new StringBuilder(t.name);
            var parent = t.parent;
            while (parent != null)
            {
                sb.Insert(0, '/');
                sb.Insert(0, parent.name);
                parent = parent.parent;
            }
            return sb.ToString();
        }
    }
}
