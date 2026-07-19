using UnityEditor;
using UnityEngine;

namespace Light2D.Examples
{
    internal static class HingeJointTools
    {
        [MenuItem("CONTEXT/HingeJoint2D/Align Anchor To Connected Anchor")]
        private static void AlignAnchor(MenuCommand menuCommand)
        {
            var joint = (HingeJoint2D)menuCommand.context;
            if (joint.connectedBody == null)
                return;

            Undo.RecordObject(joint, "Align Hinge Anchor");
            var worldAnchor = joint.connectedBody.transform.TransformPoint(joint.connectedAnchor);
            joint.anchor = joint.transform.InverseTransformPoint(worldAnchor);
            EditorUtility.SetDirty(joint);
        }
    }
}
