using UnityEditor;
using UnityEngine;

namespace Light2D
{
    internal static class Light2DObstacleSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return new SettingsProvider("Project/Light2D", SettingsScope.Project)
            {
                label = "Light2D",
                keywords = new[] { "obstacle", "color", "light", "shadow", "textured", "solid" },
                guiHandler = _ =>
                {
                    var settings = Light2DObstacleSettings.GetOrCreate();
                    EditorGUI.BeginChangeCheck();
                    settings.TexturedMultiplicativeColor = EditorGUILayout.ColorField(
                        new GUIContent("Textured Obstacle Color",
                            "Default multiplicative color for textured obstacles (sprite/tilemap/textured mesh). " +
                            "White keeps the shadow shaped by the texture."),
                        settings.TexturedMultiplicativeColor, true, true, false);
                    settings.SolidMultiplicativeColor = EditorGUILayout.ColorField(
                        new GUIContent("Solid Obstacle Color",
                            "Default multiplicative color for solid/untextured mesh obstacles. Opaque black " +
                            "casts a full shadow; white is a silent no-op on solids."),
                        settings.SolidMultiplicativeColor, true, true, false);
                    settings.AdditiveColor = EditorGUILayout.ColorField(
                        new GUIContent("Additive Color", "Default additive color for new obstacles."),
                        settings.AdditiveColor, true, true, false);
                    if (EditorGUI.EndChangeCheck())
                        settings.Save();
                }
            };
        }
    }
}
