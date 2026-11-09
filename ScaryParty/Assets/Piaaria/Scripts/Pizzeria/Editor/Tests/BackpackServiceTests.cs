#if UNITY_EDITOR
using NUnit.Framework;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Editor.Tests
{
    public class BackpackServiceTests
    {
        private PizzeriaState _state;
        private BackpackId _bag;
        private ItemState _box;
        private LocationRef _hostHand;

        [SetUp]
        public void SetUp()
        {
            _state = new PizzeriaState();
            _bag = new BackpackId(37);
            _hostHand = LocationRef.InHand(0, HandSlotIndex.Left);
            _box = new ItemState(_state.GenerateItemId(), 1, ItemCategory.Box, _hostHand)
            {
                PackagingState = PackagingState.Boxed,
                LabelAddressId = 42
            };
            _state.Items.Add(_box.Id, _box);
            Assert.IsTrue(BackpackService.Register(_state, _bag, 1, new DomainVector3(2, 0, 3), DomainQuaternion.Identity).Success);
        }

        [Test]
        public void StoreOnFloorEquipDropAndNewOwnerPreserveIdentity()
        {
            Assert.IsTrue(BackpackService.StoreItem(_state, _bag, _box.Id, _hostHand).Success);
            Assert.IsTrue(BackpackService.Equip(_state, _bag, 0).Success);
            Assert.IsFalse(BackpackService.Equip(_state, _bag, 1).Success);
            Assert.IsTrue(BackpackService.Drop(_state, 0, new DomainVector3(5, 0, 6), DomainQuaternion.Identity).Success);
            Assert.IsTrue(BackpackService.Equip(_state, _bag, 87).Success);
            Assert.AreEqual(87ul, _state.Backpacks[_bag].EquippedBy);
            Assert.AreEqual(_bag.Value, _box.Location.HolderId);
            Assert.AreEqual(42, _box.LabelAddressId);
        }

        [Test]
        public void UnknownBagAndDuplicateRegistrationDoNotChangeState()
        {
            uint revision = _state.Revision;
            Assert.IsFalse(BackpackService.Equip(_state, new BackpackId(88), 0).Success);
            Assert.IsFalse(BackpackService.Register(_state, _bag, 3, default, DomainQuaternion.Identity).Success);
            Assert.IsFalse(BackpackService.StoreItem(_state, new BackpackId(88), _box.Id, _hostHand).Success);
            Assert.AreEqual(revision, _state.Revision);
            Assert.AreEqual(_hostHand, _box.Location);
        }

        [Test]
        public void CapacityAndReservationsCannotLoseBoxes()
        {
            _box.ReservedByHold = OperationState.ServerWorker;
            Assert.IsFalse(BackpackService.StoreItem(_state, _bag, _box.Id, _hostHand).Success);
            Assert.IsEmpty(_state.Backpacks[_bag].Items);
            Assert.AreEqual(_hostHand, _box.Location);
            _box.ReservedByHold = 0;
            Assert.IsTrue(BackpackService.StoreItem(_state, _bag, _box.Id, _hostHand).Success);
            Assert.IsFalse(BackpackService.StoreItem(_state, _bag, _box.Id, _hostHand).Success);
            Assert.IsTrue(BackpackService.Equip(_state, _bag, 0).Success);
            Assert.IsTrue(BackpackService.RetrieveItem(_state, _bag, _box.Id, _hostHand).Success);
            Assert.AreEqual(42, _box.LabelAddressId);
            Assert.IsEmpty(_state.Backpacks[_bag].Items);
        }
    }
}
#endif
