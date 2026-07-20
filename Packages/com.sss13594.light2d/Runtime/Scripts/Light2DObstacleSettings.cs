#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Light2D
{
    // Edit-time-only obstacle authoring defaults, persisted OUTSIDE the AssetDatabase at
    // ProjectSettings/Light2DObstacleSettings.asset. internal + #if UNITY_EDITOR, so it is invisible to
    // ContractTests' exported-type check and stripped from player builds (no runtime dependency).
    internal sealed class Light2DObstacleSettings : ScriptableObject
    {
        internal const string AssetPath = "ProjectSettings/Light2DObstacleSettings.asset";

        public Color TexturedMultiplicativeColor = Color.white;                 // textured obstacles
        public Color SolidMultiplicativeColor = new Color(0f, 0f, 0f, 1f);      // solid/untextured obstacles
        public Color AdditiveColor = Color.clear;

        private static Light2DObstacleSettings _instance;

        internal static Light2DObstacleSettings GetOrCreate()
        {
            if (_instance != null) return _instance;
            var loaded = InternalEditorUtility.LoadSerializedFileAndForget(AssetPath);
            _instance = loaded != null && loaded.Length > 0
                ? loaded[0] as Light2DObstacleSettings
                : CreateInstance<Light2DObstacleSettings>();      // in-memory defaults; not written until Save()
            _instance.hideFlags = HideFlags.HideAndDontSave;
            return _instance;
        }

        internal void Save() => InternalEditorUtility.SaveToSerializedFileAndForget(
            new Object[] { this }, AssetPath, true);

        // Chooses the multiplicative default for a source that is/ isn't textured.
        internal Color MultiplicativeFor(bool textured) => textured ? TexturedMultiplicativeColor : SolidMultiplicativeColor;
    }
}
#endif
