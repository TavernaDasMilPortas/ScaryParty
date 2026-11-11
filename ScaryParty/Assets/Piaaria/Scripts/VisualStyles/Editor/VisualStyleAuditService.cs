#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ScaryParty.City.Modular;
using ScaryParty.Pizzeria.Presentation;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ScaryParty.VisualStyles.Editor
{
    public sealed class VisualStyleAuditSummary
    {
        public string timestampUtc;
        public string activeRenderPipelineName;
        public string activeRendererDataName;
        public string activeRendererDataPath;
        public bool allIllustrateShadersPresent;
        public int totalRenderersInspected;
        public int totalSlotsInspected;
        public int uniqueMaterialsCount;
        public int convertibleCount;
        public int preservedCount;
        public int manualReviewCount;
        public int warningCount;
        public int modularPieceCount;
        public int itemPresentationProfileCount;
        public int networkPrefabCount;
        public List<string> warnings = new List<string>();
        public List<string> missingShaders = new List<string>();
        public List<AuditedSlotItem> slots = new List<AuditedSlotItem>();
        public string reportPath;
    }

    [Serializable]
    public sealed class AuditedSlotItem
    {
        public string sourceContext;
        public string assetPath;
        public string rendererType;
        public string objectPath;
        public int slotIndex;
        public string materialName;
        public string materialGuid;
        public long materialLocalFileId;
        public string shaderName;
        public bool hasTexture;
        public bool isTransparent;
        public bool isAlphaClipped;
        public VisualClassificationDecision decision;
    }

    /// <summary>
    /// Performs non-destructive audits of URP pipeline, Illustrate shaders, canonical assets,
    /// modular catalogs, item presentation catalogs, and network prefabs.
    /// </summary>
    public static class VisualStyleAuditService
    {
        public const string CanonicalGameScenePath = "Assets/ThirdParty/StarterAssets/ThirdPersonController/Scenes/GameScene.unity";
        public const string VariantGameScenePath = "Assets/Piaaria/Scenes/GameplayVariants/GameScene_CelShaded.unity";
        public const string PizzeriaManualPrefabPath = "Assets/Piaaria/Prefabs/Pizzeria/PizzeriaModular_Manual.prefab";
        public const string PizzeriaStage1CelPrefabPath = "Assets/Piaaria/Prefabs/Pizzeria/PizzeriaModular_CelShaded.prefab";
        public const string CharacterVisualPrefabPath = "Assets/Piaaria/Prefabs/Characters/CharacterVisual.prefab";
        public const string CharacterResourcesPrefabPath = "Assets/Piaaria/Resources/CharacterVisual.prefab";
        public const string PlayerArmaturePrefabPath = "Assets/ThirdParty/StarterAssets/ThirdPersonController/Prefabs/PlayerArmature.prefab";
        public const string CityModuleCatalogPath = "Assets/Piaaria/ScriptableObjects/City/Modular/CityModuleCatalog.asset";
        public const string ItemPresentationCatalogPath = "Assets/Piaaria/ScriptableObjects/Pizzeria/ItemPresentationCatalog.asset";
        public const string DefaultNetworkPrefabsPath = "Assets/Piaaria/DefaultNetworkPrefabs.asset";
        public const string ReportsFolder = "Assets/Piaaria/Art/VisualStyles/Reports";

        public static VisualStyleAuditSummary RunFullAudit(VisualConversionRuleSet ruleSet, bool writeMarkdownReport = true)
        {
            if (ruleSet == null)
            {
                ruleSet = VisualStylePresetSeeder.EnsureDefaultRuleSet();
            }

            var summary = new VisualStyleAuditSummary
            {
                timestampUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'")
            };

            InspectRenderPipeline(summary);
            InspectShaders(summary);

            var uniqueMaterials = new HashSet<Material>();

            InspectPrefab(PizzeriaManualPrefabPath, "Canonical:PizzeriaManual", ruleSet, summary, uniqueMaterials, VisualCategoryIds.Architecture, VisualSemanticRole.PizzeriaWall);
            if (File.Exists(PizzeriaStage1CelPrefabPath))
            {
                InspectPrefab(PizzeriaStage1CelPrefabPath, "Stage1:PizzeriaCelShaded", ruleSet, summary, uniqueMaterials, VisualCategoryIds.Architecture, VisualSemanticRole.PizzeriaWall);
            }
            InspectPrefab(CharacterVisualPrefabPath, "Canonical:CharacterVisual", ruleSet, summary, uniqueMaterials, VisualCategoryIds.Characters, VisualSemanticRole.PlayerBody, allowDynamicTint: true);
            if (File.Exists(CharacterResourcesPrefabPath))
            {
                InspectPrefab(CharacterResourcesPrefabPath, "Resources:CharacterVisual", ruleSet, summary, uniqueMaterials, VisualCategoryIds.Characters, VisualSemanticRole.NpcBody, allowDynamicTint: true);
            }
            InspectPrefab(PlayerArmaturePrefabPath, "Canonical:PlayerArmature", ruleSet, summary, uniqueMaterials, VisualCategoryIds.Characters, VisualSemanticRole.PlayerBody, allowDynamicTint: true);

            InspectCityModuleCatalog(ruleSet, summary, uniqueMaterials);
            InspectItemPresentationCatalog(ruleSet, summary, uniqueMaterials);
            InspectNetworkPrefabs(ruleSet, summary, uniqueMaterials);
            InspectSceneNonDestructively(CanonicalGameScenePath, "Canonical:GameScene", ruleSet, summary, uniqueMaterials);

            summary.uniqueMaterialsCount = uniqueMaterials.Count;
            summary.warningCount = summary.warnings.Count;

            if (writeMarkdownReport)
            {
                summary.reportPath = SaveBaselineReport(summary);
            }

            return summary;
        }

        private static void InspectRenderPipeline(VisualStyleAuditSummary summary)
        {
            var rp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (rp == null)
            {
                summary.activeRenderPipelineName = "<None or non-URP>";
                summary.warnings.Add("Active RenderPipeline is null or not UniversalRenderPipelineAsset.");
                return;
            }

            summary.activeRenderPipelineName = rp.name;
            var rdListProp = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var defaultIdxProp = typeof(UniversalRenderPipelineAsset).GetField("m_DefaultRendererIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (rdListProp?.GetValue(rp) is ScriptableRendererData[] dataList && dataList.Length > 0)
            {
                int idx = defaultIdxProp != null ? Mathf.Clamp((int)defaultIdxProp.GetValue(rp), 0, dataList.Length - 1) : 0;
                if (dataList[idx] != null)
                {
                    summary.activeRendererDataName = dataList[idx].name;
                    summary.activeRendererDataPath = AssetDatabase.GetAssetPath(dataList[idx]);
                }
            }
        }

        private static void InspectShaders(VisualStyleAuditSummary summary)
        {
            string[] required =
            {
                VisualStyleShaderUtility.ShaderOpaque,
                VisualStyleShaderUtility.ShaderSimple,
                VisualStyleShaderUtility.ShaderTransparent,
                VisualStyleShaderUtility.ShaderAlphaClipped,
                VisualStyleShaderUtility.ShaderOutline
            };

            summary.allIllustrateShadersPresent = true;
            for (int i = 0; i < required.Length; i++)
            {
                if (Shader.Find(required[i]) == null)
                {
                    summary.allIllustrateShadersPresent = false;
                    summary.missingShaders.Add(required[i]);
                    summary.warnings.Add($"Missing shader: {required[i]}");
                }
            }
        }

        private static void InspectPrefab(
            string prefabPath,
            string contextLabel,
            VisualConversionRuleSet ruleSet,
            VisualStyleAuditSummary summary,
            HashSet<Material> uniqueMaterials,
            string declaredCategory = null,
            VisualSemanticRole declaredRole = VisualSemanticRole.Unclassified,
            bool allowDynamicTint = false)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                summary.warnings.Add($"Missing prefab at '{prefabPath}'.");
                return;
            }

            InspectHierarchy(prefab, contextLabel, prefabPath, ruleSet, summary, uniqueMaterials, declaredCategory, declaredRole, allowDynamicTint);
        }

        private static void InspectCityModuleCatalog(VisualConversionRuleSet ruleSet, VisualStyleAuditSummary summary, HashSet<Material> uniqueMaterials)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CityModuleCatalog>(CityModuleCatalogPath);
            if (catalog == null)
            {
                summary.warnings.Add($"CityModuleCatalog missing at '{CityModuleCatalogPath}'.");
                return;
            }

            summary.modularPieceCount = catalog.definitions.Count;
            for (int i = 0; i < catalog.definitions.Count; i++)
            {
                var def = catalog.definitions[i];
                if (def == null || def.prefab == null)
                {
                    summary.warnings.Add($"CityModuleCatalog definition[{i}] ({def?.id}) has null prefab.");
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(def.prefab);
                VisualSemanticRole role = def.materialRoles != null && def.materialRoles.Length > 0
                    ? MapModularRole(def.materialRoles[0], def)
                    : VisualSemanticRole.BuildingFacade;
                InspectHierarchy(def.prefab, $"ModularKit:{def.id}", path, ruleSet, summary, uniqueMaterials, VisualCategoryIds.City, role, true);
            }
        }

        private static void InspectItemPresentationCatalog(VisualConversionRuleSet ruleSet, VisualStyleAuditSummary summary, HashSet<Material> uniqueMaterials)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemPresentationCatalog>(ItemPresentationCatalogPath);
            if (catalog == null)
            {
                summary.warnings.Add($"ItemPresentationCatalog missing at '{ItemPresentationCatalogPath}'.");
                return;
            }

            summary.itemPresentationProfileCount = catalog.profiles.Count;
            for (int i = 0; i < catalog.profiles.Count; i++)
            {
                var p = catalog.profiles[i];
                if (p == null) continue;
                if (p.visualPrefab == null)
                {
                    summary.warnings.Add($"ItemPresentationProfile '{p.presentationKey}' has no visualPrefab (uses procedural fallback).");
                    continue;
                }
                string path = AssetDatabase.GetAssetPath(p.visualPrefab);
                string cat = p.isTool ? VisualCategoryIds.Props : VisualCategoryIds.Food;
                VisualSemanticRole role = p.isTool ? VisualSemanticRole.KitchenTool : VisualSemanticRole.FoodGeneric;
                InspectHierarchy(p.visualPrefab, $"ItemCatalog:{p.presentationKey}", path, ruleSet, summary, uniqueMaterials, cat, role, !p.isTool);
            }
        }

        private static void InspectNetworkPrefabs(VisualConversionRuleSet ruleSet, VisualStyleAuditSummary summary, HashSet<Material> uniqueMaterials)
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(DefaultNetworkPrefabsPath);
            if (list == null)
            {
                summary.warnings.Add($"DefaultNetworkPrefabs missing at '{DefaultNetworkPrefabsPath}'.");
                return;
            }

            summary.networkPrefabCount = list.PrefabList.Count;
            foreach (var netPrefab in list.PrefabList)
            {
                if (netPrefab?.Prefab == null) continue;
                string path = AssetDatabase.GetAssetPath(netPrefab.Prefab);
                InspectHierarchy(netPrefab.Prefab, $"NetPrefab:{netPrefab.Prefab.name}", path, ruleSet, summary, uniqueMaterials);
            }
        }

        private static void InspectSceneNonDestructively(string scenePath, string label, VisualConversionRuleSet ruleSet, VisualStyleAuditSummary summary, HashSet<Material> uniqueMaterials)
        {
            if (!File.Exists(scenePath))
            {
                summary.warnings.Add($"Scene not found at '{scenePath}'.");
                return;
            }

            Scene existingOpen = SceneManager.GetSceneByPath(scenePath);
            bool openedTemp = false;
            Scene scene = existingOpen;
            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                openedTemp = true;
            }

            try
            {
                var roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    InspectHierarchy(roots[i], label, scenePath, ruleSet, summary, uniqueMaterials);
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

        private static void InspectHierarchy(
            GameObject root,
            string sourceContext,
            string rootAssetPath,
            VisualConversionRuleSet ruleSet,
            VisualStyleAuditSummary summary,
            HashSet<Material> uniqueMaterials,
            string declaredCategory = null,
            VisualSemanticRole declaredRole = VisualSemanticRole.Unclassified,
            bool allowDynamicTint = false)
        {
            if (root == null) return;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer rend = renderers[r];
                if (rend == null) continue;
                summary.totalRenderersInspected++;

                string hierarchyPath = GetRelativePath(root.transform, rend.transform);
                Material[] mats = rend.sharedMaterials;
                var identifier = rend.GetComponent<VisualStyleIdentifier>();

                if (mats == null || mats.Length == 0)
                {
                    summary.warnings.Add($"Renderer '{hierarchyPath}' in '{sourceContext}' has no materials.");
                    continue;
                }

                for (int slot = 0; slot < mats.Length; slot++)
                {
                    Material mat = mats[slot];
                    summary.totalSlotsInspected++;
                    if (mat != null) uniqueMaterials.Add(mat);

                    string matPath = mat != null ? AssetDatabase.GetAssetPath(mat) : "";
                    string matGuid = "";
                    long localId = 0;
                    if (mat != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mat, out string g, out long lid))
                    {
                        matGuid = g;
                        localId = lid;
                    }
                    else if (!string.IsNullOrEmpty(rootAssetPath))
                    {
                        matGuid = AssetDatabase.AssetPathToGUID(rootAssetPath);
                    }

                    string effectiveDeclaredCat = declaredCategory;
                    VisualSemanticRole effectiveDeclaredRole = declaredRole;
                    bool hasDeclared = !string.IsNullOrEmpty(declaredCategory) && declaredRole != VisualSemanticRole.Unclassified;

                    if (identifier != null)
                    {
                        var slotMeta = identifier.GetMetadataForSlot(slot);
                        if (slotMeta != null)
                        {
                            hasDeclared = true;
                            effectiveDeclaredCat = slotMeta.categoryId;
                            effectiveDeclaredRole = slotMeta.role;
                            allowDynamicTint = slotMeta.allowDynamicTint;
                        }
                        else if (!string.IsNullOrEmpty(identifier.defaultCategoryId))
                        {
                            hasDeclared = true;
                            effectiveDeclaredCat = identifier.defaultCategoryId;
                            effectiveDeclaredRole = identifier.defaultRole;
                        }
                    }

                    var descriptor = BuildDescriptor(rend, slot, mat, matPath, matGuid, localId, hierarchyPath,
                        hasDeclared, effectiveDeclaredCat, effectiveDeclaredRole, allowDynamicTint, sourceContext);

                    VisualClassificationDecision decision = ruleSet.Evaluate(in descriptor);
                    switch (decision.action)
                    {
                        case VisualConversionAction.Convert:
                            summary.convertibleCount++;
                            break;
                        case VisualConversionAction.Preserve:
                        case VisualConversionAction.Skip:
                            summary.preservedCount++;
                            break;
                        case VisualConversionAction.ManualReview:
                            summary.manualReviewCount++;
                            break;
                    }

                    summary.slots.Add(new AuditedSlotItem
                    {
                        sourceContext = sourceContext,
                        assetPath = string.IsNullOrEmpty(matPath) ? rootAssetPath : matPath,
                        rendererType = rend.GetType().Name,
                        objectPath = hierarchyPath,
                        slotIndex = slot,
                        materialName = mat != null ? mat.name : "<Null Material>",
                        materialGuid = matGuid,
                        materialLocalFileId = localId,
                        shaderName = mat != null && mat.shader != null ? mat.shader.name : "<Missing Shader>",
                        hasTexture = descriptor.hasMainTexture,
                        isTransparent = descriptor.isTransparentQueue,
                        isAlphaClipped = descriptor.isAlphaClipped,
                        decision = decision
                    });

                    if (mat == null || mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")
                    {
                        summary.warnings.Add($"Broken/missing material at '{sourceContext}' -> '{hierarchyPath}[{slot}]'.");
                    }
                }
            }
        }

        public static VisualAssetSlotDescriptor BuildDescriptor(
            Renderer rend,
            int slotIndex,
            Material mat,
            string matPath,
            string matGuid,
            long localId,
            string hierarchyPath,
            bool hasDeclared,
            string declaredCat,
            VisualSemanticRole declaredRole,
            bool allowDynamicTint,
            string declaredReason)
        {
            string shaderName = mat != null && mat.shader != null ? mat.shader.name : "";
            Vector2 _;
            bool hasTex = mat != null && VisualStyleShaderUtility.ExtractMainTexture(mat, out _, out _) != null;
            bool hasNormal = mat != null &&
                             ((mat.HasProperty("_BumpMap") && mat.GetTexture("_BumpMap") != null) ||
                              (mat.HasProperty("_NormalMap") && mat.GetTexture("_NormalMap") != null));
            bool isTransparent = mat != null && mat.renderQueue >= 3000;
            bool isAlphaClip = mat != null &&
                               (mat.IsKeywordEnabled("_ALPHATEST_ON") ||
                                (mat.HasProperty("_AlphaClip") && mat.GetFloat("_AlphaClip") > 0.5f) ||
                                (mat.HasProperty("_Surface") && mat.GetFloat("_Surface") < 0.5f && mat.HasProperty("_AlphaClip") && mat.GetFloat("_AlphaClip") > 0.5f));
            bool hasEmission = mat != null &&
                               (mat.IsKeywordEnabled("_EMISSION") ||
                                (mat.HasProperty("_EmissionColor") && mat.GetColor("_EmissionColor").maxColorComponent > 0.05f));
            bool isGraph = shaderName.StartsWith("Shader Graphs/", StringComparison.OrdinalIgnoreCase);

            return new VisualAssetSlotDescriptor
            {
                sourceAssetGuid = matGuid,
                sourceMaterialGuid = matGuid,
                sourceLocalFileId = localId,
                slotIndex = slotIndex,
                rendererTypeName = rend != null ? rend.GetType().Name : "MeshRenderer",
                rendererRelativePath = hierarchyPath,
                gameObjectName = rend != null ? rend.gameObject.name : "",
                gameObjectTag = rend != null ? rend.gameObject.tag : "Untagged",
                assetPath = matPath,
                materialName = mat != null ? mat.name : "",
                shaderName = shaderName,
                hasMainTexture = hasTex,
                hasNormalMap = hasNormal,
                isTransparentQueue = isTransparent,
                isAlphaClipped = isAlphaClip,
                hasEmission = hasEmission,
                isShaderGraph = isGraph,
                hasDeclaredMetadata = hasDeclared,
                declaredCategoryId = declaredCat,
                declaredRole = declaredRole,
                declaredShaderFamily = isTransparent ? IllustrateShaderFamily.Transparent : (isAlphaClip ? IllustrateShaderFamily.AlphaClipped : IllustrateShaderFamily.Opaque),
                declaredAction = VisualConversionAction.Convert,
                declaredAllowDynamicTint = allowDynamicTint,
                declaredReason = declaredReason
            };
        }

        public static void BatchSeedAndAudit()
        {
            VisualStylePresetSeeder.EnsureAllDefaultsExist();
            var summary = RunFullAudit(VisualStylePresetSeeder.EnsureDefaultRuleSet(), writeMarkdownReport: true);
            Debug.Log($"[VisualStyleAuditService] Phase0 Audit Complete -> Report: {summary.reportPath}, Slots: {summary.totalSlotsInspected}, UniqueMats: {summary.uniqueMaterialsCount}, Warnings: {summary.warningCount}");
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

        private static string GetRelativePath(Transform root, Transform target)
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

        private static string SaveBaselineReport(VisualStyleAuditSummary summary)
        {
            VisualStylePresetSeeder.EnsureFolder(ReportsFolder);
            string fullPath = Path.Combine(ReportsFolder, "Phase0_Baseline_Report.md");
            var sb = new StringBuilder();
            sb.AppendLine("# Global Cel Shading — Phase 0 Coverage & Baseline Audit");
            sb.AppendLine();
            sb.AppendLine($"- **Timestamp:** {summary.timestampUtc}");
            sb.AppendLine($"- **Unity:** {Application.unityVersion}");
            sb.AppendLine($"- **Active Render Pipeline:** `{summary.activeRenderPipelineName}`");
            sb.AppendLine($"- **Active Renderer Data:** `{summary.activeRendererDataName}` (`{summary.activeRendererDataPath}`)");
            sb.AppendLine($"- **All Distant Lands Illustrate Shaders Present:** `{summary.allIllustrateShadersPresent}`");
            sb.AppendLine($"- **Renderers Inspected:** {summary.totalRenderersInspected} | **Slots:** {summary.totalSlotsInspected} | **Unique Materials:** {summary.uniqueMaterialsCount}");
            sb.AppendLine($"- **Classification Breakdown:** Convert={summary.convertibleCount}, Preserve/Skip={summary.preservedCount}, ManualReview={summary.manualReviewCount}");
            sb.AppendLine($"- **CityModuleCatalog Pieces:** {summary.modularPieceCount} | **ItemPresentation Profiles:** {summary.itemPresentationProfileCount} | **Network Prefabs:** {summary.networkPrefabCount}");
            sb.AppendLine();

            if (summary.warnings.Count > 0)
            {
                sb.AppendLine("## Warnings");
                for (int i = 0; i < summary.warnings.Count; i++)
                {
                    sb.AppendLine($"- {summary.warnings[i]}");
                }
                sb.AppendLine();
            }

            sb.AppendLine("## Sample Audited Slots (First 120)");
            sb.AppendLine("| Context | Object | Slot | Material | Shader | Category | Action | Confidence |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");
            int limit = Math.Min(120, summary.slots.Count);
            for (int i = 0; i < limit; i++)
            {
                var s = summary.slots[i];
                sb.AppendLine($"| `{s.sourceContext}` | `{s.objectPath}` | {s.slotIndex} | `{s.materialName}` | `{s.shaderName}` | `{s.decision.categoryId}` | {s.decision.action} | {s.decision.confidence:0.00} |");
            }

            File.WriteAllText(fullPath, sb.ToString());
            AssetDatabase.ImportAsset(fullPath);
            return fullPath;
        }
    }
}
#endif
