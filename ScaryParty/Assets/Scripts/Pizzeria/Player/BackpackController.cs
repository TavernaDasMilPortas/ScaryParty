using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Presentation;

namespace ScaryParty.Pizzeria.Player
{
    public class BackpackController : NetworkBehaviour
    {
        [Header("Configuração de Capacidade")]
        public int capacity = 3;
        public ulong backpackId = 0;

        public override void OnNetworkSpawn()
        {
            if (backpackId == 0)
            {
                backpackId = OwnerClientId + 1; // ID default correspondente ao jogador
            }

            // Assegurar visualizador da mochila
            if (GetComponent<BackpackVisualizer>() == null)
            {
                gameObject.AddComponent<BackpackVisualizer>();
            }
        }

        private void Update()
        {
            if (!IsOwner) return;

            // Tecla [B] para inspecionar / abrir painel interativo da mochila
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.bKey.wasPressedThisFrame)
            {
                if (!GameplayInputGate.IsLocked)
                {
                    if (BackpackPanelPresenter.Instance != null)
                    {
                        BackpackPanelPresenter.Instance.Toggle();
                    }
                    else
                    {
                        ToggleBackpackQuickInspection();
                    }
                }
            }
        }

        public IReadOnlyList<NetItemDto> GetBoxesInBackpack()
        {
            var list = new List<NetItemDto>();
            var netState = PizzeriaNetworkState.Instance;
            if (netState == null || !IsSpawned) return list;

            ulong myId = OwnerClientId;
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

        public bool TryStoreFromHand(HandSlotIndex hand)
        {
            var netState = PizzeriaNetworkState.Instance;
            var logistics = LogisticsCommands.Instance;
            if (netState == null || logistics == null) return false;

            var boxes = GetBoxesInBackpack();
            if (boxes.Count >= capacity)
            {
                NotificationManager.Show($"Mochila cheia! ({boxes.Count}/{capacity})", NotificationType.Warning);
                return false;
            }

            NetItemDto boxToStore = default;
            bool found = false;

            for (int i = 0; i < netState.Items.Count; i++)
            {
                var it = netState.Items[i];
                if (it.LocationType == (byte)LocationType.Hand && it.HolderId == OwnerClientId && it.SlotId == (int)hand)
                {
                    if (it.PackagingState == (byte)PackagingState.Boxed || it.Category == (byte)ItemCategory.Box)
                    {
                        boxToStore = it;
                        found = true;
                        break;
                    }
                }
            }

            if (!found)
            {
                NotificationManager.Show("Você precisa segurar uma caixa de pizza para guardar na mochila.", NotificationType.Warning);
                return false;
            }

            logistics.StoreBackpackItemServerRpc(backpackId, boxToStore.ItemId, hand);
            return true;
        }

        public bool TryRetrieveToHand(ulong boxItemId, HandSlotIndex hand)
        {
            var logistics = LogisticsCommands.Instance;
            if (logistics == null) return false;

            logistics.RetrieveBackpackItemServerRpc(backpackId, boxItemId, hand);
            return true;
        }

        private void ToggleBackpackQuickInspection()
        {
            var boxes = GetBoxesInBackpack();
            if (boxes.Count == 0)
            {
                NotificationManager.Show($"🎒 Mochila Vazia (0/{capacity} caixas)", NotificationType.Info);
                return;
            }

            string info = $"🎒 Mochila ({boxes.Count}/{capacity} caixas):\n";
            for (int i = 0; i < boxes.Count; i++)
            {
                info += $"• Caixa #{boxes[i].ItemId} -> {AddressFormat.Display(boxes[i].LabelDestinationId)}\n";
            }
            NotificationManager.Show(info.TrimEnd(), NotificationType.Success);
        }
    }
}
