using UnityEditor;

namespace Light2D.Examples
{
    [CustomEditor(typeof(InputManager))]
    internal sealed class InputManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Keyboard: arrows move, Space toggles landing gear, C drops flares, R restarts, and F1 pauses. Touch controls are enabled on mobile devices.",
                MessageType.Info);
            DrawDefaultInspector();
        }
    }
}
