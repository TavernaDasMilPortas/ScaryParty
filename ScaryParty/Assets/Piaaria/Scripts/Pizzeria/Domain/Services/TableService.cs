using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Services
{
    /// <summary>
    /// Gerencia o estado das mesas do salão (1 mesa inteira por NPC nesta versão).
    /// - Não permite registrar pedido presencial sem reservar uma mesa livre.
    /// - Libera a mesa quando o cliente termina de comer e sai, ou quando o pedido expira/cancela.
    /// </summary>
    public class TableService
    {
        public void EnsureTablesInitialized(PizzeriaState state, IEnumerable<int> tableIds)
        {
            if (state == null || tableIds == null) return;
            foreach (int id in tableIds)
            {
                if (id <= 0) continue;
                if (!state.Tables.ContainsKey(id))
                {
                    state.Tables[id] = new TableState(id);
                }
            }
        }

        public void EnsureDefaultTablesIfEmpty(PizzeriaState state, int defaultTableCount = 4)
        {
            if (state == null || state.Tables.Count > 0) return;
            for (int i = 1; i <= defaultTableCount; i++)
            {
                state.Tables[i] = new TableState(i);
            }
        }

        public bool HasFreeTable(PizzeriaState state)
        {
            if (state == null) return false;
            foreach (var table in state.Tables.Values)
            {
                if (table.IsFree) return true;
            }
            return false;
        }

        public TableState FindFirstFreeTable(PizzeriaState state)
        {
            if (state == null) return null;
            TableState best = null;
            foreach (var table in state.Tables.Values)
            {
                if (!table.IsFree) continue;
                if (best == null || table.TableId < best.TableId)
                {
                    best = table;
                }
            }
            return best;
        }

        public CommandResult<int> TryReserveFreeTable(PizzeriaState state, CustomerId customerId, OrderId orderId)
        {
            if (state == null)
                return CommandResult<int>.Fail(CommandError.InvalidState, "Estado inválido.");

            var freeTable = FindFirstFreeTable(state);
            if (freeTable == null)
            {
                return CommandResult<int>.Fail(CommandError.SlotFull, "Todas as mesas estão ocupadas! Aguarde liberar uma mesa.");
            }

            freeTable.Reserve(customerId, orderId);
            state.IncrementRevision();
            return CommandResult<int>.Ok(freeTable.TableId);
        }

        public void ReleaseTable(PizzeriaState state, int tableId)
        {
            if (state == null || tableId <= 0) return;
            if (state.Tables.TryGetValue(tableId, out var table))
            {
                table.Release();
                state.IncrementRevision();
            }
        }

        public void ReleaseTableForCustomer(PizzeriaState state, CustomerId customerId)
        {
            if (state == null || !customerId.IsValid) return;
            foreach (var table in state.Tables.Values)
            {
                if (table.ReservedByCustomerId == customerId)
                {
                    table.Release();
                    state.IncrementRevision();
                }
            }
        }
    }
}
