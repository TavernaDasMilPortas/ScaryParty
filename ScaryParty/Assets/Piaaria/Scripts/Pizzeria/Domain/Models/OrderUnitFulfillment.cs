using System;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Models
{
    /// <summary>
    /// Identidade única e autoritativa de uma unidade de trabalho de pedido dentro de uma geração (RUN).
    /// Evita colisão entre pedidos antigos/reabertos e vincula no máximo um ItemId físico pronto.
    /// </summary>
    public readonly struct OrderUnitKey : IEquatable<OrderUnitKey>
    {
        public readonly uint GenerationId;
        public readonly OrderId OrderId;
        public readonly int LineIndex;
        public readonly int UnitIndex;

        public OrderUnitKey(uint generationId, OrderId orderId, int lineIndex, int unitIndex)
        {
            GenerationId = generationId;
            OrderId = orderId;
            LineIndex = lineIndex;
            UnitIndex = unitIndex;
        }

        public bool IsValid => GenerationId > 0 && OrderId.IsValid && LineIndex >= 0 && UnitIndex >= 0;

        public bool Equals(OrderUnitKey other)
        {
            return GenerationId == other.GenerationId &&
                   OrderId.Equals(other.OrderId) &&
                   LineIndex == other.LineIndex &&
                   UnitIndex == other.UnitIndex;
        }

        public override bool Equals(object obj) => obj is OrderUnitKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(GenerationId, OrderId, LineIndex, UnitIndex);

        public override string ToString() => $"G{GenerationId}:O{OrderId.Value}:L{LineIndex}:U{UnitIndex}";
    }

    public enum OrderUnitStatus : byte
    {
        Pending = 0,
        InProgress = 1,
        BoundToReadyItem = 2,
        Delivered = 3,
        Cancelled = 4
    }

    public sealed class OrderUnitState
    {
        public OrderUnitKey Key { get; }
        public PizzaSpecification Spec { get; }
        public bool RequiresBox { get; }
        public AddressId LabelAddress { get; }
        public OrderUnitStatus Status { get; set; } = OrderUnitStatus.Pending;
        public int AssignedEmployeeId { get; set; } = -1;
        public ItemId BoundItemId { get; set; } = ItemId.None;
        public double ReservedAtSimTime { get; set; }
        public uint Revision { get; set; } = 1;

        public OrderUnitState(OrderUnitKey key, PizzaSpecification spec, bool requiresBox, AddressId labelAddress)
        {
            Key = key;
            Spec = spec ?? PizzaSpecification.CreateDefaultCheese();
            RequiresBox = requiresBox;
            LabelAddress = labelAddress;
        }
    }

    public sealed class StationSlotReservation
    {
        public int StationId { get; }
        public int SlotIndex { get; }
        public ulong ActorKey { get; set; }
        public int EmployeeId { get; set; }
        public OrderUnitKey UnitKey { get; set; }
        public double ExpiresAtSimTime { get; set; }

        public StationSlotReservation(int stationId, int slotIndex, ulong actorKey, int employeeId, OrderUnitKey unitKey, double expiresAtSimTime)
        {
            StationId = stationId;
            SlotIndex = slotIndex;
            ActorKey = actorKey;
            EmployeeId = employeeId;
            UnitKey = unitKey;
            ExpiresAtSimTime = expiresAtSimTime;
        }
    }
}
