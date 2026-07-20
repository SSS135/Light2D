#nullable enable
using System.ComponentModel;
using com.IvanMurzak.ReflectorNet.Model;

namespace EA
{
    /// <summary>
    /// Data model for ParticleSystem component containing serialized data for all modules.
    /// Each module is stored as a SerializedMember chunk for efficient data transfer.
    /// </summary>
    [Description("ParticleSystem data model containing serialized data for all modules. " +
        "Each module is stored as a SerializedMember chunk that can be independently retrieved or modified.")]
    public class ParticleSystemData
    {
        [Description("Reference to the GameObject containing the ParticleSystem component.")]
        public GameObjectRef? gameObjectRef;

        [Description("Reference to the ParticleSystem component.")]
        public ComponentRef? componentRef;

        [Description("Index of the ParticleSystem component in the GameObject's component list.")]
        public int componentIndex = -1;

        [Description("Main module data: duration, looping, prewarm, startDelay, startLifetime, startSpeed, startSize, startRotation, startColor, gravityModifier, simulationSpace, scalingMode, playOnAwake, maxParticles, etc.")]
        public SerializedMember? main;

        [Description("Emission module data: enabled, rateOverTime, rateOverDistance, bursts.")]
        public SerializedMember? emission;

        [Description("Shape module data: enabled, shapeType, radius, angle, arc, position, rotation, scale, mesh, meshRenderer, skinnedMeshRenderer, sprite, spriteRenderer, texture, etc.")]
        public SerializedMember? shape;

        [Description("Velocity over Lifetime module data: enabled, x, y, z, space, orbitalX, orbitalY, orbitalZ, orbitalOffsetX, orbitalOffsetY, orbitalOffsetZ, radial, speedModifier.")]
        public SerializedMember? velocityOverLifetime;

        [Description("Limit Velocity over Lifetime module data: enabled, limitX, limitY, limitZ, limit, dampen, separateAxes, space, drag, multiplyDragByParticleSize, multiplyDragByParticleVelocity.")]
        public SerializedMember? limitVelocityOverLifetime;

        [Description("Inherit Velocity module data: enabled, mode, curve.")]
        public SerializedMember? inheritVelocity;

        [Description("Lifetime by Emitter Speed module data: enabled, curve, range.")]
        public SerializedMember? lifetimeByEmitterSpeed;

        [Description("Force over Lifetime module data: enabled, x, y, z, space, randomized.")]
        public SerializedMember? forceOverLifetime;

        [Description("Color over Lifetime module data: enabled, color.")]
        public SerializedMember? colorOverLifetime;

        [Description("Color by Speed module data: enabled, color, range.")]
        public SerializedMember? colorBySpeed;

        [Description("Size over Lifetime module data: enabled, size, x, y, z, separateAxes.")]
        public SerializedMember? sizeOverLifetime;

        [Description("Size by Speed module data: enabled, size, x, y, z, separateAxes, range.")]
        public SerializedMember? sizeBySpeed;

        [Description("Rotation over Lifetime module data: enabled, x, y, z, separateAxes.")]
        public SerializedMember? rotationOverLifetime;

        [Description("Rotation by Speed module data: enabled, x, y, z, separateAxes, range.")]
        public SerializedMember? rotationBySpeed;

        [Description("External Forces module data: enabled, multiplier, multiplierCurve, influenceFilter, influenceMask.")]
        public SerializedMember? externalForces;

        [Description("Noise module data: enabled, strength, strengthX, strengthY, strengthZ, separateAxes, frequency, scrollSpeed, damping, octaveCount, octaveMultiplier, octaveScale, quality, remapEnabled, remap, remapX, remapY, remapZ, positionAmount, rotationAmount, sizeAmount.")]
        public SerializedMember? noise;

        [Description("Collision module data: enabled, type, mode, planes, dampen, bounce, lifetimeLoss, minKillSpeed, maxKillSpeed, radiusScale, quality, voxelSize, collidesWith, maxCollisionShapes, enableDynamicColliders, enableInteriorCollisions, colliderForce, multiplyColliderForceByCollisionAngle, multiplyColliderForceByParticleSpeed, multiplyColliderForceByParticleSize, sendCollisionMessages.")]
        public SerializedMember? collision;

        [Description("Trigger module data: enabled, inside, outside, enter, exit, colliderQueryMode, radiusScale.")]
        public SerializedMember? trigger;

        [Description("Sub Emitters module data: enabled, subEmittersCount, birth0, collision0, death0, trigger0, manual0.")]
        public SerializedMember? subEmitters;

        [Description("Texture Sheet Animation module data: enabled, mode, numTilesX, numTilesY, animation, useRandomRow, rowIndex, frameOverTime, startFrame, cycleCount, uvChannelMask, sprites, rowMode, speedRange.")]
        public SerializedMember? textureSheetAnimation;

        [Description("Lights module data: enabled, ratio, useRandomDistribution, light, useParticleColor, sizeAffectsRange, alphaAffectsIntensity, range, rangeCurve, intensity, intensityCurve, maxLights.")]
        public SerializedMember? lights;

        [Description("Trails module data: enabled, mode, ratio, lifetime, minVertexDistance, textureMode, worldSpace, dieWithParticles, sizeAffectsWidth, sizeAffectsLifetime, inheritParticleColor, colorOverLifetime, widthOverTrail, colorOverTrail, generateLightingData, ribbonCount, shadowBias, splitSubEmitterRibbons, attachRibbonsToTransform.")]
        public SerializedMember? trails;

        [Description("Custom Data module data: enabled, mode1, mode2, vectorComponentCount1, vectorComponentCount2, color1, color2, vector1_0, vector1_1, vector1_2, vector1_3, vector2_0, vector2_1, vector2_2, vector2_3.")]
        public SerializedMember? customData;

        [Description("Renderer module data: renderMode, sortMode, lengthScale, velocityScale, cameraVelocityScale, normalDirection, sortingFudge, minParticleSize, maxParticleSize, alignment, flip, allowRoll, pivot, maskInteraction, material, trailMaterial, shadowCastingMode, receiveShadows, shadowBias, motionVectorGenerationMode, sortingLayerID, sortingLayerName, sortingOrder, lightProbeUsage, reflectionProbeUsage, probeAnchor.")]
        public SerializedMember? renderer;
    }
}
