using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScaryParty.Cutscenes
{
    public enum BindingCompatibilityStatus
    {
        Unchecked = 0,
        Compatible = 1,
        NeedsAdjustment = 2,
        Incompatible = 3
    }

    [Serializable]
    public sealed class ObjectBindingEntry
    {
        public string sourceId;
        public string sourceName;
        public string sourceType;
        public GameObject targetPrefab;
        public bool useExportedFallback;
        public Vector3 pivotOffset;
        public Vector3 scaleOverride = Vector3.one;
        public BindingCompatibilityStatus compatibility = BindingCompatibilityStatus.Unchecked;
        public string compatibilityReason;
    }

    [Serializable]
    public sealed class MaterialBindingEntry
    {
        public string sourceObjectId;
        public string rendererPath;
        public int slotIndex;
        public string slotSemanticName;
        public string sourceMaterialName;
        public Material targetMaterial;
        public bool isConfirmed;
    }

    [CreateAssetMenu(fileName = "CutsceneBindingProfile", menuName = "ScaryParty/Cutscenes/Binding Profile")]
    public sealed class CutsceneBindingProfile : ScriptableObject
    {
        [Header("Source Package")]
        public string cutsceneId;
        public string schemaVersion;
        public string manifestHash;
        public string manifestAssetPath;
        public string fbxAssetPath;

        [Header("Bindings")]
        public List<ObjectBindingEntry> objectBindings = new List<ObjectBindingEntry>();
        public List<MaterialBindingEntry> materialBindings = new List<MaterialBindingEntry>();

        public ObjectBindingEntry FindObjectBinding(string sourceId)
        {
            for (int i = 0; i < objectBindings.Count; i++)
            {
                if (objectBindings[i].sourceId == sourceId)
                    return objectBindings[i];
            }
            return null;
        }

        public MaterialBindingEntry FindMaterialBinding(string sourceObjectId, int slotIndex)
        {
            for (int i = 0; i < materialBindings.Count; i++)
            {
                var m = materialBindings[i];
                if (m.sourceObjectId == sourceObjectId && m.slotIndex == slotIndex)
                    return m;
            }
            return null;
        }
    }
}
