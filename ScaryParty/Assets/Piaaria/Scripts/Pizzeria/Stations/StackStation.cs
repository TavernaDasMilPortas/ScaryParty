using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Presentation;

namespace ScaryParty.Pizzeria.Stations
{
    public class StackStation : StationView
    {
        [Header("Configuração da Pilha")]
        public ulong stackId = 1;
        public int capacity = 6;
        public int linkedAddressId = -1; // -1 = qualquer endereço aceito

        public override string InteractPrompt
        {
            get
            {
                var netState = PizzeriaNetworkState.Instance;
                var networkManager = NetworkManager.Singleton;
                if (netState == null || networkManager == null) return "Pilha de Caixas";

                ulong localClientId = networkManager.LocalClientId;
                var stack = GetNetStackDto();

                bool playerHasBox = false;
                int heldAddressId = -1;

                for (int i = 0; i < netState.Items.Count; i++)
                {
                    var item = netState.Items[i];
                    if (item.LocationType == (byte)LocationType.Hand && item.HolderId == localClientId)
                    {
                        if (item.PackagingState == (byte)PackagingState.Boxed || item.Category == (byte)ItemCategory.Box)
                        {
                            playerHasBox = true;
                            heldAddressId = item.LabelDestinationId;
                            break;
                        }
                    }
                }

                int count = stack.ItemCount;
                int max = stack.Capacity > 0 ? stack.Capacity : capacity;

                if (playerHasBox)
                {
                    if (count >= max)
                        return $"Pilha Cheia ({count}/{max})";
                    return $"[E] Empilhar Caixa na Pilha ({count}/{max})";
                }

                if (count > 0)
                {
                    return $"[E] Retirar Caixa do Topo (Pilha: {count}/{max})";
                }

                return "Pilha Vazia";
            }
        }

        protected override void Start()
        {
            base.Start();
            stationName = "Pilha de Caixas";
        }

        private NetStackDto GetNetStackDto()
        {
            var netState = PizzeriaNetworkState.Instance;
            if (netState != null && netState.Stacks != null)
            {
                for (int i = 0; i < netState.Stacks.Count; i++)
                {
                    if (netState.Stacks[i].StackId == stackId)
                        return netState.Stacks[i];
                }
            }

            return new NetStackDto
            {
                StackId = stackId,
                StationId = stationId,
                Capacity = capacity,
                ItemCount = 0,
                TopItemId = 0
            };
        }

        public override void OnInteract(GameObject interactor)
        {
            var netObj = interactor.GetComponent<NetworkObject>();
            if (netObj == null) return;
            ulong playerId = netObj.OwnerClientId;

            var invAdapter = interactor.GetComponent<ScaryParty.Pizzeria.Player.PlayerInventoryAdapter>();
            int activeHand = invAdapter != null ? (int)invAdapter.ActiveHand : 0;

            var netState = PizzeriaNetworkState.Instance;
            var logistics = LogisticsCommands.Instance;
            if (netState == null || logistics == null) return;

            NetItemDto heldBox = default;
            bool hasBoxInHand = false;

            for (int i = 0; i < netState.Items.Count; i++)
            {
                var item = netState.Items[i];
                if (item.LocationType == (byte)LocationType.Hand && item.HolderId == playerId && item.SlotId == activeHand)
                {
                    if (item.PackagingState == (byte)PackagingState.Boxed || item.Category == (byte)ItemCategory.Box)
                    {
                        heldBox = item;
                        hasBoxInHand = true;
                        break;
                    }
                }
            }

            var stack = GetNetStackDto();

            if (hasBoxInHand)
            {
                // Empilhar caixa
                int targetAddress = heldBox.LabelDestinationId;
                logistics.DepositStackServerRpc(stackId, heldBox.ItemId, (HandSlotIndex)activeHand, targetAddress);
            }
            else if (stack.ItemCount > 0 && stack.TopItemId != 0)
            {
                // Retirar do topo
                logistics.TakeStackTopServerRpc(stackId, stack.TopItemId, (HandSlotIndex)activeHand);
            }
            else
            {
                NotificationManager.Show("Nenhuma caixa disponível nesta pilha.", NotificationType.Warning);
            }
        }
    }
}
