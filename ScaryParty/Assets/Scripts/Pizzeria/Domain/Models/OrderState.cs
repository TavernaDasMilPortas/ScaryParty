using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Models
{
    public class OrderLine
    {
        public OrderLineId Id { get; }
        public int RecipeId { get; }
        public int RequestedQuantity { get; set; }
        public int DeliveredQuantity { get; set; }

        public OrderLine(OrderLineId id, int recipeId, int requestedQuantity)
        {
            Id = id;
            RecipeId = recipeId;
            RequestedQuantity = requestedQuantity;
            DeliveredQuantity = 0;
        }
    }

    public class OrderState
    {
        public OrderId Id { get; }
        public OrderSourceType SourceType { get; }
        public int DestinationId { get; }
        public CustomerId CustomerId { get; }
        public OrderLifecycle Lifecycle { get; set; }
        public double CreatedTime { get; }
        public double DeadlineTime { get; set; }
        public int QuotedPotentialReward { get; set; }
        public int FinalEarnedReward { get; set; }
        public uint Revision { get; set; } = 1;

        private readonly List<OrderLine> _lines = new List<OrderLine>(4);
        public IReadOnlyList<OrderLine> Lines => _lines;

        public OrderState(OrderId id, OrderSourceType sourceType, int destinationId, CustomerId customerId, double createdTime, double deadlineTime)
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
            _lines.Add(new OrderLine(lineId, recipeId, quantity));
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
