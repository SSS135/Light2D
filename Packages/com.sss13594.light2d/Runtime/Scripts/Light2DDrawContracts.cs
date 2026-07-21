using System.Collections.Generic;
using UnityEngine;

namespace Light2D
{
    /// <summary>
    /// A lighting-input component that can hand the register-and-draw core a per-frame draw entry.
    /// Producers (sprite-mirroring, mesh-mirroring, self-owned-mesh, and later the Tilemap occluder)
    /// share this one path so the registry can treat them uniformly.
    /// </summary>
    internal interface ILight2DDrawable
    {
        /// <summary>
        /// Builds this frame's draw entry from the component's own mesh, material, matrix and bounds.
        /// Returns false to skip drawing this frame (disabled, no mesh/material, prefab stage, etc.).
        /// </summary>
        bool TryGetDrawEntry(out Light2DDrawEntry entry);
    }

    /// <summary>
    /// Role marker for components that draw into the obstacle render target.
    /// </summary>
    internal interface ILight2DObstacle : ILight2DDrawable
    {
    }

    /// <summary>
    /// Role marker for components that draw into the ambient emission render target.
    /// </summary>
    internal interface ILight2DAmbientEmitter : ILight2DDrawable
    {
    }

    /// <summary>
    /// Role marker for components that draw into the coverage mask render target.
    /// </summary>
    internal interface ILight2DCoverage : ILight2DDrawable
    {
    }

    /// <summary>
    /// Optional capability for producers that contribute MORE THAN ONE draw entry per frame (e.g. a
    /// texture-aware Tilemap occluder that emits one single-submesh mesh + material per source texture).
    /// The registry prefers this over ILight2DDrawable.TryGetDrawEntry when a producer implements it.
    /// Entries are appended to the caller-owned buffer (add zero to skip this frame); the buffer is
    /// reused across frames, so implementations MUST NOT clear it.
    /// </summary>
    internal interface ILight2DMultiDrawable
    {
        void GetDrawEntries(List<Light2DDrawEntry> buffer);
    }

    /// <summary>
    /// Immutable snapshot of one draw the render core issues via <see cref="Light2DDraw"/>.
    /// Built on the main thread by a producer's <see cref="ILight2DDrawable.TryGetDrawEntry"/> and
    /// consumed inside a raster pass, so it must never hold live component state that a render thread
    /// would read.
    /// </summary>
    internal readonly struct Light2DDrawEntry
    {
        public readonly Mesh Mesh;
        public readonly Material Material;
        public readonly Matrix4x4 Matrix;
        public readonly Bounds WorldBounds;
        public readonly int SubmeshCount;
        public readonly int SortKey;

        // Optional per-system scoping that replaces the retired layer scoping. Null means "any system".
        public readonly LightingSystem Owner;

        internal Light2DDrawEntry(Mesh mesh, Material material, Matrix4x4 matrix, Bounds worldBounds,
            int submeshCount, int sortKey, LightingSystem owner)
        {
            Mesh = mesh;
            Material = material;
            Matrix = matrix;
            WorldBounds = worldBounds;
            SubmeshCount = submeshCount;
            SortKey = sortKey;
            Owner = owner;
        }
    }

    /// <summary>
    /// Per-snapshot culling inputs. The backend caches the light-camera frustum planes once per frame
    /// (via <see cref="GeometryUtility.CalculateFrustumPlanes(Matrix4x4, Plane[])"/>) and an optional
    /// owning <see cref="LightingSystem"/>; the registry then does the cheap per-entry AABB test and
    /// owner filter. A default context (null planes, null owner) culls nothing and filters nothing.
    /// </summary>
    internal readonly struct Light2DCullContext
    {
        // Cached frustum planes (length 6 when present). Null disables frustum culling.
        public readonly Plane[] Planes;

        // Optional owning system. Null includes every producer regardless of its Owner.
        public readonly LightingSystem Owner;

        internal Light2DCullContext(Plane[] planes, LightingSystem owner)
        {
            Planes = planes;
            Owner = owner;
        }
    }
}
