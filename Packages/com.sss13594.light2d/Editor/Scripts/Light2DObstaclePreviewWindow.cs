using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Light2D
{
    /// <summary>
    /// Dockable editor window (Workstream B1) that previews the live lighting-input buffers a
    /// <see cref="LightingSystem"/> renders - what the lighting sees rather than the composited game image.
    /// The <c>Obstacles</c> buffer works in both backends (URP in edit and play mode via the gated copy pass;
    /// Built-in in play mode only); the <c>Light Sources</c> and <c>Ambient</c> buffers are produced by the
    /// Built-in backend only. The window throttles its repaint to ~30 Hz off <see cref="EditorApplication.update"/>
    /// and, each tick, asks the target system to run its URP obstacle-preview copy (a zero-cost stamp under
    /// Built-in), so the buffer stays live without a Game-view interaction.
    /// </summary>
    internal sealed class Light2DObstaclePreviewWindow : EditorWindow
    {
        // Which lighting-input buffer to draw. Obstacles is first so it is the serialized default.
        private enum PreviewBuffer
        {
            Obstacles,
            LightSources,
            Ambient
        }

        // ~30 Hz repaint cadence; the window is only repainted when this much wall-clock time has elapsed since
        // the last repaint, keeping the preview live without repainting on every editor-update tick.
        private const double RepaintInterval = 1.0 / 30.0;

        // Persisted across repaints and (best-effort) domain reloads by EditorWindow serialization. The target is
        // re-resolved whenever it becomes null or leaves the active-instance set.
        [SerializeField] private PreviewBuffer _buffer = PreviewBuffer.Obstacles;
        [SerializeField] private LightingSystem _target;

        private double _lastRepaintTime;

        [MenuItem("Window/Light2D/Obstacle Preview")]
        internal static void Open()
        {
            GetWindow<Light2DObstaclePreviewWindow>("Obstacle Preview");
        }

        private void OnEnable()
        {
            minSize = new Vector2(240f, 200f);
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            EnsureTarget(LightingSystem.Instances);

            // Keep the URP copy pass alive while the window is open (self-expiring stamp; a no-op under Built-in).
            if (_target != null)
                _target.RequestObstaclePreview();

            double now = EditorApplication.timeSinceStartup;
            if (now - _lastRepaintTime < RepaintInterval)
                return;
            _lastRepaintTime = now;
            Repaint();
        }

        private void OnGUI()
        {
            var instances = LightingSystem.Instances;
            EnsureTarget(instances);
            DrawToolbar(instances);

            if (instances.Count == 0 || _target == null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(
                    "No Light2D System was found in the open scenes. Add a Light2D System to a camera " +
                    "(Window > Light2D, or the Light2D System component) to preview its lighting buffers.",
                    MessageType.Info);
                return;
            }

            // Also request from OnGUI so interactive repaints (window drag/resize) keep the URP copy fresh
            // between editor-update ticks.
            _target.RequestObstaclePreview();

            if (!TryResolvePreview(_target, out var texture, out var message, out var messageType))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(message, messageType);
                return;
            }

            if (_buffer != PreviewBuffer.Obstacles)
                EditorGUILayout.HelpBox("This buffer is produced by the Built-in Render Pipeline only.",
                    MessageType.None);

            // Reserve the remaining window area every event so layout stays consistent, then draw the buffer
            // aspect-correct on Repaint. DrawPreviewTexture is used (not GUI.DrawTexture) because the obstacle
            // buffer clears to alpha 0 - GUI.DrawTexture would render nothing; DrawPreviewTexture shows opaque RGB.
            var content = GUILayoutUtility.GetRect(0f, 0f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (Event.current.type == EventType.Repaint && texture != null)
                EditorGUI.DrawPreviewTexture(content, texture, null, ScaleMode.ScaleToFit);
        }

        private void DrawToolbar(IReadOnlyList<LightingSystem> instances)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _buffer = (PreviewBuffer)EditorGUILayout.EnumPopup(_buffer, EditorStyles.toolbarPopup,
                    GUILayout.Width(120f));

                using (new EditorGUI.DisabledScope(instances.Count == 0))
                {
                    int currentIndex = IndexOf(instances, _target);
                    string[] names = instances.Count == 0
                        ? new[] { "None" }
                        : BuildTargetNames(instances);
                    int newIndex = EditorGUILayout.Popup(Mathf.Max(currentIndex, 0), names,
                        EditorStyles.toolbarPopup, GUILayout.Width(220f));
                    if (instances.Count > 0 && newIndex != currentIndex && newIndex >= 0 && newIndex < instances.Count)
                        _target = instances[newIndex];
                }

                GUILayout.FlexibleSpace();
            }
        }

        // Resolves the buffer texture to draw, or the empty-state message to show, for the current backend and
        // play state. Returns true with a non-null texture when there is something to draw.
        private bool TryResolvePreview(LightingSystem system, out Texture texture, out string message,
            out MessageType messageType)
        {
            texture = null;
            message = null;
            messageType = MessageType.Info;
            bool isUrp = GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset;

            switch (_buffer)
            {
                case PreviewBuffer.Obstacles:
                    if (isUrp)
                    {
                        var status = Light2DUniversalRendererFeatureInstaller.GetStatus(system.SourceCamera, out _);
                        if (status != Light2DRendererFeatureStatus.Installed)
                        {
                            message = "The Light2D renderer feature is not installed on this camera's renderer. " +
                                      "Install it from the Light2D System inspector or Tools > Light2D > " +
                                      "Universal Render Pipeline to preview obstacles.";
                            messageType = MessageType.Warning;
                            return false;
                        }
                        // URP path: the gated copy pass writes the resolved obstacles here (edit and play mode).
                        texture = system.ObstaclePreviewTexture;
                    }
                    else if (!Application.isPlaying)
                    {
                        message = "Obstacle preview under the Built-in Render Pipeline is available in Play mode.";
                        return false;
                    }
                    else
                    {
                        // Built-in play mode: the obstacle RT is built in OnRenderImage.
                        texture = system.ObstaclesTexture;
                    }
                    break;

                case PreviewBuffer.LightSources:
                case PreviewBuffer.Ambient:
                    if (isUrp)
                    {
                        message = "Light Sources and Ambient previews are available under the Built-in Render " +
                                  "Pipeline only.";
                        return false;
                    }
                    if (!Application.isPlaying)
                    {
                        message = "Light Sources and Ambient previews under the Built-in Render Pipeline are " +
                                  "available in Play mode.";
                        return false;
                    }
                    texture = _buffer == PreviewBuffer.LightSources
                        ? system.LightSourcesTexture
                        : system.AmbientTexture;
                    break;
            }

            if (texture == null)
            {
                message = "Waiting for the first rendered frame...";
                return false;
            }
            return true;
        }

        // Keeps _target pointing at a live, registered system. Re-resolves when the target is null, destroyed, or
        // no longer active. Default preference: the system whose camera is Camera.main, else the first active one.
        private void EnsureTarget(IReadOnlyList<LightingSystem> instances)
        {
            if (_target != null && IndexOf(instances, _target) >= 0)
                return;
            _target = ResolveDefaultTarget(instances);
        }

        private static LightingSystem ResolveDefaultTarget(IReadOnlyList<LightingSystem> instances)
        {
            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                for (int i = 0; i < instances.Count; i++)
                {
                    if (instances[i] != null && instances[i].SourceCamera == mainCamera)
                        return instances[i];
                }
            }
            for (int i = 0; i < instances.Count; i++)
            {
                if (instances[i] != null)
                    return instances[i];
            }
            return null;
        }

        private static string[] BuildTargetNames(IReadOnlyList<LightingSystem> instances)
        {
            var names = new string[instances.Count];
            for (int i = 0; i < instances.Count; i++)
            {
                var system = instances[i];
                if (system == null)
                {
                    names[i] = "<missing>";
                    continue;
                }
                var camera = system.SourceCamera;
                names[i] = camera != null && camera.name != system.gameObject.name
                    ? $"{system.gameObject.name} ({camera.name})"
                    : system.gameObject.name;
            }
            return names;
        }

        private static int IndexOf(IReadOnlyList<LightingSystem> instances, LightingSystem target)
        {
            for (int i = 0; i < instances.Count; i++)
            {
                if (instances[i] == target)
                    return i;
            }
            return -1;
        }
    }
}
