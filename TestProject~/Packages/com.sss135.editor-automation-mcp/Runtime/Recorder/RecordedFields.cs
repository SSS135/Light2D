using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace com.SSS135.EditorAutomation.Recorder
{
    /// <summary>
    /// Drag-on convenience recorder for arbitrary script state: reflects each
    /// target component's <c>[Record]</c>-marked fields/properties once in
    /// <c>Awake</c>, caching the accessors, then logs their current values every
    /// frame in <c>LateUpdate</c> while a session is active. Each target keeps its
    /// own identity (its <c>GetInstanceID()</c>), so values land on the component
    /// that owns them. Inert while no session is active.
    ///
    /// Reflection works under the Editor's Mono runtime; IL2CPP/AOT builds would
    /// need <c>[Preserve]</c>/link.xml to keep the members — out of scope here
    /// (the recorder is an editor-only debugging tool).
    /// </summary>
    [AddComponentMenu("Editor Automation/Recorded Fields")]
    public sealed class RecordedFields : MonoBehaviour
    {
        [Tooltip("Components whose [Record]-marked members are logged. Empty = all sibling components on this GameObject.")]
        [SerializeField] Component[] targets;

        const BindingFlags MemberFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        struct RecordedMember
        {
            public Component Target;
            public string Tag;
            public Func<object, object> Get;
        }

        readonly List<RecordedMember> _members = new List<RecordedMember>();

        void Awake()
        {
            var sources = targets != null && targets.Length > 0
                ? targets
                : GetComponents<Component>();

            foreach (var target in sources)
            {
                if (target == null || target is RecordedFields)
                    continue;
                CacheMembers(target);
            }
        }

        void CacheMembers(Component target)
        {
            var type = target.GetType();

            foreach (var field in type.GetFields(MemberFlags))
            {
                var attr = field.GetCustomAttribute<RecordAttribute>();
                if (attr == null)
                    continue;

                var captured = field;
                _members.Add(new RecordedMember
                {
                    Target = target,
                    Tag = attr.Tag ?? field.Name,
                    Get = instance => captured.GetValue(instance)
                });
            }

            foreach (var property in type.GetProperties(MemberFlags))
            {
                var attr = property.GetCustomAttribute<RecordAttribute>();
                if (attr == null || !property.CanRead || property.GetIndexParameters().Length > 0)
                    continue;

                var getter = property.GetGetMethod(nonPublic: true);
                if (getter == null)
                    continue;

                var captured = property;
                _members.Add(new RecordedMember
                {
                    Target = target,
                    Tag = attr.Tag ?? property.Name,
                    Get = instance => captured.GetValue(instance)
                });
            }
        }

        void LateUpdate()
        {
            if (Recorder.ActiveSession == null || _members.Count == 0)
                return;

            for (int i = 0; i < _members.Count; i++)
            {
                var member = _members[i];
                if (member.Target == null)
                    continue;

                var value = member.Get(member.Target);
                Recorder.LogVariable(member.Target, member.Tag, value);
            }
        }
    }
}
