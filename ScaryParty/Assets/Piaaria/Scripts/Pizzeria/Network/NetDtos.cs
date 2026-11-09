using System;
using System.Collections.Generic;
using Unity.Netcode;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Network
{
    public struct NetPizzaSpecDto : INetworkSerializable, IEquatable<NetPizzaSpecDto>
    {
        public int BaseDoughId;
        public byte RequiredDoughStage;
        public int SauceId;
        public bool RequiresSauce;
        public byte SaucePortions;
        public byte ToppingCount;
        public int ToppingId_0;
        public byte ToppingStage_0;
        public byte ToppingQty_0;
        public int ToppingId_1;
        public byte ToppingStage_1;
        public byte ToppingQty_1;
        public int ToppingId_2;
        public byte ToppingStage_2;
        public byte ToppingQty_2;
        public int ToppingId_3;
        public byte ToppingStage_3;
        public byte ToppingQty_3;
        public int QuotedPrice;
        public int LegacyRecipeId;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref BaseDoughId);
            serializer.SerializeValue(ref RequiredDoughStage);
            serializer.SerializeValue(ref SauceId);
            serializer.SerializeValue(ref RequiresSauce);
            serializer.SerializeValue(ref SaucePortions);
            serializer.SerializeValue(ref ToppingCount);
            serializer.SerializeValue(ref ToppingId_0);
            serializer.SerializeValue(ref ToppingStage_0);
            serializer.SerializeValue(ref ToppingQty_0);
            serializer.SerializeValue(ref ToppingId_1);
            serializer.SerializeValue(ref ToppingStage_1);
            serializer.SerializeValue(ref ToppingQty_1);
            serializer.SerializeValue(ref ToppingId_2);
            serializer.SerializeValue(ref ToppingStage_2);
            serializer.SerializeValue(ref ToppingQty_2);
            serializer.SerializeValue(ref ToppingId_3);
            serializer.SerializeValue(ref ToppingStage_3);
            serializer.SerializeValue(ref ToppingQty_3);
            serializer.SerializeValue(ref QuotedPrice);
            serializer.SerializeValue(ref LegacyRecipeId);
        }

        public static NetPizzaSpecDto FromDomain(PizzaSpecification spec)
        {
            if (spec == null)
            {
                spec = PizzaSpecification.CreateDefaultCheese();
            }

            int count = spec.Toppings != null ? Math.Min(4, spec.Toppings.Count) : 0;
            var dto = new NetPizzaSpecDto
            {
                BaseDoughId = spec.BaseDoughId,
                RequiredDoughStage = (byte)spec.RequiredDoughStage,
                SauceId = spec.SauceId,
                RequiresSauce = spec.RequiresSauce,
                SaucePortions = (byte)Math.Clamp(spec.SaucePortions, 0, 255),
                ToppingCount = (byte)count,
                QuotedPrice = spec.QuotedPrice,
                LegacyRecipeId = spec.LegacyRecipeId
            };

            if (count > 0)
            {
                dto.ToppingId_0 = spec.Toppings[0].IngredientDefId;
                dto.ToppingStage_0 = (byte)spec.Toppings[0].RequiredStage;
                dto.ToppingQty_0 = (byte)Math.Clamp(spec.Toppings[0].PortionCount, 1, 255);
            }
            if (count > 1)
            {
                dto.ToppingId_1 = spec.Toppings[1].IngredientDefId;
                dto.ToppingStage_1 = (byte)spec.Toppings[1].RequiredStage;
                dto.ToppingQty_1 = (byte)Math.Clamp(spec.Toppings[1].PortionCount, 1, 255);
            }
            if (count > 2)
            {
                dto.ToppingId_2 = spec.Toppings[2].IngredientDefId;
                dto.ToppingStage_2 = (byte)spec.Toppings[2].RequiredStage;
                dto.ToppingQty_2 = (byte)Math.Clamp(spec.Toppings[2].PortionCount, 1, 255);
            }
            if (count > 3)
            {
                dto.ToppingId_3 = spec.Toppings[3].IngredientDefId;
                dto.ToppingStage_3 = (byte)spec.Toppings[3].RequiredStage;
                dto.ToppingQty_3 = (byte)Math.Clamp(spec.Toppings[3].PortionCount, 1, 255);
            }

            return dto;
        }

        public PizzaSpecification ToDomain()
        {
            var toppings = new List<ToppingRequirement>(ToppingCount);
            if (ToppingCount > 0 && ToppingId_0 > 0)
                toppings.Add(new ToppingRequirement(ToppingId_0, (ProcessingStage)ToppingStage_0, Math.Max(1, (int)ToppingQty_0)));
            if (ToppingCount > 1 && ToppingId_1 > 0)
                toppings.Add(new ToppingRequirement(ToppingId_1, (ProcessingStage)ToppingStage_1, Math.Max(1, (int)ToppingQty_1)));
            if (ToppingCount > 2 && ToppingId_2 > 0)
                toppings.Add(new ToppingRequirement(ToppingId_2, (ProcessingStage)ToppingStage_2, Math.Max(1, (int)ToppingQty_2)));
            if (ToppingCount > 3 && ToppingId_3 > 0)
                toppings.Add(new ToppingRequirement(ToppingId_3, (ProcessingStage)ToppingStage_3, Math.Max(1, (int)ToppingQty_3)));

            return new PizzaSpecification(
                BaseDoughId > 0 ? BaseDoughId : 2,
                SauceId > 0 ? SauceId : 3,
                RequiresSauce || BaseDoughId == 0,
                toppings,
                QuotedPrice > 0 ? QuotedPrice : 15,
                15f,
                (ProcessingStage)RequiredDoughStage,
                SaucePortions > 0 ? SaucePortions : 1,
                LegacyRecipeId);
        }

        public bool Equals(NetPizzaSpecDto other)
        {
            return BaseDoughId == other.BaseDoughId &&
                   RequiredDoughStage == other.RequiredDoughStage &&
                   SauceId == other.SauceId &&
                   RequiresSauce == other.RequiresSauce &&
                   SaucePortions == other.SaucePortions &&
                   ToppingCount == other.ToppingCount &&
                   ToppingId_0 == other.ToppingId_0 &&
                   ToppingStage_0 == other.ToppingStage_0 &&
                   ToppingQty_0 == other.ToppingQty_0 &&
                   ToppingId_1 == other.ToppingId_1 &&
                   ToppingStage_1 == other.ToppingStage_1 &&
                   ToppingQty_1 == other.ToppingQty_1 &&
                   ToppingId_2 == other.ToppingId_2 &&
                   ToppingStage_2 == other.ToppingStage_2 &&
                   ToppingQty_2 == other.ToppingQty_2 &&
                   ToppingId_3 == other.ToppingId_3 &&
                   ToppingStage_3 == other.ToppingStage_3 &&
                   ToppingQty_3 == other.ToppingQty_3 &&
                   QuotedPrice == other.QuotedPrice &&
                   LegacyRecipeId == other.LegacyRecipeId;
        }

        public override bool Equals(object obj) => obj is NetPizzaSpecDto other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(BaseDoughId, SauceId, ToppingCount, ToppingId_0, ToppingId_1, QuotedPrice);
    }

    public struct NetItemDto : INetworkSerializable, IEquatable<NetItemDto>
    {
        public ulong ItemId;
        public int DefinitionId;
        public int BaseDoughDefinitionId;
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
        public int LabelAddressId;
        public ulong ReservedByHold;
        public uint Revision;
        public byte IngredientMask;
        public byte IngredientCount;
        public int IngredientId_0;
        public int IngredientId_1;
        public int IngredientId_2;
        public int IngredientId_3;

        public int LabelDestinationId
        {
            get => LabelAddressId;
            set => LabelAddressId = value;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ItemId);
            serializer.SerializeValue(ref DefinitionId);
            serializer.SerializeValue(ref BaseDoughDefinitionId);
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
            serializer.SerializeValue(ref LabelAddressId);
            serializer.SerializeValue(ref ReservedByHold);
            serializer.SerializeValue(ref Revision);
            serializer.SerializeValue(ref IngredientMask);
            serializer.SerializeValue(ref IngredientCount);
            serializer.SerializeValue(ref IngredientId_0);
            serializer.SerializeValue(ref IngredientId_1);
            serializer.SerializeValue(ref IngredientId_2);
            serializer.SerializeValue(ref IngredientId_3);
        }

        public bool Equals(NetItemDto other)
            => ItemId == other.ItemId &&
               Revision == other.Revision &&
               BaseDoughDefinitionId == other.BaseDoughDefinitionId &&
               LocationType == other.LocationType &&
               HolderId == other.HolderId &&
               SlotId == other.SlotId &&
               CookingStage == other.CookingStage &&
               PackagingState == other.PackagingState &&
               LabelAddressId == other.LabelAddressId &&
               ReservedByHold == other.ReservedByHold &&
               IngredientMask == other.IngredientMask &&
               IngredientCount == other.IngredientCount &&
               IngredientId_0 == other.IngredientId_0 &&
               IngredientId_1 == other.IngredientId_1 &&
               IngredientId_2 == other.IngredientId_2 &&
               IngredientId_3 == other.IngredientId_3 &&
               Math.Abs(CookProgress - other.CookProgress) < 0.005f &&
               Math.Abs(BurnProgress - other.BurnProgress) < 0.005f;

        public override bool Equals(object obj) => obj is NetItemDto other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(ItemId, Revision, LocationType, HolderId, SlotId, CookingStage);
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
        public ulong SecondaryItemId;
        public byte Kind;
        public ulong ToolItemId;
        public ulong WorkerId;
        public ulong InitiatorClientId;
        public byte Phase;
        public float Accumulated;
        public float Duration;
        public double ServerTime;
        public int ProcessId;
        public uint Revision;

        public float Progress => Duration > 0 ? Math.Clamp(Accumulated / Duration, 0f, 1f) : 0f;
        public float CapturedDuration => Duration;
        public double OperationStartTime => ServerTime;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref StationId);
            serializer.SerializeValue(ref SlotIndex);
            serializer.SerializeValue(ref HeldItemId);
            serializer.SerializeValue(ref SecondaryItemId);
            serializer.SerializeValue(ref Kind);
            serializer.SerializeValue(ref ToolItemId);
            serializer.SerializeValue(ref WorkerId);
            serializer.SerializeValue(ref InitiatorClientId);
            serializer.SerializeValue(ref Phase);
            serializer.SerializeValue(ref Accumulated);
            serializer.SerializeValue(ref Duration);
            serializer.SerializeValue(ref ServerTime);
            serializer.SerializeValue(ref ProcessId);
            serializer.SerializeValue(ref Revision);
        }

        public bool Equals(NetStationSlotDto other)
            => StationId == other.StationId && SlotIndex == other.SlotIndex && HeldItemId == other.HeldItemId && SecondaryItemId == other.SecondaryItemId && Kind == other.Kind && WorkerId == other.WorkerId && InitiatorClientId == other.InitiatorClientId && Phase == other.Phase && Revision == other.Revision && Math.Abs(Accumulated - other.Accumulated) < 0.005f;

        public override bool Equals(object obj) => obj is NetStationSlotDto other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(StationId, SlotIndex, Revision);
    }

    public struct NetOrderDto : INetworkSerializable, IEquatable<NetOrderDto>
    {
        public ulong OrderId;
        public byte SourceType;
        public int DestinationId;
        public int TableId;
        public ulong CustomerId;
        public byte Lifecycle;
        public double DeadlineTime;
        public int QuotedReward;
        public int FinalEarnedReward;
        public bool TicketArchived;
        public int LineCount;
        public uint Revision;

        public NetPizzaSpecDto Spec_0;
        public int RecipeId_0;
        public int Qty_0;
        public NetPizzaSpecDto Spec_1;
        public int RecipeId_1;
        public int Qty_1;
        public NetPizzaSpecDto Spec_2;
        public int RecipeId_2;
        public int Qty_2;

        public bool IsDineIn => SourceType == (byte)OrderSourceType.WalkIn || TableId >= 0 || CustomerId > 0 || DestinationId >= 1000;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref OrderId);
            serializer.SerializeValue(ref SourceType);
            serializer.SerializeValue(ref DestinationId);
            serializer.SerializeValue(ref TableId);
            serializer.SerializeValue(ref CustomerId);
            serializer.SerializeValue(ref Lifecycle);
            serializer.SerializeValue(ref DeadlineTime);
            serializer.SerializeValue(ref QuotedReward);
            serializer.SerializeValue(ref FinalEarnedReward);
            serializer.SerializeValue(ref TicketArchived);
            serializer.SerializeValue(ref LineCount);

            Spec_0.NetworkSerialize(serializer);
            serializer.SerializeValue(ref RecipeId_0);
            serializer.SerializeValue(ref Qty_0);
            Spec_1.NetworkSerialize(serializer);
            serializer.SerializeValue(ref RecipeId_1);
            serializer.SerializeValue(ref Qty_1);
            Spec_2.NetworkSerialize(serializer);
            serializer.SerializeValue(ref RecipeId_2);
            serializer.SerializeValue(ref Qty_2);

            serializer.SerializeValue(ref Revision);
        }

        public bool Equals(NetOrderDto other)
            => OrderId == other.OrderId &&
               SourceType == other.SourceType &&
               DestinationId == other.DestinationId &&
               TableId == other.TableId &&
               CustomerId == other.CustomerId &&
               Lifecycle == other.Lifecycle &&
               TicketArchived == other.TicketArchived &&
               Spec_0.Equals(other.Spec_0) &&
               Spec_1.Equals(other.Spec_1) &&
               Spec_2.Equals(other.Spec_2) &&
               Revision == other.Revision;

        public override bool Equals(object obj) => obj is NetOrderDto other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(OrderId, Lifecycle, TicketArchived, Revision);
    }

    public struct NetOrderLineDto : INetworkSerializable, IEquatable<NetOrderLineDto>
    {
        public ulong OrderLineId;
        public ulong OrderId;
        public int RecipeId;
        public NetPizzaSpecDto Spec;
        public int RequestedQuantity;
        public int DeliveredQuantity;
        public uint Revision;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref OrderLineId);
            serializer.SerializeValue(ref OrderId);
            serializer.SerializeValue(ref RecipeId);
            Spec.NetworkSerialize(serializer);
            serializer.SerializeValue(ref RequestedQuantity);
            serializer.SerializeValue(ref DeliveredQuantity);
            serializer.SerializeValue(ref Revision);
        }

        public bool Equals(NetOrderLineDto other)
            => OrderLineId == other.OrderLineId &&
               RequestedQuantity == other.RequestedQuantity &&
               DeliveredQuantity == other.DeliveredQuantity &&
               Spec.Equals(other.Spec) &&
               Revision == other.Revision;

        public override bool Equals(object obj) => obj is NetOrderLineDto other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(OrderLineId, Revision);
    }

    public struct NetCallDto : INetworkSerializable, IEquatable<NetCallDto>
    {
        public ulong CallId;
        public int AddressId;
        public float ExpiresAt;
        public NetPizzaSpecDto PrimarySpec;
        public int PrimaryQty;
        public uint Revision;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref CallId);
            serializer.SerializeValue(ref AddressId);
            serializer.SerializeValue(ref ExpiresAt);
            PrimarySpec.NetworkSerialize(serializer);
            serializer.SerializeValue(ref PrimaryQty);
            serializer.SerializeValue(ref Revision);
        }

        public bool Equals(NetCallDto other)
            => CallId == other.CallId && PrimarySpec.Equals(other.PrimarySpec) && Revision == other.Revision;

        public override bool Equals(object obj) => obj is NetCallDto other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(CallId, Revision);
    }

    public struct NetStackDto : INetworkSerializable, IEquatable<NetStackDto>
    {
        public ulong StackId;
        public int StationId;
        public int Capacity;
        public int ItemCount;
        public ulong TopItemId;
        public uint Revision;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref StackId);
            serializer.SerializeValue(ref StationId);
            serializer.SerializeValue(ref Capacity);
            serializer.SerializeValue(ref ItemCount);
            serializer.SerializeValue(ref TopItemId);
            serializer.SerializeValue(ref Revision);
        }

        public bool Equals(NetStackDto other)
            => StackId == other.StackId && ItemCount == other.ItemCount && TopItemId == other.TopItemId && Revision == other.Revision;

        public override bool Equals(object obj) => obj is NetStackDto other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(StackId, Revision);
    }

    public struct NetBackpackDto : INetworkSerializable, IEquatable<NetBackpackDto>
    {
        public ulong BackpackId;
        public ulong EquippedBy;
        public float PosX, PosY, PosZ;
        public float RotX, RotY, RotZ, RotW;
        public int Capacity;
        public int ItemCount;
        public uint Revision;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref BackpackId);
            serializer.SerializeValue(ref EquippedBy);
            serializer.SerializeValue(ref PosX);
            serializer.SerializeValue(ref PosY);
            serializer.SerializeValue(ref PosZ);
            serializer.SerializeValue(ref RotX);
            serializer.SerializeValue(ref RotY);
            serializer.SerializeValue(ref RotZ);
            serializer.SerializeValue(ref RotW);
            serializer.SerializeValue(ref Capacity);
            serializer.SerializeValue(ref ItemCount);
            serializer.SerializeValue(ref Revision);
        }

        public bool Equals(NetBackpackDto other)
            => BackpackId == other.BackpackId && EquippedBy == other.EquippedBy && ItemCount == other.ItemCount && Revision == other.Revision;

        public override bool Equals(object obj) => obj is NetBackpackDto other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(BackpackId, Revision);
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

    public struct NetCustomerDto : INetworkSerializable, IEquatable<NetCustomerDto>
    {
        public ulong CustomerId;
        public byte Phase;
        public int QueueIndex;
        public int QueueSlotIndex;
        public int AssignedTableId;
        public ulong OrderId;
        public NetPizzaSpecDto RequestedSpec;
        public int PositiveTraitId;
        public int NegativeTraitId;
        public int VisualColorIndex;
        public float QueuePatienceMax;
        public float QueuePatienceRemaining;
        public float FoodPatienceMax;
        public float FoodPatienceRemaining;
        public ulong AttendedByPlayerId;
        public float AttendProgressNormalized;
        public uint Revision;

        public bool IsInQueue => Phase == (byte)CustomerPhase.EnteringQueue ||
                                 Phase == (byte)CustomerPhase.WaitingInQueue ||
                                 Phase == (byte)CustomerPhase.BeingAttended;

        public bool IsWaitingForFood => Phase == (byte)CustomerPhase.WalkingToTable ||
                                        Phase == (byte)CustomerPhase.SeatedWaitingFood;

        public float ActivePatienceNormalized
        {
            get
            {
                if (IsInQueue)
                    return QueuePatienceMax > 0.01f ? Math.Clamp(QueuePatienceRemaining / QueuePatienceMax, 0f, 1f) : 1f;
                if (IsWaitingForFood)
                    return FoodPatienceMax > 0.01f ? Math.Clamp(FoodPatienceRemaining / FoodPatienceMax, 0f, 1f) : 1f;
                return 1f;
            }
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref CustomerId);
            serializer.SerializeValue(ref Phase);
            serializer.SerializeValue(ref QueueIndex);
            serializer.SerializeValue(ref QueueSlotIndex);
            serializer.SerializeValue(ref AssignedTableId);
            serializer.SerializeValue(ref OrderId);
            RequestedSpec.NetworkSerialize(serializer);
            serializer.SerializeValue(ref PositiveTraitId);
            serializer.SerializeValue(ref NegativeTraitId);
            serializer.SerializeValue(ref VisualColorIndex);
            serializer.SerializeValue(ref QueuePatienceMax);
            serializer.SerializeValue(ref QueuePatienceRemaining);
            serializer.SerializeValue(ref FoodPatienceMax);
            serializer.SerializeValue(ref FoodPatienceRemaining);
            serializer.SerializeValue(ref AttendedByPlayerId);
            serializer.SerializeValue(ref AttendProgressNormalized);
            serializer.SerializeValue(ref Revision);
        }

        public bool Equals(NetCustomerDto other)
            => CustomerId == other.CustomerId &&
               Phase == other.Phase &&
               QueueIndex == other.QueueIndex &&
               QueueSlotIndex == other.QueueSlotIndex &&
               AssignedTableId == other.AssignedTableId &&
               OrderId == other.OrderId &&
               PositiveTraitId == other.PositiveTraitId &&
               NegativeTraitId == other.NegativeTraitId &&
               AttendedByPlayerId == other.AttendedByPlayerId &&
               Math.Abs(AttendProgressNormalized - other.AttendProgressNormalized) < 0.01f &&
               Math.Abs(QueuePatienceRemaining - other.QueuePatienceRemaining) < 0.25f &&
               Math.Abs(FoodPatienceRemaining - other.FoodPatienceRemaining) < 0.25f &&
               Revision == other.Revision;

        public override bool Equals(object obj) => obj is NetCustomerDto other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(CustomerId, Phase, QueueIndex, AssignedTableId, Revision);
    }

    public struct NetTableDto : INetworkSerializable, IEquatable<NetTableDto>
    {
        public int TableId;
        public ulong ReservedByCustomerId;
        public ulong ActiveOrderId;
        public ulong ServedPizzaItemId;
        public uint Revision;

        public bool IsFree => ReservedByCustomerId == 0;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref TableId);
            serializer.SerializeValue(ref ReservedByCustomerId);
            serializer.SerializeValue(ref ActiveOrderId);
            serializer.SerializeValue(ref ServedPizzaItemId);
            serializer.SerializeValue(ref Revision);
        }

        public bool Equals(NetTableDto other)
            => TableId == other.TableId &&
               ReservedByCustomerId == other.ReservedByCustomerId &&
               ActiveOrderId == other.ActiveOrderId &&
               ServedPizzaItemId == other.ServedPizzaItemId &&
               Revision == other.Revision;

        public override bool Equals(object obj) => obj is NetTableDto other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(TableId, ReservedByCustomerId, ServedPizzaItemId, Revision);
    }

    public static class LocationRefSerializationExtensions
    {
        public static void WriteValueSafe(this FastBufferWriter writer, in LocationRef location)
        {
            writer.WriteValueSafe((byte)location.Type);
            writer.WriteValueSafe(location.HolderId);
            writer.WriteValueSafe(location.SlotId);
            writer.WriteValueSafe(location.WorldX);
            writer.WriteValueSafe(location.WorldY);
            writer.WriteValueSafe(location.WorldZ);
        }

        public static void ReadValueSafe(this FastBufferReader reader, out LocationRef location)
        {
            reader.ReadValueSafe(out byte type);
            reader.ReadValueSafe(out ulong holderId);
            reader.ReadValueSafe(out int slotId);
            reader.ReadValueSafe(out float x);
            reader.ReadValueSafe(out float y);
            reader.ReadValueSafe(out float z);
            location = new LocationRef((LocationType)type, holderId, slotId, x, y, z);
        }
    }
}
