using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Services
{
    public class CallService
    {
        public CommandResult<CallId> EnqueueCall(
            PizzeriaState state,
            AddressId addressId,
            List<OrderLineState> lines,
            float now,
            float expirationSeconds)
        {
            if (state.Calls.Count >= 10)
                return CommandResult<CallId>.Fail(CommandError.LimitReached, "Limite de chamadas atingido.");

            CallId callId = state.GenerateCallId();
            var call = new PendingCall(callId, addressId, lines, now + expirationSeconds);
            state.Calls[callId] = call;
            state.IncrementRevision();

            return CommandResult<CallId>.Ok(callId);
        }

        public void TickCalls(PizzeriaState state, float now)
        {
            var expired = new List<CallId>();
            foreach (var kvp in state.Calls)
            {
                if (now >= kvp.Value.ExpiresAt)
                    expired.Add(kvp.Key);
            }

            foreach (var id in expired)
            {
                state.Calls.Remove(id);
                state.IncrementRevision();
            }
        }

        public CommandResult<OrderId> AnswerCall(
            PizzeriaState state,
            CallId callId,
            OrderService orderService,
            Definitions.DefinitionCatalog catalog,
            Ports.IClock clock,
            int maxActiveOrders,
            double deliveryDuration = 300.0)
        {
            if (!state.Calls.TryGetValue(callId, out var call))
                return CommandResult<OrderId>.Fail(CommandError.ItemNotFound, "Chamada não encontrada ou já atendida.");

            var requestedSpecs = new List<(PizzaSpecification spec, int qty)>();
            foreach (var line in call.Lines)
            {
                var spec = line.Specification;
                if (spec == null || (line.RecipeId > 0 && catalog != null && catalog.Recipes.ContainsKey(line.RecipeId)))
                {
                    spec = catalog != null
                        ? catalog.ResolveSpecificationFromRecipeOrDefault(line.RecipeId)
                        : OrderLineState.CreateSpecFromLegacyRecipeId(line.RecipeId);
                }
                requestedSpecs.Add((spec, line.RequestedQuantity));
            }

            var result = orderService.CreatePhoneOrderBySpecifications(call.AddressId, requestedSpecs, state, catalog, clock, maxActiveOrders, deliveryDuration);

            if (result.Success)
            {
                state.Calls.Remove(callId);
                state.IncrementRevision();
            }

            return result;
        }
    }
}
