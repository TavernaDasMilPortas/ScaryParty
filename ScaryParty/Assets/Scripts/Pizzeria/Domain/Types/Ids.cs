using System;

namespace ScaryParty.Pizzeria.Domain.Types
{
    public readonly struct ItemId : IEquatable<ItemId>, IComparable<ItemId>
    {
        public readonly ulong Value;
        public static ItemId None => new ItemId(0);
        public bool IsValid => Value != 0;

        public ItemId(ulong value) => Value = value;
        public bool Equals(ItemId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ItemId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(ItemId other) => Value.CompareTo(other.Value);
        public override string ToString() => $"Item({Value})";
        public static bool operator ==(ItemId a, ItemId b) => a.Value == b.Value;
        public static bool operator !=(ItemId a, ItemId b) => a.Value != b.Value;
        public static explicit operator ulong(ItemId id) => id.Value;
        public static explicit operator ItemId(ulong val) => new ItemId(val);
    }

    public readonly struct ToolItemId : IEquatable<ToolItemId>, IComparable<ToolItemId>
    {
        public readonly ulong Value;
        public static ToolItemId None => new ToolItemId(0);
        public bool IsValid => Value != 0;

        public ToolItemId(ulong value) => Value = value;
        public bool Equals(ToolItemId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ToolItemId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(ToolItemId other) => Value.CompareTo(other.Value);
        public override string ToString() => $"Tool({Value})";
        public static bool operator ==(ToolItemId a, ToolItemId b) => a.Value == b.Value;
        public static bool operator !=(ToolItemId a, ToolItemId b) => a.Value != b.Value;
        public static explicit operator ulong(ToolItemId id) => id.Value;
        public static explicit operator ToolItemId(ulong val) => new ToolItemId(val);
    }

    public readonly struct OrderId : IEquatable<OrderId>, IComparable<OrderId>
    {
        public readonly ulong Value;
        public static OrderId None => new OrderId(0);
        public bool IsValid => Value != 0;

        public OrderId(ulong value) => Value = value;
        public bool Equals(OrderId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is OrderId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(OrderId other) => Value.CompareTo(other.Value);
        public override string ToString() => $"Order({Value})";
        public static bool operator ==(OrderId a, OrderId b) => a.Value == b.Value;
        public static bool operator !=(OrderId a, OrderId b) => a.Value != b.Value;
        public static explicit operator ulong(OrderId id) => id.Value;
        public static explicit operator OrderId(ulong val) => new OrderId(val);
    }

    public readonly struct OrderLineId : IEquatable<OrderLineId>, IComparable<OrderLineId>
    {
        public readonly ulong Value;
        public static OrderLineId None => new OrderLineId(0);
        public bool IsValid => Value != 0;

        public OrderLineId(ulong value) => Value = value;
        public bool Equals(OrderLineId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is OrderLineId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(OrderLineId other) => Value.CompareTo(other.Value);
        public override string ToString() => $"Line({Value})";
        public static bool operator ==(OrderLineId a, OrderLineId b) => a.Value == b.Value;
        public static bool operator !=(OrderLineId a, OrderLineId b) => a.Value != b.Value;
    }

    public readonly struct OperationId : IEquatable<OperationId>, IComparable<OperationId>
    {
        public readonly ulong Value;
        public static OperationId None => new OperationId(0);
        public bool IsValid => Value != 0;

        public OperationId(ulong value) => Value = value;
        public bool Equals(OperationId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is OperationId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(OperationId other) => Value.CompareTo(other.Value);
        public override string ToString() => $"Op({Value})";
        public static bool operator ==(OperationId a, OperationId b) => a.Value == b.Value;
        public static bool operator !=(OperationId a, OperationId b) => a.Value != b.Value;
    }

    public readonly struct SupplyOrderId : IEquatable<SupplyOrderId>, IComparable<SupplyOrderId>
    {
        public readonly ulong Value;
        public static SupplyOrderId None => new SupplyOrderId(0);
        public bool IsValid => Value != 0;

        public SupplyOrderId(ulong value) => Value = value;
        public bool Equals(SupplyOrderId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is SupplyOrderId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(SupplyOrderId other) => Value.CompareTo(other.Value);
        public override string ToString() => $"SupplyOrder({Value})";
        public static bool operator ==(SupplyOrderId a, SupplyOrderId b) => a.Value == b.Value;
        public static bool operator !=(SupplyOrderId a, SupplyOrderId b) => a.Value != b.Value;
    }

    public readonly struct CustomerId : IEquatable<CustomerId>, IComparable<CustomerId>
    {
        public readonly ulong Value;
        public static CustomerId None => new CustomerId(0);
        public bool IsValid => Value != 0;

        public CustomerId(ulong value) => Value = value;
        public bool Equals(CustomerId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is CustomerId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(CustomerId other) => Value.CompareTo(other.Value);
        public override string ToString() => $"Customer({Value})";
        public static bool operator ==(CustomerId a, CustomerId b) => a.Value == b.Value;
        public static bool operator !=(CustomerId a, CustomerId b) => a.Value != b.Value;
    }

    public readonly struct StationSlotId : IEquatable<StationSlotId>, IComparable<StationSlotId>
    {
        public readonly int StationId;
        public readonly int SlotIndex;

        public StationSlotId(int stationId, int slotIndex)
        {
            StationId = stationId;
            SlotIndex = slotIndex;
        }

        public bool Equals(StationSlotId other) => StationId == other.StationId && SlotIndex == other.SlotIndex;
        public override bool Equals(object obj) => obj is StationSlotId other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(StationId, SlotIndex);
        public int CompareTo(StationSlotId other)
        {
            int c = StationId.CompareTo(other.StationId);
            return c != 0 ? c : SlotIndex.CompareTo(other.SlotIndex);
        }
        public override string ToString() => $"Slot({StationId}:{SlotIndex})";
        public static bool operator ==(StationSlotId a, StationSlotId b) => a.Equals(b);
        public static bool operator !=(StationSlotId a, StationSlotId b) => !a.Equals(b);
    }

    public enum ItemCategory : byte
    {
        Dough = 1,
        Sauce = 2,
        Cheese = 3,
        Topping = 4,
        Tool = 5,
        PizzaBase = 6,
        Box = 7,
        Ingredient = 8,
        Other = 9,
        Pizza = 10
    }

    [Flags]
    public enum ToolCapability : byte
    {
        None = 0,
        Grate = 1 << 0,
        Slice = 1 << 1
    }

    public enum LocationType : byte
    {
        Destroyed = 0,
        StorageSlot = 1,
        Hand = 2,
        StationSlot = 3,
        Backpack = 4,
        World = 5,
        StagingSlot = 6,
        ReceivingSlot = 7
    }

    public enum HandSlotIndex : byte
    {
        Left = 0,
        Right = 1
    }

    public enum ProcessingStage : byte
    {
        Raw = 0,
        Prepared = 1
    }

    public enum CookingStage : byte
    {
        Uncooked = 0,
        Cooking = 1,
        Baked = 2,
        Burned = 3
    }

    public enum PackagingState : byte
    {
        Unboxed = 0,
        Boxed = 1
    }

    public enum OrderSourceType : byte
    {
        Phone = 1,
        WalkIn = 2
    }

    public enum OrderLifecycle : byte
    {
        Accepted = 1,
        Completed = 2,
        Cancelled = 3,
        Expired = 4
    }

    public enum SupplyOrderLifecycle : byte
    {
        InTransit = 1,
        WaitingForReceivingSlot = 2,
        Arrived = 3,
        Unloaded = 4
    }

    public enum UpgradeScope : byte
    {
        Oven = 1,
        Prep = 2,
        Packaging = 3,
        Storage = 4,
        Backpack = 5,
        Restaurant = 6,
        Recipe = 7
    }

    public enum UpgradeOperation : byte
    {
        FlatAdd = 1,
        PercentAdd = 2,
        Multiply = 3,
        BoolSet = 4,
        Override = 5
    }
}
