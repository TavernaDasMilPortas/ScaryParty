using NUnit.Framework;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;
using Unity.Collections;
using UnityEngine;

namespace ScaryParty.Pizzeria.Editor.Tests
{
    [TestFixture]
    public class RunIntermissionNetworkTests
    {
        [Test]
        public void NetRunStateDto_IntermissionFieldsParticipateInEquality()
        {
            var a = new NetRunStateDto
            {
                CurrentRunNight = 1,
                TargetNightNumber = 2,
                IntermissionStage = 3,
                RevealStartedAtServerTime = 100.0,
                CountdownStartsAtServerTime = 104.5,
                StartNightAtServerTime = 107.5,
                WinningHindranceId = new FixedString64Bytes("vampire_attack"),
                WinningOpportunityId = new FixedString64Bytes("calm_shift"),
                Revision = 10
            };

            var b = a;
            Assert.IsTrue(a.Equals(b));

            b.IntermissionStage = 4;
            Assert.IsFalse(a.Equals(b));
        }

        [Test]
        public void NetPlayerIntermissionDto_TracksPerCategoryVotesAndReadyState()
        {
            var p1 = new NetPlayerIntermissionDto
            {
                ClientId = 1,
                PlayerName = new FixedString32Bytes("Chef"),
                HasVotedHindrance = true,
                HasVotedOpportunity = true,
                IsReady = true,
                VotedHindranceId = new FixedString64Bytes("heavy_rush"),
                VotedOpportunityId = new FixedString64Bytes("express_supply")
            };

            var p2 = p1;
            Assert.IsTrue(p1.Equals(p2));

            p2.IsReady = false;
            Assert.IsFalse(p1.Equals(p2));
        }

        [Test]
        public void UIToolkitTweenService_KillTweens_ClearsActiveTweens()
        {
            var el = new UnityEngine.UIElements.VisualElement();
            ScaryParty.UI.Piaria.Core.UIToolkitTweenService.DropInCard(el, 60f, 0.2f);
            ScaryParty.UI.Piaria.Core.UIToolkitTweenService.KillTweens(el);
            Assert.AreEqual(0, ScaryParty.UI.Piaria.Core.UIToolkitTweenService.GetActiveTweenCount(el));
        }

        [Test]
        public void UIToolkitTweenService_KillTweensRecursive_ClearsDescendantTweens()
        {
            var parent = new UnityEngine.UIElements.VisualElement();
            var child = new UnityEngine.UIElements.VisualElement();
            parent.Add(child);

            ScaryParty.UI.Piaria.Core.UIToolkitTweenService.DropInCard(child, 40f, 0.3f);
            Assert.Greater(ScaryParty.UI.Piaria.Core.UIToolkitTweenService.GetActiveTweenCount(child), 0);

            ScaryParty.UI.Piaria.Core.UIToolkitTweenService.KillTweensRecursive(parent);
            Assert.AreEqual(0, ScaryParty.UI.Piaria.Core.UIToolkitTweenService.GetActiveTweenCount(child));
        }

        [Test]
        public void Intermission_FrozenParticipantsAndShopLockGuards_WorkAsExpected()
        {
            var snapshot = RunBalancingSnapshot.CreateForDomainUnitTests();
            var run = new RunProgressionService(12345, 3, RunDifficulty.Normal, snapshot);
            var state = new PizzeriaState { RestaurantBudget = 1000, CurrentNightPhase = NightPhase.PreparingNextNight };
            run.OpenTypedBallots(1, 3, 3);

            string hOpt = run.CurrentVoteStages[0].Options[0].Id;
            string oOpt = run.CurrentVoteStages[1].Options[0].Id;
            Assert.IsTrue(run.CastCategoryVote(0UL, RunModifierKind.Hindrance, hOpt));
            Assert.IsTrue(run.CastCategoryVote(0UL, RunModifierKind.Opportunity, oOpt));
            Assert.IsTrue(run.TrySetPlayerReady(0UL, true, out _));
            Assert.IsTrue(run.HasAllParticipantsVotedMandatoryStages(new ulong[] { 0UL }));
            Assert.IsFalse(run.HasAllParticipantsVotedMandatoryStages(new ulong[] { 0UL, 1UL }));

            Assert.IsTrue(run.TryLockAndResolveBallotsServer(100.0, _ => 0, 4.5f, 3.0f, new ulong[] { 0UL }));
            // Late joiner (clientId=2) does not block locked participants from starting
            Assert.IsTrue(run.CanStartNight(new ulong[] { 0UL, 2UL }));

            // Shop blocked during lock
            var offer = snapshot.ShopOffers[0];
            Assert.IsFalse(run.TryEvaluateShopOffer(state, offer, null, out _, out string lockReason));
            Assert.IsNotEmpty(lockReason);
        }

        [Test]
        public void PizzeriaFootprint_ExcludesExteriorPatioFromInteriorCheck()
        {
            var go = new GameObject("TestFootprint");
            try
            {
                var fp = go.AddComponent<PizzeriaFootprint>();
                Vector3 insideSalao = go.transform.TransformPoint(new Vector3(10f, fp.floorSurfaceLocalY + 0.5f, 0f));
                Vector3 insidePatio = go.transform.TransformPoint(new Vector3(8f, fp.floorSurfaceLocalY + 0.5f, -20f));

                Assert.IsTrue(fp.IsWorldPositionInsideInterior(insideSalao));
                Assert.IsFalse(fp.IsWorldPositionInsideInterior(insidePatio));
                Assert.IsTrue(fp.IsWorldPositionInExteriorPatio(insidePatio));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SupplyBundle_AggregatesDuplicateIngredients_AndIsAtomicOnInsufficientCapacity()
        {
            var state = new PizzeriaState();
            var slotKey = (1, 10);
            state.StorageSlots[slotKey] = new StorageSlotState(1, 10, 2, 10);

            // Duas linhas do mesmo ingrediente (4 + 4 = 8): cabe exatamente (2 + 8 = 10)
            var exactLines = new[]
            {
                new SupplyBundleLineData(10, 4),
                new SupplyBundleLineData(10, 4)
            };
            Assert.IsTrue(PizzeriaCommandHandler.CanFitFullSupplyBundleServer(state, exactLines, out _));

            // Uma unidade a mais (4 + 5 = 9): deve falhar atomicamente sem mutar o estoque
            var overLines = new[]
            {
                new SupplyBundleLineData(10, 4),
                new SupplyBundleLineData(10, 5)
            };
            Assert.IsFalse(PizzeriaCommandHandler.CanFitFullSupplyBundleServer(state, overLines, out string reason));
            Assert.IsNotEmpty(reason);
            Assert.AreEqual(0, PizzeriaCommandHandler.ApplySupplyBundleLinesServer(state, overLines));
            Assert.AreEqual(2, state.StorageSlots[slotKey].Quantity);

            // Aplicando o pacote exato: deve entregar todas as 8 unidades
            Assert.AreEqual(8, PizzeriaCommandHandler.ApplySupplyBundleLinesServer(state, exactLines));
            Assert.AreEqual(10, state.StorageSlots[slotKey].Quantity);
        }

        [Test]
        public void ResourcesUiCatalog_ContainsIntermissionTemplatesAndSettings_ForBuildParity()
        {
            var catalog = Resources.Load<ScaryParty.UI.Piaria.Core.PiariaUIAssetCatalog>("PiariaUIAssetCatalog");
            Assert.IsNotNull(catalog, "Assets/Resources/PiariaUIAssetCatalog.asset precisa existir para builds standalone.");
            Assert.IsNotNull(catalog.panelSettings, "PanelSettings ausente no catálogo de Resources.");
            Assert.IsNotNull(catalog.nightIntermissionTheme, "nightIntermissionTheme ausente no catálogo de Resources.");
            Assert.IsNotNull(catalog.nightIntermissionIconCatalog, "nightIntermissionIconCatalog ausente no catálogo de Resources.");
            Assert.IsNotNull(catalog.templateNightIntermissionScreen, "templateNightIntermissionScreen ausente no catálogo de Resources.");
            Assert.IsNotNull(catalog.templateNightSummaryPage, "templateNightSummaryPage ausente no catálogo de Resources.");
            Assert.IsNotNull(catalog.templateModifierVotePage, "templateModifierVotePage ausente no catálogo de Resources.");
            Assert.IsNotNull(catalog.templateShopPreparationPage, "templateShopPreparationPage ausente no catálogo de Resources.");
            Assert.IsNotNull(catalog.templateLotteryRevealPage, "templateLotteryRevealPage ausente no catálogo de Resources.");
            Assert.IsNotNull(catalog.templateOperationsStatusOverlay, "templateOperationsStatusOverlay ausente no catálogo de Resources.");
        }
    }
}
