#if UNITY_EDITOR
using NUnit.Framework;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Editor.Tests
{
    public class ProcessingInterruptionTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void InterruptionReleasesInputForAnotherPlayer(bool timeout)
        {
            var state = new PizzeriaState();
            var slot = new StationSlotId(12, 0);
            var item = new ItemState(state.GenerateItemId(), 1, ItemCategory.Dough,
                LocationRef.InStation(12, 0)) { ReservedByHold = 42 };
            state.Items.Add(item.Id, item);
            state.ActiveOperations.Add(slot, new OperationState(state.GenerateOperationId(), slot,
                OperationKind.Process, ResumePolicy.Resumable, item.Id, ToolItemId.None,
                1, 42, 3f, 0f, 0.75f));
            var service = new ProcessingService();

            if (timeout) service.TickOperations(state, new DefinitionCatalog(), 1f);
            else Assert.IsTrue(service.CancelOperation(state, slot, 42, 0.5f).Success);

            Assert.AreEqual(OperationPhase.Paused, state.ActiveOperations[slot].Phase);
            Assert.AreEqual(0ul, item.ReservedByHold);
            Assert.AreEqual(LocationRef.InStation(12, 0), item.Location);
            Assert.IsTrue(TransferService.TransferItem(state, item.Id,
                LocationRef.InHand(99, HandSlotIndex.Left), 99).Success);
            Assert.IsFalse(state.ActiveOperations.ContainsKey(slot));
        }
    }
}
#endif