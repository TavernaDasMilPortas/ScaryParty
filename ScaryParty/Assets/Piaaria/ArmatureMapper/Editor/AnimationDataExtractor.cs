using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

namespace ScaryParty.ArmatureMapper
{
    public static class AnimationDataExtractor
    {
        public static void ExtractAnimationData(GameObject root, ArmatureMap map)
        {
            Animator animator = root.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                Debug.LogWarning("ArmatureMapper: No Animator or Controller found in the prefab hierarchy.");
                return;
            }

            map.animatorInfo = new AnimatorInfo();
            AnimatorController controller = GetEffectiveController(animator.runtimeAnimatorController);
            
            if (controller == null)
            {
                Debug.LogWarning("ArmatureMapper: Could not resolve Editor AnimatorController.");
                return;
            }

            map.animatorInfo.controllerName = controller.name;
            map.animatorInfo.controllerPath = AssetDatabase.GetAssetPath(controller);

            // Extract Avatar Info
            if (animator.avatar != null)
            {
                map.animatorInfo.avatar = new AvatarInfo();
                map.animatorInfo.avatar.name = animator.avatar.name;
                map.animatorInfo.avatar.isHuman = animator.avatar.isHuman;
                
                if (animator.avatar.isHuman)
                {
                    HumanDescription desc = animator.avatar.humanDescription;
                    foreach (var humanBone in desc.human)
                    {
                        map.animatorInfo.avatar.humanBoneMapping.Add(new HumanBoneMapping
                        {
                            humanBoneName = humanBone.humanName,
                            boneName = humanBone.boneName
                        });
                    }
                }
            }

            // Extract Parameters
            foreach (var param in controller.parameters)
            {
                AnimatorParameterInfo paramInfo = new AnimatorParameterInfo();
                paramInfo.name = param.name;
                paramInfo.type = param.type.ToString();
                
                switch (param.type)
                {
                    case AnimatorControllerParameterType.Float:
                        paramInfo.defaultValue = param.defaultFloat.ToString();
                        break;
                    case AnimatorControllerParameterType.Int:
                        paramInfo.defaultValue = param.defaultInt.ToString();
                        break;
                    case AnimatorControllerParameterType.Bool:
                        paramInfo.defaultValue = param.defaultBool.ToString();
                        break;
                    case AnimatorControllerParameterType.Trigger:
                        paramInfo.defaultValue = "False"; // Triggers don't have default state
                        break;
                }
                map.animatorInfo.parameters.Add(paramInfo);
            }

            // Extract Layers and States
            HashSet<AnimationClip> uniqueClips = new HashSet<AnimationClip>();

            foreach (var layer in controller.layers)
            {
                AnimatorLayerInfo layerInfo = new AnimatorLayerInfo();
                layerInfo.name = layer.name;
                layerInfo.blendingMode = layer.blendingMode.ToString();
                layerInfo.weight = layer.defaultWeight;

                ExtractStateMachine(layer.stateMachine, layerInfo, uniqueClips);
                map.animatorInfo.layers.Add(layerInfo);
            }

            // Extract Animation Clips Data
            foreach (var clip in uniqueClips)
            {
                if (clip == null) continue;
                map.animationClips.Add(ExtractClipData(clip));
            }
        }

        private static AnimatorController GetEffectiveController(RuntimeAnimatorController runtimeController)
        {
            if (runtimeController is AnimatorController ac)
                return ac;
            if (runtimeController is AnimatorOverrideController aoc)
                return aoc.runtimeAnimatorController as AnimatorController;
            return null;
        }

        private static void ExtractStateMachine(AnimatorStateMachine stateMachine, AnimatorLayerInfo layerInfo, HashSet<AnimationClip> uniqueClips)
        {
            foreach (var childState in stateMachine.states)
            {
                AnimatorState state = childState.state;
                AnimatorStateInfo stateInfo = new AnimatorStateInfo();
                stateInfo.name = state.name;
                stateInfo.speed = state.speed;
                stateInfo.tag = state.tag;
                stateInfo.isDefault = (stateMachine.defaultState == state);
                
                if (state.motion != null)
                {
                    stateInfo.motionName = state.motion.name;
                    if (state.motion is AnimationClip clip)
                    {
                        stateInfo.motionType = "Clip";
                        uniqueClips.Add(clip);
                    }
                    else if (state.motion is BlendTree blendTree)
                    {
                        stateInfo.motionType = "BlendTree";
                        ExtractClipsFromBlendTree(blendTree, uniqueClips);
                    }
                }
                else
                {
                    stateInfo.motionType = "None";
                }

                // Extract transitions
                foreach (var transition in state.transitions)
                {
                    AnimatorTransitionInfo transInfo = new AnimatorTransitionInfo();
                    transInfo.destinationState = transition.destinationState != null ? transition.destinationState.name : "Exit";
                    transInfo.duration = transition.duration;
                    transInfo.offset = transition.offset;
                    transInfo.hasExitTime = transition.hasExitTime;
                    transInfo.exitTime = transition.exitTime;

                    foreach (var cond in transition.conditions)
                    {
                        AnimatorConditionInfo condInfo = new AnimatorConditionInfo();
                        condInfo.mode = cond.mode.ToString();
                        condInfo.parameter = cond.parameter;
                        condInfo.threshold = cond.threshold;
                        transInfo.conditions.Add(condInfo);
                    }

                    stateInfo.transitions.Add(transInfo);
                }

                layerInfo.states.Add(stateInfo);
            }

            // Recursively process child state machines
            foreach (var childSM in stateMachine.stateMachines)
            {
                ExtractStateMachine(childSM.stateMachine, layerInfo, uniqueClips);
            }
        }

        private static void ExtractClipsFromBlendTree(BlendTree blendTree, HashSet<AnimationClip> uniqueClips)
        {
            foreach (var child in blendTree.children)
            {
                if (child.motion is AnimationClip clip)
                {
                    uniqueClips.Add(clip);
                }
                else if (child.motion is BlendTree childTree)
                {
                    ExtractClipsFromBlendTree(childTree, uniqueClips);
                }
            }
        }

        private static AnimationClipData ExtractClipData(AnimationClip clip)
        {
            AnimationClipData clipData = new AnimationClipData();
            clipData.clipName = clip.name;
            clipData.duration = clip.length;
            clipData.frameRate = clip.frameRate;
            clipData.isLooping = clip.isLooping;
            clipData.wrapMode = clip.wrapMode.ToString();
            clipData.assetPath = AssetDatabase.GetAssetPath(clip);

            // Events
            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
            foreach (var ev in events)
            {
                clipData.events.Add(new AnimationEventData
                {
                    time = ev.time,
                    functionName = ev.functionName,
                    stringParameter = ev.stringParameter,
                    floatParameter = ev.floatParameter,
                    intParameter = ev.intParameter
                });
            }

            // Curves
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            foreach (var binding in bindings)
            {
                CurveBindingData cbData = new CurveBindingData();
                cbData.path = binding.path;
                cbData.propertyName = binding.propertyName;
                cbData.type = binding.type.Name;

                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve != null)
                {
                    foreach (var kf in curve.keys)
                    {
                        cbData.keyframes.Add(new KeyframeData
                        {
                            time = kf.time,
                            value = kf.value,
                            inTangent = kf.inTangent,
                            outTangent = kf.outTangent,
                            inWeight = kf.inWeight,
                            outWeight = kf.outWeight,
                            weightedMode = kf.weightedMode.ToString()
                        });
                    }
                }
                clipData.curveBindings.Add(cbData);
            }

            return clipData;
        }
    }
}
