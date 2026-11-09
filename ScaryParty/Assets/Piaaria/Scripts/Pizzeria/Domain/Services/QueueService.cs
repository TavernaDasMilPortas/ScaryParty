using System;
using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Services
{
    /// <summary>
    /// Gerencia as 2 filas de clientes presenciais:
    /// - O NPC escolhe a fila com mais vagas (menor ocupação) ao entrar e nunca fica trocando continuamente.
    /// - Quando o primeiro cliente da fila sai (atendido ou irritado), os clientes de trás avançam um slot.
    /// - Rastreia o instante do último avanço da fila para cálculo de estagnação (fila parada).
    /// </summary>
    public class QueueService
    {
        public int GetQueueOccupancy(PizzeriaState state, int queueIndex)
        {
            if (state == null) return 0;
            int count = 0;
            foreach (var cust in state.Customers.Values)
            {
                if (cust.QueueIndex == queueIndex && cust.IsInQueue)
                {
                    count++;
                }
            }
            return count;
        }

        private static readonly Random SharedRng = new Random();

        public int ChooseBestQueueOnEntry(
            PizzeriaState state,
            int queueCount = 2,
            int maxCustomersPerQueue = 3,
            Random rng = null)
        {
            if (state == null || queueCount <= 0) return -1;

            int bestQueue = -1;
            int bestOccupancy = int.MaxValue;
            var tiedQueues = new List<int>();

            for (int q = 0; q < queueCount; q++)
            {
                int occ = GetQueueOccupancy(state, q);
                if (occ >= maxCustomersPerQueue) continue;

                if (occ < bestOccupancy)
                {
                    bestOccupancy = occ;
                    bestQueue = q;
                    tiedQueues.Clear();
                    tiedQueues.Add(q);
                }
                else if (occ == bestOccupancy)
                {
                    tiedQueues.Add(q);
                }
            }

            if (tiedQueues.Count == 0) return -1;
            if (tiedQueues.Count == 1) return bestQueue;

            var r = rng ?? SharedRng;
            int idx = r.Next(0, tiedQueues.Count);
            return tiedQueues[idx];
        }

        public CustomerState GetFrontCustomer(PizzeriaState state, int queueIndex)
        {
            if (state == null) return null;
            CustomerState front = null;
            foreach (var cust in state.Customers.Values)
            {
                if (cust.QueueIndex == queueIndex && cust.IsInQueue && cust.QueueSlotIndex == 0)
                {
                    front = cust;
                    break;
                }
            }
            return front;
        }

        public void CompactAndAdvanceQueue(PizzeriaState state, int queueIndex, double now)
        {
            if (state == null) return;

            var inQueue = new List<CustomerState>();
            foreach (var cust in state.Customers.Values)
            {
                if (cust.QueueIndex == queueIndex && cust.IsInQueue)
                {
                    inQueue.Add(cust);
                }
            }

            inQueue.Sort((a, b) =>
            {
                int c = a.QueueSlotIndex.CompareTo(b.QueueSlotIndex);
                return c != 0 ? c : a.Id.CompareTo(b.Id);
            });

            bool anyMoved = false;
            for (int i = 0; i < inQueue.Count; i++)
            {
                if (inQueue[i].QueueSlotIndex != i)
                {
                    inQueue[i].QueueSlotIndex = i;
                    inQueue[i].Revision++;
                    anyMoved = true;
                }
            }

            state.QueueLastAdvanceTime[queueIndex] = now;
            if (anyMoved)
            {
                state.IncrementRevision();
            }
        }

        public bool IsQueueStagnant(PizzeriaState state, int queueIndex, double now, float stagnationThresholdSeconds = 35f)
        {
            if (state == null) return false;
            if (!state.QueueLastAdvanceTime.TryGetValue(queueIndex, out double lastAdv))
                return false;
            return (now - lastAdv) >= stagnationThresholdSeconds;
        }
    }
}
