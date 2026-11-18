using NUnit.Framework;
using ScaryParty.Pizzeria.Network;
using Unity.Collections;

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
    }
}
