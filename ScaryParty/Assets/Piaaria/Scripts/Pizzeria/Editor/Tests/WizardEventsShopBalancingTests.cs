using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using ScaryParty.Combat.Monsters;
using ScaryParty.Editor.Playtest;
using ScaryParty.Lighting;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Editor.Migration;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.UI.Piaria.Presenters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScaryParty.Pizzeria.Editor.Tests
{
    public sealed class WizardEventsShopBalancingTests
    {
        private static RunBalancingSnapshot LoadOrFallbackCatalogSnapshot()
        {
            var cat = AssetDatabase.LoadAssetAtPath<RunBalancingCatalog>(RunBalancingMigrationRunner.CatalogAssetPath);
            return cat != null ? cat.BuildDomainSnapshot() : RunBalancingSnapshot.CreateForDomainUnitTests();
        }

        [Test]
        public void T01_IdempotentMigration_SecondPassZeroChangesAndPreservesIds()
        {
            var r1 = RunBalancingMigrationRunner.RunIdempotentMigration();
            var r2 = RunBalancingMigrationRunner.RunIdempotentMigration();
            Assert.AreEqual(0, r2.TotalChanges, "Segunda execucao da migracao deve ser 100% idempotente (0 alteracoes).");

            var catalog = AssetDatabase.LoadAssetAtPath<RunBalancingCatalog>(RunBalancingMigrationRunner.CatalogAssetPath);
            Assert.IsNotNull(catalog);
            string[] legacyIds = { "blood_moon", "vampire_attack", "heavy_rush", "express_supply", "oven_timer", "reinforced_bag", "staff_training" };
            for (int i = 0; i < legacyIds.Length; i++)
            {
                Assert.IsNotNull(catalog.FindModifierById(legacyIds[i]), $"ID legado '{legacyIds[i]}' deve estar preservado no catalogo.");
            }
        }

        [Test]
        public void T02_SingleSourceOfTruth_ConfigResolvesCatalogAndFormatsSameValues()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<PizzeriaConfig>(RunBalancingMigrationRunner.ActivePizzeriaConfigPath);
            Assert.IsNotNull(cfg);
            var snap = cfg.ResolveBalancingSnapshot(out string err);
            Assert.IsTrue(string.IsNullOrEmpty(err), err);
            Assert.IsNotNull(snap);
            Assert.IsNotNull(snap.FindModifierById("blood_moon"));
            Assert.IsNotNull(snap.FindShopOfferById("oven_timer_offer"));
        }

        [Test]
        public void T03_CompositionOrder_BaseFlatPercentMultiplyLimits_IsDeterministic()
        {
            var effects = new[]
            {
                new RunModifierEffectData(RunEffectTarget.OrderArrivalRate, RunEffectOperation.AddFlat, RunEffectUnit.FlatUnits, 0.2f),
                new RunModifierEffectData(RunEffectTarget.OrderArrivalRate, RunEffectOperation.PercentDelta, RunEffectUnit.RelativePercent, 50f),
                new RunModifierEffectData(RunEffectTarget.OrderArrivalRate, RunEffectOperation.Multiply, RunEffectUnit.Multiplier, 2.0f, true, 0.5f, 3.0f)
            };
            var def = new RunModifierDefinition
            {
                Id = "combo_test",
                Kind = RunModifierKind.Event,
                Duration = RunModifierDuration.OneNight,
                Effects = effects
            };
            var snap = new RunBalancingSnapshot(new[] { def }, null, null, null);
            var run = new RunProgressionService(101, 3, RunDifficulty.Normal, snap);
            run.ApplyModifier(def);

            // (1.0 + 0.2) * (1 + 0.50) * 2.0 = 3.6 -> clamped maxLimit 3.0
            var stats1 = run.RecalculateEffectiveStats();
            var stats2 = run.RecalculateEffectiveStats();
            Assert.AreEqual(3.0f, stats1.DemandMultiplier, 0.001f);
            Assert.AreEqual(stats1.DemandMultiplier, stats2.DemandMultiplier, 0.0001f);
        }

        [Test]
        public void T04_RelativeErrorProbability_NeverSubtractsFlatZeroingSmallRates()
        {
            var profile = new EmployeeRoleProfileData(EmployeeRole.Pizzaiolo, "Pizzaiolo", 120, 5, 1f, 0, 0.20f, 0.15f, 0.02f, 1.8f);
            var snap = new RunBalancingSnapshot(
                RunBalancingSnapshot.CreateForDomainUnitTests().Modifiers,
                RunBalancingSnapshot.CreateForDomainUnitTests().ShopOffers,
                new[] { profile },
                RunBalancingSnapshot.CreateForDomainUnitTests().PreparationSchedules);

            var empService = new EmployeeTaskService(snap) { MinSkillErrorRate = 0.02f };
            var state = new PizzeriaState { RestaurantBudget = 1000 };
            var emp = empService.TryHireEmployee(state, EmployeeRole.Pizzaiolo);
            Assert.IsNotNull(emp);

            // 20% base com -10% relativo => 0.20 * 0.90 = 0.18 (18%)
            float prob = empService.ComputeEffectiveErrorProbability(emp, 0.90f);
            Assert.AreEqual(0.18f, prob, 0.001f);
        }

        [Test]
        public void T05_DescriptionFormatter_ReflectsOnlyConfiguredEffects()
        {
            var snap = LoadOrFallbackCatalogSnapshot();
            var bag = snap.FindModifierById("reinforced_bag");
            Assert.IsNotNull(bag);
            string desc = RunDescriptionFormatter.FormatModifierDescription(bag);
            StringAssert.Contains("Capacidade da mochila", desc);
            Assert.IsFalse(desc.ToLowerInvariant().Contains("resist"), "reinforced_bag nao deve mencionar resistencia nao implementada.");
        }

        [Test]
        public void T06_DomainValidator_DetectsBrokenIdsMissingEffectsAndMinGreaterThanBaseError()
        {
            var brokenMod = new RunModifierDefinition { Id = "broken", Effects = Array.Empty<RunModifierEffectData>() };
            var brokenShop = new RunShopOfferData("broken_shop", "Broken", "", true, ShopBenefitKind.GrantModifier, "missing_id", EmployeeRole.Cashier, null, 50, 0, 1, 0, ShopPhaseAvailability.PreparationAndActive);
            var brokenEmp = new EmployeeRoleProfileData(EmployeeRole.Cashier, "Caixa", 100, 3, 1f, 0, 0.05f, 0.1f, 0.20f, 2f);
            var snap = new RunBalancingSnapshot(new[] { brokenMod, brokenMod }, new[] { brokenShop }, new[] { brokenEmp }, null);

            var report = RunBalancingValidator.Validate(snap);
            Assert.IsTrue(report.HasErrors);
            Assert.GreaterOrEqual(report.ErrorCount, 4);
        }

        [Test]
        public void T07_RuntimeConsumers_VampireSpawnAndOrderArrivalRespondToMultipliers()
        {
            int normalSpawn = MonsterPopulationDirector.ComputeVampireSpawnCount(1, 1.0f);
            int bloodMoonSpawn = MonsterPopulationDirector.ComputeVampireSpawnCount(1, 1.5f);
            Assert.Greater(bloodMoonSpawn, normalSpawn);

            float normalInterval = DemandDirector.ComputeEffectiveSpawnInterval(12f, 1.0f);
            float heavyRushInterval = DemandDirector.ComputeEffectiveSpawnInterval(12f, 1.3f);
            Assert.Less(heavyRushInterval, normalInterval);
        }

        [Test]
        public void T08_NewEffectsConsumers_VampireAggressionRangeAndCustomerPatienceWorkWithoutMakingCustomersHostile()
        {
            float baseRange = 15f;
            float aggressiveRange = MonsterNpcController.ComputeEffectiveAcquisitionRange(baseRange, 1.35f, isEligibleAggressiveVampire: true);
            float peacefulCustomerRange = MonsterNpcController.ComputeEffectiveAcquisitionRange(baseRange, 1.35f, isEligibleAggressiveVampire: false);
            Assert.AreEqual(15f * 1.35f, aggressiveRange, 0.01f);
            Assert.AreEqual(baseRange, peacefulCustomerRange, 0.01f);

            var snap = LoadOrFallbackCatalogSnapshot();
            var run = new RunProgressionService(1, 3, RunDifficulty.Normal, snap);
            run.ApplyModifier(snap.FindModifierById("impatient_customers"));
            Assert.AreEqual(0.75f, run.RecalculateEffectiveStats().CustomerPatienceMultiplier, 0.01f);

            run = new RunProgressionService(1, 3, RunDifficulty.Normal, snap);
            run.ApplyModifier(snap.FindModifierById("calm_customers"));
            Assert.AreEqual(1.25f, run.RecalculateEffectiveStats().CustomerPatienceMultiplier, 0.01f);
        }

        [Test]
        public void T09_Upgrades_OvenTimerBackpackAndStaffTrainingPropagateThroughStats()
        {
            var snap = LoadOrFallbackCatalogSnapshot();
            var run = new RunProgressionService(2, 3, RunDifficulty.Normal, snap);
            run.ApplyModifier(snap.FindModifierById("oven_timer"));
            run.ApplyModifier(snap.FindModifierById("reinforced_bag"));
            run.ApplyModifier(snap.FindModifierById("reinforced_bag"));
            run.ApplyModifier(snap.FindModifierById("staff_training"));

            var stats = run.RecalculateEffectiveStats();
            Assert.IsTrue(stats.OvenAutoShutoffEnabled);
            Assert.AreEqual(2, stats.ExtraBackpackCapacity);
            Assert.AreEqual(0.75f, stats.EmployeeErrorMultiplier, 0.01f);
        }

        [Test]
        public void T10_BaseSupplyIds_Use1Massa3Molho4QueijoInteiroAndSkip2MassaAberta()
        {
            var state = new PizzeriaState { RestaurantBudget = 300 };
            state.StorageSlots[(1, 1)] = new StorageSlotState(1, 1, 0, 10);
            state.StorageSlots[(1, 2)] = new StorageSlotState(1, 2, 0, 10); // Massa Aberta (preparo, nao raw)
            state.StorageSlots[(1, 3)] = new StorageSlotState(1, 3, 0, 10);
            state.StorageSlots[(1, 4)] = new StorageSlotState(1, 4, 0, 10);

            NightCycleService.EnsureMinimumIngredientFloor(state, minUnitsPerBaseSlot: 2);
            Assert.AreEqual(2, state.StorageSlots[(1, 1)].Quantity);
            Assert.AreEqual(0, state.StorageSlots[(1, 2)].Quantity);
            Assert.AreEqual(2, state.StorageSlots[(1, 3)].Quantity);
            Assert.AreEqual(2, state.StorageSlots[(1, 4)].Quantity);
        }

        [Test]
        public void T11_BaseSuppliesShopOffer_RejectsWhenStorageFullWithoutSpendingMoney()
        {
            var snap = LoadOrFallbackCatalogSnapshot();
            var run = new RunProgressionService(3, 3, RunDifficulty.Normal, snap);
            var state = new PizzeriaState
            {
                RestaurantBudget = 500,
                CurrentNightPhase = NightPhase.PreparingNextNight,
                Revision = 10
            };
            state.StorageSlots[(1, 1)] = new StorageSlotState(1, 1, 10, 10);
            state.StorageSlots[(1, 3)] = new StorageSlotState(1, 3, 10, 10);
            state.StorageSlots[(1, 4)] = new StorageSlotState(1, 4, 10, 10);

            bool ok = run.TryPurchaseCatalogOffer(
                state,
                "base_supplies_offer",
                null,
                lines => PizzeriaCommandHandler.ApplySupplyBundleLinesServer(state, lines),
                out string failure,
                out _,
                out _);

            Assert.IsFalse(ok, "Compra sem efeito com estoque cheio deve ser rejeitada.");
            Assert.IsFalse(string.IsNullOrEmpty(failure));
            Assert.AreEqual(500, state.RestaurantBudget);
            Assert.AreEqual(0, state.NightShopSpent);
            Assert.AreEqual(10u, state.Revision);
        }

        [Test]
        public void T12_SharedMaxStacks_AcrossVoteAndShop()
        {
            var snap = LoadOrFallbackCatalogSnapshot();
            var run = new RunProgressionService(4, 3, RunDifficulty.Normal, snap);
            var state = new PizzeriaState { RestaurantBudget = 1000, CurrentNightPhase = NightPhase.PreparingNextNight };
            var bagDef = snap.FindModifierById("reinforced_bag");

            // 1 nivel por votacao + 1 por loja = 2/2 MaxStacks
            run.ApplyModifier(bagDef);
            Assert.IsTrue(run.TryPurchaseCatalogOffer(state, "reinforced_bag_offer", null, null, out _, out _, out _));
            Assert.AreEqual(2, run.GetModifierStacks("reinforced_bag"));

            // Terceira tentativa bloqueada na loja e fora do pool
            Assert.IsFalse(run.CanApplyModifierStack("reinforced_bag"));
            Assert.IsFalse(run.TryPurchaseCatalogOffer(state, "reinforced_bag_offer", null, null, out string lockReason, out _, out _));
            StringAssert.Contains("Nivel maximo", lockReason);
        }

        [Test]
        public void T13_DrawSimulator_RunsIsolated1000IterationsWithoutMutatingRunState()
        {
            var snap = LoadOrFallbackCatalogSnapshot();
            var run = new RunProgressionService(5, 3, RunDifficulty.Normal, snap);
            run.ApplyModifier(snap.FindModifierById("blood_moon"));

            var res = RunBalancingValidator.SimulateCategoryDraw(snap, RunModifierKind.Hindrance, 1, RunDifficulty.Normal, 2, 1000, 999);
            Assert.AreEqual(1000, res.Iterations);
            Assert.GreaterOrEqual(res.EligiblePoolCount, 2);
            Assert.AreEqual(1, run.ActiveModifiers.Count);
            Assert.IsTrue(run.HasModifier("blood_moon"));
        }

        [Test]
        public void T14_PreNight1Preparation_OpensBeforeNight1AndPausesNightClock()
        {
            var snap = LoadOrFallbackCatalogSnapshot();
            var run = new RunProgressionService(6, 3, RunDifficulty.Normal, snap);
            var state = new PizzeriaState
            {
                CurrentNightNumber = 0,
                TargetNightNumber = 1,
                CurrentNightPhase = NightPhase.PreparingNextNight,
                NightRemainingSeconds = 600f
            };
            run.OpenPreparationStages(1);

            Assert.GreaterOrEqual(run.CurrentVoteStages.Count, 1);
            Assert.AreEqual(1, state.TargetNightNumber);
            NightCycleService.TickNight(state, 30f);
            Assert.AreEqual(NightPhase.PreparingNextNight, state.CurrentNightPhase);
            Assert.AreEqual(600f, state.NightRemainingSeconds, 0.001f);
        }

        [Test]
        public void T15_MultiStageVoting_Night4ThreeEventsAndMandatoryCategoryBlock()
        {
            var snap = LoadOrFallbackCatalogSnapshot();
            var run = new RunProgressionService(7, 5, RunDifficulty.Normal, snap);
            var schedNight4 = new NightPreparationScheduleData(4, mandatoryHindranceStages: 1, hindranceAlternatives: 2, opportunityStages: 1, opportunityAlternatives: 2, additionalEventStages: 3, eventAlternatives: 2);
            var stages = run.OpenPreparationStages(4, schedNight4);

            Assert.AreEqual(5, stages.Count); // 1 entrave + 1 beneficio + 3 eventos
            Assert.IsTrue(stages[0].IsMandatory);
            Assert.AreEqual(RunModifierKind.Hindrance, stages[0].Category);
            Assert.IsFalse(stages[1].IsMandatory);
            Assert.AreEqual(RunModifierKind.Opportunity, stages[1].Category);
            Assert.AreEqual(RunModifierKind.Event, stages[2].Category);
            Assert.AreEqual(RunModifierKind.Event, stages[3].Category);
            Assert.AreEqual(RunModifierKind.Event, stages[4].Category);

            run.SetPlayerReady(0UL, true);
            Assert.IsFalse(run.AreAllMandatoryStagesVoted());
            Assert.IsFalse(run.CanStartNight(new[] { 0UL }));

            Assert.IsTrue(run.CastStageVote(0UL, 0, stages[0].Options[0].Id));
            Assert.IsTrue(run.AreAllMandatoryStagesVoted());
            var winners = run.ResolveAllStagesServer();
            Assert.GreaterOrEqual(winners.Count, 4); // 1 entrave votado + 3 eventos (beneficio sem voto opcional fica fora)
            Assert.IsTrue(run.CanStartNight(new[] { 0UL }));
        }

        [Test]
        public void T16_ConnectedHumanDisconnect_DoesNotDeadlockPreparationReadyCheck()
        {
            var snap = LoadOrFallbackCatalogSnapshot();
            var run = new RunProgressionService(8, 3, RunDifficulty.Normal, snap);
            var stages = run.OpenPreparationStages(1);

            run.CastStageVote(0UL, 0, stages[0].Options[0].Id);
            run.SetPlayerReady(0UL, true);
            Assert.IsFalse(run.CanStartNight(new[] { 0UL, 2UL }));

            run.OnParticipantDisconnected(2UL);
            run.ResolveAllStagesServer();
            Assert.IsTrue(run.CanStartNight(new[] { 0UL }));
        }

        [Test]
        public void T17_ServerDtosAndPresenters_ProjectDynamicCostsAndDurationTags()
        {
            var snap = LoadOrFallbackCatalogSnapshot();
            var ovenMod = snap.FindModifierById("oven_timer");
            var rushMod = snap.FindModifierById("heavy_rush");
            Assert.AreEqual("Permanente (RUN)", RunDescriptionFormatter.FormatDurationTag(ovenMod.Duration));
            Assert.AreEqual("1 Noite", RunDescriptionFormatter.FormatDurationTag(rushMod.Duration));

            var offer = snap.FindShopOfferById("oven_timer_offer");
            var run = new RunProgressionService(9, 3, RunDifficulty.Normal, snap);
            var state = new PizzeriaState { RestaurantBudget = 500, CurrentNightPhase = NightPhase.PreparingNextNight };
            Assert.IsTrue(run.TryEvaluateShopOffer(state, offer, null, out int cost, out string lockReason));
            Assert.AreEqual(offer.BaseCost, cost);
            Assert.IsTrue(string.IsNullOrEmpty(lockReason));
        }

        [Test]
        public void T18_ValidatorBaselineCatalog_HasZeroErrorsAcrossAllTabs()
        {
            var snap = LoadOrFallbackCatalogSnapshot();
            var rep = RunBalancingValidator.Validate(snap);
            Assert.IsFalse(rep.HasErrors, rep.BuildSummaryText());
        }

        [Test]
        public void T19_NoiteDeTestePreset_SelectableAndConfiguredWithoutShiftTimeout()
        {
            var preset = AssetDatabase.LoadAssetAtPath<PizzeriaRunPreset>(PlaytestPresetBuilder.NoiteDeTestePresetPath);
            Assert.IsNotNull(preset);
            Assert.AreEqual("NoiteDeTeste", preset.presetId);
            Assert.IsTrue(preset.unlimitedShiftTime);
            Assert.IsTrue(preset.allowDebugPanel);
            Assert.IsTrue(preset.ToDomainData().UnlimitedShiftTime);
        }

        [Test]
        public void T20_UnlimitedShiftTime_DoesNotTerminateNightWhenElapsedExceedsDuration()
        {
            var state = new PizzeriaState
            {
                CurrentNightNumber = 1,
                CurrentNightPhase = NightPhase.Active,
                NightDurationSeconds = 600f,
                NightRemainingSeconds = 600f,
                UnlimitedShiftTime = true,
                NightEarnedRevenue = 0,
                NightRevenueGoal = 60
            };

            for (int i = 0; i < 100; i++)
            {
                NightCycleService.TickNight(state, 10f); // 1000s > 600s
            }

            Assert.AreEqual(NightPhase.Active, state.CurrentNightPhase, "Turno ilimitado nao deve encerrar por tempo.");
            Assert.AreEqual(1000f, state.NightElapsedSeconds, 0.1f);
            Assert.AreEqual(600f, state.NightRemainingSeconds, 0.1f);
        }

        [Test]
        public void T21_UnlimitedShiftTime_OtherNightRulesReputationZeroDefeatAndTimersRemainActive()
        {
            var state = new PizzeriaState
            {
                CurrentNightNumber = 1,
                CurrentNightPhase = NightPhase.Active,
                NightDurationSeconds = 600f,
                NightRemainingSeconds = 600f,
                UnlimitedShiftTime = true,
                Reputation = 10
            };

            NightCycleService.AdjustReputation(state, -15, "Cliente perdeu paciencia");
            Assert.AreEqual(0, state.Reputation);
            Assert.AreEqual(NightPhase.Defeat, state.CurrentNightPhase, "Reputacao zerada continua encerrando turno mesmo no preset Sem Limite.");
        }

        [Test]
        public void T22_NightGoalParameters_PropagatesUnlimitedFlagFromPresetThroughResolver()
        {
            var preset = AssetDatabase.LoadAssetAtPath<PizzeriaRunPreset>(PlaytestPresetBuilder.NoiteDeTestePresetPath);
            Assert.IsNotNull(preset);
            var resolved = NightParameterResolver.ResolveEffectiveParameters(1, 2, RunDifficulty.Normal, preset.ToDomainData());
            Assert.IsTrue(resolved.UnlimitedShiftTime);
            Assert.IsTrue(resolved.ToNightGoalParameters().UnlimitedShiftTime);
        }

        [Test]
        public void T23_GameViewOverlaysF8AndF6_NoLongerAutoInstalledOrDrawnInScenes()
        {
            var originalScene = EditorSceneManager.GetActiveScene().path;
            string[] scenes =
            {
                "Assets/ThirdParty/StarterAssets/ThirdPersonController/Scenes/GameScene.unity",
                "Assets/Piaaria/Scenes/GameplayVariants/GameScene_CelShaded.unity"
            };

            foreach (string path in scenes)
            {
                var sc = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                bool hasDebugCentral = false;
                foreach (var root in sc.GetRootGameObjects())
                {
                    if (root.GetComponentInChildren<PlaytestDebugController>(true) != null)
                        hasDebugCentral = true;
                    Assert.IsNull(root.GetComponentInChildren<PlaytestDiagnosticsPresenter>(true), $"{path} ainda possui PlaytestDiagnosticsPresenter [F8].");
                }
                Assert.IsTrue(hasDebugCentral, $"{path} deve conter [Debug] Central de Playtest com PlaytestDebugController.");
            }

            if (!string.IsNullOrEmpty(originalScene))
                EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
        }

        [Test]
        public void T24_OdinPlaytestCentralMenuItem_CompliantWithToolsOrganization()
        {
            var method = typeof(Playtest.PlaytestDebugMenu).GetMethod("SelectPlaytestCentralInInspector");
            Assert.IsNotNull(method);
            var attrs = method.GetCustomAttributes(typeof(MenuItem), false);
            Assert.AreEqual(1, attrs.Length);
            var mi = (MenuItem)attrs[0];
            StringAssert.StartsWith("Tools/Scary Party/Uso Comum/Pizzaria/", mi.menuItem);
            Assert.AreEqual(118, mi.priority);
        }

        [Test]
        public void T25_HostInEditor_CanExecuteDebugMutationsOnAnyPresetIncludingEntrega()
        {
            var state = new PizzeriaState { AllowDebugPanel = false }; // Entrega_3Noites
            Assert.IsTrue(PizzeriaCommandHandler.CanExecuteHostDebugMutation(true, 0UL, 0UL, state, isEditorEnvironment: true));
            state.AllowDebugPanel = true;
            Assert.IsTrue(PizzeriaCommandHandler.CanExecuteHostDebugMutation(true, 0UL, 0UL, state, isEditorEnvironment: true));
        }

        [Test]
        public void T26_RemoteClientsAndNonEditorBuilds_StrictlyRejectedEvenInNoiteDeTeste()
        {
            var state = new PizzeriaState { AllowDebugPanel = true };
            Assert.IsFalse(PizzeriaCommandHandler.CanExecuteHostDebugMutation(true, 2UL, 0UL, state, isEditorEnvironment: true), "Cliente remoto bloqueado.");
            Assert.IsFalse(PizzeriaCommandHandler.CanExecuteHostDebugMutation(false, 0UL, 0UL, state, isEditorEnvironment: true), "Nao-servidor bloqueado.");
            Assert.IsFalse(PizzeriaCommandHandler.CanExecuteHostDebugMutation(true, 0UL, 0UL, state, isEditorEnvironment: false), "Build normal bloqueada.");
        }

        [Test]
        public void T27_CheckpointPrePreparation_RestoresNightPhaseModifiersAndBudget()
        {
            var snap = LoadOrFallbackCatalogSnapshot();
            var run = new RunProgressionService(11, 3, RunDifficulty.Normal, snap);
            var emp = new EmployeeTaskService(snap);
            var store = new PlaytestCheckpointStore();
            var state = new PizzeriaState
            {
                CurrentNightNumber = 0,
                TargetNightNumber = 1,
                CurrentNightPhase = NightPhase.PreparingNextNight,
                RestaurantBudget = 500
            };
            state.StorageSlots[(1, 1)] = new StorageSlotState(1, 1, 5, 20);
            store.Capture(PlaytestCheckpointKind.PrePreparation, state, run, emp, 10.0);

            state.RestaurantBudget = 120;
            state.StorageSlots[(1, 1)].Quantity = 1;
            state.CurrentNightPhase = NightPhase.Defeat;

            Assert.IsTrue(store.TryRestore(PlaytestCheckpointKind.PrePreparation, state, run, emp, 50.0, preserveCurrentBudget: false));
            Assert.AreEqual(NightPhase.PreparingNextNight, state.CurrentNightPhase);
            Assert.AreEqual(1, state.TargetNightNumber);
            Assert.AreEqual(500, state.RestaurantBudget);
            Assert.AreEqual(5, state.StorageSlots[(1, 1)].Quantity);
        }

        [Test]
        public void T28_CheckpointNightStart_PreservesCurrentBudgetWhenRequested()
        {
            var snap = LoadOrFallbackCatalogSnapshot();
            var run = new RunProgressionService(12, 3, RunDifficulty.Normal, snap);
            var emp = new EmployeeTaskService(snap);
            var store = new PlaytestCheckpointStore();
            var state = new PizzeriaState
            {
                CurrentNightNumber = 2,
                TargetNightNumber = 2,
                CurrentNightPhase = NightPhase.Active,
                RestaurantBudget = 640,
                NightDurationSeconds = 720f,
                NightRemainingSeconds = 720f
            };
            run.ApplyModifier(snap.FindModifierById("oven_timer"));
            store.Capture(PlaytestCheckpointKind.NightStart, state, run, emp, 20.0);

            state.RestaurantBudget = 999;
            state.NightRemainingSeconds = 5f;
            state.CurrentNightPhase = NightPhase.Defeat;

            Assert.IsTrue(store.TryRestore(PlaytestCheckpointKind.NightStart, state, run, emp, 80.0, preserveCurrentBudget: true));
            Assert.AreEqual(NightPhase.Active, state.CurrentNightPhase);
            Assert.AreEqual(999, state.RestaurantBudget);
            Assert.AreEqual(720f, state.NightRemainingSeconds, 0.01f);
            Assert.IsTrue(run.HasModifier("oven_timer"));
        }

        [Test]
        public void T29_RebaseRelativeDeadlines_ProtectsOrdersAndSuppliesFromSpuriousTimeout()
        {
            var state = new PizzeriaState();
            var supplyId = new SupplyOrderId(1);
            state.SupplyOrders[supplyId] = new SupplyOrderState(supplyId, 10.0, 30.0, 5);
            var orderId = new OrderId(1);
            state.Orders[orderId] = new OrderState(orderId, OrderSourceType.Phone, new AddressId(1), CustomerId.None, 10.0, 70.0);

            PlaytestCheckpointStore.RebaseRelativeDeadlines(state, 100.0);
            Assert.AreEqual(130.0, state.SupplyOrders[supplyId].ExpectedArrivalTime, 0.001);
            Assert.AreEqual(170.0, state.Orders[orderId].DeadlineTime, 0.001);
        }

        [Test]
        public void T30_ShiftClockPauseAndSpeedMultiplier_ControlShiftTimeIndependently()
        {
            var state = new PizzeriaState
            {
                CurrentNightPhase = NightPhase.Active,
                NightRemainingSeconds = 300f,
                NightElapsedSeconds = 0f,
                IsShiftClockPaused = true,
                ShiftClockSpeedMultiplier = 2.0f
            };

            NightCycleService.TickNight(state, 10f);
            Assert.AreEqual(300f, state.NightRemainingSeconds, 0.001f);
            Assert.AreEqual(0f, state.NightElapsedSeconds, 0.001f);

            state.IsShiftClockPaused = false;
            NightCycleService.TickNight(state, 10f);
            Assert.AreEqual(280f, state.NightRemainingSeconds, 0.001f);
            Assert.AreEqual(20f, state.NightElapsedSeconds, 0.001f);
        }

        [Test]
        public void T31_WorldLightingAndShiftClock_RemainDecoupledByDefault()
        {
            var go = new GameObject("TestLightingDirector");
            try
            {
                var director = go.AddComponent<TimeOfDayLightingDirector>();
                director.SetClockHours(17f, evaluateNow: false);
                var state = new PizzeriaState { CurrentNightPhase = NightPhase.Active, NightRemainingSeconds = 500f };

                NightCycleService.TickNight(state, 20f);
                Assert.AreEqual(480f, state.NightRemainingSeconds, 0.01f);
                Assert.AreEqual(17f, director.currentClockHours, 0.01f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void T32_PlaytestDebugController_OptionalWorldSyncIsOffByDefault()
        {
            var go = new GameObject("TestPlaytestCentral");
            try
            {
                var ctrl = go.AddComponent<PlaytestDebugController>();
                Assert.IsFalse(ctrl.optionalSyncLightingWithShift);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void T33_RealNightOutcomeVsForcedDebugOutcome_MarkedDistinctly()
        {
            var state = new PizzeriaState
            {
                CurrentNightPhase = NightPhase.Active,
                NightRemainingSeconds = 1f,
                NightRevenueGoal = 60,
                NightEarnedRevenue = 100
            };

            NightCycleService.TickNight(state, 2f);
            Assert.AreEqual(NightPhase.Results, state.CurrentNightPhase);
            Assert.IsFalse(state.IsForcedDebugOutcome);

            state.IsForcedDebugOutcome = true;
            Assert.IsTrue(state.IsForcedDebugOutcome);
        }

        [Test]
        public void T34_FullRunReset_ClearsModifiersEmployeesAndBumpsGenerationId()
        {
            var snap = LoadOrFallbackCatalogSnapshot();
            var run = new RunProgressionService(14, 3, RunDifficulty.Normal, snap);
            var emp = new EmployeeTaskService(snap);
            var state = new PizzeriaState { GenerationId = 3 };

            run.ApplyModifier(snap.FindModifierById("oven_timer"));
            emp.TryHireEmployee(state, EmployeeRole.Cashier);

            run.ResetRun(state, 999, 3, RunDifficulty.Normal);
            emp.ResetForNewRun();

            Assert.AreEqual(4u, state.GenerationId);
            Assert.AreEqual(0, run.ActiveModifiers.Count);
            Assert.AreEqual(0, emp.Employees.Count);
        }

        [Test]
        public void T35_NightGoalMultiplier_ComposesWithHumanScaleAndDifficulty()
        {
            var goalEffect = new RunModifierEffectData(RunEffectTarget.NightRevenueGoal, RunEffectOperation.PercentDelta, RunEffectUnit.RelativePercent, 25f);
            var mod = new RunModifierDefinition { Id = "high_quota", Effects = new[] { goalEffect } };
            var snap = new RunBalancingSnapshot(new[] { mod }, null, null, null);
            var run = new RunProgressionService(15, 3, RunDifficulty.Normal, snap);
            run.ApplyModifier(mod);

            float mult = run.RecalculateEffectiveStats().NightGoalMultiplier;
            Assert.AreEqual(1.25f, mult, 0.001f);

            var resolved = NightParameterResolver.ResolveEffectiveParameters(1, 2, RunDifficulty.Normal, null, null, 600f, 15, mult);
            // Night 1 base=60 * 2P(1.6) * 1.25 = 120
            Assert.AreEqual(120, resolved.FinalGoal);
        }

        [Test]
        public void T36_ActiveManifestAndConfig_HaveZeroMissingCoreReferences()
        {
            var manifest = AssetDatabase.LoadAssetAtPath<PlaytestReferenceManifest>(RunBalancingMigrationRunner.ManifestAssetPath);
            Assert.IsNotNull(manifest);
            Assert.IsNotNull(manifest.activeBalancingCatalog);
            Assert.IsNotNull(manifest.activePizzeriaConfig);
            Assert.AreSame(manifest.activePizzeriaConfig.balancingCatalog, manifest.activeBalancingCatalog);
        }
    }
}
