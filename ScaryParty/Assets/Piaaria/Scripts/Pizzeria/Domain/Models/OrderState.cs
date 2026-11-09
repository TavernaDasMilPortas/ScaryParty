using System;
using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Models
{
    public class OrderLineState
    {
        public OrderLineId Id { get; }
        public PizzaSpecification Specification { get; }
        public int RecipeId => Specification != null && Specification.LegacyRecipeId > 0 ? Specification.LegacyRecipeId : _legacyRecipeId;
        public int RequestedQuantity { get; set; }
        public int Quantity
        {
            get => RequestedQuantity;
            set => RequestedQuantity = value;
        }
        public int DeliveredQuantity { get; set; }

        private readonly int _legacyRecipeId;

        public OrderLineState(OrderLineId id, PizzaSpecification specification, int requestedQuantity, int legacyRecipeId = 0)
        {
            Id = id;
            Specification = specification ?? PizzaSpecification.CreateDefaultCheese();
            _legacyRecipeId = legacyRecipeId > 0 ? legacyRecipeId : Specification.LegacyRecipeId;
            RequestedQuantity = Math.Max(1, requestedQuantity);
            DeliveredQuantity = 0;
        }

        public OrderLineState(PizzaSpecification specification, int requestedQuantity)
            : this(OrderLineId.None, specification, requestedQuantity)
        {
        }

        public OrderLineState(OrderLineId id, int recipeId, int requestedQuantity)
        {
            Id = id;
            _legacyRecipeId = recipeId;
            Specification = CreateSpecFromLegacyRecipeId(recipeId);
            RequestedQuantity = Math.Max(1, requestedQuantity);
            DeliveredQuantity = 0;
        }

        public OrderLineState(int recipeId, int requestedQuantity)
            : this(OrderLineId.None, recipeId, requestedQuantity)
        {
        }

        public static PizzaSpecification CreateSpecFromLegacyRecipeId(int recipeId)
        {
            if (recipeId == 2)
            {
                return new PizzaSpecification(
                    2, 3, true,
                    new[]
                    {
                        new ToppingRequirement(5, ProcessingStage.Prepared, 1),
                        new ToppingRequirement(7, ProcessingStage.Prepared, 1)
                    },
                    20, 15f, ProcessingStage.Prepared, 1, 2);
            }
            if (recipeId == 3)
            {
                return new PizzaSpecification(
                    2, 3, true,
                    new[]
                    {
                        new ToppingRequirement(5, ProcessingStage.Prepared, 1),
                        new ToppingRequirement(9, ProcessingStage.Prepared, 1)
                    },
                    22, 15f, ProcessingStage.Prepared, 1, 3);
            }
            return PizzaSpecification.CreateDefaultCheese(15, 15f);
        }
    }

    public class OrderState
    {
        public OrderId Id { get; }
        public OrderSourceType SourceType { get; }
        public AddressId DestinationId { get; }
        public int TableId { get; set; } = -1;
        public CustomerId CustomerId { get; }
        public OrderLifecycle Lifecycle { get; set; }
        public double CreatedTime { get; }
        public double DeadlineTime { get; set; }
        public int QuotedPotentialReward { get; set; }
        public int FinalEarnedReward { get; set; }
        public bool TicketArchived { get; set; } = false;
        public bool IsArchivedFromBoard
        {
            get => TicketArchived;
            set => TicketArchived = value;
        }
        public uint Revision { get; set; } = 1;

        public bool IsDineIn => SourceType == OrderSourceType.WalkIn || TableId >= 0 || CustomerId.IsValid;

        private readonly List<OrderLineState> _lines = new List<OrderLineState>();
        public IReadOnlyList<OrderLineState> Lines => _lines;

        public OrderState(
            OrderId id,
            OrderSourceType sourceType,
            AddressId destinationId,
            CustomerId customerId,
            double createdTime,
            double deadlineTime,
            int tableId = -1)
        {
            Id = id;
            SourceType = sourceType;
            DestinationId = destinationId;
            CustomerId = customerId;
            TableId = tableId;
            Lifecycle = OrderLifecycle.Accepted;
            CreatedTime = createdTime;
            DeadlineTime = deadlineTime;
        }

        public void AddLine(OrderLineId lineId, PizzaSpecification specification, int quantity)
        {
            _lines.Add(new OrderLineState(lineId, specification, quantity));
            Revision++;
        }

        public void AddLine(OrderLineId lineId, int recipeId, int quantity)
        {
            _lines.Add(new OrderLineState(lineId, recipeId, quantity));
            Revision++;
        }

        public void SetLines(IEnumerable<OrderLineState> lines)
        {
            _lines.Clear();
            if (lines != null) _lines.AddRange(lines);
            Revision++;
        }

        public int TotalRequestedCount
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < _lines.Count; i++)
                    sum += _lines[i].RequestedQuantity;
                return sum;
            }
        }
    }
}
