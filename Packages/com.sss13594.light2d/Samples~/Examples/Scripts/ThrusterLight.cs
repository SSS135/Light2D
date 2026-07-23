using UnityEngine;

namespace Light2D.Examples
{
    // Drives a LightSprite from a RocketEngine's throttle so each thruster glows while it fires. The
    // glow chases the engine's visible power - Clamp01(ForcePercent), the same value the flame force
    // uses, gated by IsEnabled so the light dies exactly when the flame particles stop emitting - with
    // an asymmetric, frame-rate-independent ramp: a fast RiseTime constant when spooling up and a
    // slower FallTime when cutting off, so the exhaust glow lingers briefly like the real thing.
    public class ThrusterLight : MonoBehaviour
    {
        [Tooltip("Light driven by this thruster. Leave empty to use the LightSprite on this GameObject.")]
        public LightSprite Light;

        [Tooltip("Engine whose power this light follows. Leave empty to use the nearest RocketEngine parent.")]
        public RocketEngine Engine;

        [Tooltip("Peak intensity written to the light when the thruster runs at full power (default 1.5).")]
        public float MaxIntensity = 1.5f;

        [Tooltip("Rise time constant in seconds; the glow covers ~63% of a power increase in this time (default 0.05).")]
        public float RiseTime = 0.05f;

        [Tooltip("Fall time constant in seconds; the glow decays ~63% of the way down in this time (default 0.3).")]
        public float FallTime = 0.3f;

        private float _power;

        void Start()
        {
            // Both references may be left unassigned in the inspector; resolve them from the hierarchy so a
            // hand-placed thruster light still works, and disable cleanly if the wiring is incomplete.
            if (Light == null)
                Light = GetComponent<LightSprite>();
            if (Engine == null)
                Engine = GetComponentInParent<RocketEngine>();

            if (Light == null || Engine == null)
            {
                enabled = false;
                return;
            }

            _power = TargetPower();
            Light.Intensity = MaxIntensity*_power;
        }

        void Update()
        {
            var target = TargetPower();

            // Frame-rate-independent asymmetric smoothing: rising uses the short RiseTime constant and
            // falling the longer FallTime, so the response tracks wall-clock time at any frame rate.
            var tau = target > _power ? RiseTime : FallTime;
            var k = tau > 0f ? 1f - Mathf.Exp(-Time.deltaTime/tau) : 1f;
            _power = Mathf.Lerp(_power, target, k);

            Light.Intensity = MaxIntensity*_power;
        }

        // The engine's visible power: Clamp01(ForcePercent) matched to the flame force, zeroed while the
        // engine is disabled so the light tracks whether the flame particles are actually emitting.
        private float TargetPower()
        {
            return Engine.IsEnabled ? Mathf.Clamp01(Engine.ForcePercent) : 0f;
        }
    }
}
