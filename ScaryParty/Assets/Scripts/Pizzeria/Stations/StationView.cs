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

        [Tooltip("Slots posicionados por objetos físicos (pratos etc.) via builder. Desativa a auto-calibração em runtime.")]
        public bool slotsBoundToObjects;

        [HideInInspector] public Vector3 LastAimWorldPoint;
        [HideInInspector] public int LastAimExplicitSlot = -1;

        public void SetAimContext(Vector3 worldPoint, int explicitSlot = -1)
        {
            LastAimWorldPoint = worldPoint;
            LastAimExplicitSlot = explicitSlot;
        }

        public int ResolveTargetSlotIndex(Vector3? aimWorldPoint = null, int explicitSlot = -1)
        {
            if (explicitSlot >= 0) return explicitSlot;
            if (LastAimExplicitSlot >= 0) return LastAimExplicitSlot;

            if (slotAnchors == null || slotAnchors.Length <= 1)
                return 0;

            Vector3 refPoint = aimWorldPoint ?? LastAimWorldPoint;
            if (refPoint != Vector3.zero)
            {
                int bestIdx = 0;
                float bestDistSq = float.MaxValue;
                for (int i = 0; i < slotAnchors.Length; i++)
                {
                    if (slotAnchors[i] == null) continue;
                    float dSq = (slotAnchors[i].position - refPoint).sqrMagnitude;
                    if (dSq < bestDistSq)
                    {
                        bestDistSq = dSq;
                        bestIdx = i;
                    }
                }
                return bestIdx;
            }

            return 0;
        }

        public virtual string InteractPrompt 
        {
            get
            {
                var netState = PizzeriaNetworkState.Instance;
                var networkManager = NetworkManager.Singleton;
                if (netState == null || networkManager == null) return "[E] Interagir com " + stationName;

                int slotIdx = ResolveTargetSlotIndex();
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
                    if (item.LocationType == (byte)LocationType.StationSlot && item.HolderId == (ulong)stationId && item.SlotId == slotIdx)
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
                    if (tool.LocationType == (byte)LocationType.StationSlot && tool.HolderId == (ulong)stationId && tool.SlotId == slotIdx)
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

                // Fallback: se o slot mirado estiver vazio e o jogador tiver mãos vazias, checar qualquer slot ocupado na estação
                if (!stationHasItem && !stationHasTool && !playerHasItem && !playerHasTool)
                {
                    for (int i = 0; i < netState.Items.Count; i++)
                    {
                        var item = netState.Items[i];
                        if (item.LocationType == (byte)LocationType.StationSlot && item.HolderId == (ulong)stationId)
                        {
                            stationHasItem = true;
                            break;
                        }
                    }
                    for (int i = 0; i < netState.Tools.Count; i++)
                    {
                        var tool = netState.Tools[i];
                        if (tool.LocationType == (byte)LocationType.StationSlot && tool.HolderId == (ulong)stationId)
                        {
                            stationHasTool = true;
                            toolName = tool.DefinitionId == 1 ? "Ralador" : "Faca";
                            break;
                        }
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
            LastAimExplicitSlot = -1;
        }

        public Vector3 GetSlotPosition(int slotIndex)
        {
            if (slotAnchors != null && slotIndex >= 0 && slotIndex < slotAnchors.Length && slotAnchors[slotIndex] != null)
                return slotAnchors[slotIndex].position;
            return transform.position + Vector3.up * 0.38f + transform.right * (slotIndex * 0.6f);
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
            ValidateAndCalibrateSlotAnchors();
            _progressBar = ScaryParty.Pizzeria.Presentation.StationProgressBar.Create(transform);
            // Default offset for the bar above the station
            var pos = GetSlotPosition(0);
            _progressBar.transform.position = pos + Vector3.up * 0.4f;
        }

        private void ValidateAndCalibrateSlotAnchors()
        {
            const float TableTopY = 0.38f;
            if (slotsBoundToObjects) return;
            // Apenas para bancadas de preparo, montagem, balcão e empacotamento
            if (stationId == 5 || stationId == 6 || stationId == 7 || stationId == 4 || stationId == 9)
            {
                if (slotAnchors == null || slotAnchors.Length < 2 || slotAnchors[0] == null || slotAnchors[0].localPosition.y > 0.5f || Mathf.Abs(slotAnchors[0].localPosition.x) > 1.0f)
                {
                    Transform slotsParent = transform.Find("Slots");
                    if (slotsParent == null)
                    {
                        var go = new GameObject("Slots");
                        go.transform.SetParent(transform, false);
                        slotsParent = go.transform;
                    }

                    Transform s0 = slotsParent.Find("SlotAnchor_0") ?? slotsParent.Find("Slot_0");
                    if (s0 == null)
                    {
                        var go = new GameObject("SlotAnchor_0");
                        go.transform.SetParent(slotsParent, false);
                        s0 = go.transform;
                    }

                    Transform s1 = slotsParent.Find("SlotAnchor_1") ?? slotsParent.Find("Slot_1");
                    if (s1 == null)
                    {
                        var go = new GameObject("SlotAnchor_1");
                        go.transform.SetParent(slotsParent, false);
                        s1 = go.transform;
                    }

                    if (stationId == 4)
                    {
                        s0.localPosition = new Vector3(0f, TableTopY, -0.4f);
                        s1.localPosition = new Vector3(0f, TableTopY, 0.4f);
                    }
                    else
                    {
                        s0.localPosition = new Vector3(-0.35f, TableTopY, 0f);
                        s1.localPosition = new Vector3(0.35f, TableTopY, 0f);
                    }
                    s0.localRotation = Quaternion.identity;
                    s1.localRotation = Quaternion.identity;

                    slotAnchors = new Transform[] { s0, s1 };
                }
            }
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
                        _progressBar.transform.position = GetSlotPosition(slot.SlotIndex) + Vector3.up * 0.4f;
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

            int slotIndex = ResolveTargetSlotIndex();

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

            // Se o slot mirado está ocupado e o jogador quer colocar um item sem combinar, procurar um slot livre se houver múltiplos slots
            int totalSlots = slotAnchors != null && slotAnchors.Length > 0 ? slotAnchors.Length : 1;
            if ((hasItemOnStation || hasToolOnStation) && (hasItemInHand || hasToolInHand) && totalSlots > 1 && LastAimExplicitSlot < 0)
            {
                bool canCombine = hasItemOnStation && hasItemInHand &&
                    (itemOnStation.Category == (byte)ItemCategory.Dough || itemOnStation.Category == (byte)ItemCategory.Pizza) &&
                    (itemInHand.Category == (byte)ItemCategory.Sauce || itemInHand.Category == (byte)ItemCategory.Cheese || itemInHand.Category == (byte)ItemCategory.Topping);

                if (!canCombine)
                {
                    for (int s = 0; s < totalSlots; s++)
                    {
                        bool occupied = false;
                        for (int i = 0; i < state.Items.Count; i++)
                        {
                            var it = state.Items[i];
                            if (it.LocationType == (byte)LocationType.StationSlot && it.HolderId == (ulong)stationId && it.SlotId == s)
                            { occupied = true; break; }
                        }
                        if (!occupied)
                        {
                            for (int i = 0; i < state.Tools.Count; i++)
                            {
                                var tl = state.Tools[i];
                                if (tl.LocationType == (byte)LocationType.StationSlot && tl.HolderId == (ulong)stationId && tl.SlotId == s)
                                { occupied = true; break; }
                            }
                        }
                        if (!occupied)
                        {
                            slotIndex = s;
                            hasItemOnStation = false;
                            hasToolOnStation = false;
                            break;
                        }
                    }
                }
            }
            else if (!hasItemOnStation && !hasToolOnStation && !hasItemInHand && !hasToolInHand && totalSlots > 1 && LastAimExplicitSlot < 0)
            {
                // Mãos vazias e slot mirado vazio: procurar qualquer slot ocupado na estação para pegar
                for (int i = 0; i < state.Items.Count; i++)
                {
                    var item = state.Items[i];
                    if (item.LocationType == (byte)LocationType.StationSlot && item.HolderId == (ulong)stationId)
                    {
                        slotIndex = item.SlotId;
                        itemOnStation = item;
                        hasItemOnStation = true;
                        break;
                    }
                }
                if (!hasItemOnStation)
                {
                    for (int i = 0; i < state.Tools.Count; i++)
                    {
                        var tool = state.Tools[i];
                        if (tool.LocationType == (byte)LocationType.StationSlot && tool.HolderId == (ulong)stationId)
                        {
                            slotIndex = tool.SlotId;
                            toolOnStation = tool;
                            hasToolOnStation = true;
                            break;
                        }
                    }
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
