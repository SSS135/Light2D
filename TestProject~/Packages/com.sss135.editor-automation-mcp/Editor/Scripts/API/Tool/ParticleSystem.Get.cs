#nullable enable
using System;
using System.ComponentModel;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.IvanMurzak.ReflectorNet.Utils;
using EA;
using com.SSS135.EditorAutomation.Runtime.Extensions;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_ParticleSystem
    {
        public const string ParticleSystemGetToolId = "particle-system-get";

        [McpTool
        (
            ParticleSystemGetToolId,
            Title = "ParticleSystem / Get",
            ReadOnlyHint = true,
            DestructiveHint = false,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Get detailed information about a ParticleSystem component on a GameObject. " +
            "Returns particle system state and optionally serialized data for each module. " +
            "Use the boolean flags to request specific modules. " +
            "Use this to inspect ParticleSystem data before modifying it.")]
        public GetParticleSystemResponse Get
        (
            [Description("Reference to the GameObject containing the ParticleSystem component.")]
            GameObjectRef gameObjectRef,

            [Description("Optional reference to a specific ParticleSystem component if the GameObject has multiple. " +
                "If not provided, uses the first ParticleSystem found.")]
            ComponentRef? componentRef = null,

            [Description("Include Main module data (duration, looping, prewarm, startDelay, startLifetime, startSpeed, startSize, startRotation, startColor, gravityModifier, simulationSpace, scalingMode, playOnAwake, maxParticles, etc.).")]
            bool includeMain = true,

            [Description("Include Emission module data (rateOverTime, rateOverDistance, bursts).")]
            bool includeEmission = false,

            [Description("Include Shape module data (shapeType, radius, angle, arc, position, rotation, scale, mesh, texture, etc.).")]
            bool includeShape = false,

            [Description("Include Velocity over Lifetime module data (x, y, z, space, orbital, radial, speedModifier).")]
            bool includeVelocityOverLifetime = false,

            [Description("Include Limit Velocity over Lifetime module data (limit, dampen, separateAxes, drag).")]
            bool includeLimitVelocityOverLifetime = false,

            [Description("Include Inherit Velocity module data (mode, curve).")]
            bool includeInheritVelocity = false,

            [Description("Include Lifetime by Emitter Speed module data (curve, range).")]
            bool includeLifetimeByEmitterSpeed = false,

            [Description("Include Force over Lifetime module data (x, y, z, space, randomized).")]
            bool includeForceOverLifetime = false,

            [Description("Include Color over Lifetime module data (color gradient).")]
            bool includeColorOverLifetime = false,

            [Description("Include Color by Speed module data (color, range).")]
            bool includeColorBySpeed = false,

            [Description("Include Size over Lifetime module data (size curve, separateAxes).")]
            bool includeSizeOverLifetime = false,

            [Description("Include Size by Speed module data (size, range).")]
            bool includeSizeBySpeed = false,

            [Description("Include Rotation over Lifetime module data (angular velocity, separateAxes).")]
            bool includeRotationOverLifetime = false,

            [Description("Include Rotation by Speed module data (angular velocity, range).")]
            bool includeRotationBySpeed = false,

            [Description("Include External Forces module data (multiplier, influenceFilter).")]
            bool includeExternalForces = false,

            [Description("Include Noise module data (strength, frequency, scrollSpeed, damping, octaves, quality, remap).")]
            bool includeNoise = false,

            [Description("Include Collision module data (type, mode, planes, dampen, bounce, lifetimeLoss).")]
            bool includeCollision = false,

            [Description("Include Trigger module data (inside, outside, enter, exit actions).")]
            bool includeTrigger = false,

            [Description("Include Sub Emitters module data (birth, collision, death, trigger, manual emitters).")]
            bool includeSubEmitters = false,

            [Description("Include Texture Sheet Animation module data (mode, tiles, animation, frameOverTime).")]
            bool includeTextureSheetAnimation = false,

            [Description("Include Lights module data (ratio, light, color, range, intensity).")]
            bool includeLights = false,

            [Description("Include Trails module data (mode, ratio, lifetime, width, color).")]
            bool includeTrails = false,

            [Description("Include Custom Data module data (modes, vectors, colors).")]
            bool includeCustomData = false,

            [Description("Include Renderer module data (renderMode, material, sortMode, alignment, shadows).")]
            bool includeRenderer = false,

            [Description("Include ALL modules data. Overrides individual flags.")]
            bool includeAll = false,

            [Description("Performs deep serialization including all nested objects. Otherwise, only serializes top-level members.")]
            bool deepSerialization = false
        )
        {
            if (gameObjectRef == null)
                throw new ArgumentNullException(nameof(gameObjectRef));

            if (!gameObjectRef.IsValid(out var gameObjectValidationError))
                throw new ArgumentException(gameObjectValidationError, nameof(gameObjectRef));

            return MainThread.Instance.Run(() =>
            {
                var go = gameObjectRef.FindGameObject(out var error);
                if (error != null)
                    throw new Exception(error);

                if (go == null)
                    throw new Exception("GameObject not found.");

                // Find the ParticleSystem component
                UnityEngine.ParticleSystem? ps = null;
                int psIndex = -1;

                var allComponents = go.GetComponents<UnityEngine.Component>();
                for (int i = 0; i < allComponents.Length; i++)
                {
                    var comp = allComponents[i] as UnityEngine.ParticleSystem;
                    if (comp == null)
                        continue;

                    if (componentRef != null && componentRef.IsValid(out _))
                    {
                        if (componentRef.Matches(allComponents[i], i))
                        {
                            ps = comp;
                            psIndex = i;
                            break;
                        }
                    }
                    else
                    {
                        // Use the first ParticleSystem found
                        ps = comp;
                        psIndex = i;
                        break;
                    }
                }

                if (ps == null)
                    throw new Exception("ParticleSystem component not found on the specified GameObject.");

                var response = new GetParticleSystemResponse
                {
                    gameObjectRef = new GameObjectRef(go),
                    componentRef = new ComponentRef(ps),
                    componentIndex = psIndex,
                    isPlaying = ps.isPlaying,
                    isPaused = ps.isPaused,
                    isEmitting = ps.isEmitting,
                    isStopped = ps.isStopped,
                    particleCount = ps.particleCount,
                    time = ps.time
                };

                var reflector = ReflectorFactory.Instance ?? throw new Exception("Reflector not available.");

                // Serialize requested modules
                if (includeAll || includeMain)
                {
                    var mainModule = ps.main;
                    response.main = reflector.Serialize(
                        obj: mainModule,
                        name: nameof(ps.main),
                        recursive: deepSerialization,
                        logger: _logger
                    );
                }

                if (includeAll || includeEmission)
                {
                    var emissionModule = ps.emission;
                    response.emission = reflector.Serialize(
                        obj: emissionModule,
                        name: nameof(ps.emission),
                        recursive: deepSerialization,
                        logger: _logger
                    );
                }

                if (includeAll || includeShape)
                {
                    var shapeModule = ps.shape;
                    response.shape = reflector.Serialize(
                        obj: shapeModule,
                        name: nameof(ps.shape),
                        recursive: deepSerialization,
                        logger: _logger
                    );
                }

                if (includeAll || includeVelocityOverLifetime)
                {
                    var velocityModule = ps.velocityOverLifetime;
                    response.velocityOverLifetime = reflector.Serialize(
                        obj: velocityModule,
                        name: nameof(ps.velocityOverLifetime),
                        recursive: deepSerialization,
                        logger: _logger
                    );
                }

                if (includeAll || includeLimitVelocityOverLifetime)
                {
                    var limitVelocityModule = ps.limitVelocityOverLifetime;
                    response.limitVelocityOverLifetime = reflector.Serialize(
                        obj: limitVelocityModule,
                        name: nameof(ps.limitVelocityOverLifetime),
                        recursive: deepSerialization,
                        logger: _logger
                    );
                }

                if (includeAll || includeInheritVelocity)
                {
                    var inheritVelocityModule = ps.inheritVelocity;
                    response.inheritVelocity = reflector.Serialize(
                        obj: inheritVelocityModule,
                        name: nameof(ps.inheritVelocity),
                        recursive: deepSerialization,
                        logger: _logger
                    );
                }

                if (includeAll || includeLifetimeByEmitterSpeed)
                {
                    var lifetimeByEmitterSpeedModule = ps.lifetimeByEmitterSpeed;
                    response.lifetimeByEmitterSpeed = reflector.Serialize(
                        obj: lifetimeByEmitterSpeedModule,
                        name: nameof(ps.lifetimeByEmitterSpeed),
                        recursive: deepSerialization,
                        logger: _logger
                    );
                }

                if (includeAll || includeForceOverLifetime)
                {
                    var forceModule = ps.forceOverLifetime;
                    response.forceOverLifetime = reflector.Serialize(
                        obj: forceModule,
                        name: nameof(ps.forceOverLifetime),
                        recursive: deepSerialization,
                        logger: _logger
                    );
                }

                if (includeAll || includeColorOverLifetime)
                {
                    var colorModule = ps.colorOverLifetime;
                    response.colorOverLifetime = reflector.Serialize(
                        obj: colorModule,
                        name: nameof(ps.colorOverLifetime),
                        recursive: deepSerialization,
                        logger: _logger
                    );
                }

                if (includeAll || includeColorBySpeed)
                {
                    var colorBySpeedModule = ps.colorBySpeed;
                    response.colorBySpeed = reflector.Serialize(
                        obj: colorBySpeedModule,
                        name: nameof(ps.colorBySpeed),
                        recursive: deepSerialization,
                        logger: _logger
                    );
                }

                if (includeAll || includeSizeOverLifetime)
                {
                    var sizeModule = ps.sizeOverLifetime;
                    response.sizeOverLifetime = reflector.Serialize(
                        obj: sizeModule,
                        name: nameof(ps.sizeOverLifetime),
                        recursive: deepSerialization,
                        logger: _logger
                    );
                }

                if (includeAll || includeSizeBySpeed)
                {
                    var sizeBySpeedModule = ps.sizeBySpeed;
                    response.sizeBySpeed = reflector.Serialize(
                        obj: sizeBySpeedModule,
                        name: nameof(ps.sizeBySpeed),
                        recursive: deepSerialization,
                        logger: _logger
                    );
                }

                if (includeAll || includeRotationOverLifetime)
                {
                    var rotationModule = ps.rotationOverLifetime;
                    response.rotationOverLifetime = reflector.Serialize(
                        obj: rotationModule,
                        name: nameof(ps.rotationOverLifetime),
                        recursive: deepSerialization,
                        logger: _logger
                    );
                }

                if (includeAll || includeRotationBySpeed)
                {
                    var rotationBySpeedModule = ps.rotationBySpeed;
                    response.rotationBySpeed = reflector.Serialize(
                        obj: rotationBySpeedModule,
                        name: nameof(ps.rotationBySpeed),
                        recursive: deepSerialization,
                        logger: _logger
                    );
                }

                if (includeAll || includeExternalForces)
                {
                    var externalForcesModule = ps.externalForces;
                    response.externalForces = reflector.Serialize(
                        obj: externalForcesModule,
                        name: nameof(ps.externalForces),
                        recursive: deepSerialization,
                        logger: _logger
                    );
                }

                if (includeAll || includeNoise)
                {
                    var noiseModule = ps.noise;
                    response.noise = reflector.Serialize(
                        obj: noiseModule,
                        name: nameof(ps.noise),
                        recursive: deepSerialization,
                        logger: _logger
                    );
                }

                if (includeAll || includeCollision)
                {
                    var collisionModule = ps.collision;
                    response.collision = reflector.Serialize(
                        obj: collisionModule,
                        name: nameof(ps.collision),
                        recursive: deepSerialization,
                        logger: _logger
                    );
                }

                if (includeAll || includeTrigger)
                {
                    var triggerModule = ps.trigger;
                    response.trigger = reflector.Serialize(
                        obj: triggerModule,
                        name: nameof(ps.trigger),
                        recursive: deepSerialization,
                        logger: _logger
                    );
                }

                if (includeAll || includeSubEmitters)
                {
                    var subEmittersModule = ps.subEmitters;
                    response.subEmitters = reflector.Serialize(
                        obj: subEmittersModule,
                        name: nameof(ps.subEmitters),
                        recursive: deepSerialization,
                        logger: _logger
                    );
                }

                if (includeAll || includeTextureSheetAnimation)
                {
                    var textureSheetModule = ps.textureSheetAnimation;
                    response.textureSheetAnimation = reflector.Serialize(
                        obj: textureSheetModule,
                        name: nameof(ps.textureSheetAnimation),
                        recursive: deepSerialization,
                        logger: _logger
                    );
                }

                if (includeAll || includeLights)
                {
                    var lightsModule = ps.lights;
                    response.lights = reflector.Serialize(
                        obj: lightsModule,
                        name: nameof(ps.lights),
                        recursive: deepSerialization,
                        logger: _logger
                    );
                }

                if (includeAll || includeTrails)
                {
                    var trailsModule = ps.trails;
                    response.trails = reflector.Serialize(
                        obj: trailsModule,
                        name: nameof(ps.trails),
                        recursive: deepSerialization,
                        logger: _logger
                    );
                }

                if (includeAll || includeCustomData)
                {
                    var customDataModule = ps.customData;
                    response.customData = reflector.Serialize(
                        obj: customDataModule,
                        name: nameof(ps.customData),
                        recursive: deepSerialization,
                        logger: _logger
                    );
                }

                if (includeAll || includeRenderer)
                {
                    var rendererComponent = go.GetComponent<UnityEngine.ParticleSystemRenderer>();
                    if (rendererComponent != null)
                    {
                        response.renderer = reflector.Serialize(
                            obj: rendererComponent,
                            name: nameof(response.renderer),
                            recursive: deepSerialization,
                            logger: _logger
                        );
                    }
                }

                return response;
            });
        }
    }
}
