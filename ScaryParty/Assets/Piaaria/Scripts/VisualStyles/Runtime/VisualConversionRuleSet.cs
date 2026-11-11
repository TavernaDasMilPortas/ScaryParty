using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ScaryParty.VisualStyles
{
    [Serializable]
    public class VisualCategoryDefinition
    {
        public string categoryId = VisualCategoryIds.Props;
        public string displayName = "Props";
        [TextArea] public string description;
        public IllustrateShaderFamily defaultShaderFamily = IllustrateShaderFamily.Opaque;
        public VisualSemanticRole defaultSemanticRole = VisualSemanticRole.GenericOpaque;
        public VisualConversionAction defaultAction = VisualConversionAction.Convert;
        public bool preserveTexturesByDefault = true;
        public bool allowDynamicTint = false;
    }

    [Serializable]
    public class VisualExclusionRule
    {
        public string ruleName = "Exclude UI/Effects";
        public bool enabled = true;
        public string shaderNameContains;
        public string assetPathPrefix;
        public string rendererTypeName;
        public string materialNameRegex;
        public string tagEquals;
        public string reason = "Specialized shader or UI preserved automatically.";
    }

    [Serializable]
    public class VisualClassificationRule
    {
        public string ruleName = "Rule";
        public bool enabled = true;
        public int priority = 100;

        [Header("Match Criteria (all non-empty criteria must match)")]
        public string assetPathPrefix;
        public string assetPathRegex;
        public string materialNameRegex;
        public string sourceShaderContains;
        public string requiredTextureProperty;
        public bool requireTransparentQueue;
        public bool requireAlphaClipped;
        public bool requireEmission;
        public string requiredTag;

        [Header("Result")]
        public string targetCategoryId = VisualCategoryIds.Props;
        public VisualSemanticRole targetRole = VisualSemanticRole.GenericOpaque;
        public IllustrateShaderFamily targetShaderFamily = IllustrateShaderFamily.Opaque;
        public VisualConversionAction action = VisualConversionAction.Convert;
        [Range(0f, 1f)] public float confidence = 0.9f;
        public bool preserveTexture = true;
        public bool preserveColor = true;
        public bool allowDynamicTint = false;
        public string reason = "Matched user classification rule.";
    }

    [Serializable]
    public class VisualSlotOverride
    {
        public string sourceAssetGuid;
        public long sourceLocalFileId;
        public int slotIndex = -1;
        public string rendererRelativePath;
        public string displayLabel;

        public string overrideCategoryId = VisualCategoryIds.Props;
        public VisualSemanticRole overrideRole = VisualSemanticRole.GenericOpaque;
        public IllustrateShaderFamily overrideShaderFamily = IllustrateShaderFamily.Opaque;
        public VisualConversionAction overrideAction = VisualConversionAction.Convert;
        public bool preserveTexture = true;
        public bool preserveColor = true;
        public bool allowDynamicTint = false;
        public string notes = "Manual override";
    }

    /// <summary>
    /// Input metadata descriptor evaluated by VisualConversionRuleSet.
    /// Produced by Editor discovery without tying runtime to asset scanning APIs.
    /// </summary>
    [Serializable]
    public struct VisualAssetSlotDescriptor
    {
        public string sourceAssetGuid;
        public string sourceMaterialGuid;
        public long sourceLocalFileId;
        public int slotIndex;
        public string rendererTypeName;
        public string rendererRelativePath;
        public string gameObjectName;
        public string gameObjectTag;
        public string assetPath;
        public string materialName;
        public string shaderName;
        public bool hasMainTexture;
        public bool hasNormalMap;
        public bool isTransparentQueue;
        public bool isAlphaClipped;
        public bool hasEmission;
        public bool isShaderGraph;

        // Optional semantic metadata provided by profiles/identifiers/catalogs
        public bool hasDeclaredMetadata;
        public string declaredCategoryId;
        public VisualSemanticRole declaredRole;
        public IllustrateShaderFamily declaredShaderFamily;
        public VisualConversionAction declaredAction;
        public bool declaredAllowDynamicTint;
        public string declaredReason;
    }

    [Serializable]
    public struct VisualClassificationDecision
    {
        public string categoryId;
        public VisualSemanticRole semanticRole;
        public IllustrateShaderFamily shaderFamily;
        public VisualConversionAction action;
        public float confidence;
        public bool preserveTexture;
        public bool preserveColor;
        public bool allowDynamicTint;
        public int priorityTier;
        public string ruleName;
        public string reason;
    }

    /// <summary>
    /// Inspector-extensible ruleset controlling exclusions, explicit slot overrides,
    /// custom categories, and classification rules without modifying C# source.
    /// </summary>
    [CreateAssetMenu(fileName = "VisualConversionRuleSet", menuName = "Scary Party/Visual Styles/Conversion Rule Set")]
    public class VisualConversionRuleSet : ScriptableObject
    {
        public int ruleSetVersion = 1;
        public List<VisualCategoryDefinition> categories = new List<VisualCategoryDefinition>();
        public List<VisualExclusionRule> exclusions = new List<VisualExclusionRule>();
        public List<VisualClassificationRule> rules = new List<VisualClassificationRule>();
        public List<VisualSlotOverride> manualOverrides = new List<VisualSlotOverride>();

        /// <summary>
        /// Evaluates a material slot following strict priority order:
        /// 1. Explicit exclusion / preserve
        /// 2. Explicit manual override (GUID + localFileID + slot)
        /// 3. Declared semantic metadata
        /// 4. User rules sorted by priority
        /// 5. Material-property heuristics (with visible confidence)
        /// 6. Unclassified (ManualReview)
        /// </summary>
        public VisualClassificationDecision Evaluate(in VisualAssetSlotDescriptor slot)
        {
            // Tier 1: Exclusions / Preserve
            for (int i = 0; i < exclusions.Count; i++)
            {
                var ex = exclusions[i];
                if (ex == null || !ex.enabled) continue;
                if (MatchesExclusion(ex, in slot))
                {
                    return new VisualClassificationDecision
                    {
                        categoryId = VisualCategoryIds.Preserve,
                        semanticRole = VisualSemanticRole.Preserve,
                        shaderFamily = IllustrateShaderFamily.PreserveOriginal,
                        action = VisualConversionAction.Preserve,
                        confidence = 1f,
                        preserveTexture = true,
                        preserveColor = true,
                        allowDynamicTint = false,
                        priorityTier = 1,
                        ruleName = ex.ruleName,
                        reason = string.IsNullOrEmpty(ex.reason) ? $"Excluded by '{ex.ruleName}'." : ex.reason
                    };
                }
            }

            // Tier 2: Explicit Manual Override
            VisualSlotOverride ov = FindManualOverride(slot);
            if (ov != null)
            {
                return new VisualClassificationDecision
                {
                    categoryId = ov.overrideCategoryId,
                    semanticRole = ov.overrideRole,
                    shaderFamily = ov.overrideShaderFamily,
                    action = ov.overrideAction,
                    confidence = 1f,
                    preserveTexture = ov.preserveTexture,
                    preserveColor = ov.preserveColor,
                    allowDynamicTint = ov.allowDynamicTint,
                    priorityTier = 2,
                    ruleName = "ManualOverride",
                    reason = string.IsNullOrEmpty(ov.notes) ? "Manual GUID/slot override." : ov.notes
                };
            }

            // Tier 3: Declared Semantic Metadata (VisualStyleIdentifier, CityModuleCatalog, PizzeriaAppearanceProfile, ItemPresentationProfile)
            if (slot.hasDeclaredMetadata)
            {
                IllustrateShaderFamily family = slot.declaredShaderFamily;
                if (slot.declaredAction == VisualConversionAction.Convert)
                {
                    if (slot.isTransparentQueue && family != IllustrateShaderFamily.Transparent)
                        family = IllustrateShaderFamily.Transparent;
                    else if (slot.isAlphaClipped && family != IllustrateShaderFamily.AlphaClipped)
                        family = IllustrateShaderFamily.AlphaClipped;
                }

                return new VisualClassificationDecision
                {
                    categoryId = string.IsNullOrEmpty(slot.declaredCategoryId) ? VisualCategoryIds.Props : slot.declaredCategoryId,
                    semanticRole = slot.declaredRole,
                    shaderFamily = family,
                    action = slot.declaredAction,
                    confidence = 0.96f,
                    preserveTexture = true,
                    preserveColor = true,
                    allowDynamicTint = slot.declaredAllowDynamicTint,
                    priorityTier = 3,
                    ruleName = "DeclaredMetadata",
                    reason = string.IsNullOrEmpty(slot.declaredReason) ? "Resolved from declared semantic metadata." : slot.declaredReason
                };
            }

            // Tier 4: User Rules ordered by priority
            VisualClassificationRule bestRule = null;
            int bestPriority = int.MaxValue;
            for (int i = 0; i < rules.Count; i++)
            {
                var r = rules[i];
                if (r == null || !r.enabled) continue;
                if (r.priority < bestPriority && MatchesRule(r, in slot))
                {
                    bestRule = r;
                    bestPriority = r.priority;
                }
            }

            if (bestRule != null)
            {
                IllustrateShaderFamily resolvedFamily = bestRule.targetShaderFamily;
                if (bestRule.action == VisualConversionAction.Convert)
                {
                    if (slot.isTransparentQueue && resolvedFamily == IllustrateShaderFamily.Opaque)
                        resolvedFamily = IllustrateShaderFamily.Transparent;
                    else if (slot.isAlphaClipped && resolvedFamily == IllustrateShaderFamily.Opaque)
                        resolvedFamily = IllustrateShaderFamily.AlphaClipped;
                }

                return new VisualClassificationDecision
                {
                    categoryId = bestRule.targetCategoryId,
                    semanticRole = bestRule.targetRole,
                    shaderFamily = resolvedFamily,
                    action = bestRule.action,
                    confidence = Mathf.Clamp01(bestRule.confidence),
                    preserveTexture = bestRule.preserveTexture,
                    preserveColor = bestRule.preserveColor,
                    allowDynamicTint = bestRule.allowDynamicTint,
                    priorityTier = 4,
                    ruleName = bestRule.ruleName,
                    reason = string.IsNullOrEmpty(bestRule.reason) ? $"Matched rule '{bestRule.ruleName}'." : bestRule.reason
                };
            }

            // Tier 5: Material/property heuristic (transparent/alpha/emission/standard URP properties + context hints)
            if (TryInferHeuristically(in slot, out VisualClassificationDecision heuristic))
            {
                return heuristic;
            }

            // Tier 6: Unclassified (safe manual review)
            return new VisualClassificationDecision
            {
                categoryId = VisualCategoryIds.Unclassified,
                semanticRole = VisualSemanticRole.Unclassified,
                shaderFamily = IllustrateShaderFamily.PreserveOriginal,
                action = VisualConversionAction.ManualReview,
                confidence = 0f,
                preserveTexture = true,
                preserveColor = true,
                allowDynamicTint = false,
                priorityTier = 6,
                ruleName = "Unclassified",
                reason = "No matching rule or confident material heuristic; pending manual review."
            };
        }

        public bool HasCategory(string categoryId)
        {
            if (string.IsNullOrEmpty(categoryId)) return false;
            for (int i = 0; i < categories.Count; i++)
            {
                if (categories[i] != null && string.Equals(categories[i].categoryId, categoryId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public void SetOrUpdateManualOverride(VisualSlotOverride entry)
        {
            if (entry == null) return;
            for (int i = 0; i < manualOverrides.Count; i++)
            {
                var cur = manualOverrides[i];
                if (cur != null &&
                    cur.sourceAssetGuid == entry.sourceAssetGuid &&
                    cur.sourceLocalFileId == entry.sourceLocalFileId &&
                    cur.slotIndex == entry.slotIndex &&
                    cur.rendererRelativePath == entry.rendererRelativePath)
                {
                    manualOverrides[i] = entry;
                    return;
                }
            }
            manualOverrides.Add(entry);
        }

        public string ComputeRuleSetHash()
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = HashString(hash, ruleSetVersion.ToString());
                for (int i = 0; i < categories.Count; i++)
                {
                    var c = categories[i];
                    if (c == null) continue;
                    hash = HashString(hash, c.categoryId);
                    hash = HashString(hash, ((int)c.defaultShaderFamily).ToString());
                }
                for (int i = 0; i < rules.Count; i++)
                {
                    var r = rules[i];
                    if (r == null || !r.enabled) continue;
                    hash = HashString(hash, $"{r.ruleName}:{r.priority}:{r.targetCategoryId}:{(int)r.targetRole}:{(int)r.targetShaderFamily}:{(int)r.action}");
                }
                for (int i = 0; i < exclusions.Count; i++)
                {
                    var e = exclusions[i];
                    if (e == null || !e.enabled) continue;
                    hash = HashString(hash, $"{e.ruleName}:{e.shaderNameContains}:{e.assetPathPrefix}:{e.rendererTypeName}");
                }
                for (int i = 0; i < manualOverrides.Count; i++)
                {
                    var m = manualOverrides[i];
                    if (m == null) continue;
                    hash = HashString(hash, $"{m.sourceAssetGuid}:{m.sourceLocalFileId}:{m.slotIndex}:{m.overrideCategoryId}:{(int)m.overrideRole}:{(int)m.overrideAction}");
                }
                return hash.ToString("x8");
            }
        }

        private VisualSlotOverride FindManualOverride(in VisualAssetSlotDescriptor slot)
        {
            for (int i = 0; i < manualOverrides.Count; i++)
            {
                var ov = manualOverrides[i];
                if (ov == null) continue;
                bool guidMatch = (!string.IsNullOrEmpty(slot.sourceMaterialGuid) && ov.sourceAssetGuid == slot.sourceMaterialGuid && ov.sourceLocalFileId == slot.sourceLocalFileId)
                              || (!string.IsNullOrEmpty(slot.sourceAssetGuid) && ov.sourceAssetGuid == slot.sourceAssetGuid && ov.sourceLocalFileId == slot.sourceLocalFileId);
                if (!guidMatch) continue;

                bool slotMatch = ov.slotIndex < 0 || ov.slotIndex == slot.slotIndex;
                bool pathMatch = string.IsNullOrEmpty(ov.rendererRelativePath) || ov.rendererRelativePath == slot.rendererRelativePath;
                if (slotMatch && pathMatch)
                {
                    return ov;
                }
            }
            return null;
        }

        private static bool MatchesExclusion(VisualExclusionRule ex, in VisualAssetSlotDescriptor slot)
        {
            bool hasCondition = false;
            if (!string.IsNullOrEmpty(ex.shaderNameContains))
            {
                hasCondition = true;
                if (string.IsNullOrEmpty(slot.shaderName) || slot.shaderName.IndexOf(ex.shaderNameContains, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }
            if (!string.IsNullOrEmpty(ex.assetPathPrefix))
            {
                hasCondition = true;
                if (string.IsNullOrEmpty(slot.assetPath) || !slot.assetPath.StartsWith(ex.assetPathPrefix, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            if (!string.IsNullOrEmpty(ex.rendererTypeName))
            {
                hasCondition = true;
                if (!string.Equals(slot.rendererTypeName, ex.rendererTypeName, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            if (!string.IsNullOrEmpty(ex.tagEquals))
            {
                hasCondition = true;
                if (!string.Equals(slot.gameObjectTag, ex.tagEquals, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            if (!string.IsNullOrEmpty(ex.materialNameRegex))
            {
                hasCondition = true;
                if (string.IsNullOrEmpty(slot.materialName) || !Regex.IsMatch(slot.materialName, ex.materialNameRegex, RegexOptions.IgnoreCase))
                    return false;
            }
            return hasCondition;
        }

        private static bool MatchesRule(VisualClassificationRule rule, in VisualAssetSlotDescriptor slot)
        {
            bool hasCriterion = false;

            if (!string.IsNullOrEmpty(rule.assetPathPrefix))
            {
                hasCriterion = true;
                if (string.IsNullOrEmpty(slot.assetPath) || !slot.assetPath.StartsWith(rule.assetPathPrefix, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            if (!string.IsNullOrEmpty(rule.assetPathRegex))
            {
                hasCriterion = true;
                if (string.IsNullOrEmpty(slot.assetPath) || !Regex.IsMatch(slot.assetPath, rule.assetPathRegex, RegexOptions.IgnoreCase))
                    return false;
            }
            if (!string.IsNullOrEmpty(rule.materialNameRegex))
            {
                hasCriterion = true;
                if (string.IsNullOrEmpty(slot.materialName) || !Regex.IsMatch(slot.materialName, rule.materialNameRegex, RegexOptions.IgnoreCase))
                    return false;
            }
            if (!string.IsNullOrEmpty(rule.sourceShaderContains))
            {
                hasCriterion = true;
                if (string.IsNullOrEmpty(slot.shaderName) || slot.shaderName.IndexOf(rule.sourceShaderContains, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }
            if (!string.IsNullOrEmpty(rule.requiredTextureProperty))
            {
                hasCriterion = true;
                if (rule.requiredTextureProperty == "_NormalMap" || rule.requiredTextureProperty == "_BumpMap")
                {
                    if (!slot.hasNormalMap) return false;
                }
                else if (!slot.hasMainTexture)
                {
                    return false;
                }
            }
            if (rule.requireTransparentQueue)
            {
                hasCriterion = true;
                if (!slot.isTransparentQueue) return false;
            }
            if (rule.requireAlphaClipped)
            {
                hasCriterion = true;
                if (!slot.isAlphaClipped) return false;
            }
            if (rule.requireEmission)
            {
                hasCriterion = true;
                if (!slot.hasEmission) return false;
            }
            if (!string.IsNullOrEmpty(rule.requiredTag))
            {
                hasCriterion = true;
                if (!string.Equals(slot.gameObjectTag, rule.requiredTag, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return hasCriterion;
        }

        private bool TryInferHeuristically(in VisualAssetSlotDescriptor slot, out VisualClassificationDecision decision)
        {
            decision = default;

            // Never destructively auto-convert custom Shader Graphs unless explicitly mapped
            if (slot.isShaderGraph)
            {
                decision = new VisualClassificationDecision
                {
                    categoryId = VisualCategoryIds.Preserve,
                    semanticRole = VisualSemanticRole.Preserve,
                    shaderFamily = IllustrateShaderFamily.PreserveOriginal,
                    action = VisualConversionAction.ManualReview,
                    confidence = 0.65f,
                    preserveTexture = true,
                    preserveColor = true,
                    allowDynamicTint = false,
                    priorityTier = 5,
                    ruleName = "Heuristic:CustomShaderGraph",
                    reason = $"Custom Shader Graph '{slot.shaderName}' preserved for review."
                };
                return true;
            }

            bool isStandardConvertible = !string.IsNullOrEmpty(slot.shaderName) &&
                (slot.shaderName.StartsWith("Universal Render Pipeline/", StringComparison.OrdinalIgnoreCase) ||
                 slot.shaderName == "Standard" ||
                 slot.shaderName.StartsWith("Autodesk", StringComparison.OrdinalIgnoreCase) ||
                 slot.shaderName.StartsWith("Distant Lands/Illustrate/", StringComparison.OrdinalIgnoreCase));

            if (!isStandardConvertible)
            {
                return false;
            }

            if (slot.isTransparentQueue)
            {
                decision = new VisualClassificationDecision
                {
                    categoryId = VisualCategoryIds.Props,
                    semanticRole = VisualSemanticRole.GenericTransparent,
                    shaderFamily = IllustrateShaderFamily.Transparent,
                    action = VisualConversionAction.Convert,
                    confidence = 0.72f,
                    preserveTexture = true,
                    preserveColor = true,
                    allowDynamicTint = false,
                    priorityTier = 5,
                    ruleName = "Heuristic:TransparentSurface",
                    reason = "Standard/URP surface with Transparent render queue -> Illustrate Transparent."
                };
                return true;
            }

            if (slot.isAlphaClipped)
            {
                decision = new VisualClassificationDecision
                {
                    categoryId = VisualCategoryIds.Props,
                    semanticRole = VisualSemanticRole.GenericAlphaClipped,
                    shaderFamily = IllustrateShaderFamily.AlphaClipped,
                    action = VisualConversionAction.Convert,
                    confidence = 0.75f,
                    preserveTexture = true,
                    preserveColor = true,
                    allowDynamicTint = false,
                    priorityTier = 5,
                    ruleName = "Heuristic:AlphaClippedSurface",
                    reason = "Standard/URP surface with alpha clipping -> Illustrate Alpha Clipped."
                };
                return true;
            }

            // Combine renderer type + material hints (never rely solely on object name)
            if (string.Equals(slot.rendererTypeName, "SkinnedMeshRenderer", StringComparison.Ordinal))
            {
                decision = new VisualClassificationDecision
                {
                    categoryId = VisualCategoryIds.Characters,
                    semanticRole = VisualSemanticRole.NpcBody,
                    shaderFamily = IllustrateShaderFamily.Opaque,
                    action = VisualConversionAction.Convert,
                    confidence = 0.78f,
                    preserveTexture = true,
                    preserveColor = true,
                    allowDynamicTint = true,
                    priorityTier = 5,
                    ruleName = "Heuristic:SkinnedCharacter",
                    reason = "SkinnedMeshRenderer with convertible URP/Standard shader -> Characters Opaque."
                };
                return true;
            }

            decision = new VisualClassificationDecision
            {
                categoryId = VisualCategoryIds.Props,
                semanticRole = slot.hasEmission ? VisualSemanticRole.Emission : VisualSemanticRole.GenericOpaque,
                shaderFamily = IllustrateShaderFamily.Opaque,
                action = VisualConversionAction.Convert,
                confidence = 0.62f,
                preserveTexture = true,
                preserveColor = true,
                allowDynamicTint = false,
                priorityTier = 5,
                ruleName = "Heuristic:StandardOpaqueFallback",
                reason = "Convertible URP/Lit or Standard opaque material -> Illustrate Opaque (review recommended)."
            };
            return true;
        }

        /// <summary>
        /// Populates built-in categories, safe exclusions, and project-agnostic default rules.
        /// </summary>
        public static void PopulateDefaults(VisualConversionRuleSet target)
        {
            if (target == null) return;
            target.categories.Clear();
            target.exclusions.Clear();
            target.rules.Clear();

            AddCategory(target, VisualCategoryIds.Architecture, "Architecture", IllustrateShaderFamily.Opaque, VisualSemanticRole.PizzeriaWall, true, false);
            AddCategory(target, VisualCategoryIds.City, "City & Buildings", IllustrateShaderFamily.Opaque, VisualSemanticRole.BuildingFacade, true, true);
            AddCategory(target, VisualCategoryIds.Streets, "Streets & Signage", IllustrateShaderFamily.Simple, VisualSemanticRole.Street, true, false);
            AddCategory(target, VisualCategoryIds.Characters, "Characters & NPCs", IllustrateShaderFamily.Opaque, VisualSemanticRole.PlayerBody, true, true);
            AddCategory(target, VisualCategoryIds.Food, "Food & Pizza", IllustrateShaderFamily.Opaque, VisualSemanticRole.FoodGeneric, true, true);
            AddCategory(target, VisualCategoryIds.Props, "Props & Tools", IllustrateShaderFamily.Opaque, VisualSemanticRole.PizzeriaProp, true, false);
            AddCategory(target, VisualCategoryIds.Interaction, "Interaction", IllustrateShaderFamily.PreserveOriginal, VisualSemanticRole.Preserve, true, false, VisualConversionAction.Preserve);
            AddCategory(target, VisualCategoryIds.Effects, "VFX & Particles", IllustrateShaderFamily.PreserveOriginal, VisualSemanticRole.Particle, true, false, VisualConversionAction.Preserve);
            AddCategory(target, VisualCategoryIds.Unclassified, "Unclassified", IllustrateShaderFamily.PreserveOriginal, VisualSemanticRole.Unclassified, true, false, VisualConversionAction.ManualReview);

            // Safe Exclusions (Tier 1)
            target.exclusions.Add(new VisualExclusionRule { ruleName = "Preserve Interaction Outline", shaderNameContains = "ScaryParty/Outline", reason = "OutlineService focus highlight shader must remain untouched." });
            target.exclusions.Add(new VisualExclusionRule { ruleName = "Preserve UI Shaders", shaderNameContains = "UI/", reason = "UI materials must remain readable without cel lighting." });
            target.exclusions.Add(new VisualExclusionRule { ruleName = "Preserve TextMeshPro", shaderNameContains = "TextMeshPro", reason = "TMP fonts/SDF materials preserved." });
            target.exclusions.Add(new VisualExclusionRule { ruleName = "Preserve Particles", shaderNameContains = "Particles", reason = "Particle shaders preserved." });
            target.exclusions.Add(new VisualExclusionRule { ruleName = "Preserve ParticleSystemRenderer", rendererTypeName = "ParticleSystemRenderer", reason = "VFX particle renderer preserved." });
            target.exclusions.Add(new VisualExclusionRule { ruleName = "Preserve TrailRenderer", rendererTypeName = "TrailRenderer", reason = "Trail effect renderer preserved." });
            target.exclusions.Add(new VisualExclusionRule { ruleName = "Preserve LineRenderer", rendererTypeName = "LineRenderer", reason = "Line renderer preserved." });
            target.exclusions.Add(new VisualExclusionRule { ruleName = "Preserve SpriteRenderer", rendererTypeName = "SpriteRenderer", reason = "2D sprite UI/markers preserved." });
            target.exclusions.Add(new VisualExclusionRule { ruleName = "Preserve Sprites/Default", shaderNameContains = "Sprites/Default", reason = "2D sprite shader preserved." });
            target.exclusions.Add(new VisualExclusionRule { ruleName = "Preserve DistantLands Package", assetPathPrefix = "Assets/ThirdParty/Distant Lands/", reason = "Third-party source materials preserved." });

            // Default Rules (Tier 4) - path + shader properties (not fragile hardcoded object lists)
            target.rules.Add(new VisualClassificationRule
            {
                ruleName = "Road Signage / Markings",
                priority = 10,
                materialNameRegex = "(Crosswalk|Faixa|RoadYellow|RoadWhite|RoadSign|TrafficLightLens)",
                targetCategoryId = VisualCategoryIds.Streets,
                targetRole = VisualSemanticRole.Crosswalk,
                targetShaderFamily = IllustrateShaderFamily.Simple,
                confidence = 0.92f,
                reason = "Traffic markings and road signs use clean Illustrate Simple without outline clutter."
            });
            target.rules.Add(new VisualClassificationRule
            {
                ruleName = "Streets & Sidewalks",
                priority = 15,
                materialNameRegex = "(Street|Sidewalk|Intersection|Asphalt|Road|Calcada)",
                targetCategoryId = VisualCategoryIds.Streets,
                targetRole = VisualSemanticRole.Street,
                targetShaderFamily = IllustrateShaderFamily.Simple,
                confidence = 0.88f,
                reason = "Road surfaces use lightweight Illustrate Simple."
            });
            target.rules.Add(new VisualClassificationRule
            {
                ruleName = "City Modular Glass & Windows",
                priority = 20,
                materialNameRegex = "(Glass|Window|Vidro|Janela)",
                targetCategoryId = VisualCategoryIds.City,
                targetRole = VisualSemanticRole.BuildingWindow,
                targetShaderFamily = IllustrateShaderFamily.Transparent,
                confidence = 0.90f,
                reason = "Windows/glass preserve transparency via Illustrate Transparent."
            });
            target.rules.Add(new VisualClassificationRule
            {
                ruleName = "City Modular Buildings",
                priority = 25,
                assetPathPrefix = "Assets/Piaaria/Materials/City/",
                targetCategoryId = VisualCategoryIds.City,
                targetRole = VisualSemanticRole.BuildingFacade,
                targetShaderFamily = IllustrateShaderFamily.Opaque,
                allowDynamicTint = true,
                confidence = 0.90f,
                reason = "Modular city materials mapped to City Opaque supporting MPB facade tints."
            });
            target.rules.Add(new VisualClassificationRule
            {
                ruleName = "Characters Folder",
                priority = 30,
                assetPathPrefix = "Assets/Piaaria/Art/Characters/",
                targetCategoryId = VisualCategoryIds.Characters,
                targetRole = VisualSemanticRole.PlayerBody,
                targetShaderFamily = IllustrateShaderFamily.Opaque,
                allowDynamicTint = true,
                confidence = 0.92f,
                reason = "Character folder materials retain skin/body slots and MPB compatibility."
            });
            target.rules.Add(new VisualClassificationRule
            {
                ruleName = "Food & Pizza Assets",
                priority = 35,
                materialNameRegex = "(Pizza|Dough|Massa|Sauce|Molho|Cheese|Queijo|Pepperoni|Calabresa|Mushroom|Cogumelo|Ingredient)",
                targetCategoryId = VisualCategoryIds.Food,
                targetRole = VisualSemanticRole.FoodGeneric,
                targetShaderFamily = IllustrateShaderFamily.Opaque,
                allowDynamicTint = true,
                confidence = 0.88f,
                reason = "Food/ingredient asset converted with full texture preservation."
            });
            target.rules.Add(new VisualClassificationRule
            {
                ruleName = "Pizzeria Visuals & Props",
                priority = 40,
                assetPathPrefix = "Assets/Piaaria/Art/Pizzeria/",
                targetCategoryId = VisualCategoryIds.Props,
                targetRole = VisualSemanticRole.PizzeriaProp,
                targetShaderFamily = IllustrateShaderFamily.Opaque,
                confidence = 0.85f,
                reason = "Pizzeria props converted with Opaque shader."
            });
        }

        private static void AddCategory(
            VisualConversionRuleSet target,
            string id,
            string displayName,
            IllustrateShaderFamily family,
            VisualSemanticRole role,
            bool preserveTex,
            bool dynamicTint,
            VisualConversionAction action = VisualConversionAction.Convert)
        {
            target.categories.Add(new VisualCategoryDefinition
            {
                categoryId = id,
                displayName = displayName,
                defaultShaderFamily = family,
                defaultSemanticRole = role,
                defaultAction = action,
                preserveTexturesByDefault = preserveTex,
                allowDynamicTint = dynamicTint
            });
        }

        private static uint HashString(uint h, string value)
        {
            if (string.IsNullOrEmpty(value)) return h * 16777619u;
            for (int i = 0; i < value.Length; i++)
            {
                h ^= value[i];
                h *= 16777619u;
            }
            return h;
        }
    }
}
