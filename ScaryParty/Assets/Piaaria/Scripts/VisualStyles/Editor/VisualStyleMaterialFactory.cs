#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ScaryParty.VisualStyles.Editor
{
    public sealed class MaterialGenerationReport
    {
        public string styleId;
        public string libraryPath;
        public string manifestPath;
        public int createdCount;
        public int updatedCount;
        public int skippedUnchangedCount;
        public int preservedCount;
        public int manualReviewCount;
        public int orphanCount;
        public int errorCount;
        public List<string> errors = new List<string>();
        public List<string> warnings = new List<string>();
        public List<string> touchedAssetPaths = new List<string>();
    }

    /// <summary>
    /// Idempotent, GUID-preserving material factory creating Distant Lands Illustrate materials,
    /// VisualMaterialLibrary, and VisualStyleConversionManifest per style ID.
    /// Never overwrites canonical/third-party materials or emits duplicate _1/_2 assets.
    /// </summary>
    public static class VisualStyleMaterialFactory
    {
        public const string GeneratedRootFolder = "Assets/Piaaria/Art/VisualStyles/Generated";

        public static string GetStyleGeneratedFolder(string styleId)
        {
            string safeId = SanitizeFileToken(string.IsNullOrEmpty(styleId) ? "default" : styleId);
            return $"{GeneratedRootFolder}/{safeId}";
        }

        public static string GetLibraryPath(string styleId) => $"{GetStyleGeneratedFolder(styleId)}/Library.asset";
        public static string GetManifestPath(string styleId) => $"{GetStyleGeneratedFolder(styleId)}/GenerationManifest.asset";

        public static VisualMaterialLibrary LoadLibrary(string styleId) =>
            AssetDatabase.LoadAssetAtPath<VisualMaterialLibrary>(GetLibraryPath(styleId));

        public static VisualStyleConversionManifest LoadManifest(string styleId) =>
            AssetDatabase.LoadAssetAtPath<VisualStyleConversionManifest>(GetManifestPath(styleId));

        /// <summary>
        /// Generates or incrementally updates persistent Distant Lands materials for discovered targets.
        /// Preserves asset GUIDs and avoids rewriting unchanged material files.
        /// </summary>
        public static MaterialGenerationReport GenerateOrUpdateLibrary(
            VisualStyleProfile profile,
            VisualConversionRuleSet ruleSet,
            DiscoveryScanResult scanResult,
            bool onlyDirtyOrNew = false)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (ruleSet == null) ruleSet = VisualStylePresetSeeder.EnsureDefaultRuleSet();

            var report = new MaterialGenerationReport
            {
                styleId = profile.styleId,
                libraryPath = GetLibraryPath(profile.styleId),
                manifestPath = GetManifestPath(profile.styleId)
            };

            // Verify Illustrate shaders exist before generating
            IllustrateShaderFamily[] requiredFamilies =
            {
                IllustrateShaderFamily.Opaque,
                IllustrateShaderFamily.Simple,
                IllustrateShaderFamily.Transparent,
                IllustrateShaderFamily.AlphaClipped,
                IllustrateShaderFamily.Outline
            };
            var shaderMap = new Dictionary<IllustrateShaderFamily, Shader>();
            for (int i = 0; i < requiredFamilies.Length; i++)
            {
                string shaderName = VisualStyleShaderUtility.GetShaderName(requiredFamilies[i]);
                Shader s = Shader.Find(shaderName);
                if (s == null)
                {
                    report.errorCount++;
                    report.errors.Add($"Missing required shader '{shaderName}'.");
                }
                else
                {
                    shaderMap[requiredFamilies[i]] = s;
                }
            }

            if (report.errorCount > 0)
            {
                return report;
            }

            string styleFolder = GetStyleGeneratedFolder(profile.styleId);
            VisualStylePresetSeeder.EnsureFolder($"{styleFolder}/Materials");

            // Pre-create all material subfolders BEFORE StartAssetEditing so AssetDatabase never emits "Folder 1" duplicates
            string[] coreSubfolders = { "Architecture", "City", "Streets", "Food", "Characters", "Props", "Pizzeria" };
            var ensuredFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < coreSubfolders.Length; i++)
            {
                string f = $"{styleFolder}/Materials/{coreSubfolders[i]}";
                if (ensuredFolders.Add(f)) VisualStylePresetSeeder.EnsureFolder(f);
            }
            if (scanResult != null && scanResult.targets != null)
            {
                for (int i = 0; i < scanResult.targets.Count; i++)
                {
                    var t = scanResult.targets[i];
                    if (t == null) continue;
                    string f = $"{styleFolder}/Materials/{ResolveSubfolderForCategory(t.decision.categoryId)}";
                    if (ensuredFolders.Add(f)) VisualStylePresetSeeder.EnsureFolder(f);
                }
            }

            VisualMaterialLibrary existingLib = LoadLibrary(profile.styleId);
            VisualStyleConversionManifest existingManifest = LoadManifest(profile.styleId);

            bool libraryDirty = false;
            bool manifestDirty = false;
            string currentProfileHash = profile.ComputeContentHash();
            string currentRuleHash = ruleSet.ComputeRuleSetHash();

            var preservedTargets = new List<DiscoveredMaterialTarget>();
            var manualReviewTargets = new List<DiscoveredMaterialTarget>();
            var convertedTargets = new List<(DiscoveredMaterialTarget target, string assetPath, Material mat)>();
            var pendingRoleEntries = new List<RoleMaterialEntry>();

            AssetDatabase.StartAssetEditing();
            try
            {
                if (scanResult != null && scanResult.targets != null)
                {
                    for (int i = 0; i < scanResult.targets.Count; i++)
                    {
                        DiscoveredMaterialTarget target = scanResult.targets[i];
                        if (target == null) continue;

                        if (onlyDirtyOrNew && target.incrementalStatus == VisualAssetSyncStatus.UpToDate)
                        {
                            report.skippedUnchangedCount++;
                            continue;
                        }

                        if (target.decision.action == VisualConversionAction.Preserve || target.decision.action == VisualConversionAction.Skip)
                        {
                            report.preservedCount++;
                            preservedTargets.Add(target);
                            continue;
                        }

                        if (target.decision.action == VisualConversionAction.ManualReview)
                        {
                            report.manualReviewCount++;
                            manualReviewTargets.Add(target);
                            continue;
                        }

                        // Check promoted custom protection
                        ConversionManifestEntry existingEntry = existingManifest != null
                            ? (existingManifest.FindByKey(target.conversionKey)
                               ?? existingManifest.FindBySourceMaterial(target.sourceMaterialGuid, target.sourceLocalFileId))
                            : null;

                        if (existingEntry != null && existingEntry.isPromotedCustomMaterial)
                        {
                            report.skippedUnchangedCount++;
                            continue;
                        }

                        string subfolder = ResolveSubfolderForCategory(target.decision.categoryId);
                        string folderPath = $"{styleFolder}/Materials/{subfolder}";

                        string shortHash = ExtractShortHash(target.conversionKey);
                        string cleanMatName = SanitizeFileToken(target.descriptor.materialName);
                        string assetName = $"Mat_VS_{SanitizeFileToken(profile.styleId)}_{target.decision.semanticRole}_{cleanMatName}_{shortHash}.mat";
                        string assetPath = existingEntry != null && !string.IsNullOrEmpty(existingEntry.targetMaterialAssetPath)
                            ? existingEntry.targetMaterialAssetPath
                            : $"{folderPath}/{assetName}";

                        // Never overwrite canonical assets accidentally
                        if (!assetPath.StartsWith(GeneratedRootFolder, StringComparison.OrdinalIgnoreCase))
                        {
                            assetPath = $"{folderPath}/{assetName}";
                        }

                        Shader targetShader = shaderMap[target.decision.shaderFamily];
                        Material existingMat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                        bool isNew = existingMat == null;
                        Material mat = isNew ? new Material(targetShader) { name = Path.GetFileNameWithoutExtension(assetPath) } : existingMat;

                        bool propertiesChanged = ApplyConvertedMaterialProperties(mat, targetShader, target, profile);

                        if (isNew)
                        {
                            AssetDatabase.CreateAsset(mat, assetPath);
                            report.createdCount++;
                            report.touchedAssetPaths.Add(assetPath);
                        }
                        else if (propertiesChanged ||
                                 target.incrementalStatus == VisualAssetSyncStatus.SourceModified ||
                                 target.incrementalStatus == VisualAssetSyncStatus.ProfileOutdated)
                        {
                            EditorUtility.SetDirty(mat);
                            report.updatedCount++;
                            report.touchedAssetPaths.Add(assetPath);
                        }
                        else
                        {
                            report.skippedUnchangedCount++;
                        }

                        convertedTargets.Add((target, assetPath, mat));
                    }
                }

                // Ensure shared semantic role default materials exist cleanly inside the same asset editing batch
                bool skipUnchangedRoles = onlyDirtyOrNew &&
                    existingLib != null &&
                    string.Equals(existingLib.profileContentHash, currentProfileHash, StringComparison.Ordinal);
                libraryDirty |= EnsureSharedRoleMaterials(profile, styleFolder, shaderMap, existingLib, report, skipUnchangedRoles, pendingRoleEntries);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            if (report.createdCount > 0 || report.updatedCount > 0)
            {
                AssetDatabase.SaveAssets();
            }

            // Acquire & mutate Library and Manifest cleanly AFTER StopAssetEditing so AssetDatabase never overwrites unsaved entries
            VisualMaterialLibrary library = EnsureLibraryAsset(report.libraryPath, profile, ruleSet);
            VisualStyleConversionManifest manifest = EnsureManifestAsset(report.manifestPath, profile, ruleSet);

            for (int i = 0; i < preservedTargets.Count; i++)
            {
                var target = preservedTargets[i];
                library.UpsertMappedMaterial(new MappedMaterialEntry
                {
                    conversionKey = target.conversionKey,
                    sourceMaterialGuid = target.sourceMaterialGuid,
                    sourceLocalFileId = target.sourceLocalFileId,
                    sourceMaterialName = target.descriptor.materialName,
                    sourceShaderName = target.descriptor.shaderName,
                    sourceTextureGuid = target.mainTexGuid,
                    sourceMaterial = target.sourceMaterial,
                    categoryId = target.decision.categoryId,
                    semanticRole = target.decision.semanticRole,
                    shaderFamily = IllustrateShaderFamily.PreserveOriginal,
                    action = target.decision.action,
                    confidence = target.decision.confidence,
                    classificationReason = target.decision.reason,
                    targetMaterial = target.sourceMaterial,
                    targetMaterialGuid = target.sourceMaterialGuid,
                    allowsDynamicTint = false,
                    preservedOriginal = true
                });
                libraryDirty = true;
            }

            for (int i = 0; i < manualReviewTargets.Count; i++)
            {
                manifest.UpsertEntry(BuildManifestEntry(manualReviewTargets[i], null, null, VisualAssetSyncStatus.ManualReview));
                manifestDirty = true;
            }

            for (int i = 0; i < convertedTargets.Count; i++)
            {
                var (target, assetPath, mat) = convertedTargets[i];
                if (mat == null) mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                string targetGuid = AssetDatabase.AssetPathToGUID(assetPath);
                library.UpsertMappedMaterial(new MappedMaterialEntry
                {
                    conversionKey = target.conversionKey,
                    sourceMaterialGuid = target.sourceMaterialGuid,
                    sourceLocalFileId = target.sourceLocalFileId,
                    sourceMaterialName = target.descriptor.materialName,
                    sourceShaderName = target.descriptor.shaderName,
                    sourceTextureGuid = target.mainTexGuid,
                    sourceMaterial = target.sourceMaterial,
                    categoryId = target.decision.categoryId,
                    semanticRole = target.decision.semanticRole,
                    shaderFamily = target.decision.shaderFamily,
                    action = VisualConversionAction.Convert,
                    confidence = target.decision.confidence,
                    classificationReason = target.decision.reason,
                    targetMaterial = mat,
                    targetMaterialGuid = targetGuid,
                    allowsDynamicTint = target.decision.allowDynamicTint,
                    preservedOriginal = false
                });
                libraryDirty = true;

                manifest.UpsertEntry(BuildManifestEntry(target, assetPath, targetGuid, VisualAssetSyncStatus.UpToDate));
                manifestDirty = true;
            }

            for (int i = 0; i < pendingRoleEntries.Count; i++)
            {
                library.SetRoleDefault(pendingRoleEntries[i]);
                libraryDirty = true;
            }

            report.orphanCount = scanResult != null ? scanResult.orphanedEntries.Count : 0;

            if (libraryDirty || library.profileContentHash != currentProfileHash)
            {
                library.styleId = profile.styleId;
                library.profileVersion = profile.profileVersion;
                library.profileContentHash = currentProfileHash;
                library.generationTimestampUtc = DateTime.UtcNow.ToString("O");
                library.profile = profile;
                library.ruleSet = ruleSet;
                library.InvalidateLookupCache();
                EditorUtility.SetDirty(library);
            }

            if (manifestDirty || manifest.profileContentHash != currentProfileHash || manifest.ruleSetHash != currentRuleHash)
            {
                manifest.styleId = profile.styleId;
                manifest.styleName = profile.displayName;
                manifest.profileContentHash = currentProfileHash;
                manifest.ruleSetHash = currentRuleHash;
                manifest.unityVersion = Application.unityVersion;
                manifest.lastGeneratedUtc = DateTime.UtcNow.ToString("O");
                manifest.lastValidationPassed = report.errorCount == 0;
                EditorUtility.SetDirty(manifest);
            }

            if (report.createdCount > 0 || report.updatedCount > 0 || libraryDirty || manifestDirty)
            {
                AssetDatabase.SaveAssets();
            }

            return report;
        }

        private static bool ApplyConvertedMaterialProperties(
            Material dst,
            Shader targetShader,
            DiscoveredMaterialTarget target,
            VisualStyleProfile profile)
        {
            string beforeJson = EditorJsonUtility.ToJson(dst);

            if (dst.shader != targetShader)
            {
                dst.shader = targetShader;
            }

            Material src = target.sourceMaterial;
            Color baseColor = VisualStyleShaderUtility.ExtractBaseColor(src, Color.white);
            Vector2 texScale = Vector2.one;
            Vector2 texOffset = Vector2.zero;
            Texture mainTex = null;
            Texture normalTex = null;
            Texture emissionTex = null;
            Color? emissionColor = null;
            float? alphaCutoff = null;
            int? cullMode = null;

            EffectiveCategorySettings cat = profile.GetEffectiveCategorySettings(target.decision.categoryId);
            if (src != null)
            {
                if (cat.preserveTextures && target.decision.preserveTexture)
                {
                    mainTex = VisualStyleShaderUtility.ExtractMainTexture(src, out texScale, out texOffset);
                }

                if (profile.materials.preserveNormalMaps)
                {
                    if (src.HasProperty("_BumpMap")) normalTex = src.GetTexture("_BumpMap");
                    if (normalTex == null && src.HasProperty("_NormalMap")) normalTex = src.GetTexture("_NormalMap");
                }

                if (profile.materials.preserveEmission &&
                    (src.IsKeywordEnabled("_EMISSION") || (src.HasProperty("_EmissionColor") && src.GetColor("_EmissionColor").maxColorComponent > 0.05f)))
                {
                    if (src.HasProperty("_EmissionColor")) emissionColor = src.GetColor("_EmissionColor");
                    if (src.HasProperty("_EmissionMap")) emissionTex = src.GetTexture("_EmissionMap");
                    if (emissionTex == null && src.HasProperty("_EmissionTexture")) emissionTex = src.GetTexture("_EmissionTexture");
                }

                if (target.decision.shaderFamily == IllustrateShaderFamily.AlphaClipped)
                {
                    if (src.HasProperty("_Cutoff")) alphaCutoff = src.GetFloat("_Cutoff");
                    else if (src.HasProperty("_AlphaClipThreshold")) alphaCutoff = src.GetFloat("_AlphaClipThreshold");
                    else alphaCutoff = 0.5f;
                }

                if (src.HasProperty("_CullMode")) cullMode = Mathf.RoundToInt(src.GetFloat("_CullMode"));
                else if (src.HasProperty("_Cull")) cullMode = Mathf.RoundToInt(src.GetFloat("_Cull"));
            }

            VisualStyleShaderUtility.ApplyProfileToMaterial(
                dst,
                profile,
                target.decision.categoryId,
                target.decision.semanticRole,
                baseColor,
                target.decision.shaderFamily,
                mainTex,
                profile.materials.preserveUVTilingOffset ? texScale : (Vector2?)null,
                profile.materials.preserveUVTilingOffset ? texOffset : (Vector2?)null,
                normalTex,
                alphaCutoff,
                cullMode,
                emissionTex,
                emissionColor,
                forceUnlit: false);

            // Preserve queue on special render queues (like transparent 3000 or road marking 2010)
            if (src != null)
            {
                if (target.decision.shaderFamily == IllustrateShaderFamily.Transparent)
                {
                    int targetQueue = Math.Max(3000, src.renderQueue);
                    if (dst.renderQueue != targetQueue) dst.renderQueue = targetQueue;
                    if (!string.Equals(dst.GetTag("RenderType", false), "Transparent", StringComparison.Ordinal))
                        dst.SetOverrideTag("RenderType", "Transparent");
                }
                else if (target.decision.shaderFamily == IllustrateShaderFamily.AlphaClipped)
                {
                    int alphaQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                    if (dst.renderQueue != alphaQueue) dst.renderQueue = alphaQueue;
                    if (!string.Equals(dst.GetTag("RenderType", false), "TransparentCutout", StringComparison.Ordinal))
                        dst.SetOverrideTag("RenderType", "TransparentCutout");
                }
                else if (src.renderQueue == 2010 && dst.renderQueue != 2010)
                {
                    dst.renderQueue = 2010;
                }
            }

            // Enable instancing for shared city/food/character materials
            if (!dst.enableInstancing)
            {
                dst.enableInstancing = true;
            }

            string afterJson = EditorJsonUtility.ToJson(dst);
            return !string.Equals(beforeJson, afterJson, StringComparison.Ordinal);
        }

        private static bool EnsureSharedRoleMaterials(
            VisualStyleProfile profile,
            string styleFolder,
            Dictionary<IllustrateShaderFamily, Shader> shaderMap,
            VisualMaterialLibrary existingLibrary,
            MaterialGenerationReport report,
            bool skipUnchangedRoles,
            List<RoleMaterialEntry> outRoleEntries)
        {
            bool changed = false;

            (VisualSemanticRole role, string cat, IllustrateShaderFamily fam, Color color, string subfolder, bool dynTint, int queue, bool unlit)[] defaults =
            {
                (VisualSemanticRole.GenericOpaque, VisualCategoryIds.Props, IllustrateShaderFamily.Opaque, profile.colorPalette.neutralLight, "Props", true, 2000, false),
                (VisualSemanticRole.GenericSimple, VisualCategoryIds.Props, IllustrateShaderFamily.Simple, profile.colorPalette.neutralLight, "Props", true, 2000, false),
                (VisualSemanticRole.GenericTransparent, VisualCategoryIds.Props, IllustrateShaderFamily.Transparent, new Color(0.65f, 0.85f, 0.96f, 0.5f), "Props", false, 3000, false),
                (VisualSemanticRole.GenericAlphaClipped, VisualCategoryIds.Props, IllustrateShaderFamily.AlphaClipped, Color.white, "Props", false, 2450, false),

                // Streets & Signage
                (VisualSemanticRole.Street, VisualCategoryIds.Streets, IllustrateShaderFamily.Simple, new Color(0.26f, 0.28f, 0.32f, 1f), "Streets", false, 2000, false),
                (VisualSemanticRole.Sidewalk, VisualCategoryIds.Streets, IllustrateShaderFamily.Simple, new Color(0.70f, 0.69f, 0.65f, 1f), "Streets", false, 2000, false),
                (VisualSemanticRole.Intersection, VisualCategoryIds.Streets, IllustrateShaderFamily.Simple, new Color(0.24f, 0.26f, 0.30f, 1f), "Streets", false, 2000, false),
                (VisualSemanticRole.RoadYellow, VisualCategoryIds.Streets, IllustrateShaderFamily.Simple, profile.colorPalette.accentYellow, "Streets", false, 2010, true),
                (VisualSemanticRole.RoadWhite, VisualCategoryIds.Streets, IllustrateShaderFamily.Simple, new Color(0.96f, 0.96f, 0.96f, 1f), "Streets", false, 2010, true),
                (VisualSemanticRole.Crosswalk, VisualCategoryIds.Streets, IllustrateShaderFamily.Simple, new Color(0.96f, 0.96f, 0.96f, 1f), "Streets", false, 2010, true),
                (VisualSemanticRole.TrafficLightLens, VisualCategoryIds.Streets, IllustrateShaderFamily.Simple, Color.white, "Streets", true, 2000, true),

                // Buildings
                (VisualSemanticRole.BuildingFacade, VisualCategoryIds.City, IllustrateShaderFamily.Opaque, new Color(0.86f, 0.83f, 0.77f, 1f), "City", true, 2000, false),
                (VisualSemanticRole.BuildingRoof, VisualCategoryIds.City, IllustrateShaderFamily.Opaque, new Color(0.45f, 0.42f, 0.44f, 1f), "City", false, 2000, false),
                (VisualSemanticRole.BuildingTrim, VisualCategoryIds.City, IllustrateShaderFamily.Opaque, new Color(0.62f, 0.60f, 0.56f, 1f), "City", false, 2000, false),
                (VisualSemanticRole.BuildingDoor, VisualCategoryIds.City, IllustrateShaderFamily.Opaque, new Color(0.55f, 0.35f, 0.20f, 1f), "City", false, 2000, false),
                (VisualSemanticRole.BuildingWindow, VisualCategoryIds.City, IllustrateShaderFamily.Transparent, new Color(0.62f, 0.82f, 0.94f, 0.65f), "City", false, 3000, false),
                (VisualSemanticRole.BuildingAwning, VisualCategoryIds.City, IllustrateShaderFamily.Opaque, profile.colorPalette.accentRed, "City", false, 2000, false),

                // Food & Pizza shared fallback roles
                (VisualSemanticRole.PizzaDough, VisualCategoryIds.Food, IllustrateShaderFamily.Opaque, new Color(0.95f, 0.86f, 0.64f, 1f), "Food", true, 2000, false),
                (VisualSemanticRole.PizzaSauce, VisualCategoryIds.Food, IllustrateShaderFamily.Opaque, new Color(0.85f, 0.19f, 0.13f, 1f), "Food", true, 2000, false),
                (VisualSemanticRole.PizzaCheese, VisualCategoryIds.Food, IllustrateShaderFamily.Opaque, new Color(0.99f, 0.84f, 0.32f, 1f), "Food", true, 2000, false),
                (VisualSemanticRole.PizzaPepperoni, VisualCategoryIds.Food, IllustrateShaderFamily.Opaque, new Color(0.76f, 0.16f, 0.14f, 1f), "Food", true, 2000, false),
                (VisualSemanticRole.PizzaMushroom, VisualCategoryIds.Food, IllustrateShaderFamily.Opaque, new Color(0.76f, 0.68f, 0.58f, 1f), "Food", true, 2000, false),
                (VisualSemanticRole.PizzaBox, VisualCategoryIds.Food, IllustrateShaderFamily.Opaque, new Color(0.93f, 0.87f, 0.78f, 1f), "Food", true, 2000, false),
                (VisualSemanticRole.FoodGeneric, VisualCategoryIds.Food, IllustrateShaderFamily.Opaque, Color.white, "Food", true, 2000, false),
                (VisualSemanticRole.KitchenTool, VisualCategoryIds.Props, IllustrateShaderFamily.Opaque, new Color(0.78f, 0.80f, 0.84f, 1f), "Pizzeria", true, 2000, false),

                // Characters
                (VisualSemanticRole.PlayerBody, VisualCategoryIds.Characters, IllustrateShaderFamily.Opaque, profile.colorPalette.accentBlue, "Characters", true, 2000, false),
                (VisualSemanticRole.NpcBody, VisualCategoryIds.Characters, IllustrateShaderFamily.Opaque, profile.colorPalette.accentTurquoise, "Characters", true, 2000, false),
                (VisualSemanticRole.Emission, VisualCategoryIds.Effects, IllustrateShaderFamily.Opaque, new Color(1f, 0.55f, 0.15f, 1f), "Props", true, 2000, true),
                (VisualSemanticRole.Metal, VisualCategoryIds.Props, IllustrateShaderFamily.Opaque, new Color(0.55f, 0.58f, 0.64f, 1f), "Props", true, 2000, false),
                (VisualSemanticRole.Wood, VisualCategoryIds.Props, IllustrateShaderFamily.Opaque, new Color(0.58f, 0.38f, 0.22f, 1f), "Props", true, 2000, false),
                (VisualSemanticRole.Glass, VisualCategoryIds.City, IllustrateShaderFamily.Transparent, new Color(0.62f, 0.82f, 0.94f, 0.55f), "City", false, 3000, false)
            };

            for (int i = 0; i < defaults.Length; i++)
            {
                var d = defaults[i];
                string folder = $"{styleFolder}/Materials/{d.subfolder}";
                string path = $"{folder}/Mat_VS_Role_{d.role}.mat";

                Shader sh = shaderMap[d.fam];
                Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (skipUnchangedRoles && existing != null && existingLibrary != null && existingLibrary.GetRoleMaterial(d.role) == existing)
                {
                    report.skippedUnchangedCount++;
                    continue;
                }

                bool isNew = existing == null;
                Material mat = isNew ? new Material(sh) { name = $"Mat_VS_Role_{d.role}" } : existing;

                string before = EditorJsonUtility.ToJson(mat);
                if (mat.shader != sh) mat.shader = sh;

                Color? emissive = d.role == VisualSemanticRole.Emission ? new Color(1f, 0.55f, 0.15f) * 1.5f : (Color?)null;
                VisualStyleShaderUtility.ApplyProfileToMaterial(
                    mat, profile, d.cat, d.role, d.color, d.fam,
                    forceUnlit: d.unlit, emissionColor: emissive);
                if (mat.renderQueue != d.queue) mat.renderQueue = d.queue;
                if (!mat.enableInstancing) mat.enableInstancing = true;

                string after = EditorJsonUtility.ToJson(mat);
                if (isNew)
                {
                    AssetDatabase.CreateAsset(mat, path);
                    report.createdCount++;
                    report.touchedAssetPaths.Add(path);
                    changed = true;
                }
                else if (!string.Equals(before, after, StringComparison.Ordinal))
                {
                    EditorUtility.SetDirty(mat);
                    report.updatedCount++;
                    report.touchedAssetPaths.Add(path);
                    changed = true;
                }

                outRoleEntries.Add(new RoleMaterialEntry
                {
                    role = d.role,
                    shaderFamily = d.fam,
                    material = mat,
                    defaultColor = d.color,
                    supportsDynamicTint = d.dynTint
                });
            }

            return changed;
        }

        private static VisualMaterialLibrary EnsureLibraryAsset(string path, VisualStyleProfile profile, VisualConversionRuleSet ruleSet)
        {
            var existing = AssetDatabase.LoadAssetAtPath<VisualMaterialLibrary>(path);
            if (existing != null) return existing;

            var created = ScriptableObject.CreateInstance<VisualMaterialLibrary>();
            created.styleId = profile.styleId;
            created.profile = profile;
            created.ruleSet = ruleSet;
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static VisualStyleConversionManifest EnsureManifestAsset(string path, VisualStyleProfile profile, VisualConversionRuleSet ruleSet)
        {
            var existing = AssetDatabase.LoadAssetAtPath<VisualStyleConversionManifest>(path);
            if (existing != null) return existing;

            var created = ScriptableObject.CreateInstance<VisualStyleConversionManifest>();
            created.styleId = profile.styleId;
            created.styleName = profile.displayName;
            created.profileContentHash = profile.ComputeContentHash();
            created.ruleSetHash = ruleSet.ComputeRuleSetHash();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static ConversionManifestEntry BuildManifestEntry(
            DiscoveredMaterialTarget t,
            string targetPath,
            string targetGuid,
            VisualAssetSyncStatus status)
        {
            return new ConversionManifestEntry
            {
                conversionKey = t.conversionKey,
                sourceAssetPath = t.sourceAssetPath,
                sourceAssetGuid = t.sourceAssetGuid,
                sourceMaterialGuid = t.sourceMaterialGuid,
                sourceLocalFileId = t.sourceLocalFileId,
                sourceMaterialName = t.descriptor.materialName,
                sourceShaderName = t.descriptor.shaderName,
                sourceDependencyHash = t.sourceDependencyHash,
                sourceTextureGuid = t.mainTexGuid,
                targetMaterialAssetPath = targetPath,
                targetMaterialGuid = targetGuid,
                targetShaderName = VisualStyleShaderUtility.GetShaderName(t.decision.shaderFamily),
                categoryId = t.decision.categoryId,
                semanticRole = t.decision.semanticRole,
                shaderFamily = t.decision.shaderFamily,
                action = t.decision.action,
                status = status,
                confidence = t.decision.confidence,
                classificationReason = t.decision.reason,
                referencingAssets = new List<string>(t.usageContexts)
            };
        }

        private static string ResolveSubfolderForCategory(string categoryId)
        {
            if (string.IsNullOrEmpty(categoryId)) return "Props";
            if (categoryId == VisualCategoryIds.Architecture) return "Architecture";
            if (categoryId == VisualCategoryIds.City) return "City";
            if (categoryId == VisualCategoryIds.Streets) return "Streets";
            if (categoryId == VisualCategoryIds.Food) return "Food";
            if (categoryId == VisualCategoryIds.Characters) return "Characters";
            if (categoryId == VisualCategoryIds.Props) return "Props";
            return SanitizeFileToken(categoryId);
        }

        private static string ExtractShortHash(string conversionKey)
        {
            if (string.IsNullOrEmpty(conversionKey)) return "00000000";
            int idx = conversionKey.LastIndexOf('_');
            return idx >= 0 && idx + 1 < conversionKey.Length ? conversionKey.Substring(idx + 1) : "00000000";
        }

        public static string SanitizeFileToken(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "Unnamed";
            var sb = new StringBuilder(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                char ch = raw[i];
                sb.Append(char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' ? ch : '_');
            }
            return sb.ToString();
        }
    }
}
#endif
