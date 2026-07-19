using UnityEditor;

namespace Light2D.Examples
{
    [CustomEditor(typeof(BlockSetProfile))]
    internal sealed class BlockSetProfileEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Rocket uses this asset to generate colliding, emissive, and light-obstacle geometry.",
                MessageType.Info);
            DrawDefaultInspector();
        }
    }
}
