#if UNITY_EDITOR
using NUnit.Framework;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Editor.Tests
{
    public class BackpackLifecycleTests
    {
        private PizzeriaState _state;
        private BackpackId _bag;
        private ItemState _first;
        private ItemState _second;

        [SetUp]
        public void SetUp()
        {
            _state = new PizzeriaState();
            _bag = new BackpackId(123);
            Assert.IsTrue(BackpackService.Register(_state, _bag, 1,
                new DomainVector3(2, 0, 3), DomainQuaternion.Identity).Success);
            _first = AddBox(0, HandSlotIndex.Left, 42);
            _second = AddBox(0, HandSlotIndex.Right, -1);
        }

        private ItemState AddBox(ulong owner, HandSlotIndex hand, int address)
        {
            var box = new ItemState(_state.GenerateItemId(), 1, ItemCategory.Box,
                LocationRef.InHand(owner, hand))
            {
                PackagingState = PackagingState.Boxed,
                LabelAddressId = address
            };
            _state.Items.Add(box.Id, box);
            return box;
        }

        [Test]
        public void ActiveHandAndFullCapacityDoNotChangeItemsOrRevisionsOnFailure()
        {
            uint revision = _state.Revision;
            Assert.IsFalse(BackpackService.StoreItem(_state, _bag, _second.Id,
                LocationRef.InHand(0, HandSlotIndex.Left)).Success);
            Assert.AreEqual(revision, _state.Revision);
            Assert.AreEqual(LocationRef.InHand(0, HandSlotIndex.Right), _second.Location);

            Assert.IsTrue(BackpackService.StoreItem(_state, _bag, _first.Id,
                LocationRef.InHand(0, HandSlotIndex.Left)).Success);
            revision = _state.Revision;
            Assert.IsFalse(BackpackService.StoreItem(_state, _bag, _second.Id,
                LocationRef.InHand(0, HandSlotIndex.Right)).Success);
            Assert.AreEqual(revision, _state.Revision);
            Assert.AreEqual(LocationRef.InHand(0, HandSlotIndex.Right), _second.Location);
            Assert.AreEqual(42, _first.LabelAddressId);
        }

        [Test]
        public void CompetingEquipAndRepeatedDropDoNotMutateBag()
        {
            Assert.IsTrue(BackpackService.Equip(_state, _bag, 0).Success);
            uint revision = _state.Revision;
            Assert.IsFalse(BackpackService.Equip(_state, _bag, 0).Success);
            Assert.IsFalse(BackpackService.Equip(_state, _bag, 91).Success);
            Assert.AreEqual(revision, _state.Revision);

            Assert.IsTrue(BackpackService.Drop(_state, 0, new DomainVector3(5, 0, 6), DomainQuaternion.Identity).Success);
            revision = _state.Revision;
            Assert.IsFalse(BackpackService.Drop(_state, 0, default, DomainQuaternion.Identity).Success);
            Assert.AreEqual(revision, _state.Revision);
            Assert.AreEqual(BackpackState.NoOwner, _state.Backpacks[_bag].EquippedBy);
        }

        [Test]
        public void RecoveryAndRetrievalPreserveIdentityAndLabel()
        {
            Assert.IsTrue(BackpackService.StoreItem(_state, _bag, _first.Id,
                LocationRef.InHand(0, HandSlotIndex.Left)).Success);
            Assert.IsTrue(BackpackService.Equip(_state, _bag, 0).Success);
            BackpackService.ReleaseOnDisconnect(_state, 0, new DomainVector3(8, 0, 8));
            Assert.AreEqual(BackpackState.NoOwner, _state.Backpacks[_bag].EquippedBy);
            Assert.IsTrue(BackpackService.Equip(_state, _bag, 91).Success);
            Assert.IsFalse(BackpackService.RetrieveItem(_state, _bag, _first.Id,
                LocationRef.InHand(0, HandSlotIndex.Right)).Success);
            Assert.IsTrue(BackpackService.RetrieveItem(_state, _bag, _first.Id,
                LocationRef.InHand(91, HandSlotIndex.Left)).Success);
            Assert.AreEqual(42, _first.LabelAddressId);
            Assert.IsEmpty(_state.Backpacks[_bag].Items);
        }
    }
}
#endif
