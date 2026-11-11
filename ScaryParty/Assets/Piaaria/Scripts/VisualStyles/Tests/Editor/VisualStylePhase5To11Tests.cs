#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ScaryParty.City.Modular;
using ScaryParty.Interaction;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.Pizzeria.Visual;
using ScaryParty.Player;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ScaryParty.VisualStyles.Editor.Tests
{
    /// <summary>
    /// End-to-end EditMode verification covering Phases 5-11:
    /// URP outline pipeline cloning, non-destructive prefab/catalog variants,
    /// showcase scene, GameScene_CelShaded + exact rollback, Section 7A.7 (all 8 items),
    /// procedural City/Modular/Food/Character/NPC runtime integrations, and Before/After exports.
    /// </summary>
    public class VisualStylePhase5To11Tests
    {
        private const string TempFixtureFolder = "Assets/Piaaria/_TempFutureAssetTests";
        private VisualStyleProfile _softToon;
        private VisualConversionRuleSet _ruleSet;

        [SetUp]
        public void SetUp()
        {
            VisualStylePresetSeeder.EnsureAllDefaultsExist();
            _ruleSet = VisualStylePresetSeeder.EnsureDefaultRuleSet();
            _softToon = VisualStylePresetSeeder.LoadPreset(VisualStylePresetType.SoftToonVibrant);
            CleanupTempFixtures();
        }

        [TearDown]
        public void TearDown()
        {
            CleanupTempFixtures();
        }

        private static void CleanupTempFixtures()
        {
            if (AssetDatabase.IsValidFolder(TempFixtureFolder))
            {
                AssetDatabase.DeleteAsset(TempFixtureFolder);
                AssetDatabase.Refresh();
            }
        }

        private static string ComputeFileSha256(string path)
        {
            using var sha = SHA256.Create();
            byte[] bytes = File.ReadAllBytes(path);
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
        }

        [Test]
        public void UrpOutlineConfigurator_CreatesDuplicatedPipelineAndFeature_WithoutMutatingCanonical()
        {
            string canonicalRpHashBefore = ComputeFileSha256(VisualStyleUrpOutlineConfigurator.CanonicalRpAssetPath);
            string canonicalRendererHashBefore = ComputeFileSha256(VisualStyleUrpOutlineConfigurator.CanonicalRendererDataPath);
            RenderPipelineAsset activeBefore = GraphicsSettings.defaultRenderPipeline;

            var library = VisualStyleMaterialFactory.LoadLibrary(_softToon.styleId);
            var res = VisualStyleUrpOutlineConfigurator.CreateOrUpdateVariantRenderPipeline(_softToon, library);

            Assert.IsNotNull(res.rendererData);
            Assert.IsNotNull(res.rpAsset);
            Assert.IsNotNull(res.screenSpaceOutlineMaterial);
            Assert.IsNotNull(res.outlineFeature);
            Assert.IsTrue(res.outlineFeature.isActive);
            Assert.AreEqual(FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing, res.outlineFeature.injectionPoint);
            Assert.AreEqual("Shader Graphs/Outline Effect", res.screenSpaceOutlineMaterial.shader.name);
            Assert.Greater(res.screenSpaceOutlineMaterial.GetFloat("_Width"), 0f);

            // Running twice stays idempotent (same asset paths, single feature)
            int outlineFeatureCount = res.rendererData.rendererFeatures.Count(f => f is FullScreenPassRendererFeature);
            Assert.AreEqual(1, outlineFeatureCount);
            var resSecond = VisualStyleUrpOutlineConfigurator.CreateOrUpdateVariantRenderPipeline(_softToon, library);
            Assert.AreEqual(1, resSecond.rendererData.rendererFeatures.Count(f => f is FullScreenPassRendererFeature));
            Assert.AreEqual(res.rendererDataPath, resSecond.rendererDataPath);

            // Canonical URP pipeline untouched
            Assert.AreEqual(activeBefore, GraphicsSettings.defaultRenderPipeline);
            Assert.AreEqual(canonicalRpHashBefore, ComputeFileSha256(VisualStyleUrpOutlineConfigurator.CanonicalRpAssetPath));
            Assert.AreEqual(canonicalRendererHashBefore, ComputeFileSha256(VisualStyleUrpOutlineConfigurator.CanonicalRendererDataPath));
        }

        [Test]
        public void PrefabVariantBuilder_CreatesVariantsAndItemCatalog_WithoutMutatingCanonical()
        {
            string netPrefabsBefore = ComputeFileSha256(VisualStyleAuditService.DefaultNetworkPrefabsPath);
            string pizzeriaManualBefore = ComputeFileSha256(VisualStyleAuditService.PizzeriaManualPrefabPath);
            string charVisualBefore = ComputeFileSha256(VisualStyleAuditService.CharacterVisualPrefabPath);

            var scan = VisualStyleAssetDiscoveryService.Scan(_softToon, _ruleSet, null, VisualDiscoveryScope.GameplayReachable);
            var gen = VisualStyleMaterialFactory.GenerateOrUpdateLibrary(_softToon, _ruleSet, scan);
            Assert.AreEqual(0, gen.errorCount);

            var library = VisualStyleMaterialFactory.LoadLibrary(_softToon.styleId);
            var manifest = VisualStyleMaterialFactory.LoadManifest(_softToon.styleId);
            var variants = VisualStylePrefabVariantBuilder.BuildVariants(_softToon, library, manifest);

            Assert.IsNotNull(variants.pizzeriaVariantPrefab);
            Assert.IsNotNull(variants.characterVariantPrefab);
            Assert.IsNotNull(variants.itemCatalogVariant);
            Assert.IsNotNull(variants.pizzeriaVariantPrefab.GetComponent<VisualStyleBinder>());
            Assert.IsNotNull(variants.characterVariantPrefab.GetComponent<VisualStyleBinder>());

            Assert.AreEqual(netPrefabsBefore, ComputeFileSha256(VisualStyleAuditService.DefaultNetworkPrefabsPath));
            Assert.AreEqual(pizzeriaManualBefore, ComputeFileSha256(VisualStyleAuditService.PizzeriaManualPrefabPath));
            Assert.AreEqual(charVisualBefore, ComputeFileSha256(VisualStyleAuditService.CharacterVisualPrefabPath));
        }

        [Test]
        public void ShowcaseBuilder_GeneratesComparisonScene_WithOriginalAndCelShadedColumns()
        {
            string showcasePath = VisualStyleShowcaseBuilder.BuildOrUpdateShowcaseScene(_softToon, _ruleSet, restorePreviousActiveScene: true);
            Assert.IsTrue(File.Exists(showcasePath));

            Scene prev = SceneManager.GetActiveScene();
            Scene opened = EditorSceneManager.OpenScene(showcasePath, OpenSceneMode.Additive);
            try
            {
                var roots = opened.GetRootGameObjects();
                GameObject colOrig = roots.FirstOrDefault(r => r.name == "Column_Original");
                GameObject colToon = roots.FirstOrDefault(r => r.name == "Column_CelShaded");
                VisualStyleManager mgr = roots.Select(r => r.GetComponentInChildren<VisualStyleManager>(true)).FirstOrDefault(m => m != null);

                Assert.IsNotNull(colOrig);
                Assert.IsNotNull(colToon);
                Assert.IsNotNull(mgr);

                var id = colOrig.GetComponent<VisualStyleIdentifier>();
                Assert.IsNotNull(id);
                Assert.IsTrue(id.preserveHierarchyMaterials);

                var toonRenderers = colToon.GetComponentsInChildren<Renderer>(true);
                int illustrateCount = toonRenderers.Count(r =>
                    r != null && r.sharedMaterials.Any(m => m != null && VisualStyleShaderUtility.IsIllustrateShader(m.shader)));
                Assert.Greater(illustrateCount, 5, "Column_CelShaded must contain Illustrate-shaded renderers.");
            }
            finally
            {
                if (prev.IsValid() && prev.isLoaded) SceneManager.SetActiveScene(prev);
                EditorSceneManager.CloseScene(opened, true);
            }
        }

        [Test]
        public void GameplayIntegrator_ApplyActivateAndRollback_LeavesCanonicalGameSceneIntact()
        {
            string canonicalHashBefore = ComputeFileSha256(VisualStyleAuditService.CanonicalGameScenePath);

            try
            {
                bool applied = VisualStyleGameplayIntegrator.ApplyToGameplayVariant(_softToon, _ruleSet, keepVariantSceneOpen: false);
                Assert.IsTrue(applied);
                Assert.IsTrue(File.Exists(VisualStyleAuditService.VariantGameScenePath));

                bool activated = VisualStyleGameplayIntegrator.ActivateInGameplayPersistent(_softToon);
                Assert.IsTrue(activated);
                Assert.AreEqual("GameScene_CelShaded", VisualStyleRuntimeConfig.GetGameplaySceneName());
                Assert.IsTrue(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == VisualStyleAuditService.VariantGameScenePath));
                Assert.IsTrue(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == VisualStyleAuditService.CanonicalGameScenePath));

                bool restored = VisualStyleGameplayIntegrator.RestoreOriginalGameplay(_softToon);
                Assert.IsTrue(restored);
                Assert.AreEqual("GameScene", VisualStyleRuntimeConfig.GetGameplaySceneName());
            }
            finally
            {
                VisualStyleGameplayIntegrator.RestoreOriginalGameplay(_softToon);
            }

            Assert.AreEqual(canonicalHashBefore, ComputeFileSha256(VisualStyleAuditService.CanonicalGameScenePath),
                "Canonical GameScene.unity SHA-256 must never change.");
        }

        [Test]
        public void Section7A7_FutureGenericAsset_IncrementalSync_GuidStability_Modification_OrphanAndCustomProtection()
        {
            // Uses an isolated cloned profile so project generated assets aren't polluted
            var isolatedProfile = ScriptableObject.Instantiate(_softToon);
            isolatedProfile.styleId = "test_7a7_temp";
            isolatedProfile.displayName = "Test 7A7";
            string isolatedGenFolder = VisualStyleMaterialFactory.GetStyleGeneratedFolder(isolatedProfile.styleId);

            try
            {
                VisualStylePresetSeeder.EnsureFolder(TempFixtureFolder);
                Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

                var srcMat = new Material(urpLit) { name = "FutureGenericMat" };
                VisualStyleShaderUtility.SetColorSafe(srcMat, "_BaseColor", new Color(0.25f, 0.72f, 0.45f, 1f));
                string srcMatPath = $"{TempFixtureFolder}/FutureGenericMat.mat";
                AssetDatabase.CreateAsset(srcMat, srcMatPath);

                var tempGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tempGo.GetComponent<Renderer>().sharedMaterial = srcMat;
                string prefabPath = $"{TempFixtureFolder}/FutureGenericProp.prefab";
                GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(tempGo, prefabPath);
                UnityEngine.Object.DestroyImmediate(tempGo);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // 7A.7 #1: Future generic prefab discovered as Unprocessed
                var manifest0 = VisualStyleMaterialFactory.LoadManifest(isolatedProfile.styleId);
                var scan1 = VisualStyleAssetDiscoveryService.Scan(
                    isolatedProfile, _ruleSet, manifest0,
                    VisualDiscoveryScope.CurrentSelection,
                    currentSelection: new UnityEngine.Object[] { prefabAsset });

                var target1 = scan1.targets.FirstOrDefault(t => t.sourceMaterial == srcMat);
                Assert.IsNotNull(target1);
                Assert.AreEqual(VisualAssetSyncStatus.Unprocessed, target1.incrementalStatus);

                // 7A.7 #2: Generate once creates 1 material; running again creates/updates 0 and skips unchanged
                var gen1 = VisualStyleMaterialFactory.GenerateOrUpdateLibrary(isolatedProfile, _ruleSet, scan1, onlyDirtyOrNew: true);
                Assert.AreEqual(0, gen1.errorCount);
                var manifest1 = VisualStyleMaterialFactory.LoadManifest(isolatedProfile.styleId);
                var entry1 = manifest1.FindByKey(target1.conversionKey);
                Assert.IsNotNull(entry1);
                string firstGuid = entry1.targetMaterialGuid;
                Assert.IsFalse(string.IsNullOrEmpty(firstGuid));

                var scan2 = VisualStyleAssetDiscoveryService.Scan(
                    isolatedProfile, _ruleSet, manifest1,
                    VisualDiscoveryScope.CurrentSelection,
                    currentSelection: new UnityEngine.Object[] { prefabAsset });
                var target2 = scan2.targets.FirstOrDefault(t => t.sourceMaterial == srcMat);
                Assert.AreEqual(VisualAssetSyncStatus.UpToDate, target2.incrementalStatus);

                var gen2 = VisualStyleMaterialFactory.GenerateOrUpdateLibrary(isolatedProfile, _ruleSet, scan2, onlyDirtyOrNew: true);
                Assert.AreEqual(0, gen2.createdCount);
                Assert.AreEqual(0, gen2.updatedCount);
                Assert.AreEqual(firstGuid, VisualStyleMaterialFactory.LoadManifest(isolatedProfile.styleId).FindByKey(target1.conversionKey).targetMaterialGuid);

                // 7A.7 #3 & #4: Modify source material -> marked SourceModified, regenerates preserving exact GUID
                VisualStyleShaderUtility.SetColorSafe(srcMat, "_BaseColor", new Color(0.95f, 0.25f, 0.15f, 1f));
                EditorUtility.SetDirty(srcMat);
                AssetDatabase.SaveAssets();

                var scan3 = VisualStyleAssetDiscoveryService.Scan(
                    isolatedProfile, _ruleSet, VisualStyleMaterialFactory.LoadManifest(isolatedProfile.styleId),
                    VisualDiscoveryScope.CurrentSelection,
                    currentSelection: new UnityEngine.Object[] { prefabAsset });
                var target3 = scan3.targets.FirstOrDefault(t => t.sourceMaterial == srcMat);
                Assert.AreEqual(VisualAssetSyncStatus.SourceModified, target3.incrementalStatus);

                var gen3 = VisualStyleMaterialFactory.GenerateOrUpdateLibrary(isolatedProfile, _ruleSet, scan3, onlyDirtyOrNew: true);
                Assert.AreEqual(1, gen3.updatedCount);
                string guidAfterUpdate = VisualStyleMaterialFactory.LoadManifest(isolatedProfile.styleId).FindByKey(target1.conversionKey).targetMaterialGuid;
                Assert.AreEqual(firstGuid, guidAfterUpdate, "Regenerating modified asset must preserve exact target GUID.");

                // 7A.7 #6: Custom Promoted Material not overwritten by subsequent re-runs
                Assert.IsTrue(VisualStyleIncrementalRebuilder.PromoteToCustomMaterial(isolatedProfile.styleId, target1.conversionKey));
                VisualStyleShaderUtility.SetColorSafe(srcMat, "_BaseColor", Color.black);
                EditorUtility.SetDirty(srcMat);
                AssetDatabase.SaveAssets();

                var scan4 = VisualStyleAssetDiscoveryService.Scan(
                    isolatedProfile, _ruleSet, VisualStyleMaterialFactory.LoadManifest(isolatedProfile.styleId),
                    VisualDiscoveryScope.CurrentSelection,
                    currentSelection: new UnityEngine.Object[] { prefabAsset });
                var gen4 = VisualStyleMaterialFactory.GenerateOrUpdateLibrary(isolatedProfile, _ruleSet, scan4, onlyDirtyOrNew: true);
                Assert.AreEqual(0, gen4.updatedCount);
                var targetMatAsset = AssetDatabase.LoadAssetAtPath<Material>(entry1.targetMaterialAssetPath);
                Assert.IsNotNull(targetMatAsset);
                Assert.AreNotEqual(Color.black, VisualStyleShaderUtility.ExtractBaseColor(targetMatAsset, Color.magenta));

                // 7A.7 #5: Deleted/Orphaned source marks orphan entry without crashing or destroying promoted material
                AssetDatabase.DeleteAsset(prefabPath);
                AssetDatabase.DeleteAsset(srcMatPath);
                var scan5 = VisualStyleAssetDiscoveryService.Scan(
                    isolatedProfile, _ruleSet, VisualStyleMaterialFactory.LoadManifest(isolatedProfile.styleId),
                    VisualDiscoveryScope.FullProject);
                Assert.GreaterOrEqual(scan5.orphanedEntries.Count, 1);
                Assert.IsTrue(File.Exists(entry1.targetMaterialAssetPath), "Promoted custom material must remain on disk when source is deleted.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(isolatedProfile);
                if (AssetDatabase.IsValidFolder(isolatedGenFolder))
                {
                    AssetDatabase.DeleteAsset(isolatedGenFolder);
                }
            }
        }

        [Test]
        public void Section7A7_07_RuntimeHasZeroAssetDatabaseUsage()
        {
            string runtimeDir = Path.GetFullPath("Assets/Piaaria/Scripts/VisualStyles/Runtime");
            string[] csFiles = Directory.GetFiles(runtimeDir, "*.cs", SearchOption.AllDirectories);
            Assert.Greater(csFiles.Length, 5);

            foreach (string file in csFiles)
            {
                string code = File.ReadAllText(file);
                string stripped = Regex.Replace(code, @"//.*$|/\*[\s\S]*?\*/", string.Empty, RegexOptions.Multiline);
                Assert.IsFalse(Regex.IsMatch(stripped, @"\bAssetDatabase\b"), $"Runtime script {Path.GetFileName(file)} must not call AssetDatabase.");
                Assert.IsFalse(Regex.IsMatch(stripped, @"\busing\s+UnityEditor\b"), $"Runtime script {Path.GetFileName(file)} must not import UnityEditor.");
            }
        }

        [Test]
        public void Section7A7_08_RuntimeNew13thModularPieceStyledAutomaticallyWithoutCodeChanges()
        {
            var scan = VisualStyleAssetDiscoveryService.Scan(_softToon, _ruleSet, null, VisualDiscoveryScope.GameplayReachable);
            VisualStyleMaterialFactory.GenerateOrUpdateLibrary(_softToon, _ruleSet, scan);
            var library = VisualStyleMaterialFactory.LoadLibrary(_softToon.styleId);

            var mgrGo = new GameObject("TempStyleMgr");
            var mgr = mgrGo.AddComponent<VisualStyleManager>();
            mgr.activeProfile = _softToon;
            mgr.activeLibrary = library;
            mgr.RegisterAsActive();
            mgr.RebuildResolver();

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var unseenMat = new Material(urpLit) { name = "UnseenCustom13thFacadeMat" };
            var customPieceGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            customPieceGo.name = "CustomModule13";
            customPieceGo.GetComponent<Renderer>().sharedMaterial = unseenMat;

            try
            {
                // Simulates runtime assembly / resolver call on an unseen 13th modular piece
                VisualStyleManager.ApplyHierarchyOrFallback(customPieceGo, VisualSemanticRole.BuildingFacade, VisualCategoryIds.City);
                Material resolved = customPieceGo.GetComponent<Renderer>().sharedMaterial;
                Assert.IsTrue(VisualStyleShaderUtility.IsIllustrateShader(resolved.shader),
                    "13th modular piece must resolve dynamically to Illustrate without C# code changes.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(customPieceGo);
                UnityEngine.Object.DestroyImmediate(unseenMat);
                UnityEngine.Object.DestroyImmediate(mgrGo);
            }
        }

        [Test, Timeout(300000)]
        public void CityAndFoodIntegrations_ResolveStyledMaterialsAndPreserveDynamicPropertyBlocks()
        {
            var library = VisualStyleMaterialFactory.LoadLibrary(_softToon.styleId);
            if (library == null)
            {
                var scan = VisualStyleAssetDiscoveryService.Scan(_softToon, _ruleSet, null, VisualDiscoveryScope.GameplayReachable);
                VisualStyleMaterialFactory.GenerateOrUpdateLibrary(_softToon, _ruleSet, scan);
                library = VisualStyleMaterialFactory.LoadLibrary(_softToon.styleId);
            }

            var mgrGo = new GameObject("TempStyleMgr_CityFood");
            var mgr = mgrGo.AddComponent<VisualStyleManager>();
            mgr.activeProfile = _softToon;
            mgr.activeLibrary = library;
            mgr.RegisterAsActive();
            mgr.RebuildResolver();

            var root = new GameObject("TempIntegrations");
            try
            {
                // 1. Road Markings & Crosswalks
                var cfg = ScriptableObject.CreateInstance<CityConfig>();
                cfg.streetWidth = 8f;
                cfg.sidewalkWidth = 2f;
                var graph = new StreetGraph
                {
                    nodes = new List<StreetNode>
                    {
                        new StreetNode { id = 0, worldPosition = Vector3.zero, connectedEdges = new List<int> { 0 } },
                        new StreetNode { id = 1, worldPosition = new Vector3(0f, 0f, 30f), connectedEdges = new List<int> { 0 } }
                    },
                    edges = new List<StreetEdge> { new StreetEdge { id = 0, nodeA = 0, nodeB = 1 } }
                };

                var markings = RoadMarkingGenerator.GenerateMarkings(graph, cfg, root.transform);
                var cw = CrosswalkGenerator.GenerateCrosswalks(graph, cfg, root.transform, null, out var cwRoot);
                var markingRend = markings.GetComponentInChildren<Renderer>();
                Assert.IsNotNull(markingRend);
                Assert.IsTrue(VisualStyleShaderUtility.IsIllustrateShader(markingRend.sharedMaterial.shader));
                Assert.GreaterOrEqual(markingRend.sharedMaterial.renderQueue, 2010);

                var cwRend = cwRoot != null ? cwRoot.GetComponent<Renderer>() : null;
                if (cwRend != null)
                {
                    Assert.IsTrue(VisualStyleShaderUtility.IsIllustrateShader(cwRend.sharedMaterial.shader));
                }

                // 2. Modular Building Assembler
                var catalog = AssetDatabase.LoadAssetAtPath<CityModuleCatalog>(VisualStyleAuditService.CityModuleCatalogPath);
                if (catalog != null)
                {
                    var layout = ModularBuildingAssembler.CreateRecipeLayout(ModularBuildingRecipe.Shop, Vector3.zero, Quaternion.identity, 7, "b_test", 0);
                    var bldg = ModularBuildingAssembler.AssembleBuilding(layout, catalog, root.transform);
                    var rend = bldg.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => !r.gameObject.name.Contains("Glass"));
                    Assert.IsNotNull(rend);
                    Assert.IsTrue(VisualStyleShaderUtility.IsIllustrateShader(rend.sharedMaterial.shader));
                }

                // 3. Pizza Composer (baked + zero Material leak)
                var pizzaGo = new GameObject("TestPizza");
                pizzaGo.transform.SetParent(root.transform, false);
                var ingredients = new List<NetIngredientDto>
                {
                    new NetIngredientDto { DefinitionId = 3, Stage = (byte)ProcessingStage.Prepared, Count = 1 },
                    new NetIngredientDto { DefinitionId = 5, Stage = (byte)ProcessingStage.Prepared, Count = 1 },
                    new NetIngredientDto { DefinitionId = 7, Stage = (byte)ProcessingStage.Prepared, Count = 1 }
                };
                var comp = new PizzaVisualComposition(123UL, 2, CookingStage.Baked, 1f, 0f, ingredients, 0.36f);
                var pizzaLayout = PizzaLayoutGenerator.Generate(comp, PizzaVisualCatalog.Instance);
                var baseDough = PizzaVisualComposer.EnsureDoughBase(pizzaGo, 1f, 0f);
                PizzaVisualComposer.ApplyLayout(pizzaGo, pizzaLayout, default(NetItemDto));

                var doughRend = baseDough.GetComponent<Renderer>();
                Assert.IsTrue(VisualStyleShaderUtility.IsIllustrateShader(doughRend.sharedMaterial.shader));
                var block = new MaterialPropertyBlock();
                doughRend.GetPropertyBlock(block, 0);
                Assert.AreNotEqual(Color.clear, block.GetColor("_BaseColor"));

                UnityEngine.Object.DestroyImmediate(cfg);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(mgrGo);
            }
        }

        [Test]
        public void ReportsAndBeforeAfterScreenshots_AreGeneratedCleanly()
        {
            VisualStyleAuditService.RunFullAudit(_ruleSet, writeMarkdownReport: true);
            int exported = GlobalCelShadingWizard.CaptureAndExportABScreenshotsHeadless(_softToon, _ruleSet);
            Assert.GreaterOrEqual(exported, 8, "Should export Original + CelShaded PNG pairs for all 4 sample slots.");

            string safeId = VisualStyleMaterialFactory.SanitizeFileToken(_softToon.styleId);
            string shotFolder = $"{VisualStyleAuditService.ReportsFolder}/Screenshots/{safeId}";
            Assert.IsTrue(Directory.Exists(shotFolder));
            Assert.GreaterOrEqual(Directory.GetFiles(shotFolder, "*.png").Length, 8);
        }
    }
}
#endif
