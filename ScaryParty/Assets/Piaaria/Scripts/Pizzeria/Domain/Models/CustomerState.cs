using System;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Models
{
    public enum CustomerPhase : byte
    {
        EnteringQueue = 0,
        WaitingInQueue = 1,
        BeingAttended = 2,
        WalkingToTable = 3,
        SeatedWaitingFood = 4,
        Eating = 5,
        LeavingSatisfied = 6,
        LeavingAngry = 7,
        Despawned = 8
    }

    /// <summary>
    /// Estado autoritativo de uma mesa do salão.
    /// Nesta versão: 1 mesa inteira por NPC, reservada atomicamente quando o atendimento no balcão conclui.
    /// </summary>
    public sealed class TableState
    {
        public int TableId { get; }
        public CustomerId ReservedByCustomerId { get; set; }
        public OrderId ActiveOrderId { get; set; }
        public ItemId ServedPizzaItemId { get; set; }
        public uint Revision { get; set; } = 1;

        public bool IsFree => !ReservedByCustomerId.IsValid;

        public TableState(int tableId)
        {
            TableId = tableId;
            ReservedByCustomerId = CustomerId.None;
            ActiveOrderId = OrderId.None;
            ServedPizzaItemId = ItemId.None;
        }

        public void Reserve(CustomerId customerId, OrderId orderId)
        {
            ReservedByCustomerId = customerId;
            ActiveOrderId = orderId;
            ServedPizzaItemId = ItemId.None;
            Revision++;
        }

        public void Release()
        {
            ReservedByCustomerId = CustomerId.None;
            ActiveOrderId = OrderId.None;
            ServedPizzaItemId = ItemId.None;
            Revision++;
        }
    }

    /// <summary>
    /// Estado autoritativo de um cliente presencial (NPC) na pizzaria.
    /// </summary>
    public sealed class CustomerState
    {
        public CustomerId Id { get; }
        public CustomerPhase Phase { get; set; }
        public int QueueIndex { get; set; }
        public int QueueSlotIndex { get; set; }
        public int AssignedTableId { get; set; }
        public OrderId OrderId { get; set; }
        public PizzaSpecification RequestedPizza { get; set; }

        public int PositiveTraitId { get; set; }
        public int NegativeTraitId { get; set; }
        public int VisualColorIndex { get; set; }

        public float QueuePatienceMax { get; set; }
        public float QueuePatienceRemaining { get; set; }

        public float FoodPatienceMax { get; set; }
        public float FoodPatienceRemaining { get; set; }

        public float EatingRemainingSeconds { get; set; }
        public float LeavingRemainingSeconds { get; set; }

        public ulong AttendedByPlayerId { get; set; }
        public bool HasPaid { get; set; }
        public uint Revision { get; set; } = 1;

        public bool IsInQueue => Phase == CustomerPhase.EnteringQueue ||
                                 Phase == CustomerPhase.WaitingInQueue ||
                                 Phase == CustomerPhase.BeingAttended;

        public bool IsWaitingForFood => Phase == CustomerPhase.WalkingToTable ||
                                        Phase == CustomerPhase.SeatedWaitingFood;

        public float ActivePatienceNormalized
        {
            get
            {
                if (IsInQueue)
                {
                    return QueuePatienceMax > 0.01f ? Math.Clamp(QueuePatienceRemaining / QueuePatienceMax, 0f, 1f) : 1f;
                }
                if (IsWaitingForFood)
                {
                    return FoodPatienceMax > 0.01f ? Math.Clamp(FoodPatienceRemaining / FoodPatienceMax, 0f, 1f) : 1f;
                }
                return 1f;
            }
        }

        public CustomerState(
            CustomerId id,
            int queueIndex,
            int queueSlotIndex,
            PizzaSpecification requestedPizza,
            int positiveTraitId,
            int negativeTraitId,
            float queuePatienceMax,
            float foodPatienceMax,
            int visualColorIndex = 0)
        {
            Id = id;
            Phase = CustomerPhase.WaitingInQueue;
            QueueIndex = queueIndex;
            QueueSlotIndex = queueSlotIndex;
            AssignedTableId = -1;
            OrderId = OrderId.None;
            RequestedPizza = requestedPizza ?? PizzaSpecification.CreateDefaultCheese();
            PositiveTraitId = positiveTraitId;
            NegativeTraitId = negativeTraitId;
            VisualColorIndex = visualColorIndex;
            QueuePatienceMax = Math.Max(5f, queuePatienceMax);
            QueuePatienceRemaining = QueuePatienceMax;
            FoodPatienceMax = Math.Max(10f, foodPatienceMax);
            FoodPatienceRemaining = FoodPatienceMax;
            EatingRemainingSeconds = 0f;
            LeavingRemainingSeconds = 0f;
            AttendedByPlayerId = 0;
            HasPaid = false;
        }
    }
}
