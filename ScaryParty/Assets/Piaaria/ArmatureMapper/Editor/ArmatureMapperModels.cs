using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScaryParty.ArmatureMapper
{
    [Serializable]
    public class ArmatureMap
    {
        public PrefabInfo prefabInfo;
        public List<BoneNode> hierarchy;
        public List<SkinnedMeshInfo> skinnedMeshes;
        public AnimatorInfo animatorInfo;
        public List<AnimationClipData> animationClips;

        public ArmatureMap()
        {
            hierarchy = new List<BoneNode>();
            skinnedMeshes = new List<SkinnedMeshInfo>();
            animationClips = new List<AnimationClipData>();
        }
    }

    [Serializable]
    public class PrefabInfo
    {
        public string name;
        public string assetPath;
        public int totalBoneCount;
        public int totalComponentCount;
        public string exportTimestamp;
    }

    [Serializable]
    public class BoneNode
    {
        public string name;
        public string path;
        public Vector3 localPosition;
        public Vector3 localRotation; // Euler angles
        public Vector3 localScale;
        public Vector3 worldPosition;
        public Vector3 worldRotation;
        public int depth;
        public List<string> components;
        public List<BoneNode> children;

        public BoneNode()
        {
            components = new List<string>();
            children = new List<BoneNode>();
        }
    }

    [Serializable]
    public class SkinnedMeshInfo
    {
        public string meshName;
        public List<string> materialNames;
        public List<string> boneNames;
        public string rootBoneName;
        public List<string> blendShapeNames;
        public Bounds boundingBox;

        public SkinnedMeshInfo()
        {
            materialNames = new List<string>();
            boneNames = new List<string>();
            blendShapeNames = new List<string>();
        }
    }

    [Serializable]
    public class AnimatorInfo
    {
        public string controllerName;
        public string controllerPath;
        public List<AnimatorParameterInfo> parameters;
        public List<AnimatorLayerInfo> layers;
        public AvatarInfo avatar;

        public AnimatorInfo()
        {
            parameters = new List<AnimatorParameterInfo>();
            layers = new List<AnimatorLayerInfo>();
        }
    }

    [Serializable]
    public class AnimatorParameterInfo
    {
        public string name;
        public string type;
        public string defaultValue; // Stored as string for simplicity across types
    }

    [Serializable]
    public class AnimatorLayerInfo
    {
        public string name;
        public string blendingMode;
        public float weight;
        public List<AnimatorStateInfo> states;

        public AnimatorLayerInfo()
        {
            states = new List<AnimatorStateInfo>();
        }
    }

    [Serializable]
    public class AnimatorStateInfo
    {
        public string name;
        public float speed;
        public string tag;
        public bool isDefault;
        public string motionName;
        public string motionType; // "Clip", "BlendTree", or "None"
        public List<AnimatorTransitionInfo> transitions;

        public AnimatorStateInfo()
        {
            transitions = new List<AnimatorTransitionInfo>();
        }
    }

    [Serializable]
    public class AnimatorTransitionInfo
    {
        public string destinationState;
        public float duration;
        public float offset;
        public bool hasExitTime;
        public float exitTime;
        public List<AnimatorConditionInfo> conditions;

        public AnimatorTransitionInfo()
        {
            conditions = new List<AnimatorConditionInfo>();
        }
    }

    [Serializable]
    public class AnimatorConditionInfo
    {
        public string mode;
        public string parameter;
        public float threshold;
    }

    [Serializable]
    public class AvatarInfo
    {
        public string name;
        public bool isHuman;
        public List<HumanBoneMapping> humanBoneMapping;

        public AvatarInfo()
        {
            humanBoneMapping = new List<HumanBoneMapping>();
        }
    }

    [Serializable]
    public class HumanBoneMapping
    {
        public string humanBoneName;
        public string boneName;
    }

    [Serializable]
    public class AnimationClipData
    {
        public string clipName;
        public float duration;
        public float frameRate;
        public bool isLooping;
        public string wrapMode;
        public string assetPath;
        public List<AnimationEventData> events;
        public List<CurveBindingData> curveBindings;

        public AnimationClipData()
        {
            events = new List<AnimationEventData>();
            curveBindings = new List<CurveBindingData>();
        }
    }

    [Serializable]
    public class AnimationEventData
    {
        public float time;
        public string functionName;
        public string stringParameter;
        public float floatParameter;
        public int intParameter;
    }

    [Serializable]
    public class CurveBindingData
    {
        public string path;
        public string propertyName;
        public string type;
        public List<KeyframeData> keyframes;

        public CurveBindingData()
        {
            keyframes = new List<KeyframeData>();
        }
    }

    [Serializable]
    public class KeyframeData
    {
        public float time;
        public float value;
        public float inTangent;
        public float outTangent;
        public float inWeight;
        public float outWeight;
        public string weightedMode;
    }
}
