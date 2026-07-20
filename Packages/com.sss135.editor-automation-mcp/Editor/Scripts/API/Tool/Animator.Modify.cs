#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using EA;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Editor.Utils;
using com.SSS135.EditorAutomation.Runtime.Extensions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public static partial class Tool_Animator
    {
        public const string AnimatorModifyToolId = "animator-modify";
        [McpTool
        (
            AnimatorModifyToolId,
            Title = "Animator / Modify",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false,
            OpenWorldHint = false
        )]
        [Description("Modify Unity's AnimatorController asset. " +
            "Apply an array of modifications including adding/removing parameters, layers, states, and transitions. " +
            "Use '" + AnimatorGetDataToolId + "' tool to get valid names and parameters for modifications.")]
        public static ModifyAnimatorResponse ModifyAnimatorController
        (
            [Description("Reference to the AnimatorController asset to modify.")]
            AssetObjectRef animatorRef,

            [Description("Array of modifications to apply to the controller.")]
            AnimatorModification[] modifications
        )
        {
            if (animatorRef == null)
                throw new ArgumentNullException(nameof(animatorRef));

            if (!animatorRef.IsValid(out var animatorRefValidationError))
                throw new ArgumentException(animatorRefValidationError, nameof(animatorRef));

            if (modifications == null)
                throw new ArgumentNullException(nameof(modifications));

            if (modifications.Length == 0)
                throw new ArgumentException("Array is empty.", nameof(modifications));

            return MainThread.Instance.Run(() =>
            {
                var controller = animatorRef.FindAssetObject<AnimatorController>();
                if (controller == null)
                    throw new Exception("AnimatorController not found.");

                if (modifications == null || modifications.Length == 0)
                    throw new Exception("Modifications array is empty or null.");

                var response = new ModifyAnimatorResponse();

                for (int i = 0; i < modifications.Length; i++)
                {
                    var mod = modifications[i];
                    try
                    {
                        ApplyAnimatorModification(controller, mod);
                    }
                    catch (Exception ex)
                    {
                        response.errors ??= new List<string>();
                        response.errors.Add($"[{i}] {mod.type}: {ex.Message}");
                    }
                }

                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                EditorUtils.RepaintAllEditorWindows();

                var assetPath = AssetDatabase.GetAssetPath(controller);
                response.modifiedAsset = new ModifyAnimatorInfo
                {
                    path = assetPath,
                    instanceId = controller.GetEntityId(),
                    name = controller.name
                };

                return response;
            });
        }

        private static void ApplyAnimatorModification(AnimatorController controller, AnimatorModification mod)
        {
            switch (mod.type)
            {
                case AnimatorModificationType.AddParameter:
                    ApplyAddParameter(controller, mod);
                    break;

                case AnimatorModificationType.RemoveParameter:
                    ApplyRemoveParameter(controller, mod);
                    break;

                case AnimatorModificationType.AddLayer:
                    ApplyAddLayer(controller, mod);
                    break;

                case AnimatorModificationType.RemoveLayer:
                    ApplyRemoveLayer(controller, mod);
                    break;

                case AnimatorModificationType.AddState:
                    ApplyAddState(controller, mod);
                    break;

                case AnimatorModificationType.RemoveState:
                    ApplyRemoveState(controller, mod);
                    break;

                case AnimatorModificationType.SetDefaultState:
                    ApplySetDefaultState(controller, mod);
                    break;

                case AnimatorModificationType.AddTransition:
                    ApplyAddTransition(controller, mod);
                    break;

                case AnimatorModificationType.RemoveTransition:
                    ApplyRemoveTransition(controller, mod);
                    break;

                case AnimatorModificationType.AddAnyStateTransition:
                    ApplyAddAnyStateTransition(controller, mod);
                    break;

                case AnimatorModificationType.SetStateMotion:
                    ApplySetStateMotion(controller, mod);
                    break;

                case AnimatorModificationType.SetStateSpeed:
                    ApplySetStateSpeed(controller, mod);
                    break;

                default:
                    throw new Exception($"Unknown modification type: {mod.type}");
            }
        }

        private static void ApplyAddParameter(AnimatorController controller, AnimatorModification mod)
        {
            if (string.IsNullOrEmpty(mod.parameterName))
                throw new Exception("parameterName is required for AddParameter.");
            if (string.IsNullOrEmpty(mod.parameterType))
                throw new Exception("parameterType is required for AddParameter.");

            if (!Enum.TryParse<AnimatorControllerParameterType>(mod.parameterType, true, out var paramType))
                throw new Exception($"Invalid parameterType: {mod.parameterType}. Valid values: Float, Int, Bool, Trigger.");

            controller.AddParameter(mod.parameterName, paramType);

            // Set default value if provided
            var parameters = controller.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == mod.parameterName)
                {
                    switch (paramType)
                    {
                        case AnimatorControllerParameterType.Float:
                            parameters[i].defaultFloat = mod.defaultFloat ?? 0f;
                            break;
                        case AnimatorControllerParameterType.Int:
                            parameters[i].defaultInt = mod.defaultInt ?? 0;
                            break;
                        case AnimatorControllerParameterType.Bool:
                            parameters[i].defaultBool = mod.defaultBool ?? false;
                            break;
                    }
                    break;
                }
            }
            controller.parameters = parameters;
        }

        private static void ApplyRemoveParameter(AnimatorController controller, AnimatorModification mod)
        {
            if (string.IsNullOrEmpty(mod.parameterName))
                throw new Exception("parameterName is required for RemoveParameter.");

            controller.RemoveParameter(controller.parameters.FirstOrDefault(p => p.name == mod.parameterName));
        }

        private static void ApplyAddLayer(AnimatorController controller, AnimatorModification mod)
        {
            if (string.IsNullOrEmpty(mod.layerName))
                throw new Exception("layerName is required for AddLayer.");

            controller.AddLayer(mod.layerName);
        }

        private static void ApplyRemoveLayer(AnimatorController controller, AnimatorModification mod)
        {
            if (string.IsNullOrEmpty(mod.layerName))
                throw new Exception("layerName is required for RemoveLayer.");

            var layerIndex = GetLayerIndex(controller, mod.layerName);
            controller.RemoveLayer(layerIndex);
        }

        private static void ApplyAddState(AnimatorController controller, AnimatorModification mod)
        {
            if (string.IsNullOrEmpty(mod.layerName))
                throw new Exception("layerName is required for AddState.");
            if (string.IsNullOrEmpty(mod.stateName))
                throw new Exception("stateName is required for AddState.");

            var layer = GetLayer(controller, mod.layerName);
            var state = layer.stateMachine.AddState(mod.stateName);

            if (!string.IsNullOrEmpty(mod.motionAssetPath))
            {
                var motion = AssetDatabase.LoadAssetAtPath<Motion>(mod.motionAssetPath);
                if (motion != null)
                    state.motion = motion;
            }
        }

        private static void ApplyRemoveState(AnimatorController controller, AnimatorModification mod)
        {
            if (string.IsNullOrEmpty(mod.layerName))
                throw new Exception("layerName is required for RemoveState.");
            if (string.IsNullOrEmpty(mod.stateName))
                throw new Exception("stateName is required for RemoveState.");

            var layer = GetLayer(controller, mod.layerName);
            var state = GetState(layer.stateMachine, mod.stateName);
            layer.stateMachine.RemoveState(state);
        }

        private static void ApplySetDefaultState(AnimatorController controller, AnimatorModification mod)
        {
            if (string.IsNullOrEmpty(mod.layerName))
                throw new Exception("layerName is required for SetDefaultState.");
            if (string.IsNullOrEmpty(mod.stateName))
                throw new Exception("stateName is required for SetDefaultState.");

            var layer = GetLayer(controller, mod.layerName);
            var state = GetState(layer.stateMachine, mod.stateName);
            layer.stateMachine.defaultState = state;
        }

        private static void ApplyAddTransition(AnimatorController controller, AnimatorModification mod)
        {
            if (string.IsNullOrEmpty(mod.layerName))
                throw new Exception("layerName is required for AddTransition.");
            if (string.IsNullOrEmpty(mod.sourceStateName))
                throw new Exception("sourceStateName is required for AddTransition.");
            if (string.IsNullOrEmpty(mod.destinationStateName))
                throw new Exception("destinationStateName is required for AddTransition.");

            var layer = GetLayer(controller, mod.layerName);
            var sourceState = GetState(layer.stateMachine, mod.sourceStateName);
            var destState = GetState(layer.stateMachine, mod.destinationStateName);

            var transition = sourceState.AddTransition(destState);
            ConfigureTransition(transition, mod);
        }

        private static void ApplyRemoveTransition(AnimatorController controller, AnimatorModification mod)
        {
            if (string.IsNullOrEmpty(mod.layerName))
                throw new Exception("layerName is required for RemoveTransition.");
            if (string.IsNullOrEmpty(mod.sourceStateName))
                throw new Exception("sourceStateName is required for RemoveTransition.");
            if (string.IsNullOrEmpty(mod.destinationStateName))
                throw new Exception("destinationStateName is required for RemoveTransition.");

            var layer = GetLayer(controller, mod.layerName);
            var sourceState = GetState(layer.stateMachine, mod.sourceStateName);

            var transitionToRemove = sourceState.transitions
                .FirstOrDefault(t => t.destinationState?.name == mod.destinationStateName);

            if (transitionToRemove != null)
                sourceState.RemoveTransition(transitionToRemove);
        }

        private static void ApplyAddAnyStateTransition(AnimatorController controller, AnimatorModification mod)
        {
            if (string.IsNullOrEmpty(mod.layerName))
                throw new Exception("layerName is required for AddAnyStateTransition.");
            if (string.IsNullOrEmpty(mod.destinationStateName))
                throw new Exception("destinationStateName is required for AddAnyStateTransition.");

            var layer = GetLayer(controller, mod.layerName);
            var destState = GetState(layer.stateMachine, mod.destinationStateName);

            var transition = layer.stateMachine.AddAnyStateTransition(destState);
            ConfigureTransition(transition, mod);
        }

        private static void ApplySetStateMotion(AnimatorController controller, AnimatorModification mod)
        {
            if (string.IsNullOrEmpty(mod.layerName))
                throw new Exception("layerName is required for SetStateMotion.");
            if (string.IsNullOrEmpty(mod.stateName))
                throw new Exception("stateName is required for SetStateMotion.");
            if (string.IsNullOrEmpty(mod.motionAssetPath))
                throw new Exception("motionAssetPath is required for SetStateMotion.");

            var layer = GetLayer(controller, mod.layerName);
            var state = GetState(layer.stateMachine, mod.stateName);
            var motion = AssetDatabase.LoadAssetAtPath<Motion>(mod.motionAssetPath);

            if (motion == null)
                throw new Exception($"Motion not found at path: {mod.motionAssetPath}");

            state.motion = motion;
        }

        private static void ApplySetStateSpeed(AnimatorController controller, AnimatorModification mod)
        {
            if (string.IsNullOrEmpty(mod.layerName))
                throw new Exception("layerName is required for SetStateSpeed.");
            if (string.IsNullOrEmpty(mod.stateName))
                throw new Exception("stateName is required for SetStateSpeed.");
            if (!mod.speed.HasValue)
                throw new Exception("speed is required for SetStateSpeed.");

            var layer = GetLayer(controller, mod.layerName);
            var state = GetState(layer.stateMachine, mod.stateName);
            state.speed = mod.speed.Value;
        }

        private static void ConfigureTransition(AnimatorStateTransition transition, AnimatorModification mod)
        {
            if (mod.hasExitTime.HasValue)
                transition.hasExitTime = mod.hasExitTime.Value;
            if (mod.exitTime.HasValue)
                transition.exitTime = mod.exitTime.Value;
            if (mod.duration.HasValue)
                transition.duration = mod.duration.Value;
            if (mod.hasFixedDuration.HasValue)
                transition.hasFixedDuration = mod.hasFixedDuration.Value;

            if (mod.conditions != null)
            {
                foreach (var condition in mod.conditions)
                {
                    if (string.IsNullOrEmpty(condition.parameter))
                        continue;

                    var conditionMode = AnimatorConditionMode.Equals;
                    if (!string.IsNullOrEmpty(condition.mode))
                    {
                        if (!Enum.TryParse<AnimatorConditionMode>(condition.mode, true, out conditionMode))
                            throw new Exception($"Invalid condition mode: {condition.mode}. Valid values: If, IfNot, Greater, Less, Equals, NotEqual.");
                    }

                    transition.AddCondition(
                        conditionMode,
                        condition.threshold ?? 0f,
                        condition.parameter
                    );
                }
            }
        }

        private static int GetLayerIndex(AnimatorController controller, string? layerName)
        {
            for (int i = 0; i < controller.layers.Length; i++)
            {
                if (controller.layers[i].name == layerName)
                    return i;
            }
            throw new Exception($"Layer not found: {layerName}");
        }

        private static AnimatorControllerLayer GetLayer(AnimatorController controller, string? layerName)
        {
            var layer = controller.layers.FirstOrDefault(l => l.name == layerName);
            if (layer.stateMachine == null)
                throw new Exception($"Layer not found: {layerName}");
            return layer;
        }

        private static AnimatorState GetState(AnimatorStateMachine stateMachine, string? stateName)
        {
            var childState = stateMachine.states.FirstOrDefault(s => s.state.name == stateName);
            if (childState.state == null)
                throw new Exception($"State not found: {stateName}");
            return childState.state;
        }

        #region Modify Response Classes

        public class ModifyAnimatorResponse
        {
            public ModifyAnimatorInfo? modifiedAsset;
            public List<string>? errors;
        }

        #endregion
    }
}
