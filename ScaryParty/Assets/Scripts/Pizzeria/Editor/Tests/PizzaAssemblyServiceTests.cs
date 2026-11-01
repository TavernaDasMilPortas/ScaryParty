#if UNITY_EDITOR
using NUnit.Framework;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Editor.Tests
{
    public class PizzaAssemblyServiceTests
    {
        private PizzeriaState _state;
        private ItemState _pizza;
        private ItemState _ingredient;
        private StationSlotId _slot;

        [SetUp]
        public void SetUp()
        {
            _state = new PizzeriaState();
            _slot = new StationSlotId(12, 0);
            _pizza = new ItemState(_state.GenerateItemId(), 2, ItemCategory.Dough, LocationRef.InStation(12, 0))
            {
                PrepStage = ProcessingStage.Prepared
            };
            _ingredient = new ItemState(_state.GenerateItemId(), 3, ItemCategory.Sauce,
                LocationRef.InHand(42, HandSlotIndex.Left));
            _state.Items.Add(_pizza.Id, _pizza);
            _state.Items.Add(_ingredient.Id, _ingredient);
        }

        [Test]
        public void AssemblyConsumesOnlyOnCompletion()
        {
            Assert.IsTrue(PizzaAssemblyService.Begin(_state, _pizza.Id, _ingredient.Id, 42, 10f, 3f).Success);
            Assert.AreEqual(0, _pizza.Ingredients.Count);
            Assert.IsTrue(_state.Items.ContainsKey(_ingredient.Id));
            Assert.AreEqual(OperationState.ServerWorker, _pizza.ReservedByHold);
            Assert.AreEqual(OperationState.ServerWorker, _ingredient.ReservedByHold);
            Assert.IsFalse(TransferService.TransferItem(_state, _ingredient.Id,
                LocationRef.InStation(12, 1), 42).Success);
            Assert.IsFalse(PizzaAssemblyService.Begin(_state, _pizza.Id, _ingredient.Id, 42, 11f, 3f).Success);

            Assert.IsTrue(PizzaAssemblyService.Complete(_state, _slot));
            Assert.AreEqual(1, _pizza.Ingredients.Count);
            Assert.IsFalse(_state.Items.ContainsKey(_ingredient.Id));
            Assert.AreEqual(0, _pizza.ReservedByHold);
            Assert.IsFalse(_state.ActiveOperations.ContainsKey(_slot));
        }

        [Test]
        public void CancelReleasesBothWithoutConsuming()
        {
            Assert.IsTrue(PizzaAssemblyService.Begin(_state, _pizza.Id, _ingredient.Id, 42, 10f, 3f).Success);
            Assert.IsTrue(PizzaAssemblyService.Cancel(_state, _slot));
            Assert.AreEqual(0, _pizza.ReservedByHold);
            Assert.AreEqual(0, _ingredient.ReservedByHold);
            Assert.AreEqual(0, _pizza.Ingredients.Count);
            Assert.IsTrue(_state.Items.ContainsKey(_ingredient.Id));
        }

        [Test]
        public void RejectsOtherPlayersAndUnpreparedBase()
        {
            Assert.IsFalse(PizzaAssemblyService.Begin(_state, _pizza.Id, _ingredient.Id, 99, 10f, 3f).Success);
            _pizza.PrepStage = ProcessingStage.Raw;
            Assert.IsFalse(PizzaAssemblyService.Begin(_state, _pizza.Id, _ingredient.Id, 42, 10f, 3f).Success);
            Assert.AreEqual(0, _state.ActiveOperations.Count);
        }
    }
}
#endif
