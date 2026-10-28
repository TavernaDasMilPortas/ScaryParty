#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Editor.Authoring;
using ScaryParty.Pizzeria.Integration;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.Pizzeria.Stations;

namespace ScaryParty.Pizzeria.Editor.Tests
{
    public class PizzeriaModularBuilderTests
    {
        private const string SourceFbxPath = "Assets/Art/Pizzeria/Source/pizzariamodular (2)2 (1) (1) (2).fbx";
        private const string TempTestFolder = "Assets/Prefabs/Pizzeria/TempTests";
        private readonly List<string> _createdAssetPaths = new List<string>();
        private readonly List<GameObject> _createdSceneObjects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Pizzeria"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "Pizzeria");
            if (!AssetDatabase.IsValidFolder(TempTestFolder))
                AssetDatabase.CreateFolder("Assets/Prefabs/Pizzeria", "TempTests");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _createdSceneObjects)
            {
                if (go != null)
                    UnityEngine.Object.DestroyImmediate(go);
            }
            _createdSceneObjects.Clear();

            foreach (var path in _createdAssetPaths)
            {
                if (!string.IsNullOrEmpty(path) && AssetDatabase.LoadMainAssetAtPath(path) != null)
                {
                    AssetDatabase.DeleteAsset(path);
                }
            }
            _createdAssetPaths.Clear();

            if (AssetDatabase.IsValidFolder(TempTestFolder))
            {
                AssetDatabase.DeleteAsset(TempTestFolder);
            }
            AssetDatabase.Refresh();
        }

        private PizzeriaAuthoringSettings CreateTempSettings(string prefabName = "TestModularPizzeria")
        {
            var settings = ScriptableObject.CreateInstance<PizzeriaAuthoringSettings>();
            settings.InitializeDefaultFbxPreset();
            settings.sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(SourceFbxPath);
            settings.outputFolder = TempTestFolder;
            settings.outputPrefabName = prefabName;
            settings.organizeHierarchyOnCreate = true;
            settings.cloneConfigForNewPrefab = false;

            // Criar PizzeriaConfig temporário isolado para o teste
            var baseConfig = AssetDatabase.LoadAssetAtPath<PizzeriaConfig>("Assets/ScriptableObjects/Pizzeria/PizzeriaConfig.asset")
                          ?? AssetDatabase.LoadAssetAtPath<PizzeriaConfig>("Assets/ScriptableObjects/Pizzeria/Config/PizzeriaConfig.asset")
                          ?? AssetDatabase.LoadAssetAtPath<PizzeriaConfig>("Assets/Resources/Pizzeria/PizzeriaConfig.asset");
            PizzeriaConfig testConfig;
            if (baseConfig != null)
            {
                testConfig = UnityEngine.Object.Instantiate(baseConfig);
            }
            else
            {
                testConfig = ScriptableObject.CreateInstance<PizzeriaConfig>();
            }
            string configPath = $"{TempTestFolder}/{prefabName}_Config.asset";
            AssetDatabase.CreateAsset(testConfig, configPath);
            _createdAssetPaths.Add(configPath);
            settings.pizzeriaConfig = testConfig;

            settings.presentationCatalog = AssetDatabase.LoadAssetAtPath<ItemPresentationCatalog>(
                "Assets/ScriptableObjects/Pizzeria/Presentation/ItemPresentationCatalog.asset")
                ?? AssetDatabase.LoadAssetAtPath<ItemPresentationCatalog>(
                "Assets/ScriptableObjects/Pizzeria/ItemPresentationCatalog.asset");

            string settingsPath = $"{TempTestFolder}/{prefabName}_Settings.asset";
            AssetDatabase.CreateAsset(settings, settingsPath);
            _createdAssetPaths.Add(settingsPath);
            return settings;
        }

        private GameObject CreateSyntheticModelHierarchy(string rootName = "SyntheticModel")
        {
            var root = new GameObject(rootName);
            _createdSceneObjects.Add(root);

            AddMeshChild(root.transform, "paredecomum.001", new Vector3(0f, 1.5f, 5f), new Vector3(4f, 3f, 0.2f));
            AddMeshChild(root.transform, "piso.001", new Vector3(0f, 0f, 0f), new Vector3(10f, 0.1f, 10f));
            AddMeshChild(root.transform, "teto.001", new Vector3(0f, 3f, 0f), new Vector3(10f, 0.1f, 10f));
            AddMeshChild(root.transform, "pilastra.001", new Vector3(4f, 1.5f, 4f), new Vector3(0.4f, 3f, 0.4f));
            AddMeshChild(root.transform, "Portacomum.001", new Vector3(0f, 1.1f, -5f), new Vector3(0.9f, 2.1f, 0.1f));

            // Lixeiras vizinhas com tampas correspondentes
            AddMeshChild(root.transform, "Lixeira.001", new Vector3(-3f, 0.4f, 2f), new Vector3(0.5f, 0.8f, 0.5f));
            AddMeshChild(root.transform, "tampalixo.001", new Vector3(-3f, 0.82f, 2f), new Vector3(0.52f, 0.08f, 0.52f));
            AddMeshChild(root.transform, "Lixeira.002", new Vector3(3f, 0.4f, 2f), new Vector3(0.5f, 0.8f, 0.5f));
            AddMeshChild(root.transform, "tampalixo.002", new Vector3(3f, 0.82f, 2f), new Vector3(0.52f, 0.08f, 0.52f));

            // Forno + portaforno
            AddMeshChild(root.transform, "Forno.001", new Vector3(-2f, 1.0f, 4f), new Vector3(1.4f, 2.0f, 1.4f));
            AddMeshChild(root.transform, "portaforno.001", new Vector3(-2f, 1.1f, 3.3f), new Vector3(0.8f, 0.4f, 0.1f));

            // Geladeiras vizinhas + portas grande e pequena
            AddMeshChild(root.transform, "Geladeira.001", new Vector3(2f, 1.0f, 4f), new Vector3(0.9f, 2.0f, 0.9f));
            AddMeshChild(root.transform, "portamaiorgeldeira.001", new Vector3(2f, 1.3f, 3.55f), new Vector3(0.85f, 1.1f, 0.08f));
            AddMeshChild(root.transform, "portapequenageldeira.001", new Vector3(2f, 0.45f, 3.55f), new Vector3(0.85f, 0.6f, 0.08f));

            AddMeshChild(root.transform, "Geladeira.002", new Vector3(4f, 1.0f, 4f), new Vector3(0.9f, 2.0f, 0.9f));
            AddMeshChild(root.transform, "portamaiorgeldeira.002", new Vector3(4f, 1.3f, 3.55f), new Vector3(0.85f, 1.1f, 0.08f));
            AddMeshChild(root.transform, "portapequenageldeira.002", new Vector3(4f, 0.45f, 3.55f), new Vector3(0.85f, 0.6f, 0.08f));

            // Armário
            AddMeshChild(root.transform, "armario.001", new Vector3(-4f, 1.0f, 4f), new Vector3(1.2f, 2.0f, 0.7f));

            // Mobiliário (balcões e mesas)
            AddMeshChild(root.transform, "balcao.001", new Vector3(-2f, 0.45f, 0f), new Vector3(2.0f, 0.9f, 0.8f));
            AddMeshChild(root.transform, "balcao.002", new Vector3(0f, 0.45f, 0f), new Vector3(2.0f, 0.9f, 0.8f));
            AddMeshChild(root.transform, "balcao.003", new Vector3(2f, 0.45f, 0f), new Vector3(2.0f, 0.9f, 0.8f));
            AddMeshChild(root.transform, "balcao.004", new Vector3(4f, 0.45f, 0f), new Vector3(2.0f, 0.9f, 0.8f));
            AddMeshChild(root.transform, "Mesa_cozinha.001", new Vector3(-2f, 0.45f, -2f), new Vector3(1.8f, 0.9f, 0.9f));
            AddMeshChild(root.transform, "Mesa_cozinha.002", new Vector3(0f, 0.45f, -2f), new Vector3(1.8f, 0.9f, 0.9f));
            AddMeshChild(root.transform, "Mesa_cozinha.003", new Vector3(2f, 0.45f, -2f), new Vector3(1.8f, 0.9f, 0.9f));
            AddMeshChild(root.transform, "prateleira.001", new Vector3(-4f, 1.5f, 0f), new Vector3(1.2f, 0.3f, 0.4f));
            AddMeshChild(root.transform, "Caixa_registradora", new Vector3(-3f, 1.0f, -2f), new Vector3(0.5f, 0.4f, 0.4f));

            // Utensílios
            AddMeshChild(root.transform, "faca.001", new Vector3(-2f, 0.95f, 0f), new Vector3(0.25f, 0.05f, 0.08f));
            AddMeshChild(root.transform, "tabua.001", new Vector3(-1.5f, 0.92f, 0f), new Vector3(0.4f, 0.04f, 0.3f));
            AddMeshChild(root.transform, "suportefaca", new Vector3(-2f, 1.4f, 4.8f), new Vector3(0.6f, 0.1f, 0.1f));
            AddMeshChild(root.transform, "caixa.001", new Vector3(2f, 0.95f, 0f), new Vector3(0.45f, 0.08f, 0.45f));

            return root;
        }

        private static GameObject AddMeshChild(Transform parent, string name, Vector3 worldPos, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.position = worldPos;
            go.transform.localScale = size;
            return go;
        }

        // =====================================================================
        // TESTE 1: Criar um prefab novo a partir do FBX sem alterar o asset FBX
        // =====================================================================
        [Test]
        public void Test01_CreatePrefabFromFbx_DoesNotModifySourceFbxAndDoesNotInjectGameplayStations()
        {
            var fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SourceFbxPath);
            Assert.IsNotNull(fbxAsset, $"FBX de origem deve existir em {SourceFbxPath}");

            string fbxFullPath = Path.GetFullPath(SourceFbxPath);
            DateTime fbxWriteTimeBefore = File.GetLastWriteTimeUtc(fbxFullPath);
            int fbxChildCountBefore = fbxAsset.transform.childCount;

            var settings = CreateTempSettings("Test01_Prefab");
            string targetPrefabPath = settings.GetResolvedOutputPrefabPath();
            _createdAssetPaths.Add(targetPrefabPath);

            var prefabAsset = PizzeriaPrefabAuthoringService.CreateOrOpenEditablePrefab(
                settings, out var hierarchyReport, out bool createdNew, out string msg);

            Assert.IsNotNull(prefabAsset, msg);
            Assert.IsTrue(createdNew);
            Assert.IsNotNull(hierarchyReport);
            Assert.AreEqual(fbxWriteTimeBefore, File.GetLastWriteTimeUtc(fbxFullPath), "O arquivo FBX original não pode ser modificado.");
            Assert.AreEqual(fbxChildCountBefore, fbxAsset.transform.childCount, "A hierarquia do asset FBX original não pode ser alterada.");

            // Verificar que é um prefab regular editável (não ModelPrefab nem Variant presa ao FBX)
            var prefabType = PrefabUtility.GetPrefabAssetType(prefabAsset);
            Assert.AreEqual(PrefabAssetType.Regular, prefabType, "O prefab gerado deve ser Regular para permitir edição livre.");

            // Verificar que a Etapa 1 NÃO injeta estações de gameplay automaticamente
            var stationsInStep1 = prefabAsset.GetComponentsInChildren<StationView>(true);
            Assert.AreEqual(0, stationsInStep1.Length, "A Etapa 1 (Criar Prefab + Organizar Hierarquia) não deve atribuir funções de gameplay automaticamente.");
        }

        // =====================================================================
        // TESTE 2: Organização automática da hierarquia, conjuntos e bounds
        // =====================================================================
        [Test]
        public void Test02_HierarchyOrganizer_CreatesGroupsAssemblesEquipmentAndPreservesWorldTransforms()
        {
            var settings = CreateTempSettings("Test02_Hierarchy");
            var modelRoot = CreateSyntheticModelHierarchy("Test02_Model");

            // Capturar posições, rotações e bounds globais antes da organização
            var renderersBefore = modelRoot.GetComponentsInChildren<MeshRenderer>(true)
                .ToDictionary(r => r.gameObject.name, r => (pos: r.transform.position, rot: r.transform.rotation, bounds: r.bounds));

            var report = PizzeriaHierarchyOrganizer.OrganizeHierarchy(modelRoot, settings);

            Assert.Greater(report.totalAnalyzedNodes, 15);
            Assert.AreEqual(0, report.transformWarnings.Count, "Nenhuma peça pode sofrer deslocamento/deformação após reparentalização.");

            // Verificar grupos criados
            Assert.IsNotNull(modelRoot.transform.Find("Arquitetura/Paredes"), "Grupo Arquitetura/Paredes deve existir");
            Assert.IsNotNull(modelRoot.transform.Find("Arquitetura/Pisos"), "Grupo Arquitetura/Pisos deve existir");
            Assert.IsNotNull(modelRoot.transform.Find("Equipamentos"), "Grupo Equipamentos deve existir");
            Assert.IsNotNull(modelRoot.transform.Find("Mobiliario"), "Grupo Mobiliario deve existir");
            Assert.IsNotNull(modelRoot.transform.Find("Utensilios"), "Grupo Utensilios deve existir");

            // Verificar que Lixeira.001 recebeu tampalixo.001 e Lixeira.002 recebeu tampalixo.002 sem misturar vizinhas
            var lixeira1 = modelRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Lixeira.001");
            var tampa1 = modelRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "tampalixo.001");
            var lixeira2 = modelRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Lixeira.002");
            var tampa2 = modelRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "tampalixo.002");

            Assert.IsNotNull(lixeira1);
            Assert.IsNotNull(tampa1);
            Assert.AreEqual(lixeira1.parent, tampa1.parent, "Lixeira.001 e tampalixo.001 devem compartilhar a mesma unidade pai (Lixeira_01).");
            Assert.AreNotEqual(lixeira1.parent, tampa2.parent, "tampalixo.002 não pode ser misturada com Lixeira.001.");

            // Verificar Forno + portaforno
            var forno1 = modelRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Forno.001");
            var portaForno1 = modelRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "portaforno.001");
            Assert.AreEqual(forno1.parent, portaForno1.parent, "Forno.001 e portaforno.001 devem estar reunidos na mesma unidade Forno_01.");

            // Verificar Geladeiras vizinhas e suas portas
            var gel1 = modelRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Geladeira.001");
            var pMaior1 = modelRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "portamaiorgeldeira.001");
            var pPeq1 = modelRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "portapequenageldeira.001");
            var gel2 = modelRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Geladeira.002");
            var pMaior2 = modelRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "portamaiorgeldeira.002");

            Assert.AreEqual(gel1.parent, pMaior1.parent, "Geladeira.001 e portamaiorgeldeira.001 devem estar no mesmo grupo.");
            Assert.AreEqual(gel1.parent, pPeq1.parent, "Geladeira.001 e portapequenageldeira.001 devem estar no mesmo grupo.");
            Assert.AreNotEqual(gel1.parent, pMaior2.parent, "Portas de Geladeira.002 não podem ser misturadas com Geladeira.001.");

            // Verificar preservação estrita de transformações e bounds em espaço de mundo
            foreach (var kvp in renderersBefore)
            {
                var tr = modelRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == kvp.Key);
                Assert.IsNotNull(tr, $"Objeto {kvp.Key} deve continuar existindo");
                Assert.LessOrEqual(Vector3.Distance(kvp.Value.pos, tr.position), 0.001f, $"Posição de {kvp.Key} mudou!");
                Assert.LessOrEqual(Quaternion.Angle(kvp.Value.rot, tr.rotation), 0.1f, $"Rotação de {kvp.Key} mudou!");
            }
        }

        // =====================================================================
        // TESTE 3: Caso ambíguo enviado para Revisar e trava manual preservada
        // =====================================================================
        [Test]
        public void Test03_HierarchyOrganizer_SendsAmbiguousPartToRevisarAndPreservesManualLockOnReRun()
        {
            var settings = CreateTempSettings("Test03_Ambiguous");
            var root = new GameObject("Test03_Root");
            _createdSceneObjects.Add(root);

            // Duas lixeiras equidistantes de uma tampa solta -> ambíguo!
            var lixA = AddMeshChild(root.transform, "Lixeira.001", new Vector3(-0.35f, 0.4f, 0f), new Vector3(0.5f, 0.8f, 0.5f));
            var lixB = AddMeshChild(root.transform, "Lixeira.002", new Vector3(0.35f, 0.4f, 0f), new Vector3(0.5f, 0.8f, 0.5f));
            var tampaAmb = AddMeshChild(root.transform, "tampalixo.099", new Vector3(0.0f, 0.82f, 0f), new Vector3(0.5f, 0.08f, 0.5f));
            var unknownObj = AddMeshChild(root.transform, "ObjetoMisteriosoXYZ", new Vector3(2f, 0.5f, 0f), Vector3.one * 0.3f);

            var report1 = PizzeriaHierarchyOrganizer.OrganizeHierarchy(root, settings);

            var revisarGroup = root.transform.Find("Revisar");
            Assert.IsNotNull(revisarGroup, "Grupo Revisar deve ser criado quando há itens ambíguos ou desconhecidos.");
            Assert.IsTrue(tampaAmb.transform.IsChildOf(revisarGroup), "Tampa equidistante ambígua deve ir para Revisar.");
            Assert.IsTrue(unknownObj.transform.IsChildOf(revisarGroup), "Objeto sem regra correspondente deve ir para Revisar.");

            var tampaId = tampaAmb.GetComponent<PizzeriaNodeIdentity>();
            Assert.IsNotNull(tampaId);
            Assert.IsTrue(tampaId.pendingReview);
            Assert.GreaterOrEqual(tampaId.candidates.Count, 2, "Deve listar as duas lixeiras candidatas.");

            // Aplicar escolha manual do usuário e fixar associação
            PizzeriaHierarchyOrganizer.ApplyManualParentAndLock(tampaAmb.transform, lixA.transform.parent, PizzeriaHierarchyCategory.Equipment, out _);
            Assert.IsTrue(tampaId.manualAssociationLocked);
            Assert.IsFalse(tampaId.pendingReview);

            Transform lockedParent = tampaAmb.transform.parent;

            // Reexecutar organização automática: a escolha manual NÃO pode ser desfeita
            PizzeriaHierarchyOrganizer.OrganizeHierarchy(root, settings);
            Assert.AreEqual(lockedParent, tampaAmb.transform.parent, "Reorganizar a hierarquia deve respeitar manualAssociationLocked.");
        }

        // =====================================================================
        // TESTE 4: Reexecutar etapa 2 sem duplicar componentes nem âncoras
        // =====================================================================
        [Test]
        public void Test04_Step2BaseFunctioning_IsIdempotentAndDoesNotDuplicateComponentsOrAnchors()
        {
            var settings = CreateTempSettings("Test04_Step2");
            var root = CreateSyntheticModelHierarchy("Test04_PrefabRoot");

            bool ok1 = PizzeriaPrefabAuthoringService.InjectOrUpdateBaseFunctioning(root, settings, out string msg1);
            Assert.IsTrue(ok1, msg1);

            var pRoot = root.GetComponent<PizzeriaRoot>();
            Assert.IsNotNull(pRoot);
            Assert.IsNotNull(pRoot.entranceAnchor);
            Assert.IsNotNull(pRoot.receivingAnchor);
            Assert.IsNotNull(pRoot.recoveryAnchor);
            Assert.AreEqual(4, pRoot.playerSpawnAnchors.Length);

            // Modificar posição manual de uma âncora e reexecutar Etapa 2
            Vector3 customEntrancePos = new Vector3(1.23f, 0.5f, -4.56f);
            pRoot.entranceAnchor.localPosition = customEntrancePos;

            bool ok2 = PizzeriaPrefabAuthoringService.InjectOrUpdateBaseFunctioning(root, settings, out string msg2);
            Assert.IsTrue(ok2, msg2);

            Assert.AreEqual(1, root.GetComponents<PizzeriaRoot>().Length);
            Assert.AreEqual(1, root.GetComponents<PizzeriaFootprint>().Length);
            Assert.AreEqual(1, root.GetComponents<PizzeriaItemVisualizer>().Length);
            Assert.AreEqual(1, root.GetComponents<PizzeriaAuthoringBindings>().Length);
            Assert.AreEqual(customEntrancePos, pRoot.entranceAnchor.localPosition, "Âncoras já existentes não devem perder sua posição ajustada ao reexecutar a Etapa 2.");
        }

        // =====================================================================
        // TESTE 5: Converter seleção em Forno (OvenStation)
        // =====================================================================
        [Test]
        public void Test05_ConvertSelectionToOven_CreatesOvenStationSlotsEffectsAndRegistersInConfig()
        {
            var settings = CreateTempSettings("Test05_Oven");
            var root = CreateSyntheticModelHierarchy("Test05_Root");
            PizzeriaHierarchyOrganizer.OrganizeHierarchy(root, settings);
            PizzeriaPrefabAuthoringService.InjectOrUpdateBaseFunctioning(root, settings, out _);

            var fornoBody = root.GetComponentsInChildren<Transform>(true).First(t => t.name == "Forno.001").gameObject;
            var fornoDoor = root.GetComponentsInChildren<Transform>(true).First(t => t.name == "portaforno.001").gameObject;

            var opts = PizzeriaStationRecipeRegistry.CreateDefaultOptions(PizzeriaStationKind.Oven, settings.pizzeriaConfig);
            opts.slotCount = 2;

            var created = PizzeriaSelectionConverter.ConvertSelectionToStation(
                root, new List<GameObject> { fornoBody, fornoDoor }, opts, settings, allowReplaceDifferentKind: false, out string msg);

            Assert.AreEqual(1, created.Count, msg);
            var oven = created[0] as OvenStation;
            Assert.IsNotNull(oven, "Deve criar OvenStation");
            Assert.Greater(oven.stationId, 0);
            Assert.IsNotNull(oven.slotAnchors);
            Assert.AreEqual(2, oven.slotAnchors.Length);
            Assert.IsNotNull(oven.ovenFireLight);
            Assert.IsNotNull(oven.smokeParticles);
            Assert.IsNotNull(oven.audioSource);
            Assert.IsTrue(settings.pizzeriaConfig.stationInstances.Any(i => i.instanceId == oven.stationId && i.definition != null));
        }

        // =====================================================================
        // TESTE 6: Converter múltiplos objetos selecionados nos dois modos
        // =====================================================================
        [Test]
        public void Test06_MultiSelectionConversion_SupportsSingleEquipmentAndOnePerObjectModes()
        {
            var settings = CreateTempSettings("Test06_Multi");
            var root = CreateSyntheticModelHierarchy("Test06_Root");
            PizzeriaPrefabAuthoringService.InjectOrUpdateBaseFunctioning(root, settings, out _);

            var balcao1 = root.GetComponentsInChildren<Transform>(true).First(t => t.name == "balcao.001").gameObject;
            var balcao2 = root.GetComponentsInChildren<Transform>(true).First(t => t.name == "balcao.002").gameObject;
            var balcao3 = root.GetComponentsInChildren<Transform>(true).First(t => t.name == "balcao.003").gameObject;
            var balcao4 = root.GetComponentsInChildren<Transform>(true).First(t => t.name == "balcao.004").gameObject;

            var opts = PizzeriaStationRecipeRegistry.CreateDefaultOptions(PizzeriaStationKind.Counter, settings.pizzeriaConfig);

            // Modo 1: Um equipamento único a partir da seleção múltipla
            settings.multiSelectionMode = PizzeriaMultiSelectionMode.SingleEquipmentFromSelection;
            var singleResult = PizzeriaSelectionConverter.ConvertSelectionToStation(
                root, new List<GameObject> { balcao1, balcao2 }, opts, settings, allowReplaceDifferentKind: false, out _);
            Assert.AreEqual(1, singleResult.Count, "No modo SingleEquipmentFromSelection, 2 peças selecionadas devem gerar 1 única estação.");

            // Modo 2: Um equipamento independente por objeto selecionado
            settings.multiSelectionMode = PizzeriaMultiSelectionMode.OneEquipmentPerObject;
            var multiResult = PizzeriaSelectionConverter.ConvertSelectionToStation(
                root, new List<GameObject> { balcao3, balcao4 }, opts, settings, allowReplaceDifferentKind: false, out _);
            Assert.AreEqual(2, multiResult.Count, "No modo OneEquipmentPerObject, 2 peças selecionadas devem gerar 2 estações independentes com IDs distintos.");
            Assert.AreNotEqual(multiResult[0].stationId, multiResult[1].stationId);
        }

        // =====================================================================
        // TESTE 7: Trocar função de estação limpando a anterior sem resíduos
        // =====================================================================
        [Test]
        public void Test07_ReplaceStationFunction_RemovesPreviousStationComponentCleanly()
        {
            var settings = CreateTempSettings("Test07_Replace");
            var root = CreateSyntheticModelHierarchy("Test07_Root");
            PizzeriaPrefabAuthoringService.InjectOrUpdateBaseFunctioning(root, settings, out _);

            var target = root.GetComponentsInChildren<Transform>(true).First(t => t.name == "balcao.001").gameObject;

            var prepOpts = PizzeriaStationRecipeRegistry.CreateDefaultOptions(PizzeriaStationKind.Prep, settings.pizzeriaConfig);
            var firstList = PizzeriaSelectionConverter.ConvertSelectionToStation(
                root, new List<GameObject> { target }, prepOpts, settings, allowReplaceDifferentKind: false, out _);
            Assert.AreEqual(1, firstList.Count);
            Assert.IsNotNull(target.GetComponent<PrepStation>());
            int firstId = firstList[0].stationId;

            // Tentar converter para Packaging sem allowReplaceDifferentKind deve bloquear
            var packOpts = PizzeriaStationRecipeRegistry.CreateDefaultOptions(PizzeriaStationKind.Packaging, settings.pizzeriaConfig);
            var blockedList = PizzeriaSelectionConverter.ConvertSelectionToStation(
                root, new List<GameObject> { target }, packOpts, settings, allowReplaceDifferentKind: false, out _);
            Assert.AreEqual(0, blockedList.Count, "Sem confirmação de troca, não deve sobrescrever tipo diferente.");

            // Com allowReplaceDifferentKind = true, deve substituir PrepStation por PackagingStation mantendo o ID
            var replacedList = PizzeriaSelectionConverter.ConvertSelectionToStation(
                root, new List<GameObject> { target }, packOpts, settings, allowReplaceDifferentKind: true, out _);
            Assert.AreEqual(1, replacedList.Count);
            Assert.IsNull(target.GetComponent<PrepStation>(), "PrepStation antigo deve ter sido removido.");
            Assert.IsNotNull(target.GetComponent<PackagingStation>(), "PackagingStation novo deve estar presente.");
            Assert.AreEqual(firstId, replacedList[0].stationId, "O stationId deve ser preservado ao trocar a função do mesmo objeto.");
        }

        // =====================================================================
        // TESTE 8: Converter em Dispensa e Geladeira sem depender do nome do objeto
        // =====================================================================
        [Test]
        public void Test08_ConvertPantryAndFridge_ConfiguresStorageSlotsAndDistinguishesByBindingMetadata()
        {
            var settings = CreateTempSettings("Test08_Storage");
            var root = CreateSyntheticModelHierarchy("Test08_Root");
            PizzeriaPrefabAuthoringService.InjectOrUpdateBaseFunctioning(root, settings, out _);

            // Usar objetos com nomes neutros para provar que o validador/conversor não depende de "Geladeira" ou "Armario" no nome
            var objA = root.GetComponentsInChildren<Transform>(true).First(t => t.name == "balcao.001").gameObject;
            objA.name = "ObjetoQualquer_A";
            var objB = root.GetComponentsInChildren<Transform>(true).First(t => t.name == "balcao.002").gameObject;
            objB.name = "ObjetoQualquer_B";

            var pantryOpts = PizzeriaStationRecipeRegistry.CreateDefaultOptions(PizzeriaStationKind.Pantry, settings.pizzeriaConfig);
            var fridgeOpts = PizzeriaStationRecipeRegistry.CreateDefaultOptions(PizzeriaStationKind.Fridge, settings.pizzeriaConfig);

            var pantryRes = PizzeriaSelectionConverter.ConvertSelectionToStation(
                root, new List<GameObject> { objA }, pantryOpts, settings, false, out _);
            var fridgeRes = PizzeriaSelectionConverter.ConvertSelectionToStation(
                root, new List<GameObject> { objB }, fridgeOpts, settings, false, out _);

            Assert.AreEqual(1, pantryRes.Count);
            Assert.AreEqual(1, fridgeRes.Count);

            var bindings = root.GetComponent<PizzeriaAuthoringBindings>();
            Assert.AreEqual(PizzeriaStationKind.Pantry, PizzeriaGameplayValidator.ResolveStationKindForValidation(pantryRes[0], bindings, settings.pizzeriaConfig));
            Assert.AreEqual(PizzeriaStationKind.Fridge, PizzeriaGameplayValidator.ResolveStationKindForValidation(fridgeRes[0], bindings, settings.pizzeriaConfig));

            // Verificar slots de estoque no PizzeriaConfig
            Assert.IsTrue(settings.pizzeriaConfig.initialStorageSlots.Any(s => s.storageId == pantryRes[0].stationId && s.EffectiveIngredientId == 1));
            Assert.IsTrue(settings.pizzeriaConfig.initialStorageSlots.Any(s => s.storageId == fridgeRes[0].stationId && s.EffectiveIngredientId == 4));
        }

        // =====================================================================
        // TESTE 9: Converter nos demais tipos (Prep, Assembly, Packaging, Staging, ToolRack, Phone, TicketBoard, SupplierTerminal, Receiving, Trash, BoxStack)
        // =====================================================================
        [Test]
        public void Test09_ConvertAllRemainingStationKinds_ConfiguresRequiredComponentsAndReferences()
        {
            var settings = CreateTempSettings("Test09_AllKinds");
            var root = CreateSyntheticModelHierarchy("Test09_Root");
            PizzeriaPrefabAuthoringService.InjectOrUpdateBaseFunctioning(root, settings, out _);

            var kindsToTest = new[]
            {
                PizzeriaStationKind.Counter,
                PizzeriaStationKind.Prep,
                PizzeriaStationKind.Assembly,
                PizzeriaStationKind.Packaging,
                PizzeriaStationKind.Staging,
                PizzeriaStationKind.ToolRack,
                PizzeriaStationKind.Phone,
                PizzeriaStationKind.TicketBoard,
                PizzeriaStationKind.SupplierTerminal,
                PizzeriaStationKind.Receiving,
                PizzeriaStationKind.Trash,
                PizzeriaStationKind.BoxStack
            };

            for (int i = 0; i < kindsToTest.Length; i++)
            {
                var kind = kindsToTest[i];
                var piece = AddMeshChild(root.transform, $"Piece_{kind}", new Vector3(i * 1.5f, 0.5f, -3f), new Vector3(1f, 1f, 1f));
                var opts = PizzeriaStationRecipeRegistry.CreateDefaultOptions(kind, settings.pizzeriaConfig);
                var res = PizzeriaSelectionConverter.ConvertSelectionToStation(
                    root, new List<GameObject> { piece }, opts, settings, false, out string msg);

                Assert.AreEqual(1, res.Count, $"Falha ao converter {kind}: {msg}");
                var desc = PizzeriaStationRecipeRegistry.GetDescriptor(kind);
                Assert.IsTrue(desc.runtimeComponentType.IsInstanceOfType(res[0]), $"Tipo esperado {desc.runtimeComponentType.Name} para {kind}");
            }
        }

        // =====================================================================
        // TESTE 10, 11 e 12: Validador acusa faltantes, acusa erros específicos e aprova pizzaria completa
        // =====================================================================
        [Test]
        public void Test10_11_12_Validator_DetectsMissingTypesDetectsConfigErrorsAndApprovesValidPrefab()
        {
            var settings = CreateTempSettings("Test10_Validator");
            var root = CreateSyntheticModelHierarchy("Test10_Root");
            PizzeriaPrefabAuthoringService.InjectOrUpdateBaseFunctioning(root, settings, out _);

            // Marcar âncoras como verificadas
            var bindings = root.GetComponent<PizzeriaAuthoringBindings>();
            bindings.anchorsVerifiedByUser = true;
            bindings.entranceAnchorProvisional = false;
            bindings.receivingAnchorProvisional = false;
            bindings.recoveryAnchorProvisional = false;
            bindings.playerSpawnsProvisional = false;

            // 10. Sem estações, deve acusar todos os 15 tipos faltantes
            var emptyReport = PizzeriaGameplayValidator.ValidatePrefabAndCycle(root, settings, alsoValidateActiveSceneDependencies: false);
            Assert.IsFalse(emptyReport.isPrefabReadyForGameplay);
            Assert.AreEqual(15, emptyReport.coverageTable.Count);
            Assert.IsTrue(emptyReport.coverageTable.All(r => r.status == PizzeriaValidationSeverity.BlockingError));

            // Construir 1 estação válida de cada um dos 15 tipos
            int idx = 0;
            StationView createdPrep = null;
            foreach (var desc in PizzeriaStationRecipeRegistry.AllRecipes)
            {
                var piece = AddMeshChild(root.transform, $"ValidStation_{desc.kind}", new Vector3((idx % 5) * 2f - 4f, 0.5f, (idx / 5) * 2f - 2f), new Vector3(1f, 1f, 1f));
                var opts = PizzeriaStationRecipeRegistry.CreateDefaultOptions(desc.kind, settings.pizzeriaConfig);
                var created = PizzeriaSelectionConverter.ConvertSelectionToStation(
                    root, new List<GameObject> { piece }, opts, settings, false, out _);
                if (desc.kind == PizzeriaStationKind.Prep) createdPrep = created[0];
                idx++;
            }

            // 12. Agora o prefab completo deve ser aprovado sem erros bloqueantes de prefab ou ciclo
            var validReport = PizzeriaGameplayValidator.ValidatePrefabAndCycle(root, settings, alsoValidateActiveSceneDependencies: false);
            int prefabAndCycleBlocking = validReport.prefabIssues.Count(i => i.severity == PizzeriaValidationSeverity.BlockingError)
                                       + validReport.cycleIssues.Count(i => i.severity == PizzeriaValidationSeverity.BlockingError);
            Assert.AreEqual(0, prefabAndCycleBlocking,
                "Prefab completo não deve ter erros bloqueantes: " +
                string.Join(" | ", validReport.prefabIssues.Concat(validReport.cycleIssues)
                    .Where(i => i.severity == PizzeriaValidationSeverity.BlockingError)
                    .Select(i => $"{i.title}: {i.description}")));
            Assert.IsTrue(validReport.isPrefabReadyForGameplay);

            // 11. Introduzir um erro específico (afastar slotAnchor para 10m de distância -> fora do alcance)
            createdPrep.slotAnchors[0].position += Vector3.up * 10f;
            var errorReport = PizzeriaGameplayValidator.ValidatePrefabAndCycle(root, settings, alsoValidateActiveSceneDependencies: false);
            Assert.IsTrue(errorReport.prefabIssues.Any(i =>
                i.severity == PizzeriaValidationSeverity.BlockingError && i.title.Contains("fora do alcance")),
                "O validador deve acusar erro bloqueante quando um slotAnchor está fora do alcance de interação.");
        }

        // =====================================================================
        // TESTE 13 e 14: Validador ignora estações soltas na cena e é somente leitura
        // =====================================================================
        [Test]
        public void Test13_14_Validator_IgnoresLooseSceneStationsAndRunsReadOnly()
        {
            var settings = CreateTempSettings("Test13_ReadOnly");
            var root = CreateSyntheticModelHierarchy("Test13_Root");
            PizzeriaPrefabAuthoringService.InjectOrUpdateBaseFunctioning(root, settings, out _);

            // Criar um OvenStation solto na cena FORA do prefab-alvo
            var looseOvenGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            looseOvenGo.name = "LooseSceneOven";
            _createdSceneObjects.Add(looseOvenGo);
            var looseOven = looseOvenGo.AddComponent<OvenStation>();
            looseOven.stationId = 8;

            int childCountBefore = root.GetComponentsInChildren<Transform>(true).Length;
            int compCountBefore = root.GetComponentsInChildren<Component>(true).Length;

            var report = PizzeriaGameplayValidator.ValidatePrefabAndCycle(root, settings, alsoValidateActiveSceneDependencies: false);

            int childCountAfter = root.GetComponentsInChildren<Transform>(true).Length;
            int compCountAfter = root.GetComponentsInChildren<Component>(true).Length;

            // Teste 13: O OvenStation solto na cena não pode contar para o prefab-alvo
            var ovenRow = report.coverageTable.First(r => r.kind == PizzeriaStationKind.Oven);
            Assert.AreEqual(0, ovenRow.totalFound, "Estações soltas na cena fora do prefab-alvo devem ser ignoradas na contagem.");

            // Teste 14: Validador é somente leitura
            Assert.AreEqual(childCountBefore, childCountAfter, "O validador não pode criar nem destruir filhos no prefab.");
            Assert.AreEqual(compCountBefore, compCountAfter, "O validador não pode adicionar componentes ao prefab.");
        }

        // =====================================================================
        // TESTE 15 e 16: Multi-slot e resolução sem IDs fixos em runtime
        // =====================================================================
        [Test]
        public void Test15_16_MultiSlotAndNonCanonicalIds_ResolvedProperlyByBindingsAndStationView()
        {
            var settings = CreateTempSettings("Test16_RuntimeIds");
            var root = CreateSyntheticModelHierarchy("Test16_Root");
            PizzeriaPrefabAuthoringService.InjectOrUpdateBaseFunctioning(root, settings, out _);

            var bindings = root.GetComponent<PizzeriaAuthoringBindings>();
            Assert.IsNotNull(bindings);

            // Criar Geladeira com ID não-canônico (ex: 42 em vez de 1) e Dispensa com ID 43 (em vez de 2)
            var fridgePiece = root.GetComponentsInChildren<Transform>(true).First(t => t.name == "Geladeira.001").gameObject;
            var pantryPiece = root.GetComponentsInChildren<Transform>(true).First(t => t.name == "armario.001").gameObject;
            var counterPiece = root.GetComponentsInChildren<Transform>(true).First(t => t.name == "balcao.001").gameObject;

            var fridgeOpts = PizzeriaStationRecipeRegistry.CreateDefaultOptions(PizzeriaStationKind.Fridge, settings.pizzeriaConfig);
            var pantryOpts = PizzeriaStationRecipeRegistry.CreateDefaultOptions(PizzeriaStationKind.Pantry, settings.pizzeriaConfig);
            var counterOpts = PizzeriaStationRecipeRegistry.CreateDefaultOptions(PizzeriaStationKind.Counter, settings.pizzeriaConfig);
            counterOpts.slotCount = 2;

            var fridgeSt = PizzeriaSelectionConverter.ConvertSelectionToStation(root, new List<GameObject> { fridgePiece }, fridgeOpts, settings, false, out _)[0];
            var pantrySt = PizzeriaSelectionConverter.ConvertSelectionToStation(root, new List<GameObject> { pantryPiece }, pantryOpts, settings, false, out _)[0];
            var counterSt = PizzeriaSelectionConverter.ConvertSelectionToStation(root, new List<GameObject> { counterPiece }, counterOpts, settings, false, out _)[0];

            fridgeSt.stationId = 42;
            pantrySt.stationId = 43;

            Assert.AreEqual(42, bindings.ResolvePrimaryFridgeId(1), "Bindings deve resolver o ID real da geladeira primária (42) sem depender do ID fixo 1.");
            Assert.AreEqual(43, bindings.ResolvePrimaryPantryId(2), "Bindings deve resolver o ID real da dispensa primária (43) sem depender do ID fixo 2.");

            // Testar adição de posto de trabalho extra (+1 slot) e seleção de slot por ponto de mira em StationView
            var addedSlot = PizzeriaSelectionConverter.AddWorkpostToStation(counterSt, out _);
            Assert.IsNotNull(addedSlot);
            Assert.AreEqual(3, counterSt.slotAnchors.Length, "Bancada tinha 2 slots e deve passar a ter 3 após AddWorkpostToStation.");

            // Simular mira no slot 2
            Vector3 slot2Pos = counterSt.slotAnchors[2].position;
            counterSt.SetAimContext(slot2Pos, -1);
            Assert.AreEqual(2, counterSt.ResolveTargetSlotIndex(), "StationView.ResolveTargetSlotIndex deve resolver o slot mais próximo do ponto mirado.");
        }

        // =====================================================================
        // TESTE 17: Comando explícito "Usar esta pizzaria no jogo" e Asset de Exemplo
        // =====================================================================
        [Test]
        public void Test17_ApplyPrefabToGameIntegration_UpdatesCityGeneratorEvenWhenAlreadyAssignedAndEnsuresExampleAsset()
        {
            // Garantir criação do asset de configuração de exemplo apontando para o FBX modular
            var defaultSettings = PizzeriaAuthoringSettings.FindOrCreateDefault();
            Assert.IsNotNull(defaultSettings, "Asset de configuração de exemplo PizzeriaAuthoringSettings deve existir.");
            Assert.IsNotNull(defaultSettings.sourceModel, "O asset de exemplo deve referenciar o modelo FBX modular de origem.");

            // Testar que ApplyPrefabToGameIntegration atualiza um CityGenerator na cena mesmo quando pizzeriaPrefab já estava preenchido
            var settings = CreateTempSettings("Test17_Integration");
            string prefabPath = settings.GetResolvedOutputPrefabPath();
            _createdAssetPaths.Add(prefabPath);

            var newPrefab = PizzeriaPrefabAuthoringService.CreateOrOpenEditablePrefab(settings, out _, out _, out _);
            Assert.IsNotNull(newPrefab);

            var dummyOldPrefabGo = new GameObject("DummyOldPrefab");
            _createdSceneObjects.Add(dummyOldPrefabGo);

            var cityGenGo = new GameObject("TestCityGenerator");
            _createdSceneObjects.Add(cityGenGo);
            var cityGen = cityGenGo.AddComponent<CityGenerator>();
            cityGen.pizzeriaPrefab = dummyOldPrefabGo; // Já preenchido!

            // Restaurar DefaultNetworkPrefabs após o teste para não sujar a lista com asset temporário
            var netPrefabs = AssetDatabase.LoadAssetAtPath<Unity.Netcode.NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");

            var changes = PizzeriaGameplayValidator.ApplyPrefabToGameIntegration(newPrefab, updateGameSceneIfClosed: false);
            Assert.IsNotEmpty(changes);
            Assert.AreEqual(newPrefab, cityGen.pizzeriaPrefab, "Usar esta pizzaria no jogo deve substituir CityGenerator.pizzeriaPrefab mesmo quando não estava nulo.");

            if (netPrefabs != null)
            {
                var addedEntry = netPrefabs.PrefabList.FirstOrDefault(p => p != null && p.Prefab == newPrefab);
                if (addedEntry != null)
                {
                    netPrefabs.Remove(addedEntry);
                    EditorUtility.SetDirty(netPrefabs);
                    AssetDatabase.SaveAssets();
                }
            }
        }
    }
}
#endif
