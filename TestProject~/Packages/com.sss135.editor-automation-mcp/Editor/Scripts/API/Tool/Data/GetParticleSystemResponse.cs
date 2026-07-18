#nullable enable
using System.ComponentModel;
using com.IvanMurzak.ReflectorNet.Model;

namespace EA
{
    /// <summary>
    /// Response model for Get ParticleSystem tool.
    /// </summary>
    [Description("Response containing ParticleSystem data with requested modules.")]
    public class GetParticleSystemResponse
    {
        [Description("Reference to the GameObject containing the ParticleSystem component.")]
        public GameObjectRef? gameObjectRef;

        [Description("Reference to the ParticleSystem component.")]
        public ComponentRef? componentRef;

        [Description("Index of the ParticleSystem component in the GameObject's component list.")]
        public int componentIndex = -1;

        [Description("Whether the ParticleSystem is currently playing.")]
        public bool isPlaying;

        [Description("Whether the ParticleSystem is currently paused.")]
        public bool isPaused;

        [Description("Whether the ParticleSystem is currently emitting.")]
        public bool isEmitting;

        [Description("Whether the ParticleSystem is currently stopped.")]
        public bool isStopped;

        [Description("Current particle count.")]
        public int particleCount;

        [Description("Current simulation time.")]
        public float time;

        [Description("Main module data.")]
        public SerializedMember? main;

        [Description("Emission module data.")]
        public SerializedMember? emission;

        [Description("Shape module data.")]
        public SerializedMember? shape;

        [Description("Velocity over Lifetime module data.")]
        public SerializedMember? velocityOverLifetime;

        [Description("Limit Velocity over Lifetime module data.")]
        public SerializedMember? limitVelocityOverLifetime;

        [Description("Inherit Velocity module data.")]
        public SerializedMember? inheritVelocity;

        [Description("Lifetime by Emitter Speed module data.")]
        public SerializedMember? lifetimeByEmitterSpeed;

        [Description("Force over Lifetime module data.")]
        public SerializedMember? forceOverLifetime;

        [Description("Color over Lifetime module data.")]
        public SerializedMember? colorOverLifetime;

        [Description("Color by Speed module data.")]
        public SerializedMember? colorBySpeed;

        [Description("Size over Lifetime module data.")]
        public SerializedMember? sizeOverLifetime;

        [Description("Size by Speed module data.")]
        public SerializedMember? sizeBySpeed;

        [Description("Rotation over Lifetime module data.")]
        public SerializedMember? rotationOverLifetime;

        [Description("Rotation by Speed module data.")]
        public SerializedMember? rotationBySpeed;

        [Description("External Forces module data.")]
        public SerializedMember? externalForces;

        [Description("Noise module data.")]
        public SerializedMember? noise;

        [Description("Collision module data.")]
        public SerializedMember? collision;

        [Description("Trigger module data.")]
        public SerializedMember? trigger;

        [Description("Sub Emitters module data.")]
        public SerializedMember? subEmitters;

        [Description("Texture Sheet Animation module data.")]
        public SerializedMember? textureSheetAnimation;

        [Description("Lights module data.")]
        public SerializedMember? lights;

        [Description("Trails module data.")]
        public SerializedMember? trails;

        [Description("Custom Data module data.")]
        public SerializedMember? customData;

        [Description("Renderer module data.")]
        public SerializedMember? renderer;
    }
}
