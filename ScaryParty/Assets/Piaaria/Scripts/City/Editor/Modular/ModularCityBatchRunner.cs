using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ScaryParty.City.Modular;

namespace ScaryParty.City.Editor.Modular
{
    /// <summary>
    /// Utilitário de linha de comando / batchmode para construir o kit,
    /// gerar a cena de teste e executar validações automaticamente.
    /// </summary>
    public static class ModularCityBatchRunner
    {
        public const string TestScenePath = "Assets/Piaaria/Scenes/Tests/ModularCityTestScene.unity";
        public const string DefaultConfigPath = "Assets/Piaaria/ScriptableObjects/City/City config.asset";

        [MenuItem("Tools/Piaaria/Cidade Modular/Gerar Cena de Teste (12 Peças + 4 Prédios)", false, 20)]
        public static void GenerateTestSceneMenuItem()
        {
            BuildAndGenerateTestScene();
        }

        public static void RunBatchBuildAndTest()
        {
            try
            {
                Debug.Log("[ModularCityBatchRunner] === INICIANDO BUILD E VALIDAÇÃO DA CIDADE MODULAR ===");

                // 1. Constrói o kit completo de 12 peças (forçando atualização dos placeholders)
                CityModuleCatalog catalog = ModularCityKitBuilder.BuildCompleteKit(true);
                if (catalog == null)
                {
                    Debug.LogError("[ModularCityBatchRunner] FALHA: Não foi possível construir o catálogo.");
                    EditorApplication.Exit(1);
                    return;
                }

                // 2. Valida o kit
                var valResult = ModularCityValidator.ValidateKit(catalog);
                Debug.Log($"[ModularCityBatchRunner] {valResult.Summary}");
                if (!valResult.isValid)
                {
                    foreach (var err in valResult.errors) Debug.LogError($"[ModularCityBatchRunner] ERRO: {err}");
                    EditorApplication.Exit(1);
                    return;
                }

                // 3. Atualiza o CityConfig padrão
                CityConfig config = AssetDatabase.LoadAssetAtPath<CityConfig>(DefaultConfigPath);
                if (config != null)
                {
                    config.buildingGenerationMode = CityBuildingGenerationMode.ModularKit;
                    config.modularCatalog = catalog;
                    EditorUtility.SetDirty(config);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[ModularCityBatchRunner] CityConfig padrão ({DefaultConfigPath}) atualizado com o catálogo modular!");
                }

                // 4. Cria a cena de teste
                BuildAndGenerateTestScene();

                // 5. Executa a bateria de testes unitários
                RunAllTestsDirectInternal();

                Debug.Log("[ModularCityBatchRunner] === TUDO CONCLUÍDO COM SUCESSO! ===");
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorApplication.Exit(1);
            }
        }

        public static void RunAllTestsDirect()
        {
            try
            {
                RunAllTestsDirectInternal();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorApplication.Exit(1);
            }
        }

        private static void RunAllTestsDirectInternal()
        {
            Debug.Log("[ModularCityBatchRunner] === EXECUTANDO BATERIA DE TESTES DA CIDADE MODULAR ===");
            var test = new ScaryParty.City.Editor.Tests.ModularCityTests();
            test.Setup();

            test.Test01_AllTwelveModulesPresentInCatalog();
            Debug.Log("✅ Test01_AllTwelveModulesPresentInCatalog: PASSOU");

            test.Test02_CanonicalDimensionsVerification();
            Debug.Log("✅ Test02_CanonicalDimensionsVerification: PASSOU");

            test.Test03_DoorPassageHasNoBlockingCollider();
            Debug.Log("✅ Test03_DoorPassageHasNoBlockingCollider: PASSOU");

            test.Test04_FourTestRecipesAssembleCorrectly();
            Debug.Log("✅ Test04_FourTestRecipesAssembleCorrectly: PASSOU");

            test.Test05_LotPlannerEnforcesPolygonContainment();
            Debug.Log("✅ Test05_LotPlannerEnforcesPolygonContainment: PASSOU");

            test.Test06_DeterministicGenerationProducesIdenticalLayouts();
            Debug.Log("✅ Test06_DeterministicGenerationProducesIdenticalLayouts: PASSOU");

            test.Test07_CatalogSignatureIsStable();
            Debug.Log("✅ Test07_CatalogSignatureIsStable: PASSOU");

            test.Test08_ComplexPolygonsAndPizzeriaReservationHandling();
            Debug.Log("✅ Test08_ComplexPolygonsAndPizzeriaReservationHandling: PASSOU");

            test.Test09_FullModularProceduralAssemblyAndAnchors();
            Debug.Log("✅ Test09_FullModularProceduralAssemblyAndAnchors: PASSOU");

            test.Test10_KitBuilderIdempotencyAndNoAssetDuplication();
            Debug.Log("✅ Test10_KitBuilderIdempotencyAndNoAssetDuplication: PASSOU");

            Debug.Log("[ModularCityBatchRunner] === TODOS OS 10 TESTES PASSARAM COM SUCESSO! ===");
        }

        public static void BuildAndGenerateTestScene()
        {
            CityModuleCatalog catalog = AssetDatabase.LoadAssetAtPath<CityModuleCatalog>(ModularCityKitBuilder.CatalogPath);
            if (catalog == null)
            {
                catalog = ModularCityKitBuilder.BuildCompleteKit(false);
            }

            // Cria uma nova cena limpa
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Cria chão de referência
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground_Reference";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(15f, 1f, 15f);

            // 1. Fileira de exibição das 12 peças (A até L)
            GameObject modulesRow = new GameObject("Kit_12_Modules_Display");
            modulesRow.transform.position = new Vector3(-30f, 0f, 20f);

            float currentX = 0f;
            foreach (CityModuleId id in Enum.GetValues(typeof(CityModuleId)))
            {
                CityModuleDefinition def = catalog.GetDefinition(id);
                if (def != null && def.prefab != null)
                {
                    GameObject piece = (GameObject)PrefabUtility.InstantiatePrefab(def.prefab, modulesRow.transform);
                    piece.name = $"Piece_{id}_{def.displayName}";
                    piece.transform.localPosition = new Vector3(currentX, 0f, 0f);
                    piece.transform.localRotation = Quaternion.identity;

                    currentX += def.width + 1.5f; // Espaçamento entre peças
                }
            }

            // 2. Os 4 Edifícios de Teste (Seção 7 do plano)
            GameObject buildingsGroup = new GameObject("Four_Test_Recipes");
            buildingsGroup.transform.position = Vector3.zero;

            // Receita 1: Casa com Garagem (8m x 6m x 1 pav)
            BuildingLayout b1Layout = ModularBuildingAssembler.CreateRecipeLayout(
                ModularBuildingRecipe.HouseWithGarage, new Vector3(-25f, 0f, -10f), Quaternion.identity, 101, "Recipe_1_HouseWithGarage");
            ModularBuildingAssembler.AssembleBuilding(b1Layout, catalog, buildingsGroup.transform);

            // Receita 2: Sobrado (6m x 6m x 2 pav)
            BuildingLayout b2Layout = ModularBuildingAssembler.CreateRecipeLayout(
                ModularBuildingRecipe.TwoStoryHouse, new Vector3(-10f, 0f, -10f), Quaternion.identity, 102, "Recipe_2_TwoStoryHouse");
            ModularBuildingAssembler.AssembleBuilding(b2Layout, catalog, buildingsGroup.transform);

            // Receita 3: Loja (6m x 8m x 1 pav)
            BuildingLayout b3Layout = ModularBuildingAssembler.CreateRecipeLayout(
                ModularBuildingRecipe.Shop, new Vector3(5f, 0f, -10f), Quaternion.identity, 103, "Recipe_3_Shop");
            ModularBuildingAssembler.AssembleBuilding(b3Layout, catalog, buildingsGroup.transform);

            // Receita 4: Prédio Misto (8m x 8m x 4 pav)
            BuildingLayout b4Layout = ModularBuildingAssembler.CreateRecipeLayout(
                ModularBuildingRecipe.MixedBuilding, new Vector3(22f, 0f, -10f), Quaternion.identity, 104, "Recipe_4_MixedBuilding");
            ModularBuildingAssembler.AssembleBuilding(b4Layout, catalog, buildingsGroup.transform);

            // Posiciona a câmera para enquadrar a cena
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(0f, 18f, -32f);
                cam.transform.rotation = Quaternion.Euler(25f, 0f, 0f);
            }

            // Salva a cena
            string dir = Path.GetDirectoryName(TestScenePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            EditorSceneManager.SaveScene(scene, TestScenePath);
            Debug.Log($"[ModularCityBatchRunner] Cena de testes salva em: {TestScenePath}");
        }
    }
}
