using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Presentation;

namespace ScaryParty.Pizzeria.Editor.Tests
{
    [TestFixture]
    public class P4_UiInputAndNetworkFlowTests
    {
        [SetUp]
        public void SetUp()
        {
            GameplayInputGate.Reset();
            SessionLaunchConfig.ResetToDefault();
        }

        [TearDown]
        public void TearDown()
        {
            GameplayInputGate.Reset();
            SessionLaunchConfig.ResetToDefault();
        }

        [Test]
        public void GameplayInputGate_StackedModals_StayLockedUntilLastPop_WithoutResettingOthers()
        {
            GameplayInputGate.PushModal();
            GameplayInputGate.PushModal();
            Assert.IsTrue(GameplayInputGate.IsLocked);

            GameplayInputGate.PopModal();
            Assert.IsTrue(GameplayInputGate.IsLocked, "Primeiro Pop não deve liberar input se outro modal ainda estiver aberto.");

            GameplayInputGate.PopModal();
            Assert.IsFalse(GameplayInputGate.IsLocked);
        }

        [Test]
        public void SessionLaunchConfig_PayloadRoundTrip_PreservesHostSelection()
        {
            SessionLaunchConfig.ApplyHostSelection(
                presetId: "Playtest_Integracao",
                difficulty: RunDifficulty.Hard,
                maxNights: 5,
                seedOverride: 98765,
                includeNight0Tutorial: true,
                broadcastToClients: false);

            var expected = SessionLaunchConfig.Current;
            using var writer = new FastBufferWriter(256, Allocator.Temp);
            SessionLaunchConfig.WritePayload(writer, in expected);

            using var reader = new FastBufferReader(writer.ToArray(), Allocator.Temp);
            var decoded = SessionLaunchConfig.ReadPayload(reader);

            Assert.IsTrue(decoded.HasHostSelection);
            Assert.AreEqual("Playtest_Integracao", decoded.PresetId);
            Assert.AreEqual(RunDifficulty.Hard, decoded.Difficulty);
            Assert.AreEqual(5, decoded.MaxNights);
            Assert.AreEqual(98765, decoded.SeedOverride);
            Assert.IsTrue(decoded.IncludeNight0Tutorial);
        }

        [Test]
        public void NetSupplyDto_NetworkSerialization_RoundTripsSummaryAndUnits()
        {
            var dto = new NetSupplyDto
            {
                SupplyOrderId = 7,
                State = 1,
                OrderedTime = 12.5,
                ExpectedArrivalTime = 30.5,
                TotalCost = 45,
                ReceivingSlotIndex = 0,
                PrimaryIngredientDefId = 3,
                TotalOrderedUnits = 10,
                Summary = new FixedString64Bytes("Ing #3 (+10un)"),
                Revision = 2
            };

            using var writer = new FastBufferWriter(256, Allocator.Temp);
            writer.WriteNetworkSerializable(in dto);

            using var reader = new FastBufferReader(writer.ToArray(), Allocator.Temp);
            reader.ReadNetworkSerializable(out NetSupplyDto copy);

            Assert.IsTrue(dto.Equals(copy));
            Assert.AreEqual("Ing #3 (+10un)", copy.Summary.ToString());
            Assert.AreEqual(10, copy.TotalOrderedUnits);
            Assert.AreEqual(45, copy.TotalCost);
        }
    }
}
