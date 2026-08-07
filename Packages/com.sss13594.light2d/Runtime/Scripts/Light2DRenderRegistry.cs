using System;
using System.Collections.Generic;
using UnityEngine;

namespace Light2D
{
    /// <summary>
    /// Static register-and-draw surface for the obstacle, ambient-emitter and coverage-volume roles.
    /// Light sources keep their existing registry on <see cref="LightSprite"/>, so this class only owns
    /// those roles. Mirrors the lifecycle of <see cref="LightSprite"/>: components self-register in
    /// OnEnable and deregister in OnDisable/OnDestroy, and a SubsystemRegistration reset clears then
    /// re-finds active instances after a domain reload.
    /// </summary>
    internal static class Light2DRenderRegistry
    {
        private static readonly HashSet<ILight2DObstacle> Obstacles = new HashSet<ILight2DObstacle>();
        private static readonly HashSet<ILight2DAmbientEmitter> AmbientEmitters = new HashSet<ILight2DAmbientEmitter>();
        private static readonly HashSet<ILight2DCoverage> CoverageVolumes = new HashSet<ILight2DCoverage>();

        // Reused across snapshots so per-frame preparation allocates nothing steady-state.
        private static readonly List<Sortable> Scratch = new List<Sortable>();
        private static readonly Comparison<Sortable> CompareEntries = Compare;

        // Reused per multi-draw producer so its GetDrawEntries fill allocates nothing steady-state.
        private static readonly List<Light2DDrawEntry> EntryScratch = new List<Light2DDrawEntry>();

        internal static void RegisterObstacle(ILight2DObstacle obstacle)
        {
            if (obstacle != null)
                Obstacles.Add(obstacle);
        }

        internal static void DeregisterObstacle(ILight2DObstacle obstacle)
        {
            Obstacles.Remove(obstacle);
        }

        internal static void RegisterAmbient(ILight2DAmbientEmitter emitter)
        {
            if (emitter != null)
                AmbientEmitters.Add(emitter);
        }

        internal static void DeregisterAmbient(ILight2DAmbientEmitter emitter)
        {
            AmbientEmitters.Remove(emitter);
        }

        internal static void RegisterCoverage(ILight2DCoverage coverage)
        {
            if (coverage != null)
                CoverageVolumes.Add(coverage);
        }

        internal static void DeregisterCoverage(ILight2DCoverage coverage)
        {
            CoverageVolumes.Remove(coverage);
        }

        // Membership probes for tests; not part of the drawing path.
        internal static bool ContainsObstacle(ILight2DObstacle obstacle)
        {
            return Obstacles.Contains(obstacle);
        }

        internal static bool ContainsAmbient(ILight2DAmbientEmitter emitter)
        {
            return AmbientEmitters.Contains(emitter);
        }

        internal static bool ContainsCoverage(ILight2DCoverage coverage)
        {
            return CoverageVolumes.Contains(coverage);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()
        {
            Obstacles.Clear();
            AmbientEmitters.Clear();
            CoverageVolumes.Clear();

            // FindObjectsByType cannot take an interface, so scan every active MonoBehaviour once and
            // route each into the roles it implements. Snapshot preparation re-checks isActiveAndEnabled
            // through TryGetDrawEntry, so re-adding disabled components here is harmless.
            // The explicit sort mode keeps the 6000.3 floor: the single-argument overload only
            // exists from 6000.4. FindObjectsSortMode.None is what the shorter form does anyway.
            foreach (var behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (behaviour is ILight2DObstacle obstacle)
                    Obstacles.Add(obstacle);
                if (behaviour is ILight2DAmbientEmitter emitter)
                    AmbientEmitters.Add(emitter);
                if (behaviour is ILight2DCoverage coverage)
                    CoverageVolumes.Add(coverage);
            }
        }

        /// <summary>
        /// Culls, owner-filters and orders the obstacle registry into <paramref name="dst"/>.
        /// </summary>
        internal static void PrepareObstacleSnapshot(List<Light2DDrawEntry> dst, in Light2DCullContext ctx)
        {
            PrepareSnapshot(Obstacles, dst, ctx);
        }

        /// <summary>
        /// Culls, owner-filters and orders the ambient-emitter registry into <paramref name="dst"/>.
        /// </summary>
        internal static void PrepareAmbientSnapshot(List<Light2DDrawEntry> dst, in Light2DCullContext ctx)
        {
            PrepareSnapshot(AmbientEmitters, dst, ctx);
        }

        /// <summary>
        /// Culls, owner-filters and orders the coverage-volume registry into <paramref name="dst"/>.
        /// </summary>
        internal static void PrepareCoverageSnapshot(List<Light2DDrawEntry> dst, in Light2DCullContext ctx)
        {
            PrepareSnapshot(CoverageVolumes, dst, ctx);
        }

        private static void PrepareSnapshot<T>(HashSet<T> registry, List<Light2DDrawEntry> dst,
            in Light2DCullContext ctx) where T : class, ILight2DDrawable
        {
            if (dst == null)
                throw new ArgumentNullException(nameof(dst));

            registry.RemoveWhere(IsMissing);
            dst.Clear();
            Scratch.Clear();

            foreach (var producer in registry)
            {
                var id = EntityIdOf(producer);
                // A multi-draw producer (e.g. the texture-aware Tilemap occluder) contributes one entry
                // per source-texture group; the single-entry majority keeps the allocation-free path.
                if (producer is ILight2DMultiDrawable multi)
                {
                    EntryScratch.Clear();
                    multi.GetDrawEntries(EntryScratch);
                    for (var e = 0; e < EntryScratch.Count; e++)
                        TryAdd(EntryScratch[e], id, ctx);
                }
                else if (producer.TryGetDrawEntry(out var entry))
                {
                    TryAdd(entry, id, ctx);
                }
            }

            Scratch.Sort(CompareEntries);
            for (var i = 0; i < Scratch.Count; i++)
                dst.Add(Scratch[i].Entry);
            Scratch.Clear();
        }

        // Extracted so single- and multi-entry producers share the owner filter + frustum cull + sort
        // record. Owner scoping replaces layer scoping: an entry with an explicit Owner only draws for
        // that system; an unowned entry draws for any system. Mirrors LightSprite.PrepareSnapshot.
        private static void TryAdd(in Light2DDrawEntry entry, EntityId id, in Light2DCullContext ctx)
        {
            if (entry.Owner != null && entry.Owner != ctx.Owner)
                return;
            if (ctx.Planes != null && !GeometryUtility.TestPlanesAABB(ctx.Planes, entry.WorldBounds))
                return;
            Scratch.Add(new Sortable(entry, id));
        }

        private static bool IsMissing<T>(T producer) where T : class, ILight2DDrawable
        {
            // Interface references skip Unity's null overload, so cast to Object to catch destroyed
            // components that never got a chance to deregister.
            return !(producer is UnityEngine.Object obj) || obj == null;
        }

        private static EntityId EntityIdOf(ILight2DDrawable producer)
        {
            return producer is UnityEngine.Object obj ? obj.GetEntityId() : default;
        }

        private static int Compare(Sortable left, Sortable right)
        {
            var order = left.Entry.SortKey.CompareTo(right.Entry.SortKey);
            return order != 0 ? order : left.Id.CompareTo(right.Id);
        }

        private readonly struct Sortable
        {
            internal readonly Light2DDrawEntry Entry;
            internal readonly EntityId Id;

            internal Sortable(Light2DDrawEntry entry, EntityId id)
            {
                Entry = entry;
                Id = id;
            }
        }
    }
}
