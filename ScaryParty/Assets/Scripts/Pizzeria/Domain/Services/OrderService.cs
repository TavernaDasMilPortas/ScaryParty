using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Ports;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Services
{
    public class OrderService
    {
        public CommandResult<OrderId> CreatePhoneOrder(
            AddressId destinationId,
            IReadOnlyList<(int recipeId, int qty)> requestedRecipes,
            PizzeriaState state,
            DefinitionCatalog catalog,
            IClock clock,
            int maxActiveOrders,
            double deliveryDuration = 300.0)
        {
            if (requestedRecipes == null || requestedRecipes.Count == 0)
                return CommandResult<OrderId>.Fail(CommandError.Incompatible, "Nenhuma pizza solicitada.");

            int activeCount = 0;
            foreach (var kvp in state.Orders)
            {
                if (kvp.Value.Lifecycle == OrderLifecycle.Accepted)
                {
                    if (kvp.Value.DestinationId == destinationId)
                    {
                        return CommandResult<OrderId>.Fail(CommandError.AlreadyAllocated, "Já existe pedido em andamento para este endereço.");
                    }
                    activeCount++;
                }
            }

            if (activeCount >= maxActiveOrders)
                return CommandResult<OrderId>.Fail(CommandError.LimitReached, "Limite máximo de pedidos ativos alcançado.");

            OrderId orderId = state.GenerateOrderId();
            double now = clock.Now;
            var order = new OrderState(orderId, OrderSourceType.Phone, destinationId, CustomerId.None, now, now + deliveryDuration);

            int totalReward = 0;
            for (int i = 0; i < requestedRecipes.Count; i++)
            {
                var req = requestedRecipes[i];
                order.AddLine(state.GenerateOrderLineId(), req.recipeId, req.qty);

                if (catalog.Recipes.TryGetValue(req.recipeId, out var recipe))
                {
                    totalReward += recipe.BasePrice * req.qty;
                }
            }

            order.QuotedPotentialReward = totalReward;
            state.Orders[orderId] = order;
            state.IncrementRevision();

            return CommandResult<OrderId>.Ok(orderId);
        }

        public OrderState GetActiveOrderForDestination(AddressId destinationId, PizzeriaState state)
        {
            foreach (var kvp in state.Orders)
            {
                if (kvp.Value.DestinationId == destinationId && kvp.Value.Lifecycle == OrderLifecycle.Accepted)
                    return kvp.Value;
            }
            return null;
        }

        public void TickOrders(PizzeriaState state, IClock clock)
        {
            double now = clock.Now;
            foreach (var kvp in state.Orders)
            {
                var order = kvp.Value;
                if (order.Lifecycle == OrderLifecycle.Accepted && now >= order.DeadlineTime)
                {
                    order.Lifecycle = OrderLifecycle.Expired;
                    order.Revision++;
                    state.IncrementRevision();
                }
            }
        }

        public CommandResult ArchiveTicket(OrderId orderId, PizzeriaState state)
        {
            if (!state.Orders.TryGetValue(orderId, out var order))
                return CommandResult.Fail(CommandError.ItemNotFound, "Pedido não encontrado.");

            if (order.TicketArchived)
                return CommandResult.Ok(); // Idempotent

            order.TicketArchived = true;
            order.Revision++;
            state.IncrementRevision();
            return CommandResult.Ok();
        }

        public CommandResult RestoreTicket(OrderId orderId, PizzeriaState state)
        {
            if (!state.Orders.TryGetValue(orderId, out var order))
                return CommandResult.Fail(CommandError.ItemNotFound, "Pedido não encontrado.");

            if (order.Lifecycle != OrderLifecycle.Accepted)
                return CommandResult.Fail(CommandError.InvalidOperation, "Não é possível restaurar um pedido expirado ou concluído.");

            if (!order.TicketArchived)
                return CommandResult.Ok(); // Idempotent

            order.TicketArchived = false;
            order.Revision++;
            state.IncrementRevision();
            return CommandResult.Ok();
        }

        public CommandResult CancelOrder(OrderId orderId, PizzeriaState state)
        {
            if (!state.Orders.TryGetValue(orderId, out var order))
                return CommandResult.Fail(CommandError.ItemNotFound, "Pedido não encontrado.");

            if (order.Lifecycle != OrderLifecycle.Accepted)
                return CommandResult.Fail(CommandError.InvalidOperation, "Pedido não está ativo.");

            order.Lifecycle = OrderLifecycle.Cancelled;
            order.Revision++;
            state.IncrementRevision();
            return CommandResult.Ok();
        }
    }
}
