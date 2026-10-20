using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Presentation;

namespace ScaryParty.Pizzeria.Player
{
    public class BackpackController : MonoBehaviour
    {
        [Header("Configuração de Capacidade")]
        public int capacity = 3;
        public ulong backpackId = 0;

        private NetworkObject _netObj;

        private void Awake()
        {
            _netObj = GetComponent<NetworkObject>();
            EnsureVisualizer();
        }

        private void Start()
        {
            EnsureBackpackId();
            EnsureVisualizer();
        }

        private void EnsureBackpackId()
        {
            if (backpackId == 0)
            {
                ulong myId = GetOwnerClientId();
                backpackId = myId + 1; // ID default correspondente ao jogador
            }
        }

        private void EnsureVisualizer()
        {
            if (GetComponent<BackpackVisualizer>() == null)
            {
                gameObject.AddComponent<BackpackVisualizer>();
            }
        }

        public ulong GetOwnerClientId()
        {
            if (_netObj != null) return _netObj.OwnerClientId;
            if (NetworkManager.Singleton != null) return NetworkManager.Singleton.LocalClientId;
            return 0;
        }

        public bool IsOwnerPlayer()
        {
            if (_netObj != null) return _netObj.IsOwner;
            return true;
        }

        private void Update()
        {
            if (!IsOwnerPlayer()) return;
            EnsureBackpackId();

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
            if (netState == null) return list;

            EnsureBackpackId();
            ulong myId = GetOwnerClientId();

            for (int i = 0; i < netState.Items.Count; i++)
            {
                var it = netState.Items[i];
                if (it.LocationType == (byte)LocationType.Backpack && (it.HolderId == backpackId || it.HolderId == myId))
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

            EnsureBackpackId();
            ulong myId = GetOwnerClientId();

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
                if (it.LocationType == (byte)LocationType.Hand && it.HolderId == myId && it.SlotId == (int)hand)
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

            EnsureBackpackId();
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
