#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using ScaryParty.Pizzeria.Presentation;
using UnityEditor;
using UnityEngine;

namespace ScaryParty.VisualStyles.Editor
{
    public sealed class PrefabVariantBuildReport
    {
        public string variantFolder;
        public GameObject pizzeriaVariantPrefab;
        public GameObject characterVariantPrefab;
        public ItemPresentationCatalog itemCatalogVariant;
        public int createdOrUpdatedPrefabs;
    }

    /// <summary>
    /// Builds non-destructive variant prefabs and catalogs under Assets/Piaaria/Prefabs/VisualStyles/<StyleId>/
    /// without mutating canonical Pizzeria, Character, PlayerArmature, or DefaultNetworkPrefabs assets.
    /// </summary>
    public static class VisualStylePrefabVariantBuilder
    {
        public const string PrefabVariantsRootFolder = "Assets/Piaaria/Prefabs/VisualStyles";

        public static string GetStyleVariantFolder(string styleId)
        {
            string safeId = VisualStyleMaterialFactory.SanitizeFileToken(string.IsNullOrEmpty(styleId) ? "default" : styleId);
            return $"{PrefabVariantsRootFolder}/{safeId}";
        }

        public static PrefabVariantBuildReport BuildVariants(
            VisualStyleProfile profile,
            VisualMaterialLibrary library,
            VisualStyleConversionManifest manifest = null)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (library == null) throw new ArgumentNullException(nameof(library));

            library.EnsureLookupInitialized();
            var resolver = new VisualMaterialResolver(profile, library);

            string safeId = VisualStyleMaterialFactory.SanitizeFileToken(string.IsNullOrEmpty(profile.styleId) ? "default" : profile.styleId);
            string variantFolder = GetStyleVariantFolder(profile.styleId);
            VisualStylePresetSeeder.EnsureFolder(variantFolder);
            VisualStylePresetSeeder.EnsureFolder($"{variantFolder}/Items");

            var report = new PrefabVariantBuildReport
            {
                variantFolder = variantFolder
            };

            // 1. Pizzeria Variant Prefab (visual variant with VisualStyleBinder; keeps NGO canonical hash on PizzeriaModular_Manual intact)
            string pizzeriaVariantPath = $"{variantFolder}/PizzeriaModular_{safeId}.prefab";
            report.pizzeriaVariantPrefab = BuildPrefabVariant(
                VisualStyleAuditService.PizzeriaManualPrefabPath,
                pizzeriaVariantPath,
                resolver,
                "pizzeria_modular",
                VisualSemanticRole.BuildingFacade,
                VisualCategoryIds.Architecture,
                addBinder: true,
                manifest: manifest);
            if (report.pizzeriaVariantPrefab != null)
            {
                report.createdOrUpdatedPrefabs++;
                if (library.styledPizzeriaVariantPrefab != report.pizzeriaVariantPrefab)
                {
                    library.styledPizzeriaVariantPrefab = report.pizzeriaVariantPrefab;
                    EditorUtility.SetDirty(library);
                }
            }

            // 2. Character Visual Variant Prefab (shared cosmetic child for NPCs/preview while players keep NGO root intact)
            string charVariantPath = $"{variantFolder}/CharacterVisual_{safeId}.prefab";
            report.characterVariantPrefab = BuildPrefabVariant(
                VisualStyleAuditService.CharacterVisualPrefabPath,
                charVariantPath,
                resolver,
                "character_visual",
                VisualSemanticRole.PlayerBody,
                VisualCategoryIds.Characters,
                addBinder: true,
                manifest: manifest);
            if (report.characterVariantPrefab != null)
            {
                report.createdOrUpdatedPrefabs++;
                if (library.styledCharacterVariantPrefab != report.characterVariantPrefab)
                {
                    library.styledCharacterVariantPrefab = report.characterVariantPrefab;
                    EditorUtility.SetDirty(library);
                }
            }

            // 3. Item Presentation Catalog & Item Visual Prefabs
            report.itemCatalogVariant = BuildItemCatalogVariant(
                profile, library, resolver, variantFolder, safeId, manifest, report);

            if (report.itemCatalogVariant != null && library.styledItemCatalogAsset != report.itemCatalogVariant)
            {
                library.styledItemCatalogAsset = report.itemCatalogVariant;
                EditorUtility.SetDirty(library);
            }

            AssetDatabase.SaveAssets();
            return report;
        }

        private static GameObject BuildPrefabVariant(
            string sourcePrefabPath,
            string targetPrefabPath,
            VisualMaterialResolver resolver,
            string contextKey,
            VisualSemanticRole defaultRole,
            string categoryId,
            bool addBinder,
            VisualStyleConversionManifest manifest)
        {
            GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePrefabPath);
            if (sourcePrefab == null) return null;

            GameObject instance = PrefabUtility.InstantiatePrefab(sourcePrefab) as GameObject
                                  ?? UnityEngine.Object.Instantiate(sourcePrefab);
            if (instance == null) return null;

            try
            {
                // Preserve authored prefab edits while applying converted Illustrate materials from the library.
                resolver.ApplyToHierarchy(instance, contextKey, defaultRole, categoryId, includeInactive: true, recordOriginal: false);

                if (addBinder)
                {
                    var binder = instance.GetComponent<VisualStyleBinder>() ?? instance.AddComponent<VisualStyleBinder>();
                    binder.contextKey = contextKey;
                    binder.defaultRole = defaultRole;
                    binder.defaultCategoryId = categoryId;
                    binder.includeInactiveChildren = true;
                    binder.applyOnAwake = true;
                    binder.listenForStyleChanges = true;
                }

                // Skip overwriting any promoted custom prefab if present and promoted in manifest
                if (manifest != null)
                {
                    string key = $"prefab_{Path.GetFileNameWithoutExtension(targetPrefabPath)}";
                    var existingEntry = manifest.FindByKey(key);
                    if (existingEntry != null && existingEntry.isPromotedCustomMaterial)
                    {
                        var existingCustom = AssetDatabase.LoadAssetAtPath<GameObject>(targetPrefabPath);
                        if (existingCustom != null) return existingCustom;
                    }
                }

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, targetPrefabPath);
                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static ItemPresentationCatalog BuildItemCatalogVariant(
            VisualStyleProfile profile,
            VisualMaterialLibrary library,
            VisualMaterialResolver resolver,
            string variantFolder,
            string safeId,
            VisualStyleConversionManifest manifest,
            PrefabVariantBuildReport report)
        {
            var sourceCatalog = AssetDatabase.LoadAssetAtPath<ItemPresentationCatalog>(VisualStyleAuditService.ItemPresentationCatalogPath);
            if (sourceCatalog == null) return null;

            string catalogPath = $"{variantFolder}/ItemPresentationCatalog_{safeId}.asset";
            ItemPresentationCatalog variantCatalog = AssetDatabase.LoadAssetAtPath<ItemPresentationCatalog>(catalogPath);
            if (variantCatalog == null)
            {
                variantCatalog = ScriptableObject.CreateInstance<ItemPresentationCatalog>();
                AssetDatabase.CreateAsset(variantCatalog, catalogPath);
            }

            var newProfiles = new List<ItemPresentationProfile>();
            string itemsFolder = $"{variantFolder}/Items";
            string profilesFolder = $"{itemsFolder}/Profiles";
            VisualStylePresetSeeder.EnsureFolder(profilesFolder);

            if (sourceCatalog.profiles != null)
            {
                for (int i = 0; i < sourceCatalog.profiles.Count; i++)
                {
                    ItemPresentationProfile srcProfile = sourceCatalog.profiles[i];
                    if (srcProfile == null) continue;

                    string token = VisualStyleMaterialFactory.SanitizeFileToken(
                        !string.IsNullOrEmpty(srcProfile.presentationKey) ? srcProfile.presentationKey : srcProfile.name);
                    string dstProfilePath = $"{profilesFolder}/Profile_{safeId}_{token}.asset";

                    ItemPresentationProfile dstProfile = AssetDatabase.LoadAssetAtPath<ItemPresentationProfile>(dstProfilePath);
                    if (dstProfile == null)
                    {
                        dstProfile = ScriptableObject.CreateInstance<ItemPresentationProfile>();
                        AssetDatabase.CreateAsset(dstProfile, dstProfilePath);
                    }

                    string json = EditorJsonUtility.ToJson(srcProfile);
                    EditorJsonUtility.FromJsonOverwrite(json, dstProfile);
                    dstProfile.name = Path.GetFileNameWithoutExtension(dstProfilePath);

                    if (srcProfile.visualPrefab != null)
                    {
                        string srcPrefabPath = AssetDatabase.GetAssetPath(srcProfile.visualPrefab);
                        if (!string.IsNullOrEmpty(srcPrefabPath))
                        {
                            string dstPrefabPath = $"{itemsFolder}/{VisualStyleMaterialFactory.SanitizeFileToken(srcProfile.visualPrefab.name)}_{safeId}.prefab";
                            string cat = srcProfile.isTool ? VisualCategoryIds.Props : VisualCategoryIds.Food;
                            VisualSemanticRole role = ResolveItemSemanticRole(srcProfile);

                            GameObject itemVariant = BuildPrefabVariant(
                                srcPrefabPath,
                                dstPrefabPath,
                                resolver,
                                srcProfile.presentationKey,
                                role,
                                cat,
                                addBinder: true,
                                manifest: manifest);

                            if (itemVariant != null)
                            {
                                dstProfile.visualPrefab = itemVariant;
                                report.createdOrUpdatedPrefabs++;
                            }
                        }
                    }

                    EditorUtility.SetDirty(dstProfile);
                    newProfiles.Add(dstProfile);
                }
            }

            variantCatalog.profiles = newProfiles;
            EditorUtility.SetDirty(variantCatalog);
            return variantCatalog;
        }

        private static VisualSemanticRole ResolveItemSemanticRole(ItemPresentationProfile profile)
        {
            if (profile == null) return VisualSemanticRole.FoodGeneric;
            if (profile.isTool) return VisualSemanticRole.KitchenTool;

            string key = (profile.presentationKey ?? string.Empty).ToLowerInvariant();
            if (key.Contains("box") || key.Contains("caixa")) return VisualSemanticRole.PizzaBox;
            if (key.Contains("dough") || key.Contains("massa")) return VisualSemanticRole.PizzaDough;
            if (key.Contains("sauce") || key.Contains("molho")) return VisualSemanticRole.PizzaSauce;
            if (key.Contains("cheese") || key.Contains("queijo")) return VisualSemanticRole.PizzaCheese;
            if (key.Contains("pepperoni") || key.Contains("calabresa")) return VisualSemanticRole.PizzaPepperoni;
            if (key.Contains("mushroom") || key.Contains("cogumelo")) return VisualSemanticRole.PizzaMushroom;
            return VisualSemanticRole.FoodGeneric;
        }
    }
}
#endif
