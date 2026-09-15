using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;

namespace ScaryParty.Pizzeria.Player
{
    public class BackpackController : NetworkBehaviour
    {
        [Header("Configuração de Capacidade")]
        public int capacity = 2;

        public IReadOnlyList<NetItemDto> GetBoxesInBackpack()
        {
            var list = new List<NetItemDto>();
            var netState = PizzeriaNetworkState.Instance;
            if (netState == null || !IsSpawned) return list;

            ulong myId = NetworkManager.Singleton.LocalClientId;
            for (int i = 0; i < netState.Items.Count; i++)
            {
                var it = netState.Items[i];
                if (it.LocationType == (byte)LocationType.Backpack && it.HolderId == myId)
                {
                    list.Add(it);
                }
            }
            return list;
        }

        public bool TryLoadFromHand(ulong boxItemId)
        {
            var boxes = GetBoxesInBackpack();
            if (boxes.Count >= capacity)
            {
                Debug.LogWarning("[BackpackController] Mochila cheia!");
                return false;
            }

            int nextFreeSlot = 0;
            var occupiedSlots = new HashSet<int>();
            for (int i = 0; i < boxes.Count; i++) occupiedSlots.Add(boxes[i].SlotId);
            while (occupiedSlots.Contains(nextFreeSlot)) nextFreeSlot++;

            var cmd = PizzeriaCommandHandler.Instance;
            if (cmd != null)
            {
                ulong myId = NetworkManager.Singleton.LocalClientId;
                cmd.TransferItemServerRpc(boxItemId, (byte)LocationType.Backpack, myId, nextFreeSlot);
                return true;
            }
            return false;
        }

        public bool TryUnloadToHand(int slotIndex, HandSlotIndex targetHand)
        {
            var boxes = GetBoxesInBackpack();
            ulong targetItemId = 0;
            for (int i = 0; i < boxes.Count; i++)
            {
                if (boxes[i].SlotId == slotIndex)
                {
                    targetItemId = boxes[i].ItemId;
                    break;
                }
            }

            if (targetItemId == 0) return false;

            var cmd = PizzeriaCommandHandler.Instance;
            if (cmd != null)
            {
                ulong myId = NetworkManager.Singleton.LocalClientId;
                cmd.TransferItemServerRpc(targetItemId, (byte)LocationType.Hand, myId, (int)targetHand);
                return true;
            }
            return false;
        }
    }
}
