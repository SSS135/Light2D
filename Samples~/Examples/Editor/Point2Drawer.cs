using UnityEditor;

namespace Light2D.Examples
{
    [CustomEditor(typeof(SimpleExamplesController))]
    internal sealed class SimpleExamplesControllerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Primary click or one-finger tap changes the example. Secondary click or a two-finger tap changes light color.",
                MessageType.Info);
            DrawDefaultInspector();
        }
    }
}
