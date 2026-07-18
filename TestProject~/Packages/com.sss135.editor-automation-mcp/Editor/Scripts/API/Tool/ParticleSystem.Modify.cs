#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.IvanMurzak.ReflectorNet.Model;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.Utils;
using EA;
using com.SSS135.EditorAutomation.Runtime.Extensions;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public partial class Tool_ParticleSystem
    {
        public const string ParticleSystemModifyToolId = "particle-system-modify";

        [McpTool
        (
            ParticleSystemModifyToolId,
            Title = "ParticleSystem / Modify",
            ReadOnlyHint = false,
            DestructiveHint = false,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Modify a ParticleSystem component on a GameObject. " +
            "Provide the data model with only the modules you want to change. " +
            "Use '" + ParticleSystemGetToolId + "' first to inspect the ParticleSystem structure before modifying. " +
            "Only include the modules and properties you want to change.")]
        public ModifyParticleSystemResponse Modify
        (
            [Description("Reference to the GameObject containing the ParticleSystem component.")]
            GameObjectRef gameObjectRef,

            [Description("Optional reference to a specific ParticleSystem component if the GameObject has multiple. " +
                "If not provided, uses the first ParticleSystem found.")]
            ComponentRef? componentRef = null,

            [Description("Main module data to apply. Only include properties you want to change.")]
            SerializedMember? main = null,

            [Description("Emission module data to apply. Only include properties you want to change.")]
            SerializedMember? emission = null,

            [Description("Shape module data to apply. Only include properties you want to change.")]
            SerializedMember? shape = null,

            [Description("Velocity over Lifetime module data to apply. Only include properties you want to change.")]
            SerializedMember? velocityOverLifetime = null,

            [Description("Limit Velocity over Lifetime module data to apply. Only include properties you want to change.")]
            SerializedMember? limitVelocityOverLifetime = null,

            [Description("Inherit Velocity module data to apply. Only include properties you want to change.")]
            SerializedMember? inheritVelocity = null,

            [Description("Lifetime by Emitter Speed module data to apply. Only include properties you want to change.")]
            SerializedMember? lifetimeByEmitterSpeed = null,

            [Description("Force over Lifetime module data to apply. Only include properties you want to change.")]
            SerializedMember? forceOverLifetime = null,

            [Description("Color over Lifetime module data to apply. Only include properties you want to change.")]
            SerializedMember? colorOverLifetime = null,

            [Description("Color by Speed module data to apply. Only include properties you want to change.")]
            SerializedMember? colorBySpeed = null,

            [Description("Size over Lifetime module data to apply. Only include properties you want to change.")]
            SerializedMember? sizeOverLifetime = null,

            [Description("Size by Speed module data to apply. Only include properties you want to change.")]
            SerializedMember? sizeBySpeed = null,

            [Description("Rotation over Lifetime module data to apply. Only include properties you want to change.")]
            SerializedMember? rotationOverLifetime = null,

            [Description("Rotation by Speed module data to apply. Only include properties you want to change.")]
            SerializedMember? rotationBySpeed = null,

            [Description("External Forces module data to apply. Only include properties you want to change.")]
            SerializedMember? externalForces = null,

            [Description("Noise module data to apply. Only include properties you want to change.")]
            SerializedMember? noise = null,

            [Description("Collision module data to apply. Only include properties you want to change.")]
            SerializedMember? collision = null,

            [Description("Trigger module data to apply. Only include properties you want to change.")]
            SerializedMember? trigger = null,

            [Description("Sub Emitters module data to apply. Only include properties you want to change.")]
            SerializedMember? subEmitters = null,

            [Description("Texture Sheet Animation module data to apply. Only include properties you want to change.")]
            SerializedMember? textureSheetAnimation = null,

            [Description("Lights module data to apply. Only include properties you want to change.")]
            SerializedMember? lights = null,

            [Description("Trails module data to apply. Only include properties you want to change.")]
            SerializedMember? trails = null,

            [Description("Custom Data module data to apply. Only include properties you want to change.")]
            SerializedMember? customData = null,

            [Description("Renderer module data to apply. Only include properties you want to change.")]
            SerializedMember? renderer = null
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

                var response = new ModifyParticleSystemResponse
                {
                    gameObjectRef = new GameObjectRef(go),
                    componentRef = new ComponentRef(ps),
                    componentIndex = psIndex
                };

                var logs = new List<string>();
                var reflector = ReflectorFactory.Instance ?? throw new Exception("Reflector not available.");
                bool anyModified = false;

                // Apply modifications to each module
                if (main != null)
                {
                    var module = ps.main;
                    object? boxedModule = module;
                    var moduleLogs = new Logs();
                    if (reflector.TryModify(ref boxedModule, main, logs: moduleLogs, logger: _logger))
                    {
                        anyModified = true;
                        logs.Add("[Main] Module modified successfully.");
                    }
                    logs.AddRange(moduleLogs.Select(l => $"[Main] {l}"));
                }

                if (emission != null)
                {
                    var module = ps.emission;
                    object? boxedModule = module;
                    var moduleLogs = new Logs();
                    if (reflector.TryModify(ref boxedModule, emission, logs: moduleLogs, logger: _logger))
                    {
                        anyModified = true;
                        logs.Add("[Emission] Module modified successfully.");
                    }
                    logs.AddRange(moduleLogs.Select(l => $"[Emission] {l}"));
                }

                if (shape != null)
                {
                    var module = ps.shape;
                    object? boxedModule = module;
                    var moduleLogs = new Logs();
                    if (reflector.TryModify(ref boxedModule, shape, logs: moduleLogs, logger: _logger))
                    {
                        anyModified = true;
                        logs.Add("[Shape] Module modified successfully.");
                    }
                    logs.AddRange(moduleLogs.Select(l => $"[Shape] {l}"));
                }

                if (velocityOverLifetime != null)
                {
                    var module = ps.velocityOverLifetime;
                    object? boxedModule = module;
                    var moduleLogs = new Logs();
                    if (reflector.TryModify(ref boxedModule, velocityOverLifetime, logs: moduleLogs, logger: _logger))
                    {
                        anyModified = true;
                        logs.Add("[VelocityOverLifetime] Module modified successfully.");
                    }
                    logs.AddRange(moduleLogs.Select(l => $"[VelocityOverLifetime] {l}"));
                }

                if (limitVelocityOverLifetime != null)
                {
                    var module = ps.limitVelocityOverLifetime;
                    object? boxedModule = module;
                    var moduleLogs = new Logs();
                    if (reflector.TryModify(ref boxedModule, limitVelocityOverLifetime, logs: moduleLogs, logger: _logger))
                    {
                        anyModified = true;
                        logs.Add("[LimitVelocityOverLifetime] Module modified successfully.");
                    }
                    logs.AddRange(moduleLogs.Select(l => $"[LimitVelocityOverLifetime] {l}"));
                }

                if (inheritVelocity != null)
                {
                    var module = ps.inheritVelocity;
                    object? boxedModule = module;
                    var moduleLogs = new Logs();
                    if (reflector.TryModify(ref boxedModule, inheritVelocity, logs: moduleLogs, logger: _logger))
                    {
                        anyModified = true;
                        logs.Add("[InheritVelocity] Module modified successfully.");
                    }
                    logs.AddRange(moduleLogs.Select(l => $"[InheritVelocity] {l}"));
                }

                if (lifetimeByEmitterSpeed != null)
                {
                    var module = ps.lifetimeByEmitterSpeed;
                    object? boxedModule = module;
                    var moduleLogs = new Logs();
                    if (reflector.TryModify(ref boxedModule, lifetimeByEmitterSpeed, logs: moduleLogs, logger: _logger))
                    {
                        anyModified = true;
                        logs.Add("[LifetimeByEmitterSpeed] Module modified successfully.");
                    }
                    logs.AddRange(moduleLogs.Select(l => $"[LifetimeByEmitterSpeed] {l}"));
                }

                if (forceOverLifetime != null)
                {
                    var module = ps.forceOverLifetime;
                    object? boxedModule = module;
                    var moduleLogs = new Logs();
                    if (reflector.TryModify(ref boxedModule, forceOverLifetime, logs: moduleLogs, logger: _logger))
                    {
                        anyModified = true;
                        logs.Add("[ForceOverLifetime] Module modified successfully.");
                    }
                    logs.AddRange(moduleLogs.Select(l => $"[ForceOverLifetime] {l}"));
                }

                if (colorOverLifetime != null)
                {
                    var module = ps.colorOverLifetime;
                    object? boxedModule = module;
                    var moduleLogs = new Logs();
                    if (reflector.TryModify(ref boxedModule, colorOverLifetime, logs: moduleLogs, logger: _logger))
                    {
                        anyModified = true;
                        logs.Add("[ColorOverLifetime] Module modified successfully.");
                    }
                    logs.AddRange(moduleLogs.Select(l => $"[ColorOverLifetime] {l}"));
                }

                if (colorBySpeed != null)
                {
                    var module = ps.colorBySpeed;
                    object? boxedModule = module;
                    var moduleLogs = new Logs();
                    if (reflector.TryModify(ref boxedModule, colorBySpeed, logs: moduleLogs, logger: _logger))
                    {
                        anyModified = true;
                        logs.Add("[ColorBySpeed] Module modified successfully.");
                    }
                    logs.AddRange(moduleLogs.Select(l => $"[ColorBySpeed] {l}"));
                }

                if (sizeOverLifetime != null)
                {
                    var module = ps.sizeOverLifetime;
                    object? boxedModule = module;
                    var moduleLogs = new Logs();
                    if (reflector.TryModify(ref boxedModule, sizeOverLifetime, logs: moduleLogs, logger: _logger))
                    {
                        anyModified = true;
                        logs.Add("[SizeOverLifetime] Module modified successfully.");
                    }
                    logs.AddRange(moduleLogs.Select(l => $"[SizeOverLifetime] {l}"));
                }

                if (sizeBySpeed != null)
                {
                    var module = ps.sizeBySpeed;
                    object? boxedModule = module;
                    var moduleLogs = new Logs();
                    if (reflector.TryModify(ref boxedModule, sizeBySpeed, logs: moduleLogs, logger: _logger))
                    {
                        anyModified = true;
                        logs.Add("[SizeBySpeed] Module modified successfully.");
                    }
                    logs.AddRange(moduleLogs.Select(l => $"[SizeBySpeed] {l}"));
                }

                if (rotationOverLifetime != null)
                {
                    var module = ps.rotationOverLifetime;
                    object? boxedModule = module;
                    var moduleLogs = new Logs();
                    if (reflector.TryModify(ref boxedModule, rotationOverLifetime, logs: moduleLogs, logger: _logger))
                    {
                        anyModified = true;
                        logs.Add("[RotationOverLifetime] Module modified successfully.");
                    }
                    logs.AddRange(moduleLogs.Select(l => $"[RotationOverLifetime] {l}"));
                }

                if (rotationBySpeed != null)
                {
                    var module = ps.rotationBySpeed;
                    object? boxedModule = module;
                    var moduleLogs = new Logs();
                    if (reflector.TryModify(ref boxedModule, rotationBySpeed, logs: moduleLogs, logger: _logger))
                    {
                        anyModified = true;
                        logs.Add("[RotationBySpeed] Module modified successfully.");
                    }
                    logs.AddRange(moduleLogs.Select(l => $"[RotationBySpeed] {l}"));
                }

                if (externalForces != null)
                {
                    var module = ps.externalForces;
                    object? boxedModule = module;
                    var moduleLogs = new Logs();
                    if (reflector.TryModify(ref boxedModule, externalForces, logs: moduleLogs, logger: _logger))
                    {
                        anyModified = true;
                        logs.Add("[ExternalForces] Module modified successfully.");
                    }
                    logs.AddRange(moduleLogs.Select(l => $"[ExternalForces] {l}"));
                }

                if (noise != null)
                {
                    var module = ps.noise;
                    object? boxedModule = module;
                    var moduleLogs = new Logs();
                    if (reflector.TryModify(ref boxedModule, noise, logs: moduleLogs, logger: _logger))
                    {
                        anyModified = true;
                        logs.Add("[Noise] Module modified successfully.");
                    }
                    logs.AddRange(moduleLogs.Select(l => $"[Noise] {l}"));
                }

                if (collision != null)
                {
                    var module = ps.collision;
                    object? boxedModule = module;
                    var moduleLogs = new Logs();
                    if (reflector.TryModify(ref boxedModule, collision, logs: moduleLogs, logger: _logger))
                    {
                        anyModified = true;
                        logs.Add("[Collision] Module modified successfully.");
                    }
                    logs.AddRange(moduleLogs.Select(l => $"[Collision] {l}"));
                }

                if (trigger != null)
                {
                    var module = ps.trigger;
                    object? boxedModule = module;
                    var moduleLogs = new Logs();
                    if (reflector.TryModify(ref boxedModule, trigger, logs: moduleLogs, logger: _logger))
                    {
                        anyModified = true;
                        logs.Add("[Trigger] Module modified successfully.");
                    }
                    logs.AddRange(moduleLogs.Select(l => $"[Trigger] {l}"));
                }

                if (subEmitters != null)
                {
                    var module = ps.subEmitters;
                    object? boxedModule = module;
                    var moduleLogs = new Logs();
                    if (reflector.TryModify(ref boxedModule, subEmitters, logs: moduleLogs, logger: _logger))
                    {
                        anyModified = true;
                        logs.Add("[SubEmitters] Module modified successfully.");
                    }
                    logs.AddRange(moduleLogs.Select(l => $"[SubEmitters] {l}"));
                }

                if (textureSheetAnimation != null)
                {
                    var module = ps.textureSheetAnimation;
                    object? boxedModule = module;
                    var moduleLogs = new Logs();
                    if (reflector.TryModify(ref boxedModule, textureSheetAnimation, logs: moduleLogs, logger: _logger))
                    {
                        anyModified = true;
                        logs.Add("[TextureSheetAnimation] Module modified successfully.");
                    }
                    logs.AddRange(moduleLogs.Select(l => $"[TextureSheetAnimation] {l}"));
                }

                if (lights != null)
                {
                    var module = ps.lights;
                    object? boxedModule = module;
                    var moduleLogs = new Logs();
                    if (reflector.TryModify(ref boxedModule, lights, logs: moduleLogs, logger: _logger))
                    {
                        anyModified = true;
                        logs.Add("[Lights] Module modified successfully.");
                    }
                    logs.AddRange(moduleLogs.Select(l => $"[Lights] {l}"));
                }

                if (trails != null)
                {
                    var module = ps.trails;
                    object? boxedModule = module;
                    var moduleLogs = new Logs();
                    if (reflector.TryModify(ref boxedModule, trails, logs: moduleLogs, logger: _logger))
                    {
                        anyModified = true;
                        logs.Add("[Trails] Module modified successfully.");
                    }
                    logs.AddRange(moduleLogs.Select(l => $"[Trails] {l}"));
                }

                if (customData != null)
                {
                    var module = ps.customData;
                    object? boxedModule = module;
                    var moduleLogs = new Logs();
                    if (reflector.TryModify(ref boxedModule, customData, logs: moduleLogs, logger: _logger))
                    {
                        anyModified = true;
                        logs.Add("[CustomData] Module modified successfully.");
                    }
                    logs.AddRange(moduleLogs.Select(l => $"[CustomData] {l}"));
                }

                if (renderer != null)
                {
                    var rendererComponent = go.GetComponent<UnityEngine.ParticleSystemRenderer>();
                    if (rendererComponent != null)
                    {
                        object? boxedRenderer = rendererComponent;
                        var moduleLogs = new Logs();
                        if (reflector.TryModify(ref boxedRenderer, renderer, logs: moduleLogs, logger: _logger))
                        {
                            anyModified = true;
                            logs.Add("[Renderer] Component modified successfully.");
                            UnityEditor.EditorUtility.SetDirty(rendererComponent);
                        }
                        logs.AddRange(moduleLogs.Select(l => $"[Renderer] {l}"));
                    }
                    else
                    {
                        logs.Add("[Renderer] ParticleSystemRenderer component not found.");
                    }
                }

                if (anyModified)
                {
                    UnityEditor.EditorUtility.SetDirty(go);
                    UnityEditor.EditorUtility.SetDirty(ps);
                    response.success = true;
                }
                else
                {
                    logs.Add("No modifications were made.");
                }

                EditorUtils.RepaintAllEditorWindows();

                response.logs = logs.ToArray();
                return response;
            });
        }
    }
}
