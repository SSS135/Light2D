using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Light2D.Examples
{
    /// <summary>
    /// Combines child mesh renderers into one mesh at startup.
    /// It gives much better performance for small meshes than StaticBatchingUtility.Combine.
    /// </summary>
    public class MeshCombiner : MonoBehaviour
    {
        private Coroutine _combineRoutine;
        private bool _isDone;

        private void OnEnable()
        {
            if (!_isDone && _combineRoutine == null)
                _combineRoutine = StartCoroutine(Combine());
        }

        private void OnDisable()
        {
            if (_combineRoutine == null)
                return;

            StopCoroutine(_combineRoutine);
            _combineRoutine = null;
        }

        private IEnumerator Combine()
        {
            yield return null;

            var groups = new Dictionary<GroupKey, List<MeshPart>>();
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null)
                    continue;

                var key = new GroupKey
                {
                    Material = renderer.sharedMaterial,
                    SortingOrder = renderer.sortingOrder,
                    Layer = renderer.gameObject.layer
                };
                if (!groups.TryGetValue(key, out var group))
                {
                    group = new List<MeshPart>();
                    groups.Add(key, group);
                }

                group.Add(new MeshPart(renderer, filter));
            }

            var vertices = new List<Vector3>(1024);
            var triangles = new List<int>(1024);
            var uv0 = new List<Vector2>(1024);
            var uv1 = new List<Vector2>(1024);
            var colors = new List<Color>(1024);
            var tangents = new List<Vector4>(1024);

            foreach (var groupEntry in groups)
            {
                var obj = new GameObject("Combined Mesh");
                obj.transform.SetParent(transform, false);
                obj.layer = groupEntry.Key.Layer;

                var meshRenderer = obj.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = groupEntry.Key.Material;
                meshRenderer.sortingOrder = groupEntry.Key.SortingOrder;

                var firstMesh = groupEntry.Value[0].Filter.sharedMesh;

                bool useUv0 = firstMesh.uv != null && firstMesh.uv.Length != 0;
                bool useUv1 = firstMesh.uv2 != null && firstMesh.uv2.Length != 0;
                bool useColors = firstMesh.colors != null && firstMesh.colors.Length != 0;

                vertices.Clear();
                triangles.Clear();
                tangents.Clear();

                if (useUv0) uv0.Clear();
                if (useUv1) uv1.Clear();
                if (useColors) colors.Clear();

                foreach (var meshPart in groupEntry.Value)
                {
                    var filter = meshPart.Filter;
                    var smallMesh = filter.sharedMesh;
                    var startVertexIndex = vertices.Count;
                    foreach (var vertex in smallMesh.vertices)
                        vertices.Add(transform.InverseTransformPoint(filter.transform.TransformPoint(vertex)));
                    foreach (var triangle in smallMesh.triangles)
                        triangles.Add(triangle + startVertexIndex);

                    var localTangents = smallMesh.tangents;
                    if (localTangents.Length == 0)
                    {
                        for (var index = 0; index < smallMesh.vertexCount; index++)
                            tangents.Add(new Vector4(1, 0, 0, 1));
                    }
                    else
                    {
                        foreach (var tangent in localTangents)
                        {
                            var transformed = transform.InverseTransformVector(filter.transform.TransformVector(tangent));
                            tangents.Add(new Vector4(transformed.x, transformed.y, transformed.z, tangent.w));
                        }
                    }

                    if (useUv0) uv0.AddRange(smallMesh.uv);
                    if (useUv1) uv1.AddRange(smallMesh.uv2);
                    if (useColors) colors.AddRange(smallMesh.colors);

                    var customSprite = meshPart.Renderer.GetComponent<CustomSprite>();
                    Destroy(meshPart.Renderer);
                    Destroy(filter);
                    if (customSprite != null)
                        Destroy(customSprite);
                }

                var meshFilter = obj.AddComponent<MeshFilter>();
                var mesh = new Mesh { name = $"{name} Combined Mesh" };
                meshFilter.sharedMesh = mesh;
                // GeneratedMeshOwner destroys the generated mesh together with the combined-mesh object.
                obj.AddComponent<GeneratedMeshOwner>().Mesh = mesh;

                mesh.vertices = vertices.ToArray();
                mesh.triangles = triangles.ToArray();
                mesh.tangents = tangents.ToArray();
                if (useUv0) mesh.uv = uv0.ToArray();
                if (useUv1) mesh.uv2 = uv1.ToArray();
                if (useColors) mesh.colors = colors.ToArray();
                mesh.RecalculateBounds();
            }

            _isDone = true;
            _combineRoutine = null;
        }

        private readonly struct MeshPart
        {
            public readonly MeshRenderer Renderer;
            public readonly MeshFilter Filter;

            public MeshPart(MeshRenderer renderer, MeshFilter filter)
            {
                Renderer = renderer;
                Filter = filter;
            }
        }

        struct GroupKey : IEquatable<GroupKey>
        {
            public Material Material;
            public int SortingOrder;
            public int Layer;

            public bool Equals(GroupKey other)
            {
                return Equals(Material, other.Material) && SortingOrder == other.SortingOrder && Layer == other.Layer;
            }

            public override bool Equals(object obj)
            {
                return obj is GroupKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = (Material != null ? Material.GetHashCode() : 0);
                    hashCode = (hashCode*397) ^ SortingOrder;
                    hashCode = (hashCode*397) ^ Layer;
                    return hashCode;
                }
            }

            public static bool operator ==(GroupKey left, GroupKey right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(GroupKey left, GroupKey right)
            {
                return !left.Equals(right);
            }
        }
    }
}
