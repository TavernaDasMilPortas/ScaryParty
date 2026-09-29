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
                string itemName = "Item";

                bool stationHasTool = false;
                bool playerHasTool = false;
                string toolName = "Utensílio";

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

                for (int i = 0; i < netState.Tools.Count; i++)
                {
                    var tool = netState.Tools[i];
                    if (tool.LocationType == (byte)LocationType.StationSlot && tool.HolderId == (ulong)stationId)
                    {
                        stationHasTool = true;
                        toolName = tool.DefinitionId == 1 ? "Ralador" : "Faca";
                    }
                    if (tool.LocationType == (byte)LocationType.Hand && tool.HolderId == localClientId)
                    {
                        playerHasTool = true;
                        toolName = tool.DefinitionId == 1 ? "Ralador" : "Faca";
                    }
                }

                if (stationHasTool && !playerHasItem && !playerHasTool) return $"[E] Pegar {toolName}";
                if (stationHasItem && !playerHasItem && !playerHasTool) return $"[E] Pegar {itemName}";
                if (!stationHasItem && !stationHasTool && (playerHasItem || playerHasTool)) return "[E] Colocar";
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
                if (slot.StationId == stationId && slot.HeldItemId != 0)
                {
                    float currentP = slot.Progress;
                    if (slot.CapturedDuration > 0 && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                    {
                        double now = NetworkManager.Singleton.ServerTime.Time;
                        if (now > slot.OperationStartTime)
                        {
                            currentP += (float)((now - slot.OperationStartTime) / slot.CapturedDuration);
                        }
                    }

                    if (currentP > 0 && currentP < 1f)
                    {
                        _progressBar.SetProgress(currentP, Color.green);
                        foundActive = true;
                        break;
                    }
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

            var invAdapter = interactor.GetComponent<ScaryParty.Pizzeria.Player.PlayerInventoryAdapter>();
            int activeHand = invAdapter != null ? (int)invAdapter.ActiveHand : 0;

            var state = PizzeriaNetworkState.Instance;
            var cmd = PizzeriaCommandHandler.Instance;
            if (state == null || cmd == null) return;

            // Use slotIndex 0 for generic StationView for now, unless implemented per subclass
            int slotIndex = 0; 

            NetItemDto itemOnStation = default;
            bool hasItemOnStation = false;

            NetItemDto itemInHand = default;
            bool hasItemInHand = false;

            NetToolDto toolOnStation = default;
            bool hasToolOnStation = false;

            NetToolDto toolInHand = default;
            bool hasToolInHand = false;

            for (int i = 0; i < state.Items.Count; i++)
            {
                var item = state.Items[i];
                if (item.LocationType == (byte)LocationType.StationSlot && item.HolderId == (ulong)stationId && item.SlotId == slotIndex)
                {
                    itemOnStation = item;
                    hasItemOnStation = true;
                }
                if (item.LocationType == (byte)LocationType.Hand && item.HolderId == playerId && item.SlotId == activeHand)
                {
                    itemInHand = item;
                    hasItemInHand = true;
                }
            }

            for (int i = 0; i < state.Tools.Count; i++)
            {
                var tool = state.Tools[i];
                if (tool.LocationType == (byte)LocationType.StationSlot && tool.HolderId == (ulong)stationId && tool.SlotId == slotIndex)
                {
                    toolOnStation = tool;
                    hasToolOnStation = true;
                }
                if (tool.LocationType == (byte)LocationType.Hand && tool.HolderId == playerId && tool.SlotId == activeHand)
                {
                    toolInHand = tool;
                    hasToolInHand = true;
                }
            }

            bool handEmpty = !hasItemInHand && !hasToolInHand;
            bool stationEmpty = !hasItemOnStation && !hasToolOnStation;

            // Checar se a OUTRA mão está livre
            int otherHand = activeHand == 0 ? 1 : 0;
            bool otherHandEmpty = true;
            for (int i = 0; i < state.Items.Count; i++)
            {
                var it = state.Items[i];
                if (it.LocationType == (byte)LocationType.Hand && it.HolderId == playerId && it.SlotId == otherHand)
                {
                    otherHandEmpty = false;
                    break;
                }
            }
            if (otherHandEmpty)
            {
                for (int i = 0; i < state.Tools.Count; i++)
                {
                    var t = state.Tools[i];
                    if (t.LocationType == (byte)LocationType.Hand && t.HolderId == playerId && t.SlotId == otherHand)
                    {
                        otherHandEmpty = false;
                        break;
                    }
                }
            }

            int pickHand = handEmpty ? activeHand : (otherHandEmpty ? otherHand : -1);

            if (hasToolOnStation && pickHand != -1)
            {
                // Pegar ferramenta da bancada (na mão ativa ou na mão livre)
                cmd.TransferToolServerRpc(toolOnStation.ToolId, (byte)LocationType.Hand, playerId, pickHand);
                string tName = toolOnStation.DefinitionId == 1 ? "Ralador" : "Faca";
                ScaryParty.Pizzeria.Presentation.NotificationManager.Show($"{tName} coletado", ScaryParty.Pizzeria.Presentation.NotificationType.Info);
            }
            else if (hasItemOnStation)
            {
                // Tenta combinar se a mão ativa tiver ingrediente compatível
                if (hasItemInHand)
                {
                    bool isIngredient = itemInHand.Category == (byte)ItemCategory.Sauce || 
                                        itemInHand.Category == (byte)ItemCategory.Cheese || 
                                        itemInHand.Category == (byte)ItemCategory.Topping;
                    
                    bool isPizzaOrBase = itemOnStation.Category == (byte)ItemCategory.Dough || itemOnStation.Category == (byte)ItemCategory.Pizza;
                    
                    if (isPizzaOrBase && isIngredient)
                    {
                        cmd.AddIngredientToPizzaServerRpc(itemOnStation.ItemId, itemInHand.ItemId);
                        if (invAdapter != null && !otherHandEmpty)
                        {
                            invAdapter.SetActiveHand((HandSlotIndex)otherHand);
                        }
                        return;
                    }
                }

                // Se não combinou, e temos mão livre (ativa ou oposta), pega o item!
                if (pickHand != -1)
                {
                    cmd.TransferItemServerRpc(itemOnStation.ItemId, (byte)LocationType.Hand, playerId, pickHand);
                    ScaryParty.Pizzeria.Presentation.NotificationManager.Show("Item coletado", ScaryParty.Pizzeria.Presentation.NotificationType.Info);
                }
            }
            else if (stationEmpty && hasToolInHand)
            {
                // Colocar ferramenta na bancada
                cmd.TransferToolServerRpc(toolInHand.ToolId, (byte)LocationType.StationSlot, (ulong)stationId, slotIndex);
                string tName = toolInHand.DefinitionId == 1 ? "Ralador" : "Faca";
                ScaryParty.Pizzeria.Presentation.NotificationManager.Show($"{tName} colocado", ScaryParty.Pizzeria.Presentation.NotificationType.Info);
                if (invAdapter != null && !otherHandEmpty)
                {
                    invAdapter.SetActiveHand((HandSlotIndex)otherHand);
                }
            }
            else if (stationEmpty && hasItemInHand)
            {
                // Colocar item na bancada
                cmd.TransferItemServerRpc(itemInHand.ItemId, (byte)LocationType.StationSlot, (ulong)stationId, slotIndex);
                ScaryParty.Pizzeria.Presentation.NotificationManager.Show("Item colocado", ScaryParty.Pizzeria.Presentation.NotificationType.Info);
                if (invAdapter != null && !otherHandEmpty)
                {
                    invAdapter.SetActiveHand((HandSlotIndex)otherHand);
                }
            }
            else if (hasItemOnStation && hasItemInHand)
            {
                // Combine (Add ingredient to pizza)
                bool isIngredient = itemInHand.Category == (byte)ItemCategory.Sauce || 
                                    itemInHand.Category == (byte)ItemCategory.Cheese || 
                                    itemInHand.Category == (byte)ItemCategory.Topping;
                
                bool isPizzaOrBase = itemOnStation.Category == (byte)ItemCategory.Dough || itemOnStation.Category == (byte)ItemCategory.Pizza;
                
                if (isPizzaOrBase && isIngredient)
                {
                    cmd.AddIngredientToPizzaServerRpc(itemOnStation.ItemId, itemInHand.ItemId);
                    if (invAdapter != null && !otherHandEmpty)
                    {
                        invAdapter.SetActiveHand((HandSlotIndex)otherHand);
                    }
                }
            }
        }
    }
}
