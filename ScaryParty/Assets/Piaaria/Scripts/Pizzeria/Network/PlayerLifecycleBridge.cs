using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Network
{
    public class PlayerLifecycleBridge : MonoBehaviour
    {
        private void Start()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            }
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            var root = PizzeriaRoot.Instance;
            if (root != null && root.IsServer)
            {
                root.RunProgressionService?.OnParticipantDisconnected(clientId);
                root.EmployeeTaskService?.ReleaseActorTasks(clientId, isHuman: true);
            }
            CleanupPlayerWorldAndDomainStateServer(clientId);
            if (root != null && root.IsServer)
            {
                PizzeriaCommandHandler.Instance?.CleanupDisconnectedPlayerServer(clientId);
                PizzeriaCommandHandler.Instance?.ResolveAndStartPreparedNightIfReadyServer(excludedClientId: clientId);
            }
        }

        public static void CleanupPlayerWorldAndDomainStateServer(ulong clientId)
        {
            PizzeriaCommandHandler.Instance?.ReleasePlayerSessions(clientId);

            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;

            var state = root.DomainState;
            Vector3 recoveryPos = root.recoveryAnchor != null ? root.recoveryAnchor.position : root.transform.position;
            DomainVector3 domainRecoveryPos = new DomainVector3(recoveryPos.x, recoveryPos.y, recoveryPos.z);

            // 1. Cancelar holds ativos do jogador (incluindo atendimento em balcão)
            if (state.PlayerHolds.TryGetValue(clientId, out var hold))
            {
                if (hold.Kind == PlayerHoldKind.Discard && hold.TargetItemId.IsValid)
                {
                    if (state.Items.TryGetValue(hold.TargetItemId, out var item))
                        item.ReservedByHold = ItemState.NoHoldOwner;
                }
                state.PlayerHolds.Remove(clientId);
            }

            // 1b. Liberar clientes em atendimento e comandas assumidas pelo jogador
            root.CustomerService?.ReleaseCustomersAttendedByPlayer(state, clientId);
            root.OrderService?.ReleaseOrdersClaimedByPlayer(state, clientId);
            root.EmployeeTaskService?.ReleaseActorTasks(clientId, isHuman: true);

            // Cancelar montagens cujos ingredientes estavam nas mãos do jogador desconectado/morto.
            var assemblySlots = new List<StationSlotId>();
            foreach (var pair in state.ActiveOperations)
            {
                var op = pair.Value;
                if (op.Kind == OperationKind.Assemble &&
                    state.Items.TryGetValue(op.SecondaryItemId, out var ingredient) &&
                    ingredient.Location.Type == LocationType.Hand && ingredient.Location.HolderId == clientId)
                    assemblySlots.Add(pair.Key);
            }
            foreach (var slot in assemblySlots)
                PizzaAssemblyService.Cancel(state, slot);

            // 2. Pausar/liberar operações onde o jogador era o executor garantindo desbloqueio de ReservedByHold e leases
            var opsToCancel = new List<StationSlotId>();
            foreach (var kvp in state.ActiveOperations)
            {
                if (kvp.Value != null && kvp.Value.WorkerId == clientId)
                {
                    opsToCancel.Add(kvp.Key);
                }
            }
            for (int i = 0; i < opsToCancel.Count; i++)
            {
                if (root.ProcessingService != null)
                {
                    root.ProcessingService.CancelOperation(state, opsToCancel[i], clientId, (float)root.Clock.Now);
                }
                else if (state.ActiveOperations.TryGetValue(opsToCancel[i], out var op))
                {
                    op.Pause((float)root.Clock.Now);
                    if (state.Items.TryGetValue(op.InputItemId, out var relItem) && relItem.ReservedByHold == clientId)
                        relItem.ReservedByHold = ItemState.NoHoldOwner;
                    if (op.ToolItemId.IsValid && state.Tools.TryGetValue(op.ToolItemId, out var tool))
                        tool.ReleaseLease(clientId);
                }
            }

            // 3. Liberar leases de utensílios
            foreach (var tool in state.Tools.Values)
            {
                if (tool.ReservedByWorkerId == clientId)
                {
                    tool.ReleaseLease(clientId);
                }
            }

            // 4. Largar mochila equipada na posição segura
            BackpackService.ReleaseOnDisconnect(state, clientId, domainRecoveryPos);

            // 5. Itens nas mãos do jogador:
            var handItems = new List<ItemId>();
            foreach (var item in state.Items.Values)
            {
                if (item.Location.Type == LocationType.Hand && (ulong)item.Location.HolderId == clientId)
                {
                    handItems.Add(item.Id);
                }
            }

            foreach (var itemId in handItems)
            {
                // Procurar slot livre de estação ou colocar no mundo
                var freeSlot = FindFreeNeutralSlot(state, root);
                if (freeSlot.HasValue)
                {
                    TransferService.TransferItem(state, itemId, new LocationRef(LocationType.StationSlot, (ulong)freeSlot.Value.StationId, freeSlot.Value.SlotIndex), 0, bypassReservation: true);
                }
                else
                {
                    TransferService.TransferItem(state, itemId, LocationRef.InWorld(recoveryPos.x, recoveryPos.y, recoveryPos.z), 0, bypassReservation: true);
                }
            }

            // 6. Utensílios nas mãos do jogador voltam ao ToolRack
            var handTools = new List<ToolItemId>();
            foreach (var tool in state.Tools.Values)
            {
                if (tool.Location.Type == LocationType.Hand && (ulong)tool.Location.HolderId == clientId)
                {
                    handTools.Add(tool.Id);
                }
            }

            int rackStationId = state.ToolRackStationId > 0 ? state.ToolRackStationId : 3;
            foreach (var toolId in handTools)
            {
                // Voltar ao ToolRack configurado em slot livre
                int rackSlot = FindFreeToolRackSlot(state, rackStationId);
                TransferService.TransferTool(state, toolId, LocationRef.InStation(rackStationId, rackSlot), 0, root.Clock.Now);
            }

            ScaryParty.Pizzeria.Network.PizzeriaCommandHandler.Instance?.CommitAndReplicate();
            Debug.Log($"[PlayerLifecycleBridge] Limpeza do cliente {clientId} aplicada: itens, ferramentas, balcões e comandas liberados.");
        }

        public static bool IsNeutralBenchStation(ScaryParty.Pizzeria.Stations.StationView sv)
        {
            if (sv == null) return false;
            return sv is ScaryParty.Pizzeria.Stations.PrepStation ||
                   sv is ScaryParty.Pizzeria.Stations.AssemblyStation ||
                   sv is ScaryParty.Pizzeria.Stations.PackagingStation ||
                   sv is ScaryParty.Pizzeria.Stations.CounterStation ||
                   sv is ScaryParty.Pizzeria.Stations.StagingStation ||
                   sv.GetType() == typeof(ScaryParty.Pizzeria.Stations.StationView);
        }

        private static StationSlotId? FindFreeNeutralSlot(PizzeriaState state, PizzeriaRoot root)
        {
            var stations = Object.FindObjectsByType<ScaryParty.Pizzeria.Stations.StationView>(FindObjectsSortMode.None);
            for (int i = 0; i < stations.Length; i++)
            {
                var sv = stations[i];
                if (!IsNeutralBenchStation(sv) || sv.stationId <= 0) continue;
                int slotCount = sv.slotAnchors != null && sv.slotAnchors.Length > 0 ? sv.slotAnchors.Length : 2;
                for (int s = 0; s < slotCount; s++)
                {
                    var loc = LocationRef.InStation(sv.stationId, s);
                    bool occupied = false;
                    foreach (var item in state.Items.Values)
                    {
                        if (item.Location.Equals(loc)) { occupied = true; break; }
                    }
                    if (!occupied)
                    {
                        foreach (var tool in state.Tools.Values)
                        {
                            if (tool.Location.Equals(loc)) { occupied = true; break; }
                        }
                    }
                    if (!occupied) return new StationSlotId(sv.stationId, s);
                }
            }
            return null;
        }

        private static int FindFreeToolRackSlot(PizzeriaState state, int rackStationId)
        {
            for (int i = 0; i < 16; i++)
            {
                var loc = LocationRef.InStation(rackStationId, i);
                bool occupied = false;
                foreach (var tool in state.Tools.Values)
                {
                    if (tool.Location.Equals(loc)) { occupied = true; break; }
                }
                if (!occupied) return i;
            }
            return 0;
        }
    }
}
