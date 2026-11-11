using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScaryParty.VisualStyles
{
    [Serializable]
    public class RoleMaterialEntry
    {
        public VisualSemanticRole role;
        public IllustrateShaderFamily shaderFamily = IllustrateShaderFamily.Opaque;
        public Material material;
        public Color defaultColor = Color.white;
        public bool supportsDynamicTint;
    }

    [Serializable]
    public class MappedMaterialEntry
    {
        public string conversionKey;
        public string sourceMaterialGuid;
        public long sourceLocalFileId;
        public string sourceMaterialName;
        public string sourceShaderName;
        public string sourceTextureGuid;
        public Material sourceMaterial;

        public string categoryId;
        public VisualSemanticRole semanticRole;
        public IllustrateShaderFamily shaderFamily;
        public VisualConversionAction action;
        public float confidence;
        public string classificationReason;

        public Material targetMaterial;
        public string targetMaterialGuid;
        public bool allowsDynamicTint;
        public bool preservedOriginal;
    }

    [Serializable]
    public class ContextSlotBindingEntry
    {
        public string contextKey; // e.g., prefab GUID or modular module ID
        public int slotIndex;
        public VisualSemanticRole role;
        public string categoryId;
        public Material targetMaterial;
        public bool allowsDynamicTint;
    }

    /// <summary>
    /// Serialized runtime material library mapping semantic roles, source materials, and
    /// context slots to generated Distant Lands materials without touching the Editor asset database.
    /// </summary>
    [CreateAssetMenu(fileName = "VisualMaterialLibrary", menuName = "Scary Party/Visual Styles/Material Library")]
    public class VisualMaterialLibrary : ScriptableObject
    {
        public string styleId;
        public int profileVersion;
        public string profileContentHash;
        public string generationTimestampUtc;
        public VisualStyleProfile profile;
        public VisualConversionRuleSet ruleSet;

        [Header("Variant Render / Scene References")]
        public ScriptableObject renderPipelineRendererData;
        public GameObject styledPizzeriaVariantPrefab;
        public GameObject styledCharacterVariantPrefab;
        public ScriptableObject styledItemCatalogAsset;

        [Header("Semantic Role Defaults")]
        public List<RoleMaterialEntry> roleDefaults = new List<RoleMaterialEntry>();

        [Header("Exact Source-to-Target Material Mappings")]
        public List<MappedMaterialEntry> mappedMaterials = new List<MappedMaterialEntry>();

        [Header("Context Slot Bindings (Modular Kit / Special Prefabs)")]
        public List<ContextSlotBindingEntry> contextSlotBindings = new List<ContextSlotBindingEntry>();

        [NonSerialized] private Dictionary<VisualSemanticRole, RoleMaterialEntry> _roleMap;
        [NonSerialized] private Dictionary<Material, MappedMaterialEntry> _sourceMatMap;
        [NonSerialized] private Dictionary<string, MappedMaterialEntry> _sourceNameMap;
        [NonSerialized] private Dictionary<string, ContextSlotBindingEntry> _contextSlotMap;
        [NonSerialized] private bool _initialized;

        public void InvalidateLookupCache()
        {
            _initialized = false;
            _roleMap = null;
            _sourceMatMap = null;
            _sourceNameMap = null;
            _contextSlotMap = null;
        }

        public void EnsureLookupInitialized()
        {
            if (_initialized) return;

            _roleMap = new Dictionary<VisualSemanticRole, RoleMaterialEntry>();
            if (roleDefaults != null)
            {
                for (int i = 0; i < roleDefaults.Count; i++)
                {
                    var entry = roleDefaults[i];
                    if (entry != null && !_roleMap.ContainsKey(entry.role))
                    {
                        _roleMap[entry.role] = entry;
                    }
                }
            }

            _sourceMatMap = new Dictionary<Material, MappedMaterialEntry>();
            _sourceNameMap = new Dictionary<string, MappedMaterialEntry>(StringComparer.OrdinalIgnoreCase);
            if (mappedMaterials != null)
            {
                for (int i = 0; i < mappedMaterials.Count; i++)
                {
                    var entry = mappedMaterials[i];
                    if (entry == null) continue;
                    if (entry.sourceMaterial != null && !_sourceMatMap.ContainsKey(entry.sourceMaterial))
                    {
                        _sourceMatMap[entry.sourceMaterial] = entry;
                    }
                    if (!string.IsNullOrEmpty(entry.sourceMaterialName) && !_sourceNameMap.ContainsKey(entry.sourceMaterialName))
                    {
                        _sourceNameMap[entry.sourceMaterialName] = entry;
                    }
                }
            }

            _contextSlotMap = new Dictionary<string, ContextSlotBindingEntry>(StringComparer.Ordinal);
            if (contextSlotBindings != null)
            {
                for (int i = 0; i < contextSlotBindings.Count; i++)
                {
                    var b = contextSlotBindings[i];
                    if (b == null || string.IsNullOrEmpty(b.contextKey)) continue;
                    _contextSlotMap[$"{b.contextKey}#{b.slotIndex}"] = b;
                }
            }

            _initialized = true;
        }

        public Material GetRoleMaterial(VisualSemanticRole role)
        {
            EnsureLookupInitialized();
            return _roleMap.TryGetValue(role, out var entry) && entry.material != null ? entry.material : null;
        }

        public bool TryGetMappedMaterial(Material source, out MappedMaterialEntry entry)
        {
            EnsureLookupInitialized();
            entry = null;
            if (source == null) return false;
            if (_sourceMatMap.TryGetValue(source, out entry)) return true;
            if (!string.IsNullOrEmpty(source.name) && _sourceNameMap.TryGetValue(source.name, out entry)) return true;
            return false;
        }

        public bool TryGetContextSlotMaterial(string contextKey, int slotIndex, out ContextSlotBindingEntry binding)
        {
            EnsureLookupInitialized();
            binding = null;
            if (string.IsNullOrEmpty(contextKey)) return false;
            return _contextSlotMap.TryGetValue($"{contextKey}#{slotIndex}", out binding);
        }

        public void SetRoleDefault(RoleMaterialEntry entry)
        {
            if (entry == null) return;
            for (int i = 0; i < roleDefaults.Count; i++)
            {
                if (roleDefaults[i] != null && roleDefaults[i].role == entry.role)
                {
                    roleDefaults[i] = entry;
                    InvalidateLookupCache();
                    return;
                }
            }
            roleDefaults.Add(entry);
            InvalidateLookupCache();
        }

        public void UpsertMappedMaterial(MappedMaterialEntry entry)
        {
            if (entry == null) return;
            for (int i = 0; i < mappedMaterials.Count; i++)
            {
                var existing = mappedMaterials[i];
                if (existing != null && existing.conversionKey == entry.conversionKey)
                {
                    mappedMaterials[i] = entry;
                    InvalidateLookupCache();
                    return;
                }
            }
            mappedMaterials.Add(entry);
            InvalidateLookupCache();
        }
    }
}
