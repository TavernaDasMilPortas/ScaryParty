using System;

namespace ScaryParty.Pizzeria.Domain.Types
{
    public readonly struct LocationRef : IEquatable<LocationRef>
    {
        public readonly LocationType Type;
        public readonly ulong HolderId;
        public readonly int SlotId;
        public readonly float WorldX;
        public readonly float WorldY;
        public readonly float WorldZ;

        public static LocationRef Destroyed => new LocationRef(LocationType.Destroyed, 0, 0);

        public LocationRef(LocationType type, ulong holderId, int slotId = 0, float x = 0f, float y = 0f, float z = 0f)
        {
            Type = type;
            HolderId = holderId;
            SlotId = slotId;
            WorldX = x;
            WorldY = y;
            WorldZ = z;
        }

        public static LocationRef InHand(ulong playerId, HandSlotIndex hand)
            => new LocationRef(LocationType.Hand, playerId, (int)hand);

        public static LocationRef InBackpack(ulong playerId, int slotIndex)
            => new LocationRef(LocationType.Backpack, playerId, slotIndex);

        public static LocationRef InStation(int stationId, int slotIndex)
            => new LocationRef(LocationType.StationSlot, (ulong)stationId, slotIndex);

        public static LocationRef InStaging(int stagingSlotIndex)
            => new LocationRef(LocationType.StagingSlot, 0, stagingSlotIndex);

        public static LocationRef InStorage(int storageId, int ingredientDefId)
            => new LocationRef(LocationType.StorageSlot, (ulong)storageId, ingredientDefId);

        public static LocationRef InWorld(float x, float y, float z)
            => new LocationRef(LocationType.World, 0, 0, x, y, z);

        public bool Equals(LocationRef other)
        {
            if (Type != other.Type || HolderId != other.HolderId || SlotId != other.SlotId)
                return false;
            if (Type == LocationType.World)
                return Math.Abs(WorldX - other.WorldX) < 0.001f &&
                       Math.Abs(WorldY - other.WorldY) < 0.001f &&
                       Math.Abs(WorldZ - other.WorldZ) < 0.001f;
            return true;
        }

        public override bool Equals(object obj) => obj is LocationRef other && Equals(other);

        public override int GetHashCode() => HashCode.Combine((byte)Type, HolderId, SlotId);

        public override string ToString()
        {
            return Type switch
            {
                LocationType.Destroyed => "Destroyed",
                LocationType.Hand => $"Hand({HolderId}:{(HandSlotIndex)SlotId})",
                LocationType.Backpack => $"Backpack({HolderId}:{SlotId})",
                LocationType.StationSlot => $"Station({HolderId}:{SlotId})",
                LocationType.StagingSlot => $"Staging({SlotId})",
                LocationType.StorageSlot => $"Storage({HolderId}:{SlotId})",
                LocationType.World => $"World({WorldX:F1},{WorldY:F1},{WorldZ:F1})",
                _ => $"{Type}({HolderId}:{SlotId})"
            };
        }

        public static bool operator ==(LocationRef a, LocationRef b) => a.Equals(b);
        public static bool operator !=(LocationRef a, LocationRef b) => !a.Equals(b);
    }
}
