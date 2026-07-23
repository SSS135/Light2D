using UnityEditor;

namespace Light2D
{
    /// <summary>
    /// Shared inspector for the Light2D obstacle components. It draws the default inspector minus the
    /// <c>Material</c> slot: that slot only supplies the obstacle-writing shader (no user-meaningful values),
    /// so it is hidden here and auto-seeded from the packaged default on Reset (see each component's Reset and
    /// <see cref="Light2DDefaultResources.ObstacleMaterial"/>). A power user who needs a custom-shader obstacle
    /// material can still assign it through Unity's Debug inspector, mirroring the LightingSystem
    /// composition-material treatment. This never affects <see cref="LightSprite"/>, which keeps its own
    /// <see cref="LightSpriteEditor"/> and drives the shared <see cref="CustomSprite.Material"/> slot through
    /// its Shadows/Custom-material flow.
    /// </summary>
    internal abstract class Light2DObstacleEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            // Reproduce the default inspector (disabled script field included) while skipping the Material
            // field, so any field the components gain later still shows without the hidden slot leaking back.
            serializedObject.Update();
            var property = serializedObject.GetIterator();
            var enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.name == "Material")
                    continue;

                using (new EditorGUI.DisabledScope(property.propertyPath == "m_Script"))
                    EditorGUILayout.PropertyField(property, true);
            }
            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(LightObstacleSprite))]
    [CanEditMultipleObjects]
    internal sealed class LightObstacleSpriteEditor : Light2DObstacleEditor { }

    [CustomEditor(typeof(LightObstacleMesh))]
    [CanEditMultipleObjects]
    internal sealed class LightObstacleMeshEditor : Light2DObstacleEditor { }

    [CustomEditor(typeof(LightObstacleGenerator))]
    [CanEditMultipleObjects]
    internal sealed class LightObstacleGeneratorEditor : Light2DObstacleEditor { }

#if LIGHT2D_TILEMAP
    [CustomEditor(typeof(LightObstacleTilemap))]
    [CanEditMultipleObjects]
    internal sealed class LightObstacleTilemapEditor : Light2DObstacleEditor { }
#endif
}
