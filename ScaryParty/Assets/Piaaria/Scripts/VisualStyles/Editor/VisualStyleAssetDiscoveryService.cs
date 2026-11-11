#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using ScaryParty.City.Modular;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Presentation;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ScaryParty.VisualStyles.Editor
{
    public sealed class DiscoveredMaterialTarget
    {
        public string conversionKey;
        public string sourceAssetPath;
        public string sourceAssetGuid;
        public string sourceMaterialGuid;
        public long sourceLocalFileId;
        public string sourceDependencyHash;
        public Material sourceMaterial;
        public VisualAssetSlotDescriptor descriptor;
        public VisualClassificationDecision decision;
        public VisualAssetSyncStatus incrementalStatus;
        public List<string> usageContexts = new List<string>();
        public string mainTexGuid;
        public string normalTexGuid;
        public string emissionTexGuid;
    }

    public sealed class DiscoveryScanResult
    {
        public VisualDiscoveryScope scope;
        public List<DiscoveredMaterialTarget> targets = new List<DiscoveredMaterialTarget>();
        public List<string> warnings = new List<string>();
        public List<ConversionManifestEntry> orphanedEntries = new List<ConversionManifestEntry>();
        public int totalSlotOccurrences;

        public int CountByStatus(VisualAssetSyncStatus status)
        {
            int count = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i].incrementalStatus == status) count++;
            }
            return count;
        }
    }

    /// <summary>
    /// Universal incremental scanner discovering renderers, prefabs, FBXs, standalone materials,
    /// and gameplay catalogs across configurable scopes without closing on a hardcoded asset list.
    /// </summary>
    public static class VisualStyleAssetDiscoveryService
    {
        public static DiscoveryScanResult Scan(
            VisualStyleProfile profile,
            VisualConversionRuleSet ruleSet,
            VisualStyleConversionManifest existingManifest,
            VisualDiscoveryScope scope,
            IReadOnlyList<string> selectedFolders = null,
            IReadOnlyList<UnityEngine.Object> currentSelection = null)
        {
            if (ruleSet == null) ruleSet = VisualStylePresetSeeder.EnsureDefaultRuleSet();

            var result = new DiscoveryScanResult { scope = scope };
            var uniqueMaterialTargets = new Dictionary<string, DiscoveredMaterialTarget>(StringComparer.Ordinal);
            var visitedAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            switch (scope)
            {
                case VisualDiscoveryScope.SelectedFolders:
                    ScanFolders(selectedFolders, profile, ruleSet, result, uniqueMaterialTargets, visitedAssets);
                    break;

                case VisualDiscoveryScope.CurrentSelection:
                    ScanSelection(currentSelection, profile, ruleSet, result, uniqueMaterialTargets, visitedAssets);
                    break;

                case VisualDiscoveryScope.FullProject:
                    ScanFolders(new[] { "Assets" }, profile, ruleSet, result, uniqueMaterialTargets, visitedAssets);
                    break;

                case VisualDiscoveryScope.GameplayReachable:
                default:
                    ScanGameplayReachable(profile, ruleSet, result, uniqueMaterialTargets, visitedAssets);
                    break;
            }

            foreach (var target in uniqueMaterialTargets.Values)
            {
                EvaluateIncrementalStatus(target, profile, ruleSet, existingManifest);
                result.targets.Add(target);
            }

            // Sort deterministically by category then material name
            result.targets.Sort((a, b) =>
            {
                int c = string.Compare(a.decision.categoryId, b.decision.categoryId, StringComparison.OrdinalIgnoreCase);
                if (c != 0) return c;
                return string.Compare(a.descriptor.materialName, b.descriptor.materialName, StringComparison.OrdinalIgnoreCase);
            });

            DetectOrphans(existingManifest, uniqueMaterialTargets, scope, result);
            return result;
        }

        private static void ScanGameplayReachable(
            VisualStyleProfile profile,
            VisualConversionRuleSet ruleSet,
            DiscoveryScanResult result,
            Dictionary<string, DiscoveredMaterialTarget> uniqueMap,
            HashSet<string> visitedAssets)
        {
            // Canonical gameplay & key prefabs/catalogs
            InspectPrefabByPath(VisualStyleAuditService.PizzeriaManualPrefabPath, "Gameplay:PizzeriaManual", profile, ruleSet, result, uniqueMap, visitedAssets, VisualCategoryIds.Architecture, VisualSemanticRole.PizzeriaWall);
            InspectPrefabByPath(VisualStyleAuditService.CharacterVisualPrefabPath, "Gameplay:CharacterVisual", profile, ruleSet, result, uniqueMap, visitedAssets, VisualCategoryIds.Characters, VisualSemanticRole.PlayerBody, true);
            InspectPrefabByPath(VisualStyleAuditService.CharacterResourcesPrefabPath, "Resources:CharacterVisual", profile, ruleSet, result, uniqueMap, visitedAssets, VisualCategoryIds.Characters, VisualSemanticRole.NpcBody, true);
            InspectPrefabByPath(VisualStyleAuditService.PlayerArmaturePrefabPath, "Gameplay:PlayerArmature", profile, ruleSet, result, uniqueMap, visitedAssets, VisualCategoryIds.Characters, VisualSemanticRole.PlayerBody, true);

            // City Module Catalog (includes any future 13th+ pieces!)
            var moduleCatalog = AssetDatabase.LoadAssetAtPath<CityModuleCatalog>(VisualStyleAuditService.CityModuleCatalogPath);
            if (moduleCatalog != null && moduleCatalog.definitions != null)
            {
                for (int i = 0; i < moduleCatalog.definitions.Count; i++)
                {
                    var def = moduleCatalog.definitions[i];
                    if (def?.prefab == null) continue;
                    string path = AssetDatabase.GetAssetPath(def.prefab);
                    VisualSemanticRole role = def.materialRoles != null && def.materialRoles.Length > 0
                        ? MapModularRole(def.materialRoles[0], def)
                        : VisualSemanticRole.BuildingFacade;
                    InspectPrefabObject(def.prefab, path, $"ModularKit:{def.id}", profile, ruleSet, result, uniqueMap, visitedAssets, VisualCategoryIds.City, role, true);
                }
            }

            // City placeholders & shared visual materials folder
            ScanFolders(new[] { "Assets/Piaaria/Materials/City", "Assets/Piaaria/Art/Pizzeria", "Assets/Piaaria/Art/Characters", "Assets/Piaaria/Prefabs/Pizzeria/Visuals" },
                profile, ruleSet, result, uniqueMap, visitedAssets);

            // Item Presentation Catalog (all real ingredients, pizzas, tools, boxes)
            var itemCatalog = AssetDatabase.LoadAssetAtPath<ItemPresentationCatalog>(VisualStyleAuditService.ItemPresentationCatalogPath);
            if (itemCatalog != null && itemCatalog.profiles != null)
            {
                for (int i = 0; i < itemCatalog.profiles.Count; i++)
                {
                    var p = itemCatalog.profiles[i];
                    if (p?.visualPrefab == null) continue;
                    string path = AssetDatabase.GetAssetPath(p.visualPrefab);
                    string cat = p.isTool ? VisualCategoryIds.Props : VisualCategoryIds.Food;
                    VisualSemanticRole role = p.isTool ? VisualSemanticRole.KitchenTool : VisualSemanticRole.FoodGeneric;
                    InspectPrefabObject(p.visualPrefab, path, $"ItemCatalog:{p.presentationKey}", profile, ruleSet, result, uniqueMap, visitedAssets, cat, role, !p.isTool);
                }
            }

            // Network Prefabs
            var netList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(VisualStyleAuditService.DefaultNetworkPrefabsPath);
            if (netList != null)
            {
                foreach (var np in netList.PrefabList)
                {
                    if (np?.Prefab == null) continue;
                    string path = AssetDatabase.GetAssetPath(np.Prefab);
                    InspectPrefabObject(np.Prefab, path, $"NetworkPrefab:{np.Prefab.name}", profile, ruleSet, result, uniqueMap, visitedAssets);
                }
            }

            // Canonical GameScene renderers & dependencies (non-destructive)
            InspectSceneNonDestructively(VisualStyleAuditService.CanonicalGameScenePath, "Scene:GameScene", profile, ruleSet, result, uniqueMap);
        }

        private static void ScanFolders(
            IReadOnlyList<string> folders,
            VisualStyleProfile profile,
            VisualConversionRuleSet ruleSet,
            DiscoveryScanResult result,
            Dictionary<string, DiscoveredMaterialTarget> uniqueMap,
            HashSet<string> visitedAssets)
        {
            if (folders == null || folders.Count == 0) return;

            var validFolders = new List<string>();
            for (int i = 0; i < folders.Count; i++)
            {
                if (!string.IsNullOrEmpty(folders[i]) && AssetDatabase.IsValidFolder(folders[i]))
                {
                    validFolders.Add(folders[i]);
                }
            }
            if (validFolders.Count == 0) return;

            string[] folderArray = validFolders.ToArray();

            // Prefabs & FBX/Model assets
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab t:Model", folderArray);
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (IsExcludedPath(path, profile)) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null)
                {
                    InspectPrefabObject(go, path, $"Asset:{go.name}", profile, ruleSet, result, uniqueMap, visitedAssets);
                }
            }

            // Standalone materials (which might be loaded by code or catalogs)
            string[] matGuids = AssetDatabase.FindAssets("t:Material", folderArray);
            for (int i = 0; i < matGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(matGuids[i]);
                if (IsExcludedPath(path, profile)) continue;
                var mats = AssetDatabase.LoadAllAssetsAtPath(path);
                for (int m = 0; m < mats.Length; m++)
                {
                    if (mats[m] is Material mat)
                    {
                        RegisterMaterialTarget(mat, null, 0, path, $"StandaloneMat:{mat.name}", path, profile, ruleSet, result, uniqueMap);
                    }
                }
            }
        }

        private static void ScanSelection(
            IReadOnlyList<UnityEngine.Object> selection,
            VisualStyleProfile profile,
            VisualConversionRuleSet ruleSet,
            DiscoveryScanResult result,
            Dictionary<string, DiscoveredMaterialTarget> uniqueMap,
            HashSet<string> visitedAssets)
        {
            if (selection == null) return;

            for (int i = 0; i < selection.Count; i++)
            {
                var obj = selection[i];
                if (obj == null) continue;
                string path = AssetDatabase.GetAssetPath(obj);

                if (obj is GameObject go)
                {
                    InspectPrefabObject(go, string.IsNullOrEmpty(path) ? go.name : path, $"Selection:{go.name}", profile, ruleSet, result, uniqueMap, visitedAssets);
                }
                else if (obj is Material mat)
                {
                    RegisterMaterialTarget(mat, null, 0, path, $"Selection:{mat.name}", path, profile, ruleSet, result, uniqueMap);
                }
                else if (obj is DefaultAsset && !string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path))
                {
                    ScanFolders(new[] { path }, profile, ruleSet, result, uniqueMap, visitedAssets);
                }
            }
        }

        private static void InspectPrefabByPath(
            string path,
            string context,
            VisualStyleProfile profile,
            VisualConversionRuleSet ruleSet,
            DiscoveryScanResult result,
            Dictionary<string, DiscoveredMaterialTarget> uniqueMap,
            HashSet<string> visitedAssets,
            string declaredCategory = null,
            VisualSemanticRole declaredRole = VisualSemanticRole.Unclassified,
            bool allowDynamicTint = false)
        {
            if (string.IsNullOrEmpty(path) || IsExcludedPath(path, profile)) return;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null)
            {
                InspectPrefabObject(go, path, context, profile, ruleSet, result, uniqueMap, visitedAssets, declaredCategory, declaredRole, allowDynamicTint);
            }
        }

        private static void InspectPrefabObject(
            GameObject root,
            string assetPath,
            string context,
            VisualStyleProfile profile,
            VisualConversionRuleSet ruleSet,
            DiscoveryScanResult result,
            Dictionary<string, DiscoveredMaterialTarget> uniqueMap,
            HashSet<string> visitedAssets,
            string declaredCategory = null,
            VisualSemanticRole declaredRole = VisualSemanticRole.Unclassified,
            bool allowDynamicTint = false)
        {
            if (root == null) return;
            if (!string.IsNullOrEmpty(assetPath) && IsExcludedPath(assetPath, profile)) return;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer rend = renderers[r];
                if (rend == null) continue;

                string hierarchyPath = GetHierarchyPath(root.transform, rend.transform);
                var identifier = rend.GetComponent<VisualStyleIdentifier>();
                var mats = rend.sharedMaterials;
                if (mats == null || mats.Length == 0) continue;

                for (int slot = 0; slot < mats.Length; slot++)
                {
                    Material mat = mats[slot];
                    result.totalSlotOccurrences++;
                    if (mat == null) continue;

                    string slotDeclaredCat = declaredCategory;
                    VisualSemanticRole slotDeclaredRole = declaredRole;
                    bool hasDeclared = !string.IsNullOrEmpty(declaredCategory) && declaredRole != VisualSemanticRole.Unclassified;
                    bool slotTint = allowDynamicTint;

                    if (identifier != null)
                    {
                        var meta = identifier.GetMetadataForSlot(slot);
                        if (meta != null)
                        {
                            hasDeclared = true;
                            slotDeclaredCat = meta.categoryId;
                            slotDeclaredRole = meta.role;
                            slotTint = meta.allowDynamicTint;
                        }
                        else if (!string.IsNullOrEmpty(identifier.defaultCategoryId))
                        {
                            hasDeclared = true;
                            slotDeclaredCat = identifier.defaultCategoryId;
                            slotDeclaredRole = identifier.defaultRole;
                        }
                    }

                    RegisterMaterialTarget(
                        mat,
                        rend,
                        slot,
                        hierarchyPath,
                        context,
                        assetPath,
                        profile,
                        ruleSet,
                        result,
                        uniqueMap,
                        hasDeclared,
                        slotDeclaredCat,
                        slotDeclaredRole,
                        slotTint);
                }
            }

            if (!string.IsNullOrEmpty(assetPath))
            {
                visitedAssets.Add(assetPath);
            }
        }

        private static void InspectSceneNonDestructively(
            string scenePath,
            string contextLabel,
            VisualStyleProfile profile,
            VisualConversionRuleSet ruleSet,
            DiscoveryScanResult result,
            Dictionary<string, DiscoveredMaterialTarget> uniqueMap)
        {
            if (!File.Exists(scenePath)) return;
            Scene existing = SceneManager.GetSceneByPath(scenePath);
            bool openedTemp = false;
            Scene scene = existing;
            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                openedTemp = true;
            }

            try
            {
                var roots = scene.GetRootGameObjects();
                var visitedDummy = new HashSet<string>();
                for (int i = 0; i < roots.Length; i++)
                {
                    InspectPrefabObject(roots[i], scenePath, contextLabel, profile, ruleSet, result, uniqueMap, visitedDummy);
                }
            }
            finally
            {
                if (openedTemp && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void RegisterMaterialTarget(
            Material mat,
            Renderer rend,
            int slotIndex,
            string hierarchyPath,
            string usageContext,
            string ownerAssetPath,
            VisualStyleProfile profile,
            VisualConversionRuleSet ruleSet,
            DiscoveryScanResult result,
            Dictionary<string, DiscoveredMaterialTarget> uniqueMap,
            bool hasDeclared = false,
            string declaredCat = null,
            VisualSemanticRole declaredRole = VisualSemanticRole.Unclassified,
            bool allowDynamicTint = false)
        {
            if (mat == null) return;

            string matPath = AssetDatabase.GetAssetPath(mat);
            if (!string.IsNullOrEmpty(matPath) && IsExcludedPath(matPath, profile) &&
                VisualStyleShaderUtility.IsIllustrateShader(mat.shader) &&
                matPath.StartsWith("Assets/ThirdParty/Distant Lands/", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string matGuid = "";
            long localId = 0;
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mat, out matGuid, out localId) || string.IsNullOrEmpty(matGuid))
            {
                matGuid = string.IsNullOrEmpty(ownerAssetPath) ? $"runtime_{mat.GetInstanceID()}" : AssetDatabase.AssetPathToGUID(ownerAssetPath);
            }

            string effectivePath = string.IsNullOrEmpty(matPath) ? ownerAssetPath : matPath;
            var descriptor = VisualStyleAuditService.BuildDescriptor(
                rend, slotIndex, mat, effectivePath, matGuid, localId, hierarchyPath,
                hasDeclared, declaredCat, declaredRole, allowDynamicTint, usageContext);

            VisualClassificationDecision decision = ruleSet.Evaluate(in descriptor);
            ExtractTextureGuids(mat, out string mainTexGuid, out string normalTexGuid, out string emissionTexGuid);

            string styleId = profile != null ? profile.styleId : "default";
            string convKey = ComputeConversionKey(styleId, matGuid, localId, decision.semanticRole, decision.shaderFamily,
                mainTexGuid, normalTexGuid, emissionTexGuid, descriptor.isTransparentQueue, descriptor.isAlphaClipped);

            if (uniqueMap.TryGetValue(convKey, out DiscoveredMaterialTarget existing))
            {
                string usage = $"{usageContext}::{hierarchyPath}[{slotIndex}]";
                if (!existing.usageContexts.Contains(usage))
                {
                    existing.usageContexts.Add(usage);
                }
                return;
            }

            string depHash = !string.IsNullOrEmpty(effectivePath)
                ? AssetDatabase.GetAssetDependencyHash(effectivePath).ToString()
                : "";

            // Fold live material state into dependency hash so modified textures/colors on a mat trigger SourceModified immediately
            string liveMatFingerprint = BuildMaterialContentFingerprint(mat, mainTexGuid, normalTexGuid, emissionTexGuid);
            string combinedDepHash = $"{depHash}:{liveMatFingerprint}";

            var target = new DiscoveredMaterialTarget
            {
                conversionKey = convKey,
                sourceAssetPath = ownerAssetPath,
                sourceAssetGuid = string.IsNullOrEmpty(ownerAssetPath) ? matGuid : AssetDatabase.AssetPathToGUID(ownerAssetPath),
                sourceMaterialGuid = matGuid,
                sourceLocalFileId = localId,
                sourceDependencyHash = combinedDepHash,
                sourceMaterial = mat,
                descriptor = descriptor,
                decision = decision,
                mainTexGuid = mainTexGuid,
                normalTexGuid = normalTexGuid,
                emissionTexGuid = emissionTexGuid
            };
            target.usageContexts.Add($"{usageContext}::{hierarchyPath}[{slotIndex}]");
            uniqueMap[convKey] = target;
        }

        private static void EvaluateIncrementalStatus(
            DiscoveredMaterialTarget target,
            VisualStyleProfile profile,
            VisualConversionRuleSet ruleSet,
            VisualStyleConversionManifest manifest)
        {
            if (target.decision.action == VisualConversionAction.ManualReview)
            {
                target.incrementalStatus = VisualAssetSyncStatus.ManualReview;
                return;
            }

            if (target.decision.action == VisualConversionAction.Preserve || target.decision.action == VisualConversionAction.Skip)
            {
                target.incrementalStatus = VisualAssetSyncStatus.UpToDate;
                return;
            }

            if (manifest == null)
            {
                target.incrementalStatus = VisualAssetSyncStatus.Unprocessed;
                return;
            }

            ConversionManifestEntry entry = manifest.FindByKey(target.conversionKey)
                ?? manifest.FindBySourceMaterial(target.sourceMaterialGuid, target.sourceLocalFileId);

            if (entry == null)
            {
                target.incrementalStatus = VisualAssetSyncStatus.Unprocessed;
                return;
            }

            if (entry.isPromotedCustomMaterial)
            {
                target.incrementalStatus = VisualAssetSyncStatus.PromotedCustom;
                return;
            }

            if (string.IsNullOrEmpty(entry.targetMaterialAssetPath) ||
                AssetDatabase.LoadAssetAtPath<Material>(entry.targetMaterialAssetPath) == null)
            {
                target.incrementalStatus = VisualAssetSyncStatus.Unprocessed;
                return;
            }

            if (!string.Equals(entry.sourceDependencyHash, target.sourceDependencyHash, StringComparison.Ordinal))
            {
                target.incrementalStatus = VisualAssetSyncStatus.SourceModified;
                return;
            }

            string currentProfileHash = profile != null ? profile.ComputeContentHash() : "";
            string currentRuleHash = ruleSet != null ? ruleSet.ComputeRuleSetHash() : "";
            if (!string.Equals(manifest.profileContentHash, currentProfileHash, StringComparison.Ordinal) ||
                !string.Equals(manifest.ruleSetHash, currentRuleHash, StringComparison.Ordinal))
            {
                target.incrementalStatus = VisualAssetSyncStatus.ProfileOutdated;
                return;
            }

            target.incrementalStatus = VisualAssetSyncStatus.UpToDate;
        }

        private static void DetectOrphans(
            VisualStyleConversionManifest manifest,
            Dictionary<string, DiscoveredMaterialTarget> activeTargets,
            VisualDiscoveryScope scope,
            DiscoveryScanResult result)
        {
            if (manifest == null || manifest.entries == null) return;
            if (scope == VisualDiscoveryScope.CurrentSelection || scope == VisualDiscoveryScope.SelectedFolders) return;

            for (int i = 0; i < manifest.entries.Count; i++)
            {
                var entry = manifest.entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.conversionKey)) continue;
                if (activeTargets.ContainsKey(entry.conversionKey)) continue;

                // Verify if the source GUID/path no longer resolves
                string resolvedPath = !string.IsNullOrEmpty(entry.sourceAssetGuid)
                    ? AssetDatabase.GUIDToAssetPath(entry.sourceAssetGuid)
                    : entry.sourceAssetPath;

                bool sourceMissing = string.IsNullOrEmpty(resolvedPath) || AssetDatabase.LoadMainAssetAtPath(resolvedPath) == null;
                if (sourceMissing)
                {
                    entry.status = VisualAssetSyncStatus.Orphaned;
                    result.orphanedEntries.Add(entry);
                }
            }
        }

        public static string ComputeConversionKey(
            string styleId,
            string sourceMatGuid,
            long sourceLocalFileId,
            VisualSemanticRole role,
            IllustrateShaderFamily family,
            string mainTexGuid,
            string normalTexGuid,
            string emissionTexGuid,
            bool isTransparent,
            bool isAlphaClipped)
        {
            string raw = $"{styleId}|{sourceMatGuid}|{sourceLocalFileId}|{(int)role}|{(int)family}|{mainTexGuid}|{normalTexGuid}|{emissionTexGuid}|{(isTransparent ? 1 : 0)}|{(isAlphaClipped ? 1 : 0)}";
            unchecked
            {
                uint h1 = 2166136261u;
                for (int i = 0; i < raw.Length; i++)
                {
                    h1 ^= raw[i];
                    h1 *= 16777619u;
                }
                return $"{styleId}_{sourceMatGuid}_{sourceLocalFileId}_{h1:x8}";
            }
        }

        private static string BuildMaterialContentFingerprint(Material mat, string mainTexGuid, string normalTexGuid, string emissionTexGuid)
        {
            Color c = VisualStyleShaderUtility.ExtractBaseColor(mat, Color.white);
            Vector2 sc, off;
            VisualStyleShaderUtility.ExtractMainTexture(mat, out sc, out off);
            string shader = mat.shader != null ? mat.shader.name : "";
            return $"{shader}|{c.r:F3},{c.g:F3},{c.b:F3},{c.a:F3}|{mainTexGuid}|{normalTexGuid}|{emissionTexGuid}|{sc.x:F2},{sc.y:F2}|{off.x:F2},{off.y:F2}|{mat.renderQueue}";
        }

        private static void ExtractTextureGuids(Material mat, out string mainTexGuid, out string normalTexGuid, out string emissionTexGuid)
        {
            Vector2 _, __;
            Texture mainTex = VisualStyleShaderUtility.ExtractMainTexture(mat, out _, out __);
            mainTexGuid = GetAssetGuidOrEmpty(mainTex);

            Texture normalTex = null;
            if (mat.HasProperty("_BumpMap")) normalTex = mat.GetTexture("_BumpMap");
            if (normalTex == null && mat.HasProperty("_NormalMap")) normalTex = mat.GetTexture("_NormalMap");
            normalTexGuid = GetAssetGuidOrEmpty(normalTex);

            Texture emTex = null;
            if (mat.HasProperty("_EmissionMap")) emTex = mat.GetTexture("_EmissionMap");
            if (emTex == null && mat.HasProperty("_EmissionTexture")) emTex = mat.GetTexture("_EmissionTexture");
            emissionTexGuid = GetAssetGuidOrEmpty(emTex);
        }

        private static string GetAssetGuidOrEmpty(UnityEngine.Object obj)
        {
            if (obj == null) return "";
            string p = AssetDatabase.GetAssetPath(obj);
            return string.IsNullOrEmpty(p) ? "" : AssetDatabase.AssetPathToGUID(p);
        }

        private static bool IsExcludedPath(string path, VisualStyleProfile profile)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string n = path.Replace('\\', '/');
            if (n.StartsWith("Assets/Piaaria/Art/VisualStyles/", StringComparison.OrdinalIgnoreCase) ||
                n.StartsWith("Assets/Piaaria/Prefabs/VisualStyles/", StringComparison.OrdinalIgnoreCase) ||
                n.StartsWith("Assets/Piaaria/Scenes/GameplayVariants/", StringComparison.OrdinalIgnoreCase) ||
                n.StartsWith("Assets/Piaaria/Scenes/VisualTests/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (profile?.discovery?.excludedPathPrefixes != null)
            {
                for (int i = 0; i < profile.discovery.excludedPathPrefixes.Count; i++)
                {
                    string prefix = profile.discovery.excludedPathPrefixes[i];
                    if (!string.IsNullOrEmpty(prefix) && n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }

        private static VisualSemanticRole MapModularRole(CityModuleMaterialRole r, CityModuleDefinition def)
        {
            if (def != null)
            {
                if (def.isRoof && r == CityModuleMaterialRole.Trim) return VisualSemanticRole.BuildingRoof;
                if (def.isAwning && r == CityModuleMaterialRole.Fabric) return VisualSemanticRole.BuildingAwning;
                if ((def.isEntrance || def.isDoubleDoor) && r == CityModuleMaterialRole.Wood) return VisualSemanticRole.BuildingDoor;
            }
            return r switch
            {
                CityModuleMaterialRole.Glass => VisualSemanticRole.BuildingWindow,
                CityModuleMaterialRole.Wood => VisualSemanticRole.Wood,
                CityModuleMaterialRole.Trim => VisualSemanticRole.BuildingTrim,
                CityModuleMaterialRole.Fabric => VisualSemanticRole.BuildingAwning,
                CityModuleMaterialRole.Metal => VisualSemanticRole.Metal,
                _ => VisualSemanticRole.BuildingFacade
            };
        }

        private static string GetHierarchyPath(Transform root, Transform target)
        {
            if (target == null) return "";
            if (target == root) return root.name;
            var stack = new Stack<string>();
            Transform cur = target;
            while (cur != null && cur != root)
            {
                stack.Push(cur.name);
                cur = cur.parent;
            }
            return string.Join("/", stack);
        }
    }
}
#endif
