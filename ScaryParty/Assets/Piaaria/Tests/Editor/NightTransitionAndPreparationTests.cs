using System.Collections.Generic;
using NUnit.Framework;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Tests
{
    [TestFixture]
    public class NightTransitionAndPreparationTests
    {
        [Test]
        public void Night0Victory_OpensPreparationForNight1_AndBlocksStartUntilMandatoryVotesAndReady()
        {
            var snapshot = RunBalancingSnapshot.CreateForDomainUnitTests();
            var run = new RunProgressionService(12345, 3, RunDifficulty.Normal, snapshot);
            var state = new PizzeriaState { RestaurantBudget = 500 };

            // 1. Iniciar Noite 0 (Tutorial) e vencer
            NightCycleService.StartNight(state, 0);
            Assert.AreEqual(0, state.CurrentNightNumber);
            state.NightEarnedRevenue = 15;
            state.NightPizzasDelivered = 1;
            NightCycleService.EvaluateNightOutcome(state);
            Assert.AreEqual(NightPhase.Results, state.CurrentNightPhase);

            // 2. Transição para preparação da Noite 1 (espelha SkipTutorialToNight1ServerRpc -> OpenPreparationBeforeNightServer(1))
            run.ResetRun(state, 12345, 3, RunDifficulty.Normal, 500, 100);
            Assert.AreEqual(IntermissionStage.None, run.IntermissionStage);

            state.CurrentNightPhase = NightPhase.PreparingNextNight;
            state.CurrentNightNumber = 0;
            state.TargetNightNumber = 1;
            run.OpenTypedBallots(1, 3, 3, respectCatalogSchedule: true);

            Assert.AreEqual(NightPhase.PreparingNextNight, state.CurrentNightPhase);
            Assert.AreEqual(0, state.CurrentNightNumber);
            Assert.AreEqual(1, state.TargetNightNumber);
            Assert.AreEqual(IntermissionStage.Open, run.IntermissionStage);
            Assert.GreaterOrEqual(run.CurrentVoteStages.Count, 2);

            var participants = new ulong[] { 0UL, 1UL };

            // 3. Sem votos e sem pronto, não pode iniciar Noite 1
            Assert.IsFalse(run.TrySetPlayerReady(0UL, true, out _));
            Assert.IsFalse(run.CanStartNight(participants));

            // 4. Apenas 1 jogador vota e marca pronto: ainda bloqueado para equipe de 2
            string hOpt = run.CurrentVoteStages[0].Options[0].Id;
            string oOpt = run.CurrentVoteStages[1].Options[0].Id;
            Assert.IsTrue(run.CastCategoryVote(0UL, RunModifierKind.Hindrance, hOpt));
            Assert.IsTrue(run.CastCategoryVote(0UL, RunModifierKind.Opportunity, oOpt));
            Assert.IsTrue(run.TrySetPlayerReady(0UL, true, out _));
            Assert.IsFalse(run.HasAllParticipantsVotedMandatoryStages(participants));
            Assert.IsFalse(run.CanStartNight(participants));

            // 5. Segundo jogador vota e marca pronto -> trava urnas -> reveal -> countdown -> starting
            Assert.IsTrue(run.CastCategoryVote(1UL, RunModifierKind.Hindrance, hOpt));
            Assert.IsTrue(run.CastCategoryVote(1UL, RunModifierKind.Opportunity, oOpt));
            Assert.IsTrue(run.TrySetPlayerReady(1UL, true, out _));
            Assert.IsTrue(run.HasAllParticipantsVotedMandatoryStages(participants));
            Assert.IsTrue(run.TryLockAndResolveBallotsServer(100.0, _ => 0, 4.5f, 3.0f, participants));
            Assert.AreEqual(IntermissionStage.Revealing, run.IntermissionStage);

            run.TickIntermissionServer(105.0, out bool shouldStartDuringReveal);
            Assert.IsFalse(shouldStartDuringReveal);
            Assert.AreEqual(IntermissionStage.Countdown, run.IntermissionStage);

            run.TickIntermissionServer(108.0, out bool shouldStartAtEnd);
            Assert.IsTrue(shouldStartAtEnd);
            Assert.AreEqual(IntermissionStage.Starting, run.IntermissionStage);
            Assert.IsTrue(run.CanStartNight(participants));

            // 6. Inicia Noite 1 e reseta contadores de turno
            NightCycleService.StartNight(state, 1);
            run.ResetIntermissionState();

            Assert.AreEqual(1, state.CurrentNightNumber);
            Assert.AreEqual(1, state.TargetNightNumber);
            Assert.AreEqual(0, state.NightPizzasDelivered);
            Assert.AreEqual(IntermissionStage.None, run.IntermissionStage);
            Assert.IsTrue(state.CurrentNightPhase == NightPhase.Preparing || state.CurrentNightPhase == NightPhase.Active);
        }

        [Test]
        public void Night1ToNight2_ResetsDeliveredPizzas_AndResetRunAndCheckpointClearIntermissionStage()
        {
            var snapshot = RunBalancingSnapshot.CreateForDomainUnitTests();
            var run = new RunProgressionService(99, 3, RunDifficulty.Normal, snapshot);
            var state = new PizzeriaState { RestaurantBudget = 500 };

            NightCycleService.StartNight(state, 1);
            state.NightPizzasDelivered = 4;
            state.NightEarnedRevenue = 60;
            NightCycleService.EvaluateNightOutcome(state);
            Assert.AreEqual(NightPhase.Results, state.CurrentNightPhase);

            // Abrir preparação da Noite 2 e avançar até Reveal antes de testar ResetRun / RestoreFromCheckpoint
            state.CurrentNightPhase = NightPhase.PreparingNextNight;
            state.TargetNightNumber = 2;
            run.OpenTypedBallots(2, 3, 3, respectCatalogSchedule: true);
            string hOpt = run.CurrentVoteStages[0].Options[0].Id;
            string oOpt = run.CurrentVoteStages[1].Options[0].Id;
            Assert.IsTrue(run.CastCategoryVote(0UL, RunModifierKind.Hindrance, hOpt));
            Assert.IsTrue(run.CastCategoryVote(0UL, RunModifierKind.Opportunity, oOpt));
            Assert.IsTrue(run.TrySetPlayerReady(0UL, true, out _));
            Assert.IsTrue(run.TryLockAndResolveBallotsServer(10.0, _ => 0, 4.5f, 3.0f, new ulong[] { 0UL }));
            Assert.AreEqual(IntermissionStage.Revealing, run.IntermissionStage);

            // RestoreFromCheckpoint deve limpar IntermissionStage (BUG-FLOW-08)
            run.RestoreFromCheckpoint(3, RunDifficulty.Normal, new List<(string, int)>(), new Dictionary<string, int>());
            Assert.AreEqual(IntermissionStage.None, run.IntermissionStage);
            Assert.IsFalse(run.IsIntermissionLocked);

            // ResetRun deve limpar IntermissionStage (BUG-FLOW-08)
            run.OpenTypedBallots(2, 3, 3, respectCatalogSchedule: true);
            Assert.IsTrue(run.CastCategoryVote(0UL, RunModifierKind.Hindrance, run.CurrentVoteStages[0].Options[0].Id));
            Assert.IsTrue(run.CastCategoryVote(0UL, RunModifierKind.Opportunity, run.CurrentVoteStages[1].Options[0].Id));
            Assert.IsTrue(run.TrySetPlayerReady(0UL, true, out _));
            Assert.IsTrue(run.TryLockAndResolveBallotsServer(20.0, _ => 0, 4.5f, 3.0f, new ulong[] { 0UL }));
            run.ResetRun(state, 123, 3, RunDifficulty.Normal, 500, 100);
            Assert.AreEqual(IntermissionStage.None, run.IntermissionStage);

            // Iniciar Noite 2 zera NightPizzasDelivered (BUG-FLOW-07)
            NightCycleService.StartNight(state, 2);
            Assert.AreEqual(2, state.CurrentNightNumber);
            Assert.AreEqual(0, state.NightPizzasDelivered);
        }
    }
}
