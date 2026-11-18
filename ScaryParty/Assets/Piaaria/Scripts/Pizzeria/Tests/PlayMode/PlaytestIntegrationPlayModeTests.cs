using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Ports;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ScaryParty.Pizzeria.Tests.PlayMode
{
    public class PlaytestIntegrationPlayModeTests
    {
        private const string GameScenePath = "Assets/ThirdParty/StarterAssets/ThirdPersonController/Scenes/GameScene.unity";
        private static ushort _nextPort = 7790;
        private readonly List<GameObject> _tempObjects = new List<GameObject>();

        private sealed class PlayModeClock : IClock
        {
            public double Now { get; set; } = 100.0;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int i = 0; i < _tempObjects.Count; i++)
            {
                if (_tempObjects[i] != null) UnityEngine.Object.Destroy(_tempObjects[i]);
            }
            _tempObjects.Clear();

            var monDirType = FindRuntimeType("ScaryParty.Combat.Monsters.MonsterPopulationDirector");
            if (monDirType != null)
            {
                var inst = monDirType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (inst != null)
                {
                    monDirType.GetMethod("ClearNightMonstersServer", BindingFlags.Public | BindingFlags.Instance)?.Invoke(inst, null);
                    monDirType.GetMethod("ClearAllClientProxies", BindingFlags.Public | BindingFlags.Instance)?.Invoke(inst, null);
                }
            }

            var empDirType = FindRuntimeType("ScaryParty.Pizzeria.Presentation.EmployeePopulationDirector");
            empDirType?.GetMethod("ClearAll", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);

            var csType = FindRuntimeType("ScaryParty.Cutscenes.CutscenePlaybackService");
            if (csType != null)
            {
                var csInst = csType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (csInst != null)
                    csType.GetMethod("Cancel", BindingFlags.Public | BindingFlags.Instance)?.Invoke(csInst, null);
            }

            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                if (nm.IsListening) nm.Shutdown();
                UnityEngine.Object.Destroy(nm.gameObject);
                yield return null;
                yield return null;
            }
        }

        private static Type FindRuntimeType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, throwOnError: false);
                if (t != null) return t;
            }
            return null;
        }

        private static IEnumerator LoadGameSceneAndStartHost()
        {
            var nmOld = NetworkManager.Singleton;
            if (nmOld != null)
            {
                if (nmOld.IsListening) nmOld.Shutdown();
                UnityEngine.Object.Destroy(nmOld.gameObject);
                yield return null;
            }

#if UNITY_EDITOR
            var op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                GameScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            while (op != null && !op.isDone) yield return null;
#else
            SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
            yield return null;
#endif
            for (int i = 0; i < 4; i++) yield return null;

            var nm = NetworkManager.Singleton;
            Assert.IsNotNull(nm, "NetworkManager precisa existir em GameScene.unity.");

            var utp = nm.GetComponent("UnityTransport");
            if (utp != null)
            {
                var setConn = utp.GetType().GetMethod("SetConnectionData", new[] { typeof(string), typeof(ushort), typeof(string) });
                setConn?.Invoke(utp, new object[] { "127.0.0.1", _nextPort++, null });
            }

            if (!nm.IsListening)
            {
                bool started = nm.StartHost();
                Assert.IsTrue(started, "StartHost precisa iniciar em GameScene.unity para validação PlayMode.");
            }

            var rootType = FindRuntimeType("ScaryParty.Pizzeria.Composition.PizzeriaRoot");
            Assert.IsNotNull(rootType);

            float elapsed = 0f;
            object rootInst = null;
            PizzeriaState domainState = null;
            while (elapsed < 15f)
            {
                rootInst = rootType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (rootInst != null)
                {
                    domainState = rootType.GetProperty("DomainState", BindingFlags.Public | BindingFlags.Instance)?.GetValue(rootInst) as PizzeriaState;
                    if (domainState != null) break;
                }
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsNotNull(rootInst, "PizzeriaRoot.Instance deve ser inicializado pelo Host em GameScene.");
            Assert.IsNotNull(domainState, "DomainState deve estar populado no Host.");
        }

        [UnityTest]
        public IEnumerator PlayMode_ClientStartupWithoutDomainState_SpawnsAndSyncsProxiesSafely()
        {
            var rootType = FindRuntimeType("ScaryParty.Pizzeria.Composition.PizzeriaRoot");
            var monDirType = FindRuntimeType("ScaryParty.Combat.Monsters.MonsterPopulationDirector");
            var empDirType = FindRuntimeType("ScaryParty.Pizzeria.Presentation.EmployeePopulationDirector");
            var netMonsterType = FindRuntimeType("ScaryParty.Pizzeria.Network.NetMonsterDto");
            var netEmployeeType = FindRuntimeType("ScaryParty.Pizzeria.Network.NetEmployeeDto");

            Assert.IsNotNull(rootType);
            Assert.IsNotNull(monDirType);
            Assert.IsNotNull(empDirType);
            Assert.IsNotNull(netMonsterType);
            Assert.IsNotNull(netEmployeeType);

            var go = new GameObject("PlayModeClientProxyHarness");
            _tempObjects.Add(go);
            var root = go.AddComponent(rootType);
            yield return null;

            Assert.IsNull(rootType.GetProperty("DomainState")?.GetValue(root), "Cliente nunca possui DomainState autoritativo.");
            Assert.IsFalse((bool)(rootType.GetProperty("IsServer")?.GetValue(root) ?? true));

            var monDir = monDirType.GetMethod("EnsureOn", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, new object[] { root });
            Assert.IsNotNull(monDir);

            // Monta snapshot replicado de 2 monstros
            var monsterList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(netMonsterType));
            object dtoM1 = Activator.CreateInstance(netMonsterType);
            netMonsterType.GetField("MonsterId").SetValue(dtoM1, 1001UL);
            netMonsterType.GetField("Species").SetValue(dtoM1, (byte)NpcSpecies.Vampire);
            netMonsterType.GetField("CurrentHp").SetValue(dtoM1, 130);
            netMonsterType.GetField("MaxHp").SetValue(dtoM1, 130);
            monsterList.Add(dtoM1);

            object dtoM2 = Activator.CreateInstance(netMonsterType);
            netMonsterType.GetField("MonsterId").SetValue(dtoM2, 1002UL);
            netMonsterType.GetField("Species").SetValue(dtoM2, (byte)NpcSpecies.Werewolf);
            netMonsterType.GetField("CurrentHp").SetValue(dtoM2, 180);
            netMonsterType.GetField("MaxHp").SetValue(dtoM2, 180);
            monsterList.Add(dtoM2);

            monDirType.GetMethod("SyncClientProxiesFromSnapshot")?.Invoke(monDir, new object[] { monsterList });
            yield return null;

            int mProxyCount = (int)monDirType.GetProperty("ClientProxyCount").GetValue(monDir);
            Assert.AreEqual(2, mProxyCount, "Cliente deve criar exatamente 1 proxy por monstro do snapshot.");

            // Monta snapshot replicado de 2 funcionários
            var empList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(netEmployeeType));
            object dtoE1 = Activator.CreateInstance(netEmployeeType);
            netEmployeeType.GetField("EmployeeId").SetValue(dtoE1, 1);
            netEmployeeType.GetField("Role").SetValue(dtoE1, (byte)EmployeeRole.Cashier);
            netEmployeeType.GetField("IsAlive").SetValue(dtoE1, true);
            empList.Add(dtoE1);

            object dtoE2 = Activator.CreateInstance(netEmployeeType);
            netEmployeeType.GetField("EmployeeId").SetValue(dtoE2, 2);
            netEmployeeType.GetField("Role").SetValue(dtoE2, (byte)EmployeeRole.Pizzaiolo);
            netEmployeeType.GetField("IsAlive").SetValue(dtoE2, true);
            empList.Add(dtoE2);

            empDirType.GetMethod("SyncClientProxiesFromSnapshot", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, new object[] { empList });
            yield return null;

            int eProxyCount = (int)empDirType.GetProperty("ClientProxyCount", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            Assert.AreEqual(2, eProxyCount, "Cliente deve criar exatamente 1 proxy por funcionário.");

            // Remoção por delta no snapshot
            monsterList.RemoveAt(1);
            empList.RemoveAt(1);
            monDirType.GetMethod("SyncClientProxiesFromSnapshot")?.Invoke(monDir, new object[] { monsterList });
            empDirType.GetMethod("SyncClientProxiesFromSnapshot", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, new object[] { empList });
            yield return null;

            Assert.AreEqual(1, (int)monDirType.GetProperty("ClientProxyCount").GetValue(monDir));
            Assert.AreEqual(1, (int)empDirType.GetProperty("ClientProxyCount", BindingFlags.Public | BindingFlags.Static).GetValue(null));
        }

        [UnityTest]
        public IEnumerator PlayMode_FailedProcessingOperation_LeavesItemAndStockUnmutated()
        {
            var state = new PizzeriaState { GenerationId = 1 };
            var catalog = new DefinitionCatalog();
            catalog.RegisterIngredient(new IngredientData(1, "Tomate Inteiro", ItemCategory.Topping, true, 2));
            catalog.RegisterIngredient(new IngredientData(2, "Tomate Fatiado", ItemCategory.Topping));
            catalog.RegisterTool(new ToolData(2, "Faca", ToolCapability.Slice, 1));
            catalog.RegisterProcess(new ProcessData(10, "Fatiar", 1, ProcessingStage.Raw, 2, ProcessingStage.Prepared, ToolCapability.Slice, 2f));
            catalog.RegisterStation(new StationData(4, "Bancada", 4, new[] { 10 }));

            var itemId = state.GenerateItemId();
            var item = new ItemState(itemId, 1, ItemCategory.Topping, LocationRef.InStation(4, 0));
            state.Items[itemId] = item;

            var toolId = state.GenerateToolId();
            var tool = new ToolItemState(toolId, 2, ToolCapability.Slice, LocationRef.InStation(4, 1));
            Assert.IsTrue(tool.TryAcquireLease(99UL, 100f, 30f)); // Já reservada por outro ator
            state.Tools[toolId] = tool;

            var slot = new StorageSlotState(1, 1, 7, 20);
            state.StorageSlots[(1, 1)] = slot;

            var service = new ProcessingService();
            var slotKey = new StationSlotId(4, 0);
            var res = service.BeginOperation(state, slotKey, OperationKind.Process, ResumePolicy.ResetOnCancel, itemId, toolId, 10, 11UL, catalog, 101f);
            yield return null;
            yield return null;

            Assert.IsFalse(res.Success, "Operação com ferramenta ocupada deve falhar.");
            Assert.AreEqual(1, item.DefinitionId);
            Assert.AreEqual(ProcessingStage.Raw, item.PrepStage);
            Assert.AreEqual(7, slot.Quantity);
            Assert.AreEqual(0, state.ActiveOperations.Count);
        }

        [UnityTest]
        public IEnumerator PlayMode_TwoIdenticalOrders_CompeteForOneReadyPizza_ExclusiveBinding()
        {
            var state = new PizzeriaState { GenerationId = 1 };
            var catalog = new DefinitionCatalog();
            var clock = new PlayModeClock { Now = 50.0 };
            var orderService = new OrderService();

            var cheeseSpec = PizzaSpecification.CreateDefaultCheese(20, 12f);
            var reqList = new List<(PizzaSpecification spec, int qty)> { (cheeseSpec, 1) };
            var resA = orderService.CreatePhoneOrderBySpecifications(new AddressId(1), reqList, state, catalog, clock, 6);
            var resB = orderService.CreatePhoneOrderBySpecifications(new AddressId(2), reqList, state, catalog, clock, 6);
            Assert.IsTrue(resA.Success);
            Assert.IsTrue(resB.Success);

            var orderA = state.Orders[resA.Value];
            var orderB = state.Orders[resB.Value];

            var pizzaId = state.GenerateItemId();
            var pizza = new ItemState(pizzaId, 2, ItemCategory.Pizza, LocationRef.InStation(9, 0))
            {
                PrepStage = ProcessingStage.Prepared,
                BaseDoughDefinitionId = 2,
                BaseDoughStage = ProcessingStage.Prepared
            };
            Assert.IsTrue(pizza.AddIngredient(3, ProcessingStage.Prepared, 1), "Molho deve ser adicionado antes de assar.");
            Assert.IsTrue(pizza.AddIngredient(5, ProcessingStage.Prepared, 1), "Queijo deve ser adicionado antes de assar.");
            pizza.CookingStage = CookingStage.Baked;
            pizza.CookProgress = 1f;
            pizza.PackagingState = PackagingState.Boxed;
            state.Items[pizzaId] = pizza;

            OrderUnitFulfillmentService.BindReadyPizzasAndReconcileOrder(state, orderA);
            yield return null;
            OrderUnitFulfillmentService.BindReadyPizzasAndReconcileOrder(state, orderB);
            yield return null;

            Assert.IsTrue(OrderUnitFulfillmentService.IsItemBoundToActiveUnit(state, pizzaId, out var boundKey));
            Assert.AreEqual(orderA.Id, boundKey.OrderId, "Apenas o primeiro pedido deve reivindicar a única pizza pronta.");

            var keyB = new OrderUnitKey(state.GenerationId, orderB.Id, 0, 0);
            Assert.IsFalse(OrderUnitFulfillmentService.TryBindSpecificPizzaToUnit(state, keyB, pizzaId), "Segunda ordem não pode sequestrar pizza já vinculada.");
            Assert.AreEqual(OrderUnitStatus.Pending, state.OrderUnits[keyB].Status);
        }

        [UnityTest]
        public IEnumerator PlayMode_CustomPresetGoal_PreservedAfterNightRestart()
        {
            var state = new PizzeriaState { GenerationId = 1 };
            var presetData = new RunPresetData(
                presetId: "CustomGoalPlayMode",
                totalNights: 3,
                includeNight0Tutorial: false,
                allowDebugPanel: true,
                difficulty: RunDifficulty.Normal,
                startingBudget: 500,
                startingReputation: 100,
                night0DurationSeconds: 300f,
                night0GoalsByHumanCount: new[] { 15, 15, 15, 15 },
                night0PrepSeconds: 2f,
                night0ClosingGraceSeconds: 0f,
                night0NonLethalDefeat: true,
                nights: new[]
                {
                    new NightPresetRowData(1, 180f, new[] { 111, 222, 333, 444 }, 3f, 10f, 0, 0, false)
                });

            var resolved = NightParameterResolver.ResolveEffectiveParameters(1, 3, RunDifficulty.Normal, presetData);
            NightCycleService.StartNight(state, resolved);
            NightCycleService.CaptureSnapshot(state);
            yield return null;

            Assert.AreEqual(333, state.NightRevenueGoal);
            NightCycleService.RecordNightRevenue(state, 50);
            Assert.IsTrue(NightCycleService.RetryCurrentNight(state, resolved.ToNightGoalParameters()));
            yield return null;

            Assert.AreEqual(333, state.NightRevenueGoal, "Meta customizada deve sobreviver ao RetryCurrentNight.");
            Assert.AreEqual(3, state.FrozenHumanCount);
            Assert.AreEqual(180f, state.NightDurationSeconds);
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator PlayMode_GameSceneHost_StartNewRunReconstructsKitchenWithoutDuplicates()
        {
            yield return LoadGameSceneAndStartHost();

            var rootType = FindRuntimeType("ScaryParty.Pizzeria.Composition.PizzeriaRoot");
            var rootInst = rootType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            var state = rootType.GetProperty("DomainState", BindingFlags.Public | BindingFlags.Instance).GetValue(rootInst) as PizzeriaState;
            Assert.IsNotNull(state);

            int baseTables = state.Tables.Count;
            int baseStorage = state.StorageSlots.Count;
            int baseBackpacks = state.Backpacks.Count;
            Assert.Greater(baseTables, 0, "Cozinha/salão deve iniciar com mesas registradas.");
            Assert.Greater(baseStorage, 0, "Cozinha deve iniciar com estoques registrados.");
            Assert.Greater(baseBackpacks, 0, "Cozinha deve iniciar com mochilas registradas.");

            // Injeta entidades transitórias na RUN atual
            var strayItemId = state.GenerateItemId();
            state.Items[strayItemId] = new ItemState(strayItemId, 1, ItemCategory.Dough, LocationRef.InWorld(0, 0, 0));
            yield return null;

            var reinit = rootType.GetMethod("ReinitializeForNewRunServer", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(reinit);
            reinit.Invoke(rootInst, new object[] { 424242, 3, RunDifficulty.Normal, 1 });
            yield return null;
            yield return null;

            Assert.AreEqual(0, state.Items.Count, "Nova RUN deve limpar itens transitórios.");
            Assert.AreEqual(0, state.Orders.Count, "Nova RUN deve limpar pedidos anteriores.");
            Assert.AreEqual(baseTables, state.Tables.Count, "Mesas reconstruídas sem duplicação.");
            Assert.AreEqual(baseStorage, state.StorageSlots.Count, "Estoques reconstruídos sem duplicação.");
            Assert.AreEqual(baseBackpacks, state.Backpacks.Count, "Mochilas reconstruídas sem duplicação.");
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator PlayMode_GameScene_FiveDeterministicCitySeeds_PassConnectivityAndRooftopValidation()
        {
            yield return LoadGameSceneAndStartHost();

            var cityGenType = FindRuntimeType("CityGenerator");
            Assert.IsNotNull(cityGenType);
            var cityGen = UnityEngine.Object.FindFirstObjectByType(cityGenType) as MonoBehaviour;
            Assert.IsNotNull(cityGen, "CityGenerator deve estar ativo em GameScene.unity.");

            var clearMethod = cityGenType.GetMethod("ClearLocalCity", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var genMethod = cityGenType.GetMethod("GenerateLocally", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var seedProp = cityGenType.GetProperty("CurrentSeed", BindingFlags.Public | BindingFlags.Instance);
            var roofProp = cityGenType.GetProperty("LastValidatedRooftopCount", BindingFlags.Public | BindingFlags.Instance);
            var reachProp = cityGenType.GetProperty("LastReachableDeliveryCount", BindingFlags.Public | BindingFlags.Instance);

            Assert.IsNotNull(clearMethod);
            Assert.IsNotNull(genMethod);

            int[] seeds = { 10101, 20202, 30303, 40404, 50505 };
            int firstSeedRooftops = -1;
            int firstSeedReachable = -1;

            for (int i = 0; i < seeds.Length; i++)
            {
                clearMethod.Invoke(cityGen, null);
                genMethod.Invoke(cityGen, new object[] { seeds[i] });
                yield return null;
                yield return null;

                int actualSeed = (int)seedProp.GetValue(cityGen);
                int roofs = (int)roofProp.GetValue(cityGen);
                int reachable = (int)reachProp.GetValue(cityGen);

                Assert.AreEqual(seeds[i], actualSeed);
                Assert.Greater(roofs, 0, $"Seed {seeds[i]} deve gerar telhados validados.");
                Assert.Greater(reachable, 0, $"Seed {seeds[i]} deve ter pontos de entrega alcançáveis.");

                if (i == 0)
                {
                    firstSeedRooftops = roofs;
                    firstSeedReachable = reachable;
                }
            }

            // Determinismo na repetição da primeira seed
            clearMethod.Invoke(cityGen, null);
            genMethod.Invoke(cityGen, new object[] { seeds[0] });
            yield return null;
            Assert.AreEqual(firstSeedRooftops, (int)roofProp.GetValue(cityGen), "Repetir seed deve gerar mesmo número de telhados.");
            Assert.AreEqual(firstSeedReachable, (int)reachProp.GetValue(cityGen), "Repetir seed deve gerar mesmo número de rotas válidas.");
        }
    }
}
