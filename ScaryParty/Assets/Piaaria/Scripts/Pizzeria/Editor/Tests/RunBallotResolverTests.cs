using System;
using System.Collections.Generic;
using NUnit.Framework;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Services;

namespace ScaryParty.Pizzeria.Editor.Tests
{
    [TestFixture]
    public class RunBallotResolverTests
    {
        [Test]
        public void ResolveWeighted_RespectsProportionalTicketDistribution()
        {
            var candidates = new[]
            {
                new BallotCandidate("opt_a", 3),
                new BallotCandidate("opt_b", 1)
            };

            Assert.AreEqual("opt_a", RunBallotResolver.ResolveWeighted(candidates, _ => 0).WinningOptionId);
            Assert.AreEqual("opt_a", RunBallotResolver.ResolveWeighted(candidates, _ => 1).WinningOptionId);
            Assert.AreEqual("opt_a", RunBallotResolver.ResolveWeighted(candidates, _ => 2).WinningOptionId);
            Assert.AreEqual("opt_b", RunBallotResolver.ResolveWeighted(candidates, _ => 3).WinningOptionId);
        }

        [Test]
        public void ResolveWeighted_UsesUniformFallbackWhenZeroVotes()
        {
            var candidates = new[]
            {
                new BallotCandidate("opt_a", 0),
                new BallotCandidate("opt_b", 0),
                new BallotCandidate("opt_c", 0)
            };

            var res = RunBallotResolver.ResolveWeighted(candidates, _ => 2);
            Assert.IsTrue(res.UsedUniformFallback);
            Assert.AreEqual(0, res.TotalTickets);
            Assert.AreEqual("opt_c", res.WinningOptionId);
        }

        [Test]
        public void ResolveWeighted_EqualVotesHaveNoBiasToFirstOption()
        {
            var candidates = new[]
            {
                new BallotCandidate("opt_a", 1),
                new BallotCandidate("opt_b", 1),
                new BallotCandidate("opt_c", 1)
            };

            Assert.AreEqual("opt_a", RunBallotResolver.ResolveWeighted(candidates, _ => 0).WinningOptionId);
            Assert.AreEqual("opt_b", RunBallotResolver.ResolveWeighted(candidates, _ => 1).WinningOptionId);
            Assert.AreEqual("opt_c", RunBallotResolver.ResolveWeighted(candidates, _ => 2).WinningOptionId);
        }

        [Test]
        public void OpenTypedBallots_EnforcesTwoMandatoryVotesBeforeReady()
        {
            var snapshot = RunBalancingSnapshot.CreateForDomainUnitTests();
            var service = new RunProgressionService(snapshot);
            service.OpenTypedBallots(2, 3, 3);

            Assert.AreEqual(IntermissionStage.Open, service.IntermissionStage);
            Assert.AreEqual(2, service.CurrentVoteStages.Count);

            Assert.IsFalse(service.TrySetPlayerReady(10UL, true, out var reasonMissingBoth));
            Assert.IsNotEmpty(reasonMissingBoth);

            string hindranceOpt = service.CurrentVoteStages[0].Options[0].Id;
            Assert.IsTrue(service.CastCategoryVote(10UL, RunModifierKind.Hindrance, hindranceOpt));
            Assert.IsFalse(service.TrySetPlayerReady(10UL, true, out var reasonMissingOpp));
            Assert.IsNotEmpty(reasonMissingOpp);

            string oppOpt = service.CurrentVoteStages[1].Options[1].Id;
            Assert.IsTrue(service.CastCategoryVote(10UL, RunModifierKind.Opportunity, oppOpt));
            Assert.IsTrue(service.HasPlayerVotedAllMandatoryStages(10UL));
            Assert.IsTrue(service.TrySetPlayerReady(10UL, true, out var readyErr));
            Assert.IsNull(readyErr);
        }

        [Test]
        public void TryLockAndResolveBallotsServer_RejectsLateVotesAndPreservesStateOnDisconnect()
        {
            var snapshot = RunBalancingSnapshot.CreateForDomainUnitTests();
            var service = new RunProgressionService(snapshot);
            service.OpenTypedBallots(2, 3, 3);

            string hOpt = service.CurrentVoteStages[0].Options[0].Id;
            string oOpt = service.CurrentVoteStages[1].Options[0].Id;
            service.CastCategoryVote(1UL, RunModifierKind.Hindrance, hOpt);
            service.CastCategoryVote(1UL, RunModifierKind.Opportunity, oOpt);
            Assert.IsTrue(service.TrySetPlayerReady(1UL, true, out _));

            Assert.IsTrue(service.TryLockAndResolveBallotsServer(100.0, _ => 0, 4.5f, 3.0f));
            Assert.AreEqual(IntermissionStage.Revealing, service.IntermissionStage);
            Assert.IsTrue(service.IsIntermissionLocked);
            Assert.AreEqual(hOpt, service.LastWinningHindranceId);
            Assert.AreEqual(oOpt, service.LastWinningOpportunityId);

            // Late vote & ready toggle rejected
            Assert.IsFalse(service.CastCategoryVote(2UL, RunModifierKind.Hindrance, service.CurrentVoteStages[0].Options[1].Id));
            Assert.IsFalse(service.TrySetPlayerReady(1UL, false, out _));

            // Disconnect after lock preserves votes and lock state
            service.OnParticipantDisconnected(1UL);
            Assert.IsTrue(service.IsIntermissionLocked);
            Assert.IsTrue(service.TryGetPlayerVote(1UL, RunModifierKind.Hindrance, out var preservedVote));
            Assert.AreEqual(hOpt, preservedVote);

            // Server tick transitions
            service.TickIntermissionServer(104.0, out var startEarly);
            Assert.IsFalse(startEarly);
            Assert.AreEqual(IntermissionStage.Revealing, service.IntermissionStage);

            service.TickIntermissionServer(104.5, out var startAtCountdown);
            Assert.IsFalse(startAtCountdown);
            Assert.AreEqual(IntermissionStage.Countdown, service.IntermissionStage);

            service.TickIntermissionServer(107.5, out var startNow);
            Assert.IsTrue(startNow);
            Assert.AreEqual(IntermissionStage.Starting, service.IntermissionStage);
        }
    }
}
