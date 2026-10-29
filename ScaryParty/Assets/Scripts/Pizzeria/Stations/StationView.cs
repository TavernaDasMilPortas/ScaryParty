using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;

namespace ScaryParty.Pizzeria.Stations
{
    public class StationView : MonoBehaviour, IInteractable
    {
        [Header("Identificação da Estação")]
        public int stationId;
        public string stationName = "Estação";
        public Transform[] slotAnchors;

        [Tooltip("Slots posicionados por objetos físicos (pratos etc.) via builder. Desativa a auto-calibração em runtime.")]
        public bool slotsBoundToObjects;

        [HideInInspector] public Vector3 LastAimWorldPoint;
        [HideInInspector] public int LastAimExplicitSlot = -1;

        private readonly System.Collections.Generic.HashSet<int> _boundSlotIndices = new System.Collections.Generic.HashSet<int>();
        private bool _slotBindingInitialized;

        public void SetAimContext(Vector3 worldPoint, int explicitSlot = -1)
        {
            LastAimWorldPoint = worldPoint;
            LastAimExplicitSlot = explicitSlot;
        }

        private void EnsureSlotBindingsInitialized()
        {
            if (_slotBindingInitialized) return;
            ValidateAndCalibrateSlotAnchors();
        }

        /// <summary>
        /// Retorna true se o slot informado estiver atribuído a um objeto 3D físico (ex: tábua, prato).
        /// </summary>
        public bool IsSlotBoundToObject(int slotIndex)
        {
            EnsureSlotBindingsInitialized();
            return _boundSlotIndices.Contains(slotIndex);
        }

        /// <summary>
        /// Retorna true se pelo menos um slot da estação estiver atribuído a um objeto 3D.
        /// </summary>
        public bool HasObjectBoundSlots
        {
            get
            {
                EnsureSlotBindingsInitialized();
                return _boundSlotIndices.Count > 0;
            }
        }

        /// <summary>
        /// Retorna true se a estação possuir pelo menos um slot que NÃO está atribuído a um objeto 3D
        /// (ou se for uma estação sem slots de bancada, como geladeira/forno/lixeira, que interage pela malha principal).
        /// </summary>
        public bool HasUnboundSlots
        {
            get
            {
                EnsureSlotBindingsInitialized();
                if (slotAnchors == null || slotAnchors.Length == 0)
                    return _boundSlotIndices.Count == 0;

                int validSlots = 0;
                for (int i = 0; i < slotAnchors.Length; i++)
                {
                    if (slotAnchors[i] == null) continue;
                    validSlots++;
                    if (!_boundSlotIndices.Contains(i))
                        return true;
                }
                return validSlots == 0 && _boundSlotIndices.Count == 0;
            }
        }

        public int ResolveTargetSlotIndex(Vector3? aimWorldPoint = null, int explicitSlot = -1)
        {
            EnsureSlotBindingsInitialized();

            if (explicitSlot >= 0) return explicitSlot;
            if (LastAimExplicitSlot >= 0) return LastAimExplicitSlot;

            // Regra 1: se todos os slots estão vinculados a objetos 3D e nenhum objeto 3D de slot foi mirado no raycast,
            // não permite resolver slot pela malha da mesa.
            if (!HasUnboundSlots)
                return -1;

            if (slotAnchors == null || slotAnchors.Length == 0)
                return 0;

            Vector3 refPoint = aimWorldPoint ?? LastAimWorldPoint;
            int bestIdx = -1;
            float bestDistSq = float.MaxValue;

            for (int i = 0; i < slotAnchors.Length; i++)
            {
                if (slotAnchors[i] == null) continue;
                if (_boundSlotIndices.Contains(i)) continue; // Slots com objeto 3D só podem ser mirados diretamente no objeto 3D

                if (refPoint != Vector3.zero)
                {
                    float dSq = (slotAnchors[i].position - refPoint).sqrMagnitude;
                    if (dSq < bestDistSq)
                    {
                        bestDistSq = dSq;
                        bestIdx = i;
                    }
                }
                else if (bestIdx < 0)
                {
                    bestIdx = i;
                }
            }

            return bestIdx >= 0 ? bestIdx : 0;
        }

        public virtual string InteractPrompt 
        {
            get
            {
                int slotIdx = ResolveTargetSlotIndex();
                if (slotIdx < 0) return string.Empty;

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

        protected virtual void Awake()
        {
            ValidateAndCalibrateSlotAnchors();
        }

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
            _slotBindingInitialized = true;
            _boundSlotIndices.Clear();

            // 1. Compactar slotAnchors removendo referências nulas (ex: entradas órfãs no prefab)
            if (slotAnchors != null)
            {
                var validList = new System.Collections.Generic.List<Transform>(slotAnchors.Length);
                for (int i = 0; i < slotAnchors.Length; i++)
                {
                    if (slotAnchors[i] != null)
                        validList.Add(slotAnchors[i]);
                }
                if (validList.Count != slotAnchors.Length)
                    slotAnchors = validList.ToArray();
            }

            // 2. Inspecionar StationWorkpostTarget filhos:
            // - Se o workpost tem objeto 3D próprio (Renderer), garantir Collider ativo, layer compatível e OutlineTarget próprio (Regra 1).
            // - Se o workpost NÃO tem objeto 3D próprio (ex: Workpost invisível legado), desativar para que a mesa receba o raycast/outline e posicione pelo slot mais próximo (Regra 2).
            var workposts = GetComponentsInChildren<ScaryParty.Pizzeria.Authoring.StationWorkpostTarget>(true);
            for (int i = 0; i < workposts.Length; i++)
            {
                var wp = workposts[i];
                if (wp == null) continue;
                if (wp.targetStation == null) wp.targetStation = this;
                if (wp.targetStation != this) continue;

                if (wp.IsBoundTo3DObject)
                {
                    _boundSlotIndices.Add(wp.slotIndex);
                    if (wp.outlineTarget == null)
                    {
                        wp.outlineTarget = wp.GetComponent<ScaryParty.Interaction.OutlineTarget>();
                        if (wp.outlineTarget == null)
                            wp.outlineTarget = wp.gameObject.AddComponent<ScaryParty.Interaction.OutlineTarget>();
                    }

                    var cols = wp.GetComponentsInChildren<Collider>(true);
                    if (cols == null || cols.Length == 0)
                    {
                        wp.gameObject.AddComponent<BoxCollider>();
                    }
                    else
                    {
                        for (int c = 0; c < cols.Length; c++)
                        {
                            if (cols[c] != null) cols[c].enabled = true;
                        }
                    }
                }
                else if (wp.gameObject != gameObject)
                {
                    // Workpost invisível sem malha 3D: desativar colisor/componente para obedecer à Regra 2
                    var col = wp.GetComponent<Collider>();
                    if (col != null) col.enabled = false;
                    wp.enabled = false;
                }
            }

            slotsBoundToObjects = _boundSlotIndices.Count > 0 && !HasUnboundSlots;

            var rootOutline = GetComponent<ScaryParty.Interaction.OutlineTarget>();
            if (rootOutline != null)
                rootOutline.InvalidateCache();
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
            if (slotIndex < 0) return;

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
                // Tenta combinar se a mão ativa tiver ingrediente preparado compatível e a base na bancada já estiver aberta (Massa Aberta / Pizza)
                if (hasItemInHand && !(this is PrepStation))
                {
                    bool isPreparedIngredient = itemInHand.Category == (byte)ItemCategory.Sauce ||
                                                ((itemInHand.Category == (byte)ItemCategory.Cheese || itemInHand.Category == (byte)ItemCategory.Topping) &&
                                                 itemInHand.PrepStage == (byte)ProcessingStage.Prepared);

                    bool isPreparedPizzaOrBase = itemOnStation.Category == (byte)ItemCategory.Pizza ||
                                                 itemOnStation.Category == (byte)ItemCategory.PizzaBase ||
                                                 (itemOnStation.Category == (byte)ItemCategory.Dough && itemOnStation.PrepStage == (byte)ProcessingStage.Prepared);

                    if (isPreparedPizzaOrBase && isPreparedIngredient)
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
        }
    }
}
