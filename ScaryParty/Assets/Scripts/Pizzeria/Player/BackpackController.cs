using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Presentation;

namespace ScaryParty.Pizzeria.Player
{
    public class BackpackController : MonoBehaviour
    {
        private NetworkObject _netObj;
        private void Awake()
        {
            _netObj = GetComponent<NetworkObject>();
            if (GetComponent<BackpackVisualizer>() == null) gameObject.AddComponent<BackpackVisualizer>();
        }

        public ulong GetOwnerClientId()
        {
            if (_netObj != null) return _netObj.OwnerClientId;
            return NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : BackpackState.NoOwner;
        }

        private void Update()
        {
            if (_netObj != null && !_netObj.IsOwner) return;
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.bKey.wasPressedThisFrame && !GameplayInputGate.IsLocked)
                BackpackPanelPresenter.Instance?.Toggle();
        }

        public bool TryGetEquippedBag(out NetBackpackDto bag)
            => CarriedCargoResolver.TryGetEquippedBackpack(PizzeriaNetworkState.Instance, GetOwnerClientId(), out bag);

        public IReadOnlyList<NetItemDto> GetBoxesInBackpack()
        {
            var items = new List<NetItemDto>();
            var state = PizzeriaNetworkState.Instance;
            if (state == null || !TryGetEquippedBag(out var bag)) return items;
            for (int i = 0; i < state.Items.Count; i++)
            {
                var item = state.Items[i];
                if (item.LocationType == (byte)LocationType.Backpack && item.HolderId == bag.BackpackId)
                    items.Add(item);
            }
            return items;
        }

        public bool HasBoxInHand(HandSlotIndex hand)
        {
            var state = PizzeriaNetworkState.Instance;
            if (state == null) return false;
            ulong owner = GetOwnerClientId();
            for (int i = 0; i < state.Items.Count; i++)
            {
                var item = state.Items[i];
                if (item.LocationType == (byte)LocationType.Hand && item.HolderId == owner && item.SlotId == (int)hand)
                    return item.PackagingState == (byte)PackagingState.Boxed || item.Category == (byte)ItemCategory.Box;
            }
            return false;
        }

        public bool TryStoreFromHand(HandSlotIndex hand, ulong targetBagId = 0)
        {
            var state = PizzeriaNetworkState.Instance;
            if (state == null || LogisticsCommands.Instance == null) return false;
            if (targetBagId == 0)
            {
                if (!TryGetEquippedBag(out var equipped))
                {
                    NotificationManager.Show("Sem mochila equipada.", NotificationType.Warning);
                    return false;
                }
                targetBagId = equipped.BackpackId;
            }
            ulong owner = GetOwnerClientId();
            for (int i = 0; i < state.Items.Count; i++)
            {
                var item = state.Items[i];
                if (item.LocationType != (byte)LocationType.Hand || item.HolderId != owner || item.SlotId != (int)hand) continue;
                if (item.PackagingState != (byte)PackagingState.Boxed && item.Category != (byte)ItemCategory.Box) break;
                LogisticsCommands.Instance.StoreBackpackItemServerRpc(targetBagId, item.ItemId, hand);
                return true;
            }
            NotificationManager.Show("Segure uma caixa na mão selecionada.", NotificationType.Warning);
            return false;
        }

        public bool TryRetrieveToHand(ulong itemId, HandSlotIndex hand, ulong targetBagId = 0)
        {
            if (LogisticsCommands.Instance == null) return false;
            if (targetBagId == 0)
            {
                if (!TryGetEquippedBag(out var bag)) return false;
                targetBagId = bag.BackpackId;
            }
            LogisticsCommands.Instance.RetrieveBackpackItemServerRpc(targetBagId, itemId, hand);
            return true;
        }
    }
}
