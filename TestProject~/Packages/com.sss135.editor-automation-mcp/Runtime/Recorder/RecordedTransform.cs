using UnityEngine;

namespace com.SSS135.EditorAutomation.Recorder
{
    /// <summary>
    /// Drag-on convenience recorder: logs this object's transform each frame so an
    /// LLM can query its motion without any code instrumentation. Identity keys on
    /// this component's <c>GetInstanceID()</c> (it is a <see cref="UnityEngine.Object"/>),
    /// so its hierarchy path and parent are captured automatically on first sight.
    /// AOT-safe — no reflection. Inert while no session is active.
    /// </summary>
    [AddComponentMenu("Editor Automation/Recorded Transform")]
    public sealed class RecordedTransform : MonoBehaviour
    {
        [SerializeField] bool recordPosition = true;
        [SerializeField] bool recordRotation = true;
        [SerializeField] bool recordScale = false;

        [Tooltip("Optional prefix prepended to the recorded tags (e.g. \"body.\" -> \"body.pos\").")]
        [SerializeField] string tagPrefix = "";

        void LateUpdate()
        {
            if (Recorder.ActiveSession == null)
                return;

            var prefix = tagPrefix ?? "";
            var t = transform;

            if (recordPosition)
                Recorder.LogVariable(this, prefix + "pos", t.position);
            if (recordRotation)
                Recorder.LogVariable(this, prefix + "rot", t.rotation);
            if (recordScale)
                Recorder.LogVariable(this, prefix + "scale", t.localScale);
        }
    }
}
