using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Models
{
    public class OrderLineState
    {
        public OrderLineId Id { get; }
        public int RecipeId { get; }
        public int RequestedQuantity { get; set; }
        public int DeliveredQuantity { get; set; }

        public OrderLineState(OrderLineId id, int recipeId, int requestedQuantity)
        {
            Id = id;
            RecipeId = recipeId;
            RequestedQuantity = requestedQuantity;
            DeliveredQuantity = 0;
        }

        public OrderLineState(int recipeId, int requestedQuantity)
            : this(OrderLineId.None, recipeId, requestedQuantity)
        {
        }
    }

    public class OrderState
    {
        public OrderId Id { get; }
        public OrderSourceType SourceType { get; }
        public AddressId DestinationId { get; }
        public CustomerId CustomerId { get; }
        public OrderLifecycle Lifecycle { get; set; }
        public double CreatedTime { get; }
        public double DeadlineTime { get; set; }
        public int QuotedPotentialReward { get; set; }
        public int FinalEarnedReward { get; set; }
        public bool TicketArchived { get; set; } = false;
        public uint Revision { get; set; } = 1;

        private readonly List<OrderLineState> _lines = new List<OrderLineState>();
        public IReadOnlyList<OrderLineState> Lines => _lines;

        public OrderState(OrderId id, OrderSourceType sourceType, AddressId destinationId, CustomerId customerId, double createdTime, double deadlineTime)
        {
            Id = id;
            SourceType = sourceType;
            DestinationId = destinationId;
            CustomerId = customerId;
            Lifecycle = OrderLifecycle.Accepted;
            CreatedTime = createdTime;
            DeadlineTime = deadlineTime;
        }

        public void AddLine(OrderLineId lineId, int recipeId, int quantity)
        {
            _lines.Add(new OrderLineState(lineId, recipeId, quantity));
            Revision++;
        }

        public void SetLines(IEnumerable<OrderLineState> lines)
        {
            _lines.Clear();
            _lines.AddRange(lines);
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
