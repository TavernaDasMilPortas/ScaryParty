using System;

namespace ScaryParty.Pizzeria.Domain
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

    public readonly struct AddressId : IEquatable<AddressId>, IComparable<AddressId>
    {
        public readonly int Value;
        public static AddressId None => new AddressId(-1);
        public bool IsValid => Value >= 0;

        public AddressId(int value) => Value = value;
        public bool Equals(AddressId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is AddressId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(AddressId other) => Value.CompareTo(other.Value);
        public override string ToString() => $"Address({Value})";
        public static bool operator ==(AddressId a, AddressId b) => a.Value == b.Value;
        public static bool operator !=(AddressId a, AddressId b) => a.Value != b.Value;
        public static explicit operator int(AddressId id) => id.Value;
        public static explicit operator AddressId(int val) => new AddressId(val);
    }

    public static class AddressFormat
    {
        public static string Format(AddressId id)
        {
            if (!id.IsValid) return "Sem destino";
            return $"Endereço {id.Value + 1}";
        }
        
        public static string Display(int idValue)
        {
            if (idValue < 0) return "Sem destino";
            return $"Endereço {idValue + 1}";
        }
    }

    public readonly struct CallId : IEquatable<CallId>, IComparable<CallId>
    {
        public readonly ulong Value;
        public static CallId None => new CallId(0);
        public bool IsValid => Value != 0;

        public CallId(ulong value) => Value = value;
        public bool Equals(CallId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is CallId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(CallId other) => Value.CompareTo(other.Value);
        public override string ToString() => $"Call({Value})";
        public static bool operator ==(CallId a, CallId b) => a.Value == b.Value;
        public static bool operator !=(CallId a, CallId b) => a.Value != b.Value;
    }

    public readonly struct StackId : IEquatable<StackId>, IComparable<StackId>
    {
        public readonly ulong Value;
        public static StackId None => new StackId(0);
        public bool IsValid => Value != 0;

        public StackId(ulong value) => Value = value;
        public bool Equals(StackId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is StackId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(StackId other) => Value.CompareTo(other.Value);
        public override string ToString() => $"Stack({Value})";
        public static bool operator ==(StackId a, StackId b) => a.Value == b.Value;
        public static bool operator !=(StackId a, StackId b) => a.Value != b.Value;
    }

    public readonly struct BackpackId : IEquatable<BackpackId>, IComparable<BackpackId>
    {
        public readonly ulong Value;
        public static BackpackId None => new BackpackId(0);
        public bool IsValid => Value != 0;

        public BackpackId(ulong value) => Value = value;
        public bool Equals(BackpackId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is BackpackId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(BackpackId other) => Value.CompareTo(other.Value);
        public override string ToString() => $"Backpack({Value})";
        public static bool operator ==(BackpackId a, BackpackId b) => a.Value == b.Value;
        public static bool operator !=(BackpackId a, BackpackId b) => a.Value != b.Value;
    }

    public readonly struct DomainVector3 : IEquatable<DomainVector3>
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public static DomainVector3 Zero => new DomainVector3(0f, 0f, 0f);

        public DomainVector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public bool Equals(DomainVector3 other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        public override bool Equals(object obj) => obj is DomainVector3 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
        public static bool operator ==(DomainVector3 a, DomainVector3 b) => a.Equals(b);
        public static bool operator !=(DomainVector3 a, DomainVector3 b) => !a.Equals(b);
        public override string ToString() => $"({X}, {Y}, {Z})";
    }

    public readonly struct DomainQuaternion : IEquatable<DomainQuaternion>
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;
        public readonly float W;

        public static DomainQuaternion Identity => new DomainQuaternion(0f, 0f, 0f, 1f);

        public DomainQuaternion(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public bool Equals(DomainQuaternion other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z) && W.Equals(other.W);
        public override bool Equals(object obj) => obj is DomainQuaternion other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z, W);
        public static bool operator ==(DomainQuaternion a, DomainQuaternion b) => a.Equals(b);
        public static bool operator !=(DomainQuaternion a, DomainQuaternion b) => !a.Equals(b);
        public override string ToString() => $"({X}, {Y}, {Z}, {W})";
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
        ReceivingSlot = 7,
        Stack = 8
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

    public enum OperationKind : byte
    {
        Process = 1,
        Package = 2
    }

    public enum OperationPhase : byte
    {
        Running = 1,
        Paused = 2,
        Completed = 3,
        Cancelled = 4
    }

    public enum ResumePolicy : byte
    {
        Resumable = 1,
        ResetOnCancel = 2
    }

    public enum PlayerHoldKind : byte
    {
        Discard = 1,
        ArchiveTicket = 2
    }
}
