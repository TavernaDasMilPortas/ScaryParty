#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using ScaryParty.City.Modular;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Visual;
using ScaryParty.Player;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ScaryParty.VisualStyles.Editor
{
    /// <summary>
    /// Builds Assets/Piaaria/Scenes/VisualTests/GlobalCelShading_Showcase.unity containing
    /// side-by-side Original vs Cel Shaded strips of Pizzaria, Pizza, Character, Modular Buildings, and Street strips.
    /// </summary>
    public static class VisualStyleShowcaseBuilder
    {
        public const string ShowcaseScenePath = "Assets/Piaaria/Scenes/VisualTests/GlobalCelShading_Showcase.unity";

        public static string BuildOrUpdateShowcaseScene(
            VisualStyleProfile profile,
            VisualConversionRuleSet ruleSet,
            bool restorePreviousActiveScene = true)
        {
            if (profile == null) profile = VisualStylePresetSeeder.LoadPreset(VisualStylePresetType.SoftToonVibrant);
            if (ruleSet == null) ruleSet = VisualStylePresetSeeder.EnsureDefaultRuleSet();

            // Ensure materials, URP outline data, and variants exist first
            VisualMaterialLibrary library = VisualStyleMaterialFactory.LoadLibrary(profile.styleId);
            VisualStyleConversionManifest manifest = VisualStyleMaterialFactory.LoadManifest(profile.styleId);
            if (library == null)
            {
                var scan = VisualStyleAssetDiscoveryService.Scan(profile, ruleSet, manifest, VisualDiscoveryScope.GameplayReachable);
                VisualStyleMaterialFactory.GenerateOrUpdateLibrary(profile, ruleSet, scan);
                library = VisualStyleMaterialFactory.LoadLibrary(profile.styleId);
                manifest = VisualStyleMaterialFactory.LoadManifest(profile.styleId);
            }

            var rpResult = VisualStyleUrpOutlineConfigurator.CreateOrUpdateVariantRenderPipeline(profile, library);
            VisualStylePrefabVariantBuilder.BuildVariants(profile, library, manifest);
            var resolver = new VisualMaterialResolver(profile, library);

            string previousScenePath = SceneManager.GetActiveScene().path;
            Scene prevLoadedScene = SceneManager.GetActiveScene();

            VisualStylePresetSeeder.EnsureFolder("Assets/Piaaria/Scenes/VisualTests");
            Scene showcaseScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            try
            {
                PopulateShowcaseScene(showcaseScene, profile, library, rpResult, resolver);
                EditorSceneManager.SaveScene(showcaseScene, ShowcaseScenePath);
            }
            finally
            {
                if (restorePreviousActiveScene && !string.IsNullOrEmpty(previousScenePath) && prevLoadedScene.IsValid() && prevLoadedScene.isLoaded)
                {
                    SceneManager.SetActiveScene(prevLoadedScene);
                    EditorSceneManager.CloseScene(showcaseScene, true);
                }
            }

            AssetDatabase.ImportAsset(ShowcaseScenePath);
            return ShowcaseScenePath;
        }

        private static void PopulateShowcaseScene(
            Scene scene,
            VisualStyleProfile profile,
            VisualMaterialLibrary library,
            VariantRenderPipelineResult rpResult,
            VisualMaterialResolver resolver)
        {
            SceneManager.SetActiveScene(scene);

            // 1. VisualStyleManager (keeps applyToPreplacedRenderersOnAwake=false so Original column stays Original!)
            var mgrGo = new GameObject("VisualStyleManager");
            var mgr = mgrGo.AddComponent<VisualStyleManager>();
            mgr.activeProfile = profile;
            mgr.activeLibrary = library;
            mgr.variantRendererData = rpResult?.rendererData;
            mgr.variantRenderPipelineAsset = rpResult?.rpAsset;
            mgr.applySceneLightingOnAwake = true;
            mgr.applyToPreplacedRenderersOnAwake = false;

            // 2. Lighting & Camera Setup
            var lightGo = new GameObject("Directional Light");
            var dirLight = lightGo.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.color = profile.sceneLighting.directionalLightColor;
            dirLight.intensity = profile.sceneLighting.directionalLightIntensity;
            dirLight.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderSettings.sun = dirLight;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = profile.sceneLighting.ambientSkyColor;
            RenderSettings.ambientEquatorColor = profile.sceneLighting.ambientEquatorColor;
            RenderSettings.ambientGroundColor = profile.sceneLighting.ambientGroundColor;
            RenderSettings.ambientIntensity = profile.sceneLighting.ambientIntensity;

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.21f, 0.29f, 1f);
            camGo.transform.position = new Vector3(0f, 11f, -24f);
            camGo.transform.LookAt(new Vector3(0f, 2f, 2f));
            camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();

            // Camera Bookmarks for inspection
            CreateBookmark(camGo.transform, "Bookmark_Overview", new Vector3(0f, 11f, -24f), new Vector3(0f, 2f, 2f));
            CreateBookmark(camGo.transform, "Bookmark_Characters_And_Pizza", new Vector3(0f, 2.6f, -5.5f), new Vector3(0f, 1.2f, 0f));
            CreateBookmark(camGo.transform, "Bookmark_ModularBuildings", new Vector3(0f, 7f, 10f), new Vector3(0f, 4f, 18f));
            CreateBookmark(camGo.transform, "Bookmark_PizzeriaInterior", new Vector3(0f, 5f, -11f), new Vector3(0f, 2.5f, -18f));

            // 3. Ground stage separating Original (left X = -9) vs Cel Shaded (right X = +9)
            BuildComparisonGround(resolver);

            CreateWorldHeader("ORIGINAL (URP Padrão)", new Vector3(-9f, 7.5f, -2f), Color.white);
            CreateWorldHeader($"CEL SHADED ({profile.displayName})", new Vector3(9f, 7.5f, -2f), new Color(1f, 0.88f, 0.45f, 1f));

            // 4. Populate Left (Original) and Right (Styled) Columns
            PopulateComparisonColumn(
                "Column_Original",
                xOffset: -9f,
                preserveOriginal: true,
                profile: profile,
                library: library,
                resolver: resolver);

            PopulateComparisonColumn(
                "Column_CelShaded",
                xOffset: 9f,
                preserveOriginal: false,
                profile: profile,
                library: library,
                resolver: resolver);
        }

        private static void PopulateComparisonColumn(
            string rootName,
            float xOffset,
            bool preserveOriginal,
            VisualStyleProfile profile,
            VisualMaterialLibrary library,
            VisualMaterialResolver resolver)
        {
            var colRoot = new GameObject(rootName);
            if (preserveOriginal)
            {
                var id = colRoot.AddComponent<VisualStyleIdentifier>();
                id.preserveHierarchyMaterials = true;
            }

            // A. Character sample (player teal/red tint preserved!)
            GameObject charPrefab = !preserveOriginal && library?.styledCharacterVariantPrefab != null
                ? library.styledCharacterVariantPrefab
                : AssetDatabase.LoadAssetAtPath<GameObject>(VisualStyleAuditService.CharacterVisualPrefabPath);
            if (charPrefab != null)
            {
                var charGo = PrefabUtility.InstantiatePrefab(charPrefab) as GameObject ?? UnityEngine.Object.Instantiate(charPrefab);
                charGo.transform.SetParent(colRoot.transform, false);
                charGo.transform.position = new Vector3(xOffset - 1.6f, 0f, 0f);
                charGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                if (!preserveOriginal)
                {
                    resolver.ApplyToHierarchy(charGo, "character_visual", VisualSemanticRole.PlayerBody, VisualCategoryIds.Characters, true, false);
                }
                else
                {
                    MarkHierarchyPreserved(charGo);
                }

                var app = charGo.GetComponent<CharacterAppearance>();
                if (app != null)
                {
                    app.ApplyPrimaryColor(profile.colorPalette.accentRed);
                }
            }

            // B. Rich Pizza composite sample on pedestal
            BuildShowcasePizza(colRoot.transform, new Vector3(xOffset + 1.6f, 0.9f, 0f), preserveOriginal, resolver);

            // C. Modular Building sample (facade + trim + roof + awning + glass)
            var catalog = AssetDatabase.LoadAssetAtPath<CityModuleCatalog>(VisualStyleAuditService.CityModuleCatalogPath);
            if (catalog != null)
            {
                var layout = ModularBuildingAssembler.CreateRecipeLayout(
                    ModularBuildingRecipe.Shop,
                    new Vector3(xOffset, 0f, 16f),
                    Quaternion.Euler(0f, 180f, 0f),
                    108,
                    $"Showcase_{(preserveOriginal ? "Orig" : "Toon")}",
                    0);
                GameObject buildingGo = ModularBuildingAssembler.AssembleBuilding(layout, catalog, colRoot.transform);
                if (buildingGo != null)
                {
                    if (!preserveOriginal)
                    {
                        resolver.ApplyToHierarchy(buildingGo, null, VisualSemanticRole.BuildingFacade, VisualCategoryIds.City, true, false);
                    }
                    else
                    {
                        MarkHierarchyPreserved(buildingGo);
                    }
                }
            }

            // D. Pizzeria Manual sample behind camera at z = -18 (facing showcase center)
            GameObject pizzPrefab = !preserveOriginal && library?.styledPizzeriaVariantPrefab != null
                ? library.styledPizzeriaVariantPrefab
                : AssetDatabase.LoadAssetAtPath<GameObject>(VisualStyleAuditService.PizzeriaManualPrefabPath);
            if (pizzPrefab != null)
            {
                var pizzGo = PrefabUtility.InstantiatePrefab(pizzPrefab) as GameObject ?? UnityEngine.Object.Instantiate(pizzPrefab);
                pizzGo.transform.SetParent(colRoot.transform, false);
                pizzGo.transform.position = new Vector3(xOffset, 1.6f, -18f);
                pizzGo.transform.rotation = Quaternion.identity;
                if (!preserveOriginal)
                {
                    resolver.ApplyToHierarchy(pizzGo, "pizzeria_modular", VisualSemanticRole.BuildingFacade, VisualCategoryIds.Architecture, true, false);
                }
                else
                {
                    MarkHierarchyPreserved(pizzGo);
                }
            }

            // E. Street Strip (Street + Sidewalk + Crosswalk)
            BuildStreetStrip(colRoot.transform, new Vector3(xOffset, 0.02f, 5.5f), preserveOriginal, resolver);
        }

        private static void BuildShowcasePizza(Transform parent, Vector3 pos, bool preserveOriginal, VisualMaterialResolver resolver)
        {
            var pizzaRoot = new GameObject("PizzaSample");
            pizzaRoot.transform.SetParent(parent, false);
            pizzaRoot.transform.position = pos;
            pizzaRoot.transform.localScale = Vector3.one * 2.5f;

            var ingredients = new List<NetIngredientDto>
            {
                new NetIngredientDto { DefinitionId = 3, Stage = (byte)ProcessingStage.Prepared, Count = 1 },
                new NetIngredientDto { DefinitionId = 5, Stage = (byte)ProcessingStage.Prepared, Count = 1 },
                new NetIngredientDto { DefinitionId = 7, Stage = (byte)ProcessingStage.Prepared, Count = 1 },
                new NetIngredientDto { DefinitionId = 9, Stage = (byte)ProcessingStage.Prepared, Count = 1 }
            };
            var comp = new PizzaVisualComposition(12345UL, 2, CookingStage.Baked, 0.85f, 0f, ingredients, 0.36f);
            var layout = PizzaLayoutGenerator.Generate(comp, PizzaVisualCatalog.Instance);
            PizzaVisualComposer.EnsureDoughBase(pizzaRoot, comp.CookProgress, comp.BurnProgress);
            PizzaVisualComposer.ApplyLayout(pizzaRoot, layout, default(NetItemDto));

            if (!preserveOriginal)
            {
                resolver.ApplyToHierarchy(pizzaRoot, "showcase_pizza", VisualSemanticRole.FoodGeneric, VisualCategoryIds.Food, true, false);
            }
            else
            {
                MarkHierarchyPreserved(pizzaRoot);
            }
        }

        private static void BuildStreetStrip(Transform parent, Vector3 center, bool preserveOriginal, VisualMaterialResolver resolver)
        {
            var strip = new GameObject("StreetStrip");
            strip.transform.SetParent(parent, false);
            strip.transform.position = center;

            CreateStageBox(strip.transform, "Asphalt", new Vector3(0f, 0f, 0f), new Vector3(7f, 0.08f, 3.2f),
                new Color(0.22f, 0.24f, 0.27f, 1f), preserveOriginal ? null : resolver.ResolveRoleMaterial(VisualSemanticRole.Street));
            CreateStageBox(strip.transform, "Sidewalk", new Vector3(0f, 0.06f, 2.1f), new Vector3(7f, 0.14f, 1.1f),
                new Color(0.68f, 0.67f, 0.63f, 1f), preserveOriginal ? null : resolver.ResolveRoleMaterial(VisualSemanticRole.Sidewalk));
            CreateStageBox(strip.transform, "CrosswalkStripe", new Vector3(0f, 0.05f, 0f), new Vector3(0.45f, 0.02f, 2.2f),
                new Color(0.95f, 0.95f, 0.95f, 1f), preserveOriginal ? null : resolver.ResolveRoleMaterial(VisualSemanticRole.Crosswalk));

            if (preserveOriginal) MarkHierarchyPreserved(strip);
        }

        private static void BuildComparisonGround(VisualMaterialResolver resolver)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "ShowcaseStageFloor";
            ground.transform.position = new Vector3(0f, -0.25f, 0f);
            ground.transform.localScale = new Vector3(42f, 0.5f, 48f);
            Material floorMat = resolver?.ResolveRoleMaterial(VisualSemanticRole.Sidewalk);
            if (floorMat != null) ground.GetComponent<Renderer>().sharedMaterial = floorMat;
        }

        private static void CreateStageBox(Transform parent, string name, Vector3 localPos, Vector3 scale, Color color, Material styledMat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (styledMat != null) r.sharedMaterial = styledMat;
            VisualStyleShaderUtility.ApplyColorBlockToSlot(r, 0, color);
        }

        private static void CreateWorldHeader(string text, Vector3 pos, Color color)
        {
            var go = new GameObject($"Header_{text}");
            go.transform.position = pos;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = 6f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(12f, 2f);
        }

        private static void CreateBookmark(Transform parent, string name, Vector3 pos, Vector3 lookAt)
        {
            var bm = new GameObject(name);
            bm.transform.SetParent(parent, false);
            bm.transform.position = pos;
            bm.transform.LookAt(lookAt);
        }

        private static void MarkHierarchyPreserved(GameObject root)
        {
            if (root == null) return;
            var id = root.GetComponent<VisualStyleIdentifier>() ?? root.AddComponent<VisualStyleIdentifier>();
            id.preserveHierarchyMaterials = true;
        }
    }
}
#endif
