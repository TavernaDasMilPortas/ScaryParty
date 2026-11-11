using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScaryParty.VisualStyles
{
    [Serializable]
    public class VisualSlotMetadata
    {
        public int slotIndex = 0;
        public string categoryId = VisualCategoryIds.Props;
        public VisualSemanticRole role = VisualSemanticRole.GenericOpaque;
        public IllustrateShaderFamily preferredFamily = IllustrateShaderFamily.Opaque;
        public VisualConversionAction action = VisualConversionAction.Convert;
        public bool allowDynamicTint = false;
        public bool preserveMaterial = false;
        public string notes;
    }

    /// <summary>
    /// Optional metadata component attached to variant prefabs or ambiguous meshes to declare
    /// exact per-slot semantic roles without relying solely on GameObject names.
    /// </summary>
    [DisallowMultipleComponent]
    public class VisualStyleIdentifier : MonoBehaviour
    {
        public string defaultCategoryId = VisualCategoryIds.Props;
        public VisualSemanticRole defaultRole = VisualSemanticRole.GenericOpaque;
        public bool preserveHierarchyMaterials = false;
        public List<VisualSlotMetadata> slotOverrides = new List<VisualSlotMetadata>();

        public VisualSlotMetadata GetMetadataForSlot(int slotIndex)
        {
            if (slotOverrides != null)
            {
                for (int i = 0; i < slotOverrides.Count; i++)
                {
                    var item = slotOverrides[i];
                    if (item != null && item.slotIndex == slotIndex)
                    {
                        return item;
                    }
                }
            }
            return null;
        }
    }
}
