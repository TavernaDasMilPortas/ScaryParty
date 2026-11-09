using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Models
{
    public class StackState
    {
        public StackId Id { get; }
        public int StationId { get; }
        public int Capacity { get; }
        public List<ItemId> Items { get; } = new List<ItemId>();
        public uint Revision { get; set; } = 1;

        public StackState(StackId id, int stationId, int capacity)
        {
            Id = id;
            StationId = stationId;
            Capacity = capacity;
        }

        public bool CanAdd => Items.Count < Capacity;
        public bool IsEmpty => Items.Count == 0;
        public ItemId TopItem => IsEmpty ? ItemId.None : Items[Items.Count - 1];

        public void Add(ItemId itemId)
        {
            Items.Add(itemId);
            Revision++;
        }

        public bool Remove(ItemId itemId)
        {
            if (Items.Remove(itemId))
            {
                Revision++;
                return true;
            }
            return false;
        }

        public ItemId Pop()
        {
            if (IsEmpty) return ItemId.None;
            var item = Items[Items.Count - 1];
            Items.RemoveAt(Items.Count - 1);
            Revision++;
            return item;
        }
    }

    public class BackpackState
    {
        public BackpackId Id { get; }
        public ulong EquippedBy { get; set; } = ulong.MaxValue;
        public DomainVector3 WorldPos { get; set; }
        public DomainQuaternion WorldRot { get; set; }
        public int Capacity { get; set; }
        public List<ItemId> Items { get; } = new List<ItemId>();
        public uint Revision { get; set; } = 1;

        public const ulong NoOwner = ulong.MaxValue;

        public BackpackState(BackpackId id, int capacity)
        {
            Id = id;
            Capacity = capacity;
        }

        public bool IsEquipped => EquippedBy != NoOwner;
        public bool CanAdd => Items.Count < Capacity;
        public bool IsEmpty => Items.Count == 0;

        public void Add(ItemId itemId)
        {
            Items.Add(itemId);
            Revision++;
        }

        public bool Remove(ItemId itemId)
        {
            if (Items.Remove(itemId))
            {
                Revision++;
                return true;
            }
            return false;
        }
    }

    public class PendingCall
    {
        public CallId Id { get; }
        public AddressId AddressId { get; }
        public List<OrderLineState> Lines { get; }
        public float ExpiresAt { get; }

        public PendingCall(CallId id, AddressId addressId, List<OrderLineState> lines, float expiresAt)
        {
            Id = id;
            AddressId = addressId;
            Lines = lines;
            ExpiresAt = expiresAt;
        }
    }

    public class DeliveryAttemptLog
    {
        private readonly Queue<ulong> _history = new Queue<ulong>();
        private readonly HashSet<ulong> _set = new HashSet<ulong>();
        private const int MaxHistory = 64;

        public void Record(ulong attemptId)
        {
            if (_set.Add(attemptId))
            {
                _history.Enqueue(attemptId);
                if (_history.Count > MaxHistory)
                {
                    _set.Remove(_history.Dequeue());
                }
            }
        }

        public bool HasAttempt(ulong attemptId) => _set.Contains(attemptId);
    }
}
