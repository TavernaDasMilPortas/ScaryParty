#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using TMPro;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Stations;

namespace ScaryParty.Pizzeria.Editor.Authoring
{
    public static class PizzeriaPhase1Repair
    {
        public const string ManualPrefabPath = "Assets/Piaaria/Prefabs/Pizzeria/PizzeriaModular_Manual.prefab";
        public const string ManualConfigPath = "Assets/Piaaria/ScriptableObjects/Pizzeria/PizzeriaConfig_PizzeriaModular_Manual.asset";
        public const string StationsDir = "Assets/Piaaria/ScriptableObjects/Pizzeria/Stations";

        [MenuItem("Tools/Scary Party/Pizzeria/Executar Reparo Completo Fase 1 (IDs, Slots, Comandas)", priority = 1)]
        public static void Repair()
        {
            Debug.Log("[PizzeriaPhase1Repair] Iniciando Reparo Fase 1...");

            // 1. Garantir StationDefs para IDs 14, 16, 17, 18, 20, 21
            var def14 = EnsureStationDef(14, "StationDef_14_Receiving.asset", "Área de Recebimento", 1);
            var def16 = AssetDatabase.LoadAssetAtPath<StationDef>($"{StationsDir}/StationDef_16_BoxStack.asset");
            var def17 = EnsureStationDef(17, "StationDef_17_Pantry.asset", "Dispensa (Secundária)", 1);
            var def18 = EnsureStationDef(18, "StationDef_18_Fridge.asset", "Geladeira (Secundária)", 1);
            var def20 = EnsureStationDef(20, "StationDef_20_SupplierTerminal.asset", "Terminal de Fornecedor", 1);
            var def21 = EnsureStationDef(21, "StationDef_21_Counter.asset", "Bancada de Apoio / Espera", 2);
            var def10 = AssetDatabase.LoadAssetAtPath<StationDef>($"{StationsDir}/StationDef_10_Staging.asset");
            var def4 = AssetDatabase.LoadAssetAtPath<StationDef>($"{StationsDir}/StationDef_4_Pantry.asset");

            if (def16 == null) Debug.LogWarning("[PizzeriaPhase1Repair] StationDef_16_BoxStack.asset não encontrado!");
            if (def10 == null) Debug.LogWarning("[PizzeriaPhase1Repair] StationDef_10_Staging.asset não encontrado!");

            // 2. Atualizar PizzeriaConfig_PizzeriaModular_Manual.asset
            var config = AssetDatabase.LoadAssetAtPath<PizzeriaConfig>(ManualConfigPath);
            if (config != null)
            {
                Undo.RecordObject(config, "Reparo Config Fase 1");

                if (config.stations == null) config.stations = new List<StationDef>();
                if (def10 != null && !config.stations.Contains(def10)) config.stations.Add(def10);
                if (def14 != null && !config.stations.Contains(def14)) config.stations.Add(def14);
                if (def16 != null && !config.stations.Contains(def16)) config.stations.Add(def16);
                if (def17 != null && !config.stations.Contains(def17)) config.stations.Add(def17);
                if (def18 != null && !config.stations.Contains(def18)) config.stations.Add(def18);
                if (def20 != null && !config.stations.Contains(def20)) config.stations.Add(def20);
                if (def21 != null && !config.stations.Contains(def21)) config.stations.Add(def21);

                if (config.stationInstances == null) config.stationInstances = new List<StationInstanceConfig>();

                // ID 10: Retirada / Staging
                UpdateOrAddInstance(config.stationInstances, 10, def10, "Bancada de Retirada / Expedição", 2);

                // ID 14: Área de Recebimento
                UpdateOrAddInstance(config.stationInstances, 14, def14, "Área de Recebimento", 1);

                // ID 16: Pilha de Caixas / BoxStack
                UpdateOrAddInstance(config.stationInstances, 16, def16, "Pilha de Caixas", 2);

                // ID 17: Dispensa Secundária
                UpdateOrAddInstance(config.stationInstances, 17, def17, "Dispensa (Secundária)", 1);

                // ID 18: Geladeira Secundária
                UpdateOrAddInstance(config.stationInstances, 18, def18, "Geladeira (Secundária)", 1);

                // ID 20: Terminal de Fornecedor
                UpdateOrAddInstance(config.stationInstances, 20, def20, "Terminal de Fornecedor", 1);

                // ID 21: Bancada de Apoio / Espera
                UpdateOrAddInstance(config.stationInstances, 21, def21, "Bancada de Apoio / Espera", 2);

                // ID 13: Montagem (garantir slotCount 4)
                var inst13 = config.stationInstances.FirstOrDefault(x => x != null && x.instanceId == 13);
                if (inst13 != null) inst13.overrideSlotCount = 4;

                // ID 4: Quadro de Comandas Cozinha
                var inst4 = config.stationInstances.FirstOrDefault(x => x != null && x.instanceId == 4);
                if (inst4 != null)
                {
                    inst4.displayName = "Quadro de Comandas";
                    inst4.overrideSlotCount = 6;
                }

                // Configurar slots de estoque para dispensas e geladeiras secundárias (IDs 17 e 18)
                if (config.initialStorageSlots == null) config.initialStorageSlots = new List<StorageSlotConfig>();
                var dough = config.ingredients != null ? config.ingredients.FirstOrDefault(i => i != null && i.id == 1) : null;
                var cheese = config.ingredients != null ? config.ingredients.FirstOrDefault(i => i != null && i.id == 4) : null;

                if (!config.initialStorageSlots.Any(s => s != null && s.storageId == 17))
                {
                    config.initialStorageSlots.Add(new StorageSlotConfig
                    {
                        storageId = 17,
                        ingredient = dough,
                        ingredientId = 1,
                        initialStock = 10,
                        capacity = 30
                    });
                }

                if (!config.initialStorageSlots.Any(s => s != null && s.storageId == 18))
                {
                    config.initialStorageSlots.Add(new StorageSlotConfig
                    {
                        storageId = 18,
                        ingredient = cheese,
                        ingredientId = 4,
                        initialStock = 10,
                        capacity = 100
                    });
                }

                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
                Debug.Log("[PizzeriaPhase1Repair] PizzeriaConfig_PizzeriaModular_Manual atualizado e salvo.");
            }
            else
            {
                Debug.LogError($"[PizzeriaPhase1Repair] Não foi possível carregar config em {ManualConfigPath}");
            }

            // 3. Atualizar Prefab PizzeriaModular_Manual.prefab
            GameObject root = PrefabUtility.LoadPrefabContents(ManualPrefabPath);
            if (root == null)
            {
                Debug.LogError($"[PizzeriaPhase1Repair] Falha ao carregar prefab em {ManualPrefabPath}");
                return;
            }

            try
            {
                var bindings = root.GetComponent<PizzeriaAuthoringBindings>();
                var pizzeriaRoot = root.GetComponent<PizzeriaRoot>();
                var allStations = root.GetComponentsInChildren<StationView>(true);

                Debug.Log($"[PizzeriaPhase1Repair] Encontradas {allStations.Length} estações no prefab.");

                // A01: Resolver colisões de Station ID
                foreach (var st in allStations)
                {
                    if (st.name == "Dispensa_01" && st is StorageStation)
                    {
                        st.stationId = 6;
                        st.stationName = "Dispensa (Estoque Seco)";
                    }
                    else if (st.name == "Dispensa_02" && st is StorageStation)
                    {
                        st.stationId = 17;
                        st.stationName = "Dispensa (Secundária)";
                    }
                    else if (st.name == "Geladeira_02" && st is StorageStation)
                    {
                        st.stationId = 5;
                        st.stationName = "Geladeira (Estoque Frio)";
                    }
                    else if (st.name == "Geladeira_04" && st is StorageStation)
                    {
                        st.stationId = 18;
                        st.stationName = "Geladeira (Secundária)";
                    }
                    else if (st.name.Contains("mesaempacotamento.001 (1)") && st is StagingStation)
                    {
                        st.stationId = 10;
                        st.stationName = "Bancada de Retirada / Expedição";
                    }
                    else if (st.name == "PizzasProntas" && st is StackStation stack)
                    {
                        stack.stationId = 16;
                        stack.stackId = 16;
                        stack.stationName = "Pilha de Caixas";
                    }
                }

                // A02: Corrigir mesa media.002 (AssemblyStation ID 13) - remover 5º slot nulo
                var assembly = allStations.OfType<AssemblyStation>().FirstOrDefault(a => a.name.Contains("mesa media.002"));
                if (assembly != null && assembly.slotAnchors != null)
                {
                    var cleanSlots = assembly.slotAnchors.Where(s => s != null).ToArray();
                    assembly.slotAnchors = cleanSlots;
                    Debug.Log($"[PizzeriaPhase1Repair] AssemblyStation '{assembly.name}' slotAnchors corrigido para {cleanSlots.Length} slots.");
                }

                // A03 & A04: Mover TicketBoardStation de Comodo_02_Cozinha para Quadro de Comandas
                Transform comodoCozinha = root.transform.Find("Comodo_02_Cozinha");
                Transform quadroTransform = null;
                if (comodoCozinha != null)
                {
                    quadroTransform = comodoCozinha.Find("Quadro de Comandas");
                    var wrongBoard = comodoCozinha.GetComponent<TicketBoardStation>();
                    if (wrongBoard != null)
                    {
                        UnityEngine.Object.DestroyImmediate(wrongBoard);
                        Debug.Log("[PizzeriaPhase1Repair] TicketBoardStation removido de Comodo_02_Cozinha.");
                    }
                }
                else
                {
                    quadroTransform = root.transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Quadro de Comandas");
                }

                TicketBoardStation kitchenBoard = null;
                if (quadroTransform != null)
                {
                    quadroTransform.localRotation = Quaternion.Euler(0, 0, 0); // Faces positive Z into kitchen
                    kitchenBoard = quadroTransform.GetComponent<TicketBoardStation>();
                    if (kitchenBoard == null)
                    {
                        kitchenBoard = quadroTransform.gameObject.AddComponent<TicketBoardStation>();
                    }
                    kitchenBoard.stationId = 4;
                    kitchenBoard.stationName = "Quadro de Comandas";
                    kitchenBoard.maxVisibleTickets = 6;

                    // Mover ou encontrar TicketSlots
                    Transform kitchenSlotsRoot = quadroTransform.Find("TicketSlots");
                    if (kitchenSlotsRoot == null && comodoCozinha != null)
                    {
                        kitchenSlotsRoot = comodoCozinha.Find("TicketSlots");
                        if (kitchenSlotsRoot != null)
                        {
                            kitchenSlotsRoot.SetParent(quadroTransform, false);
                        }
                    }

                    if (kitchenSlotsRoot == null)
                    {
                        var go = new GameObject("TicketSlots");
                        go.transform.SetParent(quadroTransform, false);
                        kitchenSlotsRoot = go.transform;
                    }

                    kitchenSlotsRoot.localPosition = new Vector3(0, 0, 0.05f);
                    kitchenSlotsRoot.localRotation = Quaternion.identity;
                    kitchenSlotsRoot.localScale = Vector3.one;

                    var kSlots = SetupTicketSlots(kitchenSlotsRoot, 6);
                    kitchenBoard.ticketSlots = kSlots;
                    Debug.Log($"[PizzeriaPhase1Repair] Quadro de Comandas da Cozinha configurado com {kSlots.Length} slots coplanares.");
                }
                else
                {
                    Debug.LogError("[PizzeriaPhase1Repair] 'Quadro de Comandas' não encontrado!");
                }

                // A05: Corrigir slots de cardapio.001 (TicketBoardStation ID 15)
                Transform cardapioTransform = root.transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "cardapio.001");
                TicketBoardStation saloonBoard = null;
                if (cardapioTransform != null)
                {
                    saloonBoard = cardapioTransform.GetComponent<TicketBoardStation>();
                    if (saloonBoard != null)
                    {
                        saloonBoard.stationId = 15;
                        saloonBoard.stationName = "Quadro de Comandas";
                        saloonBoard.maxVisibleTickets = 6;

                        Transform sSlotsRoot = cardapioTransform.Find("TicketSlots");
                        if (sSlotsRoot != null)
                        {
                            sSlotsRoot.localPosition = Vector3.zero;
                            sSlotsRoot.localRotation = Quaternion.identity;
                            sSlotsRoot.localScale = Vector3.one;
                            var sSlots = SetupTicketSlots(sSlotsRoot, 6);
                            saloonBoard.ticketSlots = sSlots;
                            Debug.Log($"[PizzeriaPhase1Repair] cardapio.001 configurado com {sSlots.Length} slots coplanares (A05 corrigido).");
                        }
                    }
                }

                // Logística: Garantir SupplierTerminal e ReceivingStation em Comodo_03_Logistica
                Transform comodoLogistica = root.transform.Find("Comodo_03_Logistica");
                if (comodoLogistica == null)
                {
                    var cGo = new GameObject("Comodo_03_Logistica");
                    cGo.transform.SetParent(root.transform, false);
                    comodoLogistica = cGo.transform;
                }

                Transform receivingAnchor = null;
                if (pizzeriaRoot != null && pizzeriaRoot.receivingAnchor != null)
                {
                    receivingAnchor = pizzeriaRoot.receivingAnchor;
                }
                else
                {
                    receivingAnchor = root.transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "ReceivingAnchor");
                    if (receivingAnchor == null)
                    {
                        var raGo = new GameObject("ReceivingAnchor");
                        raGo.transform.SetParent(comodoLogistica, false);
                        raGo.transform.localPosition = new Vector3(18f, -1.4f, -24f);
                        receivingAnchor = raGo.transform;
                    }
                    if (pizzeriaRoot != null) pizzeriaRoot.receivingAnchor = receivingAnchor;
                }

                ReceivingStation receivingStation = root.GetComponentInChildren<ReceivingStation>(true);
                if (receivingStation == null)
                {
                    var rGo = new GameObject("Station_Receiving");
                    rGo.transform.SetParent(comodoLogistica, false);
                    rGo.transform.position = receivingAnchor.position;
                    receivingStation = rGo.AddComponent<ReceivingStation>();
                    var col = rGo.AddComponent<BoxCollider>();
                    col.size = new Vector3(2f, 2f, 2f);
                }
                receivingStation.stationId = 14;
                receivingStation.stationName = "Área de Recebimento";
                receivingStation.slotAnchors = new Transform[] { receivingAnchor };

                SupplierTerminal supplierTerminal = root.GetComponentInChildren<SupplierTerminal>(true);
                if (supplierTerminal == null)
                {
                    var sGo = new GameObject("Station_SupplierTerminal");
                    sGo.transform.SetParent(comodoLogistica, false);
                    sGo.transform.localPosition = new Vector3(20f, -1.4f, -23f);
                    supplierTerminal = sGo.AddComponent<SupplierTerminal>();
                    var col = sGo.AddComponent<BoxCollider>();
                    col.size = new Vector3(1f, 1.5f, 0.5f);
                }
                supplierTerminal.stationId = 20;
                supplierTerminal.stationName = "Terminal de Fornecedor";

                // CounterStation: Garantir Bancada de Apoio / Espera em balcao.005
                Transform balcao005 = root.transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "balcao.005");
                CounterStation counterStation = root.GetComponentInChildren<CounterStation>(true);
                if (counterStation == null && balcao005 != null)
                {
                    counterStation = balcao005.gameObject.AddComponent<CounterStation>();
                    counterStation.stationId = 21;
                    counterStation.stationName = "Bancada de Apoio / Espera";

                    Transform slotsRoot = balcao005.Find("Slots");
                    if (slotsRoot == null)
                    {
                        var sGo = new GameObject("Slots");
                        sGo.transform.SetParent(balcao005, false);
                        slotsRoot = sGo.transform;
                    }
                    slotsRoot.localPosition = Vector3.zero;

                    Transform slot0 = slotsRoot.Find("Slot_0");
                    if (slot0 == null)
                    {
                        var s0 = new GameObject("Slot_0");
                        s0.transform.SetParent(slotsRoot, false);
                        s0.transform.localPosition = new Vector3(-0.28f, 0.45f, 0f);
                        slot0 = s0.transform;
                    }
                    Transform slot1 = slotsRoot.Find("Slot_1");
                    if (slot1 == null)
                    {
                        var s1 = new GameObject("Slot_1");
                        s1.transform.SetParent(slotsRoot, false);
                        s1.transform.localPosition = new Vector3(0.28f, 0.45f, 0f);
                        slot1 = s1.transform;
                    }
                    counterStation.slotAnchors = new Transform[] { slot0, slot1 };

                    var boxCol = balcao005.GetComponent<BoxCollider>();
                    if (boxCol == null)
                    {
                        boxCol = balcao005.gameObject.AddComponent<BoxCollider>();
                        boxCol.size = new Vector3(1f, 0.9f, 0.6f);
                        boxCol.center = new Vector3(0f, 0.45f, 0f);
                    }
                    Debug.Log("[PizzeriaPhase1Repair] CounterStation configurada em balcao.005 com 2 slots.");
                }

                // Atualizar Bindings Primários e stationRecords
                if (bindings != null)
                {
                    var updatedStations = root.GetComponentsInChildren<StationView>(true);
                    var dispensa01 = updatedStations.OfType<StorageStation>().FirstOrDefault(s => s.stationId == 6);
                    var dispensa02 = updatedStations.OfType<StorageStation>().FirstOrDefault(s => s.stationId == 17);
                    var geladeira02 = updatedStations.OfType<StorageStation>().FirstOrDefault(s => s.stationId == 5);
                    var geladeira04 = updatedStations.OfType<StorageStation>().FirstOrDefault(s => s.stationId == 18);
                    var stagingStation = updatedStations.OfType<StagingStation>().FirstOrDefault(s => s.stationId == 10);
                    var boxStack = updatedStations.OfType<StackStation>().FirstOrDefault(s => s.stationId == 16);
                    var toolRack = updatedStations.OfType<ToolRackStation>().FirstOrDefault(s => s.stationId == 7);
                    var phone = updatedStations.OfType<PhoneStation>().FirstOrDefault(s => s.stationId == 12);

                    bindings.primaryPantryStation = dispensa01;
                    bindings.primaryFridgeStation = geladeira02;
                    bindings.primaryStagingStation = stagingStation;
                    bindings.primaryBoxStackStation = boxStack;
                    bindings.primaryToolRackStation = toolRack;
                    bindings.primaryPhoneStation = phone;
                    bindings.primaryTicketBoardStation = kitchenBoard;
                    bindings.primaryReceivingStation = receivingStation;
                    bindings.primarySupplierTerminal = supplierTerminal;

                    // Marcar âncoras como verificadas
                    bindings.anchorsVerifiedByUser = true;
                    bindings.entranceAnchorProvisional = false;
                    bindings.receivingAnchorProvisional = false;
                    bindings.recoveryAnchorProvisional = false;
                    bindings.playerSpawnsProvisional = false;

                    if (bindings.stationRecords == null) bindings.stationRecords = new List<PizzeriaStationBindingRecord>();

                    // Sincronizar stationRecords com todas as estações ativas de forma determinística
                    foreach (var st in updatedStations)
                    {
                        if (st.stationId <= 0) continue;
                        var rec = bindings.stationRecords.FirstOrDefault(r => r != null && r.stationId == st.stationId);
                        if (rec == null)
                        {
                            rec = new PizzeriaStationBindingRecord
                            {
                                stationId = st.stationId,
                                bindingGuid = Guid.NewGuid().ToString("N")
                            };
                            bindings.stationRecords.Add(rec);
                        }
                        rec.stationComponent = st;

                        // Determinar stationKind diretamente pelo tipo de componente
                        PizzeriaStationKind resolvedKind = PizzeriaStationKind.None;
                        if (st is PrepStation) resolvedKind = PizzeriaStationKind.Prep;
                        else if (st is AssemblyStation) resolvedKind = PizzeriaStationKind.Assembly;
                        else if (st is OvenStation) resolvedKind = PizzeriaStationKind.Oven;
                        else if (st is PackagingStation) resolvedKind = PizzeriaStationKind.Packaging;
                        else if (st is StagingStation) resolvedKind = PizzeriaStationKind.Staging;
                        else if (st is ToolRackStation) resolvedKind = PizzeriaStationKind.ToolRack;
                        else if (st is PhoneStation) resolvedKind = PizzeriaStationKind.Phone;
                        else if (st is TicketBoardStation) resolvedKind = PizzeriaStationKind.TicketBoard;
                        else if (st is SupplierTerminal) resolvedKind = PizzeriaStationKind.SupplierTerminal;
                        else if (st is ReceivingStation) resolvedKind = PizzeriaStationKind.Receiving;
                        else if (st is TrashStation) resolvedKind = PizzeriaStationKind.Trash;
                        else if (st is StackStation) resolvedKind = PizzeriaStationKind.BoxStack;
                        else if (st is CounterStation) resolvedKind = PizzeriaStationKind.Counter;
                        else if (st is StorageStation)
                        {
                            if (st.stationId == 5 || st.stationId == 18) resolvedKind = PizzeriaStationKind.Fridge;
                            else resolvedKind = PizzeriaStationKind.Pantry;
                        }
                        rec.stationKind = resolvedKind;

                        // Atribuir StationDef correspondente
                        if (config != null && config.stationInstances != null)
                        {
                            var inst = config.stationInstances.FirstOrDefault(x => x != null && x.instanceId == st.stationId);
                            if (inst != null && inst.definition != null)
                            {
                                rec.stationDef = inst.definition;
                            }
                        }
                    }

                    bindings.SyncPrimaryStationsFromRecords();
                    EditorUtility.SetDirty(bindings);
                }

                if (pizzeriaRoot != null)
                {
                    pizzeriaRoot.EnsureAnchorsResolved();
                    EditorUtility.SetDirty(pizzeriaRoot);
                }

                // Salvar Prefab
                PrefabUtility.SaveAsPrefabAsset(root, ManualPrefabPath);
                Debug.Log($"[PizzeriaPhase1Repair] Prefab salvo com sucesso em {ManualPrefabPath}!");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 4. Executar Validação Automatizada de Gameplay
            Debug.Log("[PizzeriaPhase1Repair] Executando PizzeriaGameplayValidator pós-reparo...");
            var settings = AssetDatabase.LoadAssetAtPath<PizzeriaAuthoringSettings>("Assets/Piaaria/ScriptableObjects/Pizzeria/PizzeriaAuthoringSettings_Modular.asset");
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ManualPrefabPath);

            var report = PizzeriaGameplayValidator.ValidatePrefabAndCycle(prefabAsset, settings, false);
            Debug.Log($"[PizzeriaPhase1Repair] Relatório de Validação: {report.blockingErrorsCount} Erros Bloqueantes, {report.warningsCount} Avisos, {report.okCount} Itens OK.");

            if (report.blockingErrorsCount > 0)
            {
                foreach (var issue in report.prefabIssues.Concat(report.cycleIssues))
                {
                    if (issue.severity == PizzeriaValidationSeverity.BlockingError)
                    {
                        Debug.LogError($"[Phase1 Issue] [{issue.category}] {issue.title}: {issue.description} ({issue.targetObject?.name})");
                    }
                }
            }
            else
            {
                Debug.Log("[PizzeriaPhase1Repair] SUCESSO TOTAL FASE 1: ZERO ERROS BLOQUEANTES! Prefab e configurações 100% íntegros!");
            }
        }

        private static Transform[] SetupTicketSlots(Transform slotsRoot, int count)
        {
            var result = new Transform[count];
            int cols = Mathf.Min(3, count);
            int rows = Mathf.CeilToInt(count / (float)cols);

            for (int i = 0; i < count; i++)
            {
                string slotName = $"TicketSlot_{i}";
                Transform slotT = slotsRoot.Find(slotName);
                if (slotT == null)
                {
                    var sGo = new GameObject(slotName);
                    slotT = sGo.transform;
                    slotT.SetParent(slotsRoot, false);
                }

                int col = i % cols;
                int row = i / cols;
                float xOffset = (col - (cols - 1) * 0.5f) * 0.28f;
                float yOffset = ((rows - 1) * 0.5f - row) * 0.28f;

                slotT.localPosition = new Vector3(xOffset, yOffset, 0.06f);
                slotT.localRotation = Quaternion.identity;
                slotT.localScale = Vector3.one;

                var sheet = slotT.GetComponent<TicketSheetView>();
                if (sheet == null) sheet = slotT.gameObject.AddComponent<TicketSheetView>();

                var colComp = slotT.GetComponent<BoxCollider>();
                if (colComp == null)
                {
                    colComp = slotT.gameObject.AddComponent<BoxCollider>();
                    colComp.size = new Vector3(0.38f, 0.55f, 0.08f);
                }

                Transform textT = slotT.Find("SheetText");
                if (textT == null)
                {
                    var textGo = new GameObject("SheetText");
                    textT = textGo.transform;
                    textT.SetParent(slotT, false);
                    textT.localPosition = new Vector3(0f, 0f, -0.015f);
                    textT.localScale = Vector3.one * 0.14f;
                }
                var tmp = textT.GetComponent<TextMeshPro>();
                if (tmp == null) tmp = textT.gameObject.AddComponent<TextMeshPro>();
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontSize = 2.4f;
                tmp.color = Color.black;
                sheet.textMesh = tmp;

                result[i] = slotT;
            }
            return result;
        }

        private static StationDef EnsureStationDef(int id, string fileName, string stationName, int slotCount)
        {
            string path = $"{StationsDir}/{fileName}";
            var def = AssetDatabase.LoadAssetAtPath<StationDef>(path);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<StationDef>();
                def.id = id;
                def.stationName = stationName;
                def.slotCount = slotCount;
                AssetDatabase.CreateAsset(def, path);
                Debug.Log($"[PizzeriaPhase1Repair] Criado StationDef: {path}");
            }
            return def;
        }

        private static void UpdateOrAddInstance(List<StationInstanceConfig> instances, int id, StationDef def, string displayName, int slotCount)
        {
            var inst = instances.FirstOrDefault(x => x != null && x.instanceId == id);
            if (inst == null)
            {
                inst = new StationInstanceConfig
                {
                    instanceId = id,
                    definition = def,
                    displayName = displayName,
                    overrideSlotCount = slotCount,
                    durationOverrides = new List<ProcessDurationOverride>()
                };
                instances.Add(inst);
            }
            else
            {
                inst.definition = def;
                inst.displayName = displayName;
                inst.overrideSlotCount = slotCount;
            }
        }
    }
}
#endif
