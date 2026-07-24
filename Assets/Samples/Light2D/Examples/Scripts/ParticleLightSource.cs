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
    // Two deliberate details: pooled lights are created inactive and fully configured before activation so a
    // new light never flashes for a frame at the origin, and each light's world rotation is forced to identity
    // every frame because a point light's quad lives in the XY lighting plane - inheriting a rotated emitter's
    // rotation would tilt the quad out of that plane and hide the light.
    [RequireComponent(typeof(ParticleSystem))]
    public class ParticleLightSource : MonoBehaviour
    {
        [Tooltip("World-space radius of each per-particle point light (default 1.5).")]
        public float Radius = 1.5f;

        [Tooltip("Peak light intensity at full particle alpha; the live value is this scaled by each " +
                 "particle's current alpha, so lights dim as their particles fade out (default 2).")]
        public float Intensity = 2f;

        [Tooltip("Tint multiplied into each particle's current color before it drives the light color " +
                 "(default white passes the particle color through unchanged).")]
        public Color ColorTint = Color.white;

        [Tooltip("Upper bound on simultaneously lit particles; particles past this count cast no light so " +
                 "the light-sources pass stays bounded (default 32).")]
        public int MaxLights = 32;

        [Tooltip("Shadow quality assigned to every pooled light; Balanced ray-marches obstacles at a moderate " +
                 "cost, and the light material auto-resolves from it (default Balanced).")]
        public LightSprite.ShadowQuality Shadows = LightSprite.ShadowQuality.Balanced;

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
            var simulationSpace = _particles.main.simulationSpace;
            var customSpace = _particles.main.customSimulationSpace;

            for (int i = 0; i < lit; i++)
            {
                var particle = _buffer[i];
                var light = GetOrCreateLight(i);

                // Configure fully before the light is (re)activated so it never renders a stale frame.
                Color32 current = particle.GetCurrentColor(_particles);
                float alpha = current.a / 255f;
                light.Radius = Radius;
                light.Color = new Color(current.r / 255f * ColorTint.r, current.g / 255f * ColorTint.g,
                    current.b / 255f * ColorTint.b, 1f);
                light.Intensity = Intensity * alpha;

                // Point-light quad must stay in the XY lighting plane, so force identity world rotation
                // regardless of the (possibly rotated) emitter this light is parented to.
                light.transform.SetPositionAndRotation(
                    ParticleWorldPosition(particle, simulationSpace, customSpace), Quaternion.identity);

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
                _pool.Add(light);
            }
            return _pool[index];
        }

        // Resolves a particle's position into world space for the pooled light, honoring the system's
        // simulation space (World is already world; Local and Custom are relative to a transform).
        private Vector3 ParticleWorldPosition(in ParticleSystem.Particle particle,
            ParticleSystemSimulationSpace simulationSpace, Transform customSpace)
        {
            switch (simulationSpace)
            {
                case ParticleSystemSimulationSpace.Local:
                    return transform.TransformPoint(particle.position);
                case ParticleSystemSimulationSpace.Custom:
                    return customSpace != null ? customSpace.TransformPoint(particle.position) : particle.position;
                default:
                    return particle.position; // World
            }
        }
    }
}
