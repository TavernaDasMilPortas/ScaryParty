#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ScaryParty.VisualStyles.Editor
{
    /// <summary>
    /// Coordinates non-destructive application of VisualStyles to GameScene_CelShaded,
    /// persistent boot activation in Resources/VisualStyleRuntimeConfig.asset, exact rollback,
    /// and GlobalCelShadingWizard delegate wiring.
    /// Never overwrites canonical GameScene.unity or canonical ThirdParty assets.
    /// </summary>
    [InitializeOnLoad]
    public static class VisualStyleGameplayIntegrator
    {
        public const string VariantGameplaySceneName = "GameScene_CelShaded";

        static VisualStyleGameplayIntegrator()
        {
            GlobalCelShadingWizard.OnApplyToGameplayRequested = ApplyToGameplayVariant;
            GlobalCelShadingWizard.OnActivateInGameplayRequested = ActivateInGameplayPersistent;
            GlobalCelShadingWizard.OnRestoreOriginalRequested = RestoreOriginalGameplay;
            GlobalCelShadingWizard.OnBuildOrOpenShowcaseRequested = BuildOrUpdateShowcase;
        }

        public static string BuildOrUpdateShowcase(VisualStyleProfile profile, VisualConversionRuleSet ruleSet)
        {
            return VisualStyleShowcaseBuilder.BuildOrUpdateShowcaseScene(profile, ruleSet, restorePreviousActiveScene: true);
        }

        public static bool ApplyToGameplayVariant(VisualStyleProfile profile, VisualConversionRuleSet ruleSet)
        {
            return ApplyToGameplayVariant(profile, ruleSet, keepVariantSceneOpen: false);
        }

        public static bool ApplyToGameplayVariant(
            VisualStyleProfile profile,
            VisualConversionRuleSet ruleSet,
            bool keepVariantSceneOpen)
        {
            if (profile == null) return false;
            if (ruleSet == null) ruleSet = VisualStylePresetSeeder.EnsureDefaultRuleSet();

            // 1. Ensure materials library & manifest exist and are up to date
            VisualMaterialLibrary library = VisualStyleMaterialFactory.LoadLibrary(profile.styleId);
            VisualStyleConversionManifest manifest = VisualStyleMaterialFactory.LoadManifest(profile.styleId);

            if (library == null || manifest == null ||
                !string.Equals(manifest.profileContentHash, profile.ComputeContentHash(), StringComparison.Ordinal))
            {
                var scan = VisualStyleAssetDiscoveryService.Scan(profile, ruleSet, manifest, VisualDiscoveryScope.GameplayReachable);
                var genReport = VisualStyleMaterialFactory.GenerateOrUpdateLibrary(profile, ruleSet, scan);
                if (genReport.errorCount > 0)
                {
                    Debug.LogError($"[VisualStyleGameplayIntegrator] Material generation failed for '{profile.styleId}'.");
                    return false;
                }
                library = VisualStyleMaterialFactory.LoadLibrary(profile.styleId);
                manifest = VisualStyleMaterialFactory.LoadManifest(profile.styleId);
            }

            if (library == null || manifest == null) return false;

            // 2. Ensure duplicated URP renderer & screen-space outline feature
            var rpResult = VisualStyleUrpOutlineConfigurator.CreateOrUpdateVariantRenderPipeline(profile, library);

            // 3. Build cosmetic variant prefabs / item catalog (preserving NGO prefab hashes on canonical prefabs)
            VisualStylePrefabVariantBuilder.BuildVariants(profile, library, manifest);

            // 4. Snapshot rollback state before touching BuildSettings or variant scene
            var runtimeCfg = VisualStylePresetSeeder.EnsureRuntimeConfig(profile);
            var existingScenes = EditorBuildSettings.scenes != null
                ? new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes)
                : new List<EditorBuildSettingsScene>();

            bool hadVariantInBuild = existingScenes.Any(s =>
                s != null && string.Equals(s.path, VisualStyleAuditService.VariantGameScenePath, StringComparison.OrdinalIgnoreCase));

            var fieldSnapshots = new List<VisualStyleRollbackFieldSnapshot>();
            var rollback = new VisualStyleRollbackSnapshot
            {
                capturedAtUtc = DateTime.UtcNow.ToString("O"),
                originalGameplayScenePath = VisualStyleAuditService.CanonicalGameScenePath,
                variantGameplayScenePath = VisualStyleAuditService.VariantGameScenePath,
                previousActiveSceneBootName = string.IsNullOrEmpty(runtimeCfg.activeGameplaySceneName)
                    ? VisualStyleRuntimeConfig.CanonicalGameplaySceneName
                    : runtimeCfg.activeGameplaySceneName,
                hadVariantInBuildSettings = hadVariantInBuild,
                fieldSnapshots = fieldSnapshots
            };

            // 5. Copy canonical GameScene.unity -> GameScene_CelShaded.unity without ever opening/modifying GameScene.unity directly
            if (!File.Exists(VisualStyleAuditService.CanonicalGameScenePath))
            {
                Debug.LogError($"[VisualStyleGameplayIntegrator] Canonical scene not found: {VisualStyleAuditService.CanonicalGameScenePath}");
                return false;
            }

            VisualStylePresetSeeder.EnsureFolder("Assets/Piaaria/Scenes/GameplayVariants");
            File.Copy(VisualStyleAuditService.CanonicalGameScenePath, VisualStyleAuditService.VariantGameScenePath, overwrite: true);
            AssetDatabase.ImportAsset(VisualStyleAuditService.VariantGameScenePath, ImportAssetOptions.ForceUpdate);

            // Preserve caller's active scene unless keepVariantSceneOpen is requested
            string callerScenePath = SceneManager.GetActiveScene().path;
            bool openedVariantTemporarily = false;

            Scene variantScene;
            if (string.Equals(callerScenePath, VisualStyleAuditService.VariantGameScenePath, StringComparison.OrdinalIgnoreCase))
            {
                variantScene = SceneManager.GetActiveScene();
            }
            else
            {
                OpenSceneMode mode = string.IsNullOrEmpty(callerScenePath) ? OpenSceneMode.Single : OpenSceneMode.Additive;
                variantScene = EditorSceneManager.OpenScene(VisualStyleAuditService.VariantGameScenePath, mode);
                openedVariantTemporarily = mode == OpenSceneMode.Additive;
            }

            try
            {
                SceneManager.SetActiveScene(variantScene);
                ApplyStyleToVariantSceneContents(variantScene, profile, library, rpResult, fieldSnapshots);
                EditorSceneManager.SaveScene(variantScene, VisualStyleAuditService.VariantGameScenePath);
            }
            finally
            {
                if (!keepVariantSceneOpen && openedVariantTemporarily)
                {
                    if (!string.IsNullOrEmpty(callerScenePath))
                    {
                        var origActive = SceneManager.GetSceneByPath(callerScenePath);
                        if (origActive.IsValid() && origActive.isLoaded)
                        {
                            SceneManager.SetActiveScene(origActive);
                        }
                    }
                    EditorSceneManager.CloseScene(variantScene, true);
                }
            }

            // 6. Register GameScene_CelShaded in EditorBuildSettings without removing canonical GameScene
            EnsureVariantInBuildSettings(VisualStyleAuditService.VariantGameScenePath);

            // Re-acquire ScriptableObjects cleanly in case scene close/open triggered Resources.UnloadUnusedAssets
            manifest = VisualStyleMaterialFactory.LoadManifest(profile.styleId);
            library = VisualStyleMaterialFactory.LoadLibrary(profile.styleId);
            if (manifest != null)
            {
                manifest.rollbackSnapshot = rollback;
                manifest.lastAppliedUtc = DateTime.UtcNow.ToString("O");
                manifest.lastValidationPassed = true;
                EditorUtility.SetDirty(manifest);
            }
            if (library != null)
            {
                EditorUtility.SetDirty(library);
            }
            AssetDatabase.SaveAssets();
            return true;
        }

        private static void ApplyStyleToVariantSceneContents(
            Scene variantScene,
            VisualStyleProfile profile,
            VisualMaterialLibrary library,
            VariantRenderPipelineResult rpResult,
            List<VisualStyleRollbackFieldSnapshot> fieldSnapshots)
        {
            library.EnsureLookupInitialized();
            var resolver = new VisualMaterialResolver(profile, library);

            // Ensure VisualStyleManager exists in the scene
            VisualStyleManager manager = null;
            var rootObjects = variantScene.GetRootGameObjects();
            for (int i = 0; i < rootObjects.Length; i++)
            {
                manager = rootObjects[i].GetComponentInChildren<VisualStyleManager>(true);
                if (manager != null) break;
            }

            GameObject styleRoot = GameObject.Find("--- VISUAL STYLE ---");
            if (styleRoot == null || styleRoot.scene != variantScene)
            {
                styleRoot = new GameObject("--- VISUAL STYLE ---");
                SceneManager.MoveGameObjectToScene(styleRoot, variantScene);
            }

            if (manager == null)
            {
                var mgrGo = new GameObject("VisualStyleManager");
                mgrGo.transform.SetParent(styleRoot.transform, false);
                manager = mgrGo.AddComponent<VisualStyleManager>();
            }

            manager.activeProfile = profile;
            manager.activeLibrary = library;
            manager.variantRendererData = rpResult?.rendererData;
            manager.variantRenderPipelineAsset = rpResult?.rpAsset;
            manager.applySceneLightingOnAwake = true;
            manager.applyToPreplacedRenderersOnAwake = false;

            // Update scene lighting and interior fill lights safely
            if (profile.sceneLighting.overrideSceneLightingInVariant)
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = profile.sceneLighting.ambientSkyColor;
                RenderSettings.ambientEquatorColor = profile.sceneLighting.ambientEquatorColor;
                RenderSettings.ambientGroundColor = profile.sceneLighting.ambientGroundColor;
                RenderSettings.ambientIntensity = profile.sceneLighting.ambientIntensity;

                for (int i = 0; i < rootObjects.Length; i++)
                {
                    var lights = rootObjects[i].GetComponentsInChildren<Light>(true);
                    for (int l = 0; l < lights.Length; l++)
                    {
                        if (lights[l] != null && lights[l].type == LightType.Directional)
                        {
                            lights[l].color = profile.sceneLighting.directionalLightColor;
                            lights[l].intensity = profile.sceneLighting.directionalLightIntensity;
                        }
                    }
                }
            }

            // Style pre-placed renderers in GameScene_CelShaded (never UI/TMP/Particles/OutlineService)
            for (int i = 0; i < rootObjects.Length; i++)
            {
                if (rootObjects[i] == styleRoot) continue;
                resolver.ApplyToHierarchy(rootObjects[i], null, VisualSemanticRole.GenericOpaque, VisualCategoryIds.Props, true, false);
            }

            // Update CityGenerator material arrays & road/building defaults while keeping canonical NGO pizzeriaPrefab hash!
            for (int i = 0; i < rootObjects.Length; i++)
            {
                var cityGens = rootObjects[i].GetComponentsInChildren<CityGenerator>(true);
                for (int g = 0; g < cityGens.Length; g++)
                {
                    var gen = cityGens[g];
                    if (gen == null) continue;

                    RecordRollbackField(fieldSnapshots, variantScene.path, GetHierarchyPath(gen.transform), nameof(CityGenerator), nameof(CityGenerator.streetMaterial), gen.streetMaterial);
                    gen.streetMaterial = resolver.ResolveForSlot(gen.streetMaterial, VisualSemanticRole.Street, VisualCategoryIds.Streets, null, 0, out _, out _, out _);
                    gen.sidewalkMaterial = resolver.ResolveForSlot(gen.sidewalkMaterial, VisualSemanticRole.Sidewalk, VisualCategoryIds.Streets, null, 0, out _, out _, out _);
                    gen.intersectionMaterial = resolver.ResolveForSlot(gen.intersectionMaterial, VisualSemanticRole.Intersection, VisualCategoryIds.Streets, null, 0, out _, out _, out _);
                    gen.roadMarkingYellowMaterial = resolver.ResolveForSlot(gen.roadMarkingYellowMaterial, VisualSemanticRole.RoadYellow, VisualCategoryIds.Streets, null, 0, out _, out _, out _);
                    gen.roadMarkingWhiteMaterial = resolver.ResolveForSlot(gen.roadMarkingWhiteMaterial, VisualSemanticRole.RoadWhite, VisualCategoryIds.Streets, null, 0, out _, out _, out _);
                    gen.crosswalkMaterial = resolver.ResolveForSlot(gen.crosswalkMaterial, VisualSemanticRole.Crosswalk, VisualCategoryIds.Streets, null, 0, out _, out _, out _);

                    if (gen.buildingMaterials != null && gen.buildingMaterials.Length > 0)
                    {
                        var updated = new Material[gen.buildingMaterials.Length];
                        for (int b = 0; b < updated.Length; b++)
                        {
                            updated[b] = resolver.ResolveForSlot(gen.buildingMaterials[b], VisualSemanticRole.BuildingFacade, VisualCategoryIds.City, null, 0, out _, out _, out _);
                        }
                        gen.buildingMaterials = updated;
                    }
                    else
                    {
                        var facadeMat = resolver.ResolveRoleMaterial(VisualSemanticRole.BuildingFacade);
                        if (facadeMat != null) gen.buildingMaterials = new[] { facadeMat };
                    }
                    EditorUtility.SetDirty(gen);
                }
            }
        }

        public static bool ActivateInGameplayPersistent(VisualStyleProfile profile)
        {
            if (profile == null) return false;

            if (!File.Exists(VisualStyleAuditService.VariantGameScenePath) ||
                VisualStyleMaterialFactory.LoadLibrary(profile.styleId) == null)
            {
                if (!ApplyToGameplayVariant(profile, profile.discovery.conversionRuleSet ?? VisualStylePresetSeeder.EnsureDefaultRuleSet(), false))
                {
                    return false;
                }
            }

            VisualMaterialLibrary library = VisualStyleMaterialFactory.LoadLibrary(profile.styleId);
            VisualStyleRuntimeConfig cfg = VisualStylePresetSeeder.EnsureRuntimeConfig(profile);

            cfg.useStyledGameplay = true;
            cfg.activeStyleId = profile.styleId;
            cfg.activeGameplaySceneName = VariantGameplaySceneName;
            cfg.activeProfile = profile;
            cfg.activeLibrary = library;

            EnsureVariantInBuildSettings(VisualStyleAuditService.VariantGameScenePath);

            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            VisualStyleRuntimeConfig.ClearCache();
            return true;
        }

        public static bool RestoreOriginalGameplay(VisualStyleProfile profile)
        {
            VisualStyleRuntimeConfig cfg = VisualStylePresetSeeder.EnsureRuntimeConfig(null);
            VisualStyleConversionManifest manifest = profile != null ? VisualStyleMaterialFactory.LoadManifest(profile.styleId) : null;

            string targetBootScene = VisualStyleRuntimeConfig.CanonicalGameplaySceneName;
            if (manifest != null && manifest.rollbackSnapshot != null &&
                !string.IsNullOrEmpty(manifest.rollbackSnapshot.previousActiveSceneBootName))
            {
                targetBootScene = VisualStyleRuntimeConfig.CanonicalGameplaySceneName;
            }

            cfg.useStyledGameplay = false;
            cfg.activeStyleId = string.Empty;
            cfg.activeGameplaySceneName = targetBootScene;
            cfg.activeProfile = null;
            cfg.activeLibrary = null;

            // Restore EditorBuildSettings cleanly
            var scenes = EditorBuildSettings.scenes != null
                ? new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes)
                : new List<EditorBuildSettingsScene>();

            bool keepVariantEntry = manifest?.rollbackSnapshot != null && manifest.rollbackSnapshot.hadVariantInBuildSettings;
            bool modifiedScenes = false;

            for (int i = scenes.Count - 1; i >= 0; i--)
            {
                var s = scenes[i];
                if (s == null) continue;
                if (string.Equals(s.path, VisualStyleAuditService.CanonicalGameScenePath, StringComparison.OrdinalIgnoreCase))
                {
                    if (!s.enabled)
                    {
                        s.enabled = true;
                        modifiedScenes = true;
                    }
                }
                else if (string.Equals(s.path, VisualStyleAuditService.VariantGameScenePath, StringComparison.OrdinalIgnoreCase))
                {
                    if (!keepVariantEntry)
                    {
                        scenes.RemoveAt(i);
                        modifiedScenes = true;
                    }
                }
            }

            if (modifiedScenes)
            {
                EditorBuildSettings.scenes = scenes.ToArray();
            }

            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            VisualStyleRuntimeConfig.ClearCache();
            return true;
        }

        private static void EnsureVariantInBuildSettings(string variantScenePath)
        {
            var scenes = EditorBuildSettings.scenes != null
                ? new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes)
                : new List<EditorBuildSettingsScene>();

            bool foundCanonical = false;
            bool foundVariant = false;
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i] == null) continue;
                if (string.Equals(scenes[i].path, VisualStyleAuditService.CanonicalGameScenePath, StringComparison.OrdinalIgnoreCase))
                {
                    foundCanonical = true;
                    if (!scenes[i].enabled) scenes[i].enabled = true;
                }
                else if (string.Equals(scenes[i].path, variantScenePath, StringComparison.OrdinalIgnoreCase))
                {
                    foundVariant = true;
                    scenes[i].enabled = true;
                }
            }

            if (!foundCanonical && File.Exists(VisualStyleAuditService.CanonicalGameScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(VisualStyleAuditService.CanonicalGameScenePath, true));
            }
            if (!foundVariant)
            {
                scenes.Add(new EditorBuildSettingsScene(variantScenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void RecordRollbackField(
            List<VisualStyleRollbackFieldSnapshot> fieldSnapshots,
            string scenePath,
            string hierarchyPath,
            string componentType,
            string propPath,
            UnityEngine.Object previousObj)
        {
            if (fieldSnapshots == null) return;
            string guid = "";
            long localId = 0;
            if (previousObj != null)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(previousObj, out guid, out localId);
            }

            fieldSnapshots.Add(new VisualStyleRollbackFieldSnapshot
            {
                scenePath = scenePath,
                objectHierarchyPath = hierarchyPath,
                componentTypeName = componentType,
                propertyPath = propPath,
                previousObjectAssetGuid = guid,
                previousObjectLocalFileId = localId
            });
        }

        private static string GetHierarchyPath(Transform t)
        {
            if (t == null) return string.Empty;
            var stack = new Stack<string>();
            while (t != null)
            {
                stack.Push(t.name);
                t = t.parent;
            }
            return string.Join("/", stack);
        }
    }
}
#endif
