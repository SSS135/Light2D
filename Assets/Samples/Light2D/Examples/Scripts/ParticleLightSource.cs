using System.Collections.Generic;
using UnityEngine;

namespace Light2D.Examples
{
    // Turns a ParticleSystem's live particles into individual Light2D point lights: one pooled LightSprite
    // tracks each particle so every droplet casts its own distinct pool of light through the light-sources
    // pass, with no ambient emitter involved. Each LateUpdate reads the live particles into a cached buffer,
    // moves one pooled light to each particle's world position, and colors it from the particle's current
    // color and alpha (so a light fades as its particle dies); pooled lights beyond the live count are parked.
    //
    // Three deliberate details: pooled lights are created inactive and fully configured before activation so a
    // new light never flashes for a frame at the origin; each light's world rotation is forced to identity
    // every frame because a point light's quad lives in the XY lighting plane, so inheriting a rotated emitter's
    // rotation would tilt the quad out of that plane and hide the light; and each light's local scale is set to
    // the inverse of the emitter's lossy scale every frame so a scaled emitter cannot stretch the Radius-sized
    // light quad (Radius stays in world units, and non-uniform scale no longer squashes the light into an ellipse).
    [RequireComponent(typeof(ParticleSystem))]
    public class ParticleLightSource : MonoBehaviour
    {
        [Tooltip("World-space radius of each per-particle point light (default 1.5).")]
        public float Radius = 1.5f;

        [Tooltip("Peak light intensity at full particle alpha; the live value is this scaled by each " +
                 "particle's current alpha, so lights dim as their particles fade out (default 2).")]
        public float Intensity = 2f;

        [Tooltip("How the light tracks the particle's fade: 1 = linear (light matches sprite alpha); above " +
                 "1 the light dies early (only the particle's bright phase illuminates); below 1 the light " +
                 "lingers while the sprite fades (default 1).")]
        [Range(0.25f, 4f)]
        public float AlphaResponse = 1f;

        [Tooltip("Tint multiplied into each particle's current color before it drives the light color " +
                 "(default white passes the particle color through unchanged).")]
        public Color ColorTint = Color.white;

        [Tooltip("Upper bound on simultaneously lit particles; particles past this count cast no light so " +
                 "the light-sources pass stays bounded (default 32).")]
        public int MaxLights = 32;

        [Tooltip("Shadow quality assigned to every pooled light; Balanced ray-marches obstacles at a moderate " +
                 "cost, and the light material auto-resolves from it (default Balanced).")]
        public LightSprite.ShadowQuality Shadows = LightSprite.ShadowQuality.Balanced;

        [Tooltip("0..1 edge hardness; below 0 adds a long, dim inverse-square tail (new -1..1 falloff " +
                 "range) - negative values suit glowing spray/embers (default 0.5).")]
        [Range(-1f, 1f)]
        public float Falloff = 0f;

        private ParticleSystem _particles;
        private ParticleSystem.Particle[] _buffer = new ParticleSystem.Particle[0];
        private readonly List<LightSprite> _pool = new List<LightSprite>();

        private void Awake()
        {
            _particles = GetComponent<ParticleSystem>();
            if (_particles == null)
                enabled = false;
        }

        private void LateUpdate()
        {
            if (_particles == null)
                return;

            // Grow the read buffer to the system's capacity so GetParticles never allocates per frame.
            int capacity = _particles.main.maxParticles;
            if (_buffer.Length < capacity)
                _buffer = new ParticleSystem.Particle[capacity];

            int alive = _particles.GetParticles(_buffer);
            int lit = Mathf.Min(alive, Mathf.Max(0, MaxLights));
            var main = _particles.main;
            var simulationSpace = main.simulationSpace;
            var scalingMode = main.scalingMode;
            var customSpace = main.customSimulationSpace;

            // Pooled lights are children of this (possibly scaled) emitter, so their draw matrix
            // (transform.localToWorldMatrix) would inherit the emitter's lossy scale and stretch each light's
            // Radius-sized quad - squashing the circle into an ellipse under non-uniform scale. Cancel it by
            // setting each light's local scale to the inverse of the emitter's lossy scale so the light's world
            // scale is identity and Radius stays in world units. Recomputed every frame, so it also tracks an
            // animated emitter scale; a zero component falls back to 1 to avoid a divide-by-zero degenerate quad.
            Vector3 lossyScale = transform.lossyScale;
            Vector3 inverseScale = new Vector3(
                Mathf.Approximately(lossyScale.x, 0f) ? 1f : 1f / lossyScale.x,
                Mathf.Approximately(lossyScale.y, 0f) ? 1f : 1f / lossyScale.y,
                Mathf.Approximately(lossyScale.z, 0f) ? 1f : 1f / lossyScale.z);

            for (int i = 0; i < lit; i++)
            {
                var particle = _buffer[i];
                var light = GetOrCreateLight(i);

                // Configure fully before the light is (re)activated so it never renders a stale frame.
                Color32 current = particle.GetCurrentColor(_particles);
                float alpha = current.a / 255f;
                light.Radius = Radius;
                light.Falloff = Falloff;
                light.Color = new Color(current.r / 255f * ColorTint.r, current.g / 255f * ColorTint.g,
                    current.b / 255f * ColorTint.b, 1f);
                // Shape the alpha->intensity mapping: AlphaResponse 1 is the original linear fade, while a
                // power curve makes the light die early (>1) or linger past the sprite (<1). Mathf.Pow(0, >0)
                // stays 0 so fully faded particles cast no light, and the 0.25..4 range never hits Pow(0,0)=1.
                light.Intensity = Intensity * Mathf.Pow(alpha, AlphaResponse);

                // Neutralize the inherited emitter scale, then place the light in world space: force identity
                // world rotation (a point light's quad must stay in the XY lighting plane) and set the world
                // position (not localPosition) so the emitter's scale and rotation cannot re-warp the placement.
                var lightTransform = light.transform;
                lightTransform.localScale = inverseScale;
                lightTransform.SetPositionAndRotation(
                    ParticleWorldPosition(particle, simulationSpace, scalingMode, customSpace),
                    Quaternion.identity);

                if (!light.gameObject.activeSelf)
                    light.gameObject.SetActive(true);
            }

            // Park the surplus pool entries so dead particles leave no lingering light.
            for (int i = lit; i < _pool.Count; i++)
            {
                if (_pool[i].gameObject.activeSelf)
                    _pool[i].gameObject.SetActive(false);
            }
        }

        private LightSprite GetOrCreateLight(int index)
        {
            while (_pool.Count <= index)
            {
                // Create inactive so OnEnable (registration + mesh build) runs only after this frame has
                // positioned and colored the light, avoiding a one-frame flash at the origin.
                var go = new GameObject("ParticleLight");
                go.transform.SetParent(transform, false);
                go.SetActive(false);
                var light = go.AddComponent<LightSprite>();
                light.Type = LightSprite.LightType.Point;
                light.Shadows = Shadows;
                light.Material = null; // auto-resolves the preset material from Shadows
                light.Radius = Radius;
                light.Falloff = Falloff;
                _pool.Add(light);
            }
            return _pool[index];
        }

        // Resolves a particle's position into world space for the pooled light, honoring the system's
        // simulation space. World is already world. Local is relative to this emitter, converted through the
        // matrix that matches the emitter's scalingMode so a scaled emitter places lights exactly where it
        // renders particles. Custom is relative to the assigned custom transform (full scale), with a null
        // fallback that treats the value as world.
        private Vector3 ParticleWorldPosition(in ParticleSystem.Particle particle,
            ParticleSystemSimulationSpace simulationSpace, ParticleSystemScalingMode scalingMode,
            Transform customSpace)
        {
            switch (simulationSpace)
            {
                case ParticleSystemSimulationSpace.Local:
                    return LocalSimulationMatrix(scalingMode).MultiplyPoint3x4(particle.position);
                case ParticleSystemSimulationSpace.Custom:
                    return customSpace != null ? customSpace.TransformPoint(particle.position) : particle.position;
                default:
                    return particle.position; // World
            }
        }

        // The local-simulation-space -> world matrix that matches Unity's own particle renderer for each
        // scalingMode: Hierarchy uses the full transform hierarchy (lossy scale); Local uses only this
        // emitter's own scale, ignoring parents; Shape bakes scale into the emitted source positions, so the
        // render transform carries no scale. World position and rotation always apply so a moved or rotated
        // emitter still tracks. (Only the emitter's own transform is scaled in the common single-system case,
        // where Hierarchy and Local coincide; the split matters when the emitter sits under a scaled parent.)
        private Matrix4x4 LocalSimulationMatrix(ParticleSystemScalingMode scalingMode)
        {
            switch (scalingMode)
            {
                case ParticleSystemScalingMode.Hierarchy:
                    return transform.localToWorldMatrix;
                case ParticleSystemScalingMode.Shape:
                    return Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
                default: // Local
                    return Matrix4x4.TRS(transform.position, transform.rotation, transform.localScale);
            }
        }
    }
}
