using System;
using Unity.Netcode;

namespace ScaryParty.Pizzeria.Network
{
    public struct NetItemDto : INetworkSerializable, IEquatable<NetItemDto>
    {
        public ulong ItemId;
        public int DefinitionId;
        public byte Category;
        public byte PrepStage;
        public byte CookingStage;
        public byte PackagingState;
        public byte LocationType;
        public ulong HolderId;
        public int SlotId;
        public float Quality;
        public float CookProgress;
        public float BurnProgress;
        public int LabelDestinationId;
        public uint Revision;
        public byte IngredientMask;
        public byte IngredientCount;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ItemId);
            serializer.SerializeValue(ref DefinitionId);
            serializer.SerializeValue(ref Category);
            serializer.SerializeValue(ref PrepStage);
            serializer.SerializeValue(ref CookingStage);
            serializer.SerializeValue(ref PackagingState);
            serializer.SerializeValue(ref LocationType);
            serializer.SerializeValue(ref HolderId);
            serializer.SerializeValue(ref SlotId);
            serializer.SerializeValue(ref Quality);
            serializer.SerializeValue(ref CookProgress);
            serializer.SerializeValue(ref BurnProgress);
            serializer.SerializeValue(ref LabelDestinationId);
            serializer.SerializeValue(ref Revision);
            serializer.SerializeValue(ref IngredientMask);
            serializer.SerializeValue(ref IngredientCount);
        }

        public bool Equals(NetItemDto other)
            => ItemId == other.ItemId && Revision == other.Revision && LocationType == other.LocationType && HolderId == other.HolderId && SlotId == other.SlotId && IngredientMask == other.IngredientMask && IngredientCount == other.IngredientCount;

        public override bool Equals(object obj) => obj is NetItemDto other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(ItemId, Revision, IngredientMask);
    }

    public struct NetToolDto : INetworkSerializable, IEquatable<NetToolDto>
    {
        public ulong ToolId;
        public int DefinitionId;
        public byte Capabilities;
        public byte LocationType;
        public ulong HolderId;
        public int SlotId;
        public ulong WorkerId;
        public double LeaseExpiration;
        public uint Revision;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ToolId);
            serializer.SerializeValue(ref DefinitionId);
            serializer.SerializeValue(ref Capabilities);
            serializer.SerializeValue(ref LocationType);
            serializer.SerializeValue(ref HolderId);
            serializer.SerializeValue(ref SlotId);
            serializer.SerializeValue(ref WorkerId);
            serializer.SerializeValue(ref LeaseExpiration);
            serializer.SerializeValue(ref Revision);
        }

        public bool Equals(NetToolDto other)
            => ToolId == other.ToolId && Revision == other.Revision && WorkerId == other.WorkerId && LocationType == other.LocationType && HolderId == other.HolderId && SlotId == other.SlotId;

        public override bool Equals(object obj) => obj is NetToolDto other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(ToolId, Revision);
    }

    public struct NetStorageDto : INetworkSerializable, IEquatable<NetStorageDto>
    {
        public int StorageId;
        public int IngredientDefId;
        public int Quantity;
        public int MaxCapacity;
        public uint Revision;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref StorageId);
            serializer.SerializeValue(ref IngredientDefId);
            serializer.SerializeValue(ref Quantity);
            serializer.SerializeValue(ref MaxCapacity);
            serializer.SerializeValue(ref Revision);
        }

        public bool Equals(NetStorageDto other)
            => StorageId == other.StorageId && IngredientDefId == other.IngredientDefId && Quantity == other.Quantity && Revision == other.Revision;

        public override bool Equals(object obj) => obj is NetStorageDto other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(StorageId, IngredientDefId, Quantity);
    }

    public struct NetStationSlotDto : INetworkSerializable, IEquatable<NetStationSlotDto>
    {
        public int StationId;
        public int SlotIndex;
        public ulong HeldItemId;
        public ulong ToolItemId;
        public ulong WorkerId;
        public float Progress;
        public int ProcessId;
        public uint Revision;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref StationId);
            serializer.SerializeValue(ref SlotIndex);
            serializer.SerializeValue(ref HeldItemId);
            serializer.SerializeValue(ref ToolItemId);
            serializer.SerializeValue(ref WorkerId);
            serializer.SerializeValue(ref Progress);
            serializer.SerializeValue(ref ProcessId);
            serializer.SerializeValue(ref Revision);
        }

        public bool Equals(NetStationSlotDto other)
            => StationId == other.StationId && SlotIndex == other.SlotIndex && HeldItemId == other.HeldItemId && WorkerId == other.WorkerId && Revision == other.Revision;

        public override bool Equals(object obj) => obj is NetStationSlotDto other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(StationId, SlotIndex, Revision);
    }

    public struct NetOrderDto : INetworkSerializable, IEquatable<NetOrderDto>
    {
        public ulong OrderId;
        public int DestinationId;
        public byte Lifecycle;
        public double DeadlineTime;
        public int QuotedReward;
        public int FinalEarnedReward;
        public int LineCount;

        public int RecipeId_0;
        public int Qty_0;
        public int RecipeId_1;
        public int Qty_1;
        public int RecipeId_2;
        public int Qty_2;

        public uint Revision;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref OrderId);
            serializer.SerializeValue(ref DestinationId);
            serializer.SerializeValue(ref Lifecycle);
            serializer.SerializeValue(ref DeadlineTime);
            serializer.SerializeValue(ref QuotedReward);
            serializer.SerializeValue(ref FinalEarnedReward);
            serializer.SerializeValue(ref LineCount);

            serializer.SerializeValue(ref RecipeId_0);
            serializer.SerializeValue(ref Qty_0);
            serializer.SerializeValue(ref RecipeId_1);
            serializer.SerializeValue(ref Qty_1);
            serializer.SerializeValue(ref RecipeId_2);
            serializer.SerializeValue(ref Qty_2);

            serializer.SerializeValue(ref Revision);
        }

        public bool Equals(NetOrderDto other)
            => OrderId == other.OrderId && Lifecycle == other.Lifecycle && Revision == other.Revision;

        public override bool Equals(object obj) => obj is NetOrderDto other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(OrderId, Lifecycle, Revision);
    }

    public struct NetSupplyDto : INetworkSerializable, IEquatable<NetSupplyDto>
    {
        public ulong SupplyOrderId;
        public byte State;
        public double ExpectedArrivalTime;
        public int TotalCost;
        public int ReceivingSlotIndex;
        public uint Revision;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref SupplyOrderId);
            serializer.SerializeValue(ref State);
            serializer.SerializeValue(ref ExpectedArrivalTime);
            serializer.SerializeValue(ref TotalCost);
            serializer.SerializeValue(ref ReceivingSlotIndex);
            serializer.SerializeValue(ref Revision);
        }

        public bool Equals(NetSupplyDto other)
            => SupplyOrderId == other.SupplyOrderId && State == other.State && Revision == other.Revision;

        public override bool Equals(object obj) => obj is NetSupplyDto other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(SupplyOrderId, State, Revision);
    }

    public struct NetUpgradeDto : INetworkSerializable, IEquatable<NetUpgradeDto>
    {
        public int UpgradeId;
        public int Level;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref UpgradeId);
            serializer.SerializeValue(ref Level);
        }

        public bool Equals(NetUpgradeDto other)
            => UpgradeId == other.UpgradeId && Level == other.Level;

        public override bool Equals(object obj) => obj is NetUpgradeDto other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(UpgradeId, Level);
    }
}
