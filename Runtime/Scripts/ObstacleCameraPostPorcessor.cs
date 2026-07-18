using System;
using System.Collections.Generic;
using UnityEngine;

namespace Light2D
{
    /// <summary>
    /// This class apply post processing effect to light obstacles texture.
    /// It is drawing one pixel wide white border on light obstacles texture.
    /// Whithout it light sources with off screen origin may not work.
    /// </summary>
    public sealed class ObstacleCameraPostPorcessor : IDisposable
    {
        private Mesh _mesh;
        private Material _material;
        private Point2 _oldCameraSize;
        private float _oldPixelWidth = -1;
        private bool _disposed;
        private readonly List<Color32> _colors32 = new List<Color32>();
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<int> _indices = new List<int>();

        public ObstacleCameraPostPorcessor()
        {
            if (_material == null)
            {
                var shader = Shader.Find("Light2D/Obstacle Texture Post Porcessor");
                if (shader == null)
                    Debug.LogError("Shader 'Light2D/Obstacle Texture Post Porcessor' was not found. Obstacle borders will not be rendered.");
                else
                {
                    _material = new Material(shader);
                    _material.name = "Generated Obstacle Texture Post Processor Material";
                    _material.hideFlags = HideFlags.DontSave;
                }
            }
        }

        public void DrawMesh(Camera camera, float pixelWidth, int obstacleLayer)
        {
            if (_disposed || camera == null || _material == null || camera.pixelWidth <= 0 || camera.pixelHeight <= 0)
                return;

            var camSize = new Point2(Mathf.RoundToInt(camera.pixelWidth), Mathf.RoundToInt(camera.pixelHeight));
            if (_oldCameraSize != camSize || !Mathf.Approximately(_oldPixelWidth, pixelWidth) || _mesh == null)
            {
                _oldCameraSize = camSize;
                _oldPixelWidth = pixelWidth;
                CreateMesh(camera, pixelWidth);
            }

            Graphics.DrawMesh(_mesh, camera.transform.position, camera.transform.rotation, _material,
                obstacleLayer, camera);
        }

        /// <summary>
        /// Generating mesh with one pixel wide white border.
        /// </summary>
        private void CreateMesh(Camera camera, float pixelWidth)
        {
            var pixelSize = new Vector2(1f/camera.pixelWidth, 1f/camera.pixelHeight)*pixelWidth;

            _vertices.Clear();
            _colors32.Clear();
            _indices.Clear();

            CreateQuad(new Color32(0, 0, 0, 0), pixelSize, Vector2.one - pixelSize); // central
            CreateQuad(Color.white, Vector2.zero, new Vector2(pixelSize.x, 1)); // left
            CreateQuad(Color.white, new Vector2(1 - pixelSize.x, 0), Vector2.one); // right
            CreateQuad(Color.white, Vector2.zero, new Vector2(1, pixelSize.y)); // bottom
            CreateQuad(Color.white, new Vector2(0, 1 - pixelSize.y), Vector2.one); // top

            if (_mesh == null)
            {
                _mesh = new Mesh();
                _mesh.name = "Generated Obstacle Texture Post Processor Mesh";
                _mesh.hideFlags = HideFlags.DontSave;
            }

            _mesh.Clear();
            _mesh.vertices = _vertices.ToArray();
            _mesh.triangles = _indices.ToArray();
            _mesh.colors32 = _colors32.ToArray();
        }

        private void CreateQuad(Color32 color, Vector2 min, Vector2 max)
        {
            min = min*2 - Vector2.one;
            max = max*2 - Vector2.one;

            int startVertex = _vertices.Count;

            _indices.Add(0 + startVertex);
            _indices.Add(1 + startVertex);
            _indices.Add(3 + startVertex);
            _indices.Add(3 + startVertex);
            _indices.Add(1 + startVertex);
            _indices.Add(2 + startVertex);

            _vertices.Add(new Vector3(min.x, min.y, 1));
            _vertices.Add(new Vector3(min.x, max.y, 1));
            _vertices.Add(new Vector3(max.x, max.y, 1));
            _vertices.Add(new Vector3(max.x, min.y, 1));

            for (int i = 0; i < 4; i++)
                _colors32.Add(color);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Util.Destroy(_mesh);
            Util.Destroy(_material);
            _mesh = null;
            _material = null;
        }
    }
}
