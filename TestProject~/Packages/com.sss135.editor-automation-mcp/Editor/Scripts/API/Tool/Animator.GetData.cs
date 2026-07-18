#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using EA;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Runtime.Extensions;
using UnityEditor.Animations;

namespace com.SSS135.EditorAutomation.Editor.API
{
    public static partial class Tool_Animator
    {
        public const string AnimatorGetDataToolId = "animator-get-data";
        [McpTool
        (
            AnimatorGetDataToolId,
            Title = "Animator / Get Data",
            ReadOnlyHint = true,
            DestructiveHint = false,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description(@"Get data about a Unity AnimatorController asset file. Returns information such as name, layers, parameters, and states.")]
        public static GetAnimatorDataResponse GetData
        (
            [Description("Reference to the AnimatorController asset. The path should start with 'Assets/' and end with '.controller'.")]
            AssetObjectRef animatorRef
        )
        {
            if (animatorRef == null)
                throw new ArgumentNullException(nameof(animatorRef));

            if (!animatorRef.IsValid(out var animatorRefValidationError))
                throw new ArgumentException(animatorRefValidationError, nameof(animatorRef));

            return MainThread.Instance.Run(() =>
            {
                var controller = animatorRef.FindAssetObject<AnimatorController>();
                if (controller == null)
                    throw new Exception($"AnimatorController not found.");

                var response = new GetAnimatorDataResponse
                {
                    name = controller.name
                };

                // Get parameters
                response.parameters = new List<AnimatorParameterInfo>();
                foreach (var param in controller.parameters)
                {
                    response.parameters.Add(new AnimatorParameterInfo
                    {
                        name = param.name,
                        type = param.type.ToString(),
                        defaultFloat = param.defaultFloat,
                        defaultInt = param.defaultInt,
                        defaultBool = param.defaultBool
                    });
                }

                // Get layers
                response.layers = new List<AnimatorLayerInfo>();
                foreach (var layer in controller.layers)
                {
                    var layerInfo = new AnimatorLayerInfo
                    {
                        name = layer.name,
                        defaultWeight = layer.defaultWeight,
                        blendingMode = layer.blendingMode.ToString(),
                        syncedLayerIndex = layer.syncedLayerIndex,
                        iKPass = layer.iKPass
                    };

                    // Get states in the layer's state machine
                    if (layer.stateMachine != null)
                    {
                        layerInfo.states = new List<AnimatorStateInfo>();
                        foreach (var childState in layer.stateMachine.states)
                        {
                            var state = childState.state;
                            var stateInfo = new AnimatorStateInfo
                            {
                                name = state.name,
                                tag = state.tag,
                                speed = state.speed,
                                speedParameterActive = state.speedParameterActive,
                                speedParameter = state.speedParameter,
                                cycleOffset = state.cycleOffset,
                                cycleOffsetParameterActive = state.cycleOffsetParameterActive,
                                cycleOffsetParameter = state.cycleOffsetParameter,
                                mirror = state.mirror,
                                mirrorParameterActive = state.mirrorParameterActive,
                                mirrorParameter = state.mirrorParameter,
                                writeDefaultValues = state.writeDefaultValues,
                                motionName = state.motion != null ? state.motion.name : null
                            };

                            // Get transitions
                            stateInfo.transitions = new List<AnimatorTransitionInfo>();
                            foreach (var transition in state.transitions)
                            {
                                var transitionInfo = new AnimatorTransitionInfo
                                {
                                    destinationStateName = transition.destinationState?.name,
                                    hasExitTime = transition.hasExitTime,
                                    exitTime = transition.exitTime,
                                    hasFixedDuration = transition.hasFixedDuration,
                                    duration = transition.duration,
                                    offset = transition.offset,
                                    canTransitionToSelf = transition.canTransitionToSelf
                                };

                                // Get conditions
                                transitionInfo.conditions = new List<AnimatorConditionInfo>();
                                foreach (var condition in transition.conditions)
                                {
                                    transitionInfo.conditions.Add(new AnimatorConditionInfo
                                    {
                                        parameter = condition.parameter,
                                        mode = condition.mode.ToString(),
                                        threshold = condition.threshold
                                    });
                                }

                                stateInfo.transitions.Add(transitionInfo);
                            }

                            layerInfo.states.Add(stateInfo);
                        }

                        // Get sub-state machines
                        layerInfo.subStateMachines = new List<string>();
                        foreach (var childMachine in layer.stateMachine.stateMachines)
                        {
                            layerInfo.subStateMachines.Add(childMachine.stateMachine.name);
                        }

                        // Get default state
                        layerInfo.defaultStateName = layer.stateMachine.defaultState?.name;

                        // Get any state transitions
                        layerInfo.anyStateTransitions = new List<AnimatorTransitionInfo>();
                        foreach (var transition in layer.stateMachine.anyStateTransitions)
                        {
                            var transitionInfo = new AnimatorTransitionInfo
                            {
                                destinationStateName = transition.destinationState?.name,
                                hasExitTime = transition.hasExitTime,
                                exitTime = transition.exitTime,
                                hasFixedDuration = transition.hasFixedDuration,
                                duration = transition.duration,
                                offset = transition.offset,
                                canTransitionToSelf = transition.canTransitionToSelf
                            };

                            transitionInfo.conditions = new List<AnimatorConditionInfo>();
                            foreach (var condition in transition.conditions)
                            {
                                transitionInfo.conditions.Add(new AnimatorConditionInfo
                                {
                                    parameter = condition.parameter,
                                    mode = condition.mode.ToString(),
                                    threshold = condition.threshold
                                });
                            }

                            layerInfo.anyStateTransitions.Add(transitionInfo);
                        }
                    }

                    response.layers.Add(layerInfo);
                }

                return response;
            });
        }

        #region GetData Response Classes

        public class GetAnimatorDataResponse
        {
            public string name = string.Empty;
            public List<AnimatorParameterInfo>? parameters;
            public List<AnimatorLayerInfo>? layers;
        }

        #endregion
    }
}
