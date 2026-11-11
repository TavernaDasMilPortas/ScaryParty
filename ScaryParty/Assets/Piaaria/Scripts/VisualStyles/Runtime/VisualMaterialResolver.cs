using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScaryParty.VisualStyles
{
    /// <summary>
    /// Stateless runtime resolver wrapping a serialized VisualMaterialLibrary.
    /// Guarantees zero AssetDatabase calls, zero Shader.Find calls per spawn,
    /// shared Material assignment, and non-destructive per-slot MaterialPropertyBlock coloring.
    /// </summary>
    public sealed class VisualMaterialResolver
    {
        private readonly VisualStyleProfile _profile;
        private readonly VisualMaterialLibrary _library;
        private readonly HashSet<string> _loggedUnmapped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly MaterialPropertyBlock _sharedBlock = new MaterialPropertyBlock();
        private readonly Dictionary<Renderer, Material[]> _originalMaterials = new Dictionary<Renderer, Material[]>();

        public int UnmappedAssetCount { get; private set; }
        public int FallbackUsageCount { get; private set; }
        public VisualMaterialLibrary Library => _library;
        public VisualStyleProfile Profile => _profile;

        public VisualMaterialResolver(VisualStyleProfile profile, VisualMaterialLibrary library)
        {
            _profile = profile;
            _library = library;
            if (_library != null)
            {
                _library.EnsureLookupInitialized();
            }
        }

        public bool IsActive => _library != null;

        public Material ResolveRoleMaterial(VisualSemanticRole role)
        {
            if (_library == null) return null;
            Material mat = _library.GetRoleMaterial(role);
            if (mat != null) return mat;

            FallbackUsageCount++;
            return _library.GetRoleMaterial(VisualSemanticRole.GenericOpaque);
        }

        public Material ResolveForSlot(
            Material sourceMaterial,
            VisualSemanticRole roleHint,
            string categoryHint,
            string contextKey,
            int slotIndex,
            out bool shouldPreserve,
            out bool allowsDynamicTint,
            out bool wasUnmapped)
        {
            shouldPreserve = false;
            allowsDynamicTint = false;
            wasUnmapped = false;

            if (_library == null) return sourceMaterial;

            // 1. Context + slot binding (e.g., modular piece slots or custom multi-slot prefabs)
            if (!string.IsNullOrEmpty(contextKey) &&
                _library.TryGetContextSlotMaterial(contextKey, slotIndex, out ContextSlotBindingEntry binding))
            {
                allowsDynamicTint = binding.allowsDynamicTint;
                if (binding.targetMaterial != null)
                {
                    return binding.targetMaterial;
                }
            }

            // 2. Exact source material or name mapping in serialized library
            if (sourceMaterial != null && _library.TryGetMappedMaterial(sourceMaterial, out MappedMaterialEntry mapped))
            {
                shouldPreserve = mapped.preservedOriginal || mapped.action == VisualConversionAction.Preserve || mapped.action == VisualConversionAction.Skip;
                allowsDynamicTint = mapped.allowsDynamicTint;

                if (shouldPreserve)
                {
                    return sourceMaterial;
                }

                if (mapped.targetMaterial != null)
                {
                    return mapped.targetMaterial;
                }
            }

            // 3. Fallback to explicit semantic role in library
            if (roleHint != VisualSemanticRole.Unclassified && roleHint != VisualSemanticRole.Preserve)
            {
                Material roleMat = _library.GetRoleMaterial(roleHint);
                if (roleMat != null)
                {
                    FallbackUsageCount++;
                    return roleMat;
                }
            }

            // 4. Preserve already-stylized Illustrate materials
            if (sourceMaterial != null && VisualStyleShaderUtility.IsIllustrateShader(sourceMaterial.shader))
            {
                return sourceMaterial;
            }

            // 5. Preserve excluded built-in shader domains safely (TMP, UI, Particles, OutlineService)
            if (sourceMaterial != null && IsSpecialPreservedShader(sourceMaterial.shader))
            {
                shouldPreserve = true;
                return sourceMaterial;
            }

            // 6. Unmapped asset policy (log once, avoid per-spawn AssetDatabase or material creation)
            wasUnmapped = true;
            UnmappedAssetCount++;
            string sourceName = sourceMaterial != null ? sourceMaterial.name : "<null>";
            if (_loggedUnmapped.Add(sourceName))
            {
                Debug.LogWarning($"[VisualMaterialResolver] UnmappedAsset '{sourceName}' (category='{categoryHint}', role='{roleHint}', context='{contextKey}#{slotIndex}'). Using policy={_profile?.discovery.newUnknownMaterialPolicy}.");
            }

            if (_profile != null && _profile.discovery.newUnknownMaterialPolicy == VisualDiscoveryPolicy.Preserve)
            {
                return sourceMaterial;
            }

            FallbackUsageCount++;
            Material fallback = _library.GetRoleMaterial(VisualSemanticRole.GenericOpaque);
            return fallback != null ? fallback : sourceMaterial;
        }

        public void ApplyToRenderer(Renderer renderer, string contextKey = null, VisualSemanticRole defaultRole = VisualSemanticRole.Unclassified, string categoryHint = VisualCategoryIds.Props, bool recordOriginal = true)
        {
            if (renderer == null || _library == null) return;
            if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer || renderer is SpriteRenderer)
            {
                return;
            }

            var identifier = renderer.GetComponent<VisualStyleIdentifier>();
            if (identifier != null && identifier.preserveHierarchyMaterials)
            {
                return;
            }

            Material[] current = renderer.sharedMaterials;
            if (current == null || current.Length == 0) return;

            if (recordOriginal && !_originalMaterials.ContainsKey(renderer))
            {
                var backup = new Material[current.Length];
                Array.Copy(current, backup, current.Length);
                _originalMaterials[renderer] = backup;
            }

            bool changed = false;
            var updated = new Material[current.Length];
            for (int i = 0; i < current.Length; i++)
            {
                VisualSlotMetadata slotMeta = identifier != null ? identifier.GetMetadataForSlot(i) : null;
                if (slotMeta != null && (slotMeta.preserveMaterial || slotMeta.action == VisualConversionAction.Preserve))
                {
                    updated[i] = current[i];
                    continue;
                }

                VisualSemanticRole slotRole = slotMeta != null
                    ? slotMeta.role
                    : (identifier != null ? identifier.defaultRole : defaultRole);
                string slotCategory = slotMeta != null
                    ? slotMeta.categoryId
                    : (identifier != null ? identifier.defaultCategoryId : categoryHint);

                Material resolved = ResolveForSlot(current[i], slotRole, slotCategory, contextKey, i, out _, out _, out _);
                updated[i] = resolved != null ? resolved : current[i];
                if (!ReferenceEquals(updated[i], current[i]))
                {
                    changed = true;
                }
            }

            if (changed)
            {
                renderer.sharedMaterials = updated;
            }
        }

        public void ApplyToHierarchy(GameObject root, string contextKey = null, VisualSemanticRole defaultRole = VisualSemanticRole.Unclassified, string categoryHint = VisualCategoryIds.Props, bool includeInactive = true, bool recordOriginal = true)
        {
            if (root == null || _library == null) return;

            var rootIdentifier = root.GetComponent<VisualStyleIdentifier>();
            if (rootIdentifier != null && rootIdentifier.preserveHierarchyMaterials) return;

            var renderers = root.GetComponentsInChildren<Renderer>(includeInactive);
            for (int i = 0; i < renderers.Length; i++)
            {
                ApplyToRenderer(renderers[i], contextKey, defaultRole, categoryHint, recordOriginal);
            }
        }

        public void ApplyColor(Renderer renderer, int slotIndex, Color color)
        {
            VisualStyleShaderUtility.ApplyColorBlockToSlot(renderer, slotIndex, color, _sharedBlock);
        }

        public void RestoreRecordedOriginals()
        {
            foreach (var kvp in _originalMaterials)
            {
                if (kvp.Key != null && kvp.Value != null)
                {
                    kvp.Key.sharedMaterials = kvp.Value;
                }
            }
            _originalMaterials.Clear();
        }

        private static bool IsSpecialPreservedShader(Shader shader)
        {
            if (shader == null) return false;
            string n = shader.name;
            return n.StartsWith("ScaryParty/Outline", StringComparison.OrdinalIgnoreCase) ||
                   n.StartsWith("TextMeshPro", StringComparison.OrdinalIgnoreCase) ||
                   n.StartsWith("UI/", StringComparison.OrdinalIgnoreCase) ||
                   n.IndexOf("Particles", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   n.StartsWith("Sprites/", StringComparison.OrdinalIgnoreCase);
        }
    }
}
