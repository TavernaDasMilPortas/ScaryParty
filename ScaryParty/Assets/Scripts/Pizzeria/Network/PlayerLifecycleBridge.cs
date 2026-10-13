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
            if (root == null || !root.IsServer || root.DomainState == null) return;

            var state = root.DomainState;
            Vector3 recoveryPos = root.recoveryAnchor != null ? root.recoveryAnchor.position : root.transform.position;
            DomainVector3 domainRecoveryPos = new DomainVector3(recoveryPos.x, recoveryPos.y, recoveryPos.z);

            // 1. Cancelar holds ativos do jogador
            if (state.PlayerHolds.TryGetValue(clientId, out var hold))
            {
                if (hold.Kind == PlayerHoldKind.Discard && hold.TargetItemId.IsValid)
                {
                    if (state.Items.TryGetValue(hold.TargetItemId, out var item))
                        item.ReservedByHold = 0;
                }
                state.PlayerHolds.Remove(clientId);
            }

            // 2. Pausar operações onde o jogador era o executor
            foreach (var op in state.ActiveOperations.Values)
            {
                if (op.WorkerId == clientId)
                {
                    op.Pause((float)root.Clock.Now);
                    if (op.ToolItemId.IsValid && state.Tools.TryGetValue(op.ToolItemId, out var tool))
                    {
                        tool.ReleaseLease(clientId);
                    }
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

            foreach (var toolId in handTools)
            {
                // Voltar ao ToolRack (Station 3) em slot livre
                int rackSlot = FindFreeToolRackSlot(state);
                TransferService.TransferTool(state, toolId, LocationRef.InStation(3, rackSlot), 0, root.Clock.Now);
            }

            ScaryParty.Pizzeria.Network.PizzeriaCommandHandler.Instance?.CommitAndReplicate();
            Debug.Log($"[PlayerLifecycleBridge] Desconexão do cliente {clientId} tratada: itens recuperados e reservas limpas.");
        }

        private StationSlotId? FindFreeNeutralSlot(PizzeriaState state, PizzeriaRoot root)
        {
            // Procurar slots livres na estação neutra (ex: Station 1 slots)
            for (int s = 0; s < 4; s++)
            {
                var loc = LocationRef.InStation(1, s);
                bool occupied = false;
                foreach (var item in state.Items.Values)
                {
                    if (item.Location.Equals(loc)) { occupied = true; break; }
                }
                if (!occupied) return new StationSlotId(1, s);
            }
            return null;
        }

        private int FindFreeToolRackSlot(PizzeriaState state)
        {
            for (int i = 0; i < 16; i++)
            {
                var loc = LocationRef.InStation(3, i);
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
