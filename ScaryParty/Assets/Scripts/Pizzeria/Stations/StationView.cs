using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;

namespace ScaryParty.Pizzeria.Stations
{
    public class StationView : MonoBehaviour, IInteractable
    {
        [Header("IdentificaÃ§Ã£o da EstaÃ§Ã£o")]
        public int stationId;
        public string stationName = "EstaÃ§Ã£o";
        public Transform[] slotAnchors;

        public virtual string InteractPrompt 
        {
            get
            {
                var netState = PizzeriaNetworkState.Instance;
                var networkManager = NetworkManager.Singleton;
                if (netState == null || networkManager == null) return "[E] Interagir com " + stationName;

                ulong localClientId = networkManager.LocalClientId;
                bool stationHasItem = false;
                bool playerHasItem = false;
                string itemName = "Item"; // Without ItemLibrary, use generic name

                for (int i = 0; i < netState.Items.Count; i++)
                {
                    var item = netState.Items[i];
                    if (item.LocationType == (byte)LocationType.StationSlot && item.HolderId == (ulong)stationId)
                    {
                        stationHasItem = true;
                    }
                    if (item.LocationType == (byte)LocationType.Hand && item.HolderId == localClientId)
                    {
                        playerHasItem = true;
                    }
                }

                if (stationHasItem && !playerHasItem) return $"[E] Pegar {itemName}";
                if (!stationHasItem && playerHasItem) return "[E] Colocar";
                if (stationHasItem && playerHasItem) return "[E] Adicionar ingrediente";

                return "[E] Interagir com " + stationName;
            }
        }

        public virtual void OnInteract(GameObject interactor)
        {
            TryPickOrPlace(interactor);
        }

        public virtual void OnFocus()
        {
        }

        public virtual void OnLoseFocus()
        {
        }

        public Vector3 GetSlotPosition(int slotIndex)
        {
            if (slotAnchors != null && slotIndex >= 0 && slotIndex < slotAnchors.Length && slotAnchors[slotIndex] != null)
                return slotAnchors[slotIndex].position;
            return transform.position + Vector3.up * 0.8f + transform.right * (slotIndex * 0.6f);
        }

        public Quaternion GetSlotRotation(int slotIndex)
        {
            if (slotAnchors != null && slotIndex >= 0 && slotIndex < slotAnchors.Length && slotAnchors[slotIndex] != null)
                return slotAnchors[slotIndex].rotation;
            return transform.rotation;
        }

        protected ScaryParty.Pizzeria.Presentation.StationProgressBar _progressBar;

        protected virtual void Start()
        {
            _progressBar = ScaryParty.Pizzeria.Presentation.StationProgressBar.Create(transform);
            // Default offset for the bar above the station
            var pos = GetSlotPosition(0);
            _progressBar.transform.position = pos + Vector3.up * 0.4f;
        }

        protected virtual void Update()
        {
            if (_progressBar == null) return;
            var state = PizzeriaNetworkState.Instance;
            if (state == null) return;

            bool foundActive = false;
            for (int i = 0; i < state.StationSlots.Count; i++)
            {
                var slot = state.StationSlots[i];
                if (slot.StationId == stationId && slot.Progress > 0 && slot.Progress < 1f)
                {
                    _progressBar.SetProgress(slot.Progress, Color.green);
                    foundActive = true;
                    break;
                }
            }

            if (!foundActive)
            {
                _progressBar.Hide();
            }
        }

        protected void TryPickOrPlace(GameObject interactor)
        {
            var netObj = interactor.GetComponent<NetworkObject>();
            if (netObj == null) return;
            ulong playerId = netObj.OwnerClientId;

            var state = PizzeriaNetworkState.Instance;
            var cmd = PizzeriaCommandHandler.Instance;
            if (state == null || cmd == null) return;

            // 1. Check if there is an item on this station
            NetItemDto itemOnStation = default;
            bool hasItemOnStation = false;

            // 2. Check if player has an item in hand
            NetItemDto itemInHand = default;
            bool hasItemInHand = false;

            for (int i = 0; i < state.Items.Count; i++)
            {
                var item = state.Items[i];
                if (item.LocationType == (byte)LocationType.StationSlot && item.HolderId == (ulong)stationId)
                {
                    itemOnStation = item;
                    hasItemOnStation = true;
                }
                if (item.LocationType == (byte)LocationType.Hand && item.HolderId == playerId)
                {
                    itemInHand = item;
                    hasItemInHand = true;
                }
            }

            if (hasItemOnStation && !hasItemInHand)
            {
                // Pick up
                cmd.TransferItemServerRpc(itemOnStation.ItemId, (byte)LocationType.Hand, playerId, 0);
                ScaryParty.Pizzeria.Presentation.NotificationManager.Show("Item coletado", ScaryParty.Pizzeria.Presentation.NotificationType.Info);
            }
            else if (!hasItemOnStation && hasItemInHand)
            {
                // Place
                cmd.TransferItemServerRpc(itemInHand.ItemId, (byte)LocationType.StationSlot, (ulong)stationId, 0);
                ScaryParty.Pizzeria.Presentation.NotificationManager.Show("Item colocado", ScaryParty.Pizzeria.Presentation.NotificationType.Info);
            }
            else if (hasItemOnStation && hasItemInHand)
            {
                // Combine (Add ingredient to pizza)
                bool isIngredient = itemInHand.Category == (byte)ItemCategory.Dough || 
                                    itemInHand.Category == (byte)ItemCategory.Sauce || 
                                    itemInHand.Category == (byte)ItemCategory.Cheese || 
                                    itemInHand.Category == (byte)ItemCategory.Topping;
                
                bool isPizzaOrBase = itemOnStation.Category == (byte)ItemCategory.PizzaBase || itemOnStation.Category == (byte)ItemCategory.Pizza;
                
                if (isPizzaOrBase && isIngredient)
                {
                    cmd.AddIngredientToPizzaServerRpc(itemOnStation.ItemId, itemInHand.ItemId);
                }
            }
        }
    }
}
