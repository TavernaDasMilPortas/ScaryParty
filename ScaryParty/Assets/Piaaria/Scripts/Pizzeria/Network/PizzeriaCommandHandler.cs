using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Network
{
    public class PizzeriaCommandHandler : NetworkBehaviour
    {
        public static PizzeriaCommandHandler Instance { get; private set; }

        private ReplicationProjector _projector;

        private void Awake()
        {
            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                OnServerResetNightCombatRequested -= HandleResetNightCombatRequestedServer;
                OnServerResetNightCombatRequested += HandleResetNightCombatRequestedServer;
                OnServerResetNightCombatWithPolicyRequested -= HandleResetNightCombatServer;
                OnServerResetNightCombatWithPolicyRequested += HandleResetNightCombatServer;
                OnServerRecoverMotorcycleRequested -= HandleRecoverMotorcycleServer;
                OnServerRecoverMotorcycleRequested += HandleRecoverMotorcycleServer;
                if (_projector == null)
                {
                    var netState = GetComponent<PizzeriaNetworkState>();
                    if (netState != null)
                        _projector = new ReplicationProjector(netState);
                }
                EnsureDefaultWeaponsInitializedServer();
                CommitAndReplicate();
            }
        }

        public override void OnDestroy()
        {
            OnServerResetNightCombatRequested -= HandleResetNightCombatRequestedServer;
            OnServerResetNightCombatWithPolicyRequested -= HandleResetNightCombatServer;
            OnServerRecoverMotorcycleRequested -= HandleRecoverMotorcycleServer;
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        private void Update()
        {
            if (IsServer && IsSpawned)
            {
                TickServerCombat(Time.deltaTime);
            }
        }

        public void CommitAndReplicate()
        {
            var root = PizzeriaRoot.Instance;
            if (root != null && root.DomainState != null)
            {
                if (_projector == null)
                {
                    var netState = GetComponent<PizzeriaNetworkState>();
                    if (netState != null)
                        _projector = new ReplicationProjector(netState);
                }

                if (_projector != null)
                {
                    _projector.ProjectFullState(root.DomainState);
                }
            }
        }

        private readonly Dictionary<int, (ulong ClientId, float Expiry)> _storageSessions = new Dictionary<int, (ulong, float)>();
        private readonly Dictionary<int, (ulong PizzaId, int SlotIndex, ulong ClientId, bool IsInsertion)> _activeOvenTransfers = new Dictionary<int, (ulong, int, ulong, bool)>();

        public void ReleasePlayerSessions(ulong clientId)
        {
            var stationsToClose = new List<int>();
            foreach (var kvp in _storageSessions)
            {
                if (kvp.Value.ClientId == clientId)
                    stationsToClose.Add(kvp.Key);
            }
            for (int i = 0; i < stationsToClose.Count; i++)
            {
                _storageSessions.Remove(stationsToClose[i]);
                SyncStationDoorStateClientRpc(stationsToClose[i], false);
            }

            var ovenTransfersToClear = new List<int>();
            foreach (var kvp in _activeOvenTransfers)
            {
                if (kvp.Value.ClientId == clientId)
                    ovenTransfersToClear.Add(kvp.Key);
            }
            for (int i = 0; i < ovenTransfersToClear.Count; i++)
            {
                _activeOvenTransfers.Remove(ovenTransfersToClear[i]);
            }

            if (IsServer)
            {
                ReleaseAssistSessionsForClientServer(clientId);

                var net = PizzeriaNetworkState.Instance;
                if (net != null && net.MotorcycleState.Value.IsMounted && net.MotorcycleState.Value.RiderClientId == clientId)
                {
                    var st = net.MotorcycleState.Value;
                    st.IsMounted = false;
                    st.RiderClientId = 0;
                    st.Speed = 0f;
                    st.Revision++;
                    net.MotorcycleState.Value = st;
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestOpenStorageSessionServerRpc(int stationId, ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            if (RejectIfCannotActServer(senderClientId, requireOpenShift: true)) return;
            var targetParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { senderClientId } } };

            if (_storageSessions.TryGetValue(stationId, out var existing) && existing.ClientId != senderClientId && Time.time < existing.Expiry)
            {
                NotifyErrorClientRpc("Este armário/geladeira já está em uso por outro jogador!", targetParams);
                return;
            }

            _storageSessions[stationId] = (senderClientId, Time.time + 45f);
            GrantStorageSessionClientRpc(stationId, targetParams);
            SyncStationDoorStateClientRpc(stationId, true);
        }

        [ServerRpc(RequireOwnership = false)]
        public void CloseStorageSessionServerRpc(int stationId, ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            if (_storageSessions.TryGetValue(stationId, out var existing) && existing.ClientId == senderClientId)
            {
                _storageSessions.Remove(stationId);
                SyncStationDoorStateClientRpc(stationId, false);
            }
        }

        [ClientRpc]
        public void GrantStorageSessionClientRpc(int stationId, ClientRpcParams rpcParams = default)
        {
            ScaryParty.UI.Piaria.Presenters.StoragePresenter.Instance?.Open(stationId);
        }

        [ClientRpc]
        public void CloseStoragePickerClientRpc(ClientRpcParams rpcParams = default)
        {
            ScaryParty.UI.Piaria.Presenters.StoragePresenter.Instance?.Close();
        }

        [ClientRpc]
        public void SyncStationDoorStateClientRpc(int stationId, bool open)
        {
            if (StationRegistry.TryGetStation(stationId, out var trans) && trans != null)
            {
                var presenter = trans.GetComponent<ScaryParty.Pizzeria.Presentation.StationDoorPresenter>();
                presenter?.SetSessionOpen(open);
            }
        }

        [ClientRpc]
        public void StartOvenTransferClientRpc(int stationId, ulong pizzaId, int slotIndex, ulong clientId, byte handIndex, bool isInsertion, float doorOpenDuration, float flightDuration, float doorCloseDuration)
        {
            ScaryParty.Pizzeria.Presentation.OvenTransferVisualizer.EnsureInstance();
            ScaryParty.Pizzeria.Presentation.OvenTransferVisualizer.Instance.PlayTransfer(
                stationId,
                pizzaId,
                slotIndex,
                clientId,
                handIndex,
                isInsertion,
                doorOpenDuration,
                flightDuration,
                doorCloseDuration);
        }

        [ClientRpc]
        private void NotifyErrorClientRpc(string message, ClientRpcParams rpcParams = default)
        {
            ScaryParty.Pizzeria.Presentation.NotificationManager.Show(message, ScaryParty.Pizzeria.Presentation.NotificationType.Error);
        }

        [ClientRpc]
        private void NotifySuccessClientRpc(string message, ClientRpcParams rpcParams = default)
        {
            ScaryParty.Pizzeria.Presentation.NotificationManager.Show(message, ScaryParty.Pizzeria.Presentation.NotificationType.Success);
        }

        [ServerRpc(RequireOwnership = false)]
        public void DispenseIngredientServerRpc(int storageId, int ingredientDefId, byte hand, ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null) return;
            if (RejectIfCannotActServer(senderClientId, requireOpenShift: true)) return;

            var targetHand = (HandSlotIndex)hand;
            var targetParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { senderClientId } } };
            if (!TryPrepareHandForItemOrToolPickupServer(senderClientId, targetHand, out string weaponErr))
            {
                NotifyErrorClientRpc(weaponErr, targetParams);
                return;
            }

            var targetLoc = LocationRef.InHand(senderClientId, targetHand);
            if (root.TransferService.IsLocationOccupied(targetLoc))
            {
                NotifyErrorClientRpc("A mão selecionada está ocupada!", targetParams);
                return;
            }

            var res = root.TransferService.DispenseFromStorage(storageId, ingredientDefId, senderClientId, targetHand);
            if (res.Success)
            {
                if (_storageSessions.TryGetValue(storageId, out var existing) && existing.ClientId == senderClientId)
                {
                    _storageSessions.Remove(storageId);
                    SyncStationDoorStateClientRpc(storageId, false);
                }
                CloseStoragePickerClientRpc(targetParams);
                CommitAndReplicate();
            }
            else
            {
                NotifyErrorClientRpc(string.IsNullOrEmpty(res.Message) ? "Ambas as mãos cheias ou estoque esgotado!" : res.Message, targetParams);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void TransferItemServerRpc(ulong itemIdVal, byte targetLocType, ulong targetHolderId, int targetSlotId, ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            var targetParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { senderId } } };
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null) return;
            if (RejectIfCannotActServer(senderId, requireOpenShift: true)) return;

            var itemId = new ItemId(itemIdVal);
            if (!root.DomainState.Items.TryGetValue(itemId, out var sourceItem))
            {
                NotifyErrorClientRpc("Item não encontrado no servidor.", targetParams);
                return;
            }

            if (sourceItem.Location.Type == LocationType.StationSlot &&
                !IsStationWithinReach(senderId, (int)sourceItem.Location.HolderId, out string srcReachError, sourceItem.Location.SlotIndex))
            {
                NotifyErrorClientRpc(srcReachError, targetParams);
                return;
            }

            if (sourceItem.Location.Type == LocationType.World &&
                !InteractionValidator.ValidateDistanceAndSight(
                    senderId,
                    new Vector3(sourceItem.Location.WorldX, sourceItem.Location.WorldY, sourceItem.Location.WorldZ),
                    4.5f,
                    2.0f,
                    false))
            {
                NotifyErrorClientRpc("Você está longe demais deste item caído.", targetParams);
                return;
            }

            var loc = new LocationRef((LocationType)targetLocType, targetHolderId, targetSlotId);
            if (loc.Type == LocationType.Hand && loc.HolderId != senderId)
            {
                NotifyErrorClientRpc("Você só pode transferir itens para as suas próprias mãos.", targetParams);
                return;
            }

            if (loc.Type == LocationType.Hand &&
                !TryPrepareHandForItemOrToolPickupServer(senderId, (HandSlotIndex)loc.SlotId, out string weaponErr))
            {
                NotifyErrorClientRpc(weaponErr, targetParams);
                return;
            }

            if (loc.Type == LocationType.StationSlot &&
                !IsStationWithinReach(senderId, (int)loc.HolderId, out string dstReachError, loc.SlotId))
            {
                NotifyErrorClientRpc(dstReachError, targetParams);
                return;
            }

            if (loc.Type == LocationType.Hand && root.TransferService.IsLocationOccupied(loc))
            {
                NotifyErrorClientRpc("A mão selecionada está ocupada!", targetParams);
                return;
            }

            var res = root.TransferService.TransferItem(itemId, loc, senderId, bypassReservation: false, enforceRequesterOwnership: true);
            if (res.Success)
            {
                CommitAndReplicate();
            }
            else
            {
                NotifyErrorClientRpc(string.IsNullOrEmpty(res.Message) ? "Não foi possível mover o item." : res.Message, targetParams);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void TransferToolServerRpc(ulong toolIdVal, byte targetLocType, ulong targetHolderId, int targetSlotId, ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            var targetParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { senderId } } };
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null) return;
            if (RejectIfCannotActServer(senderId, requireOpenShift: true)) return;

            var toolId = new ToolItemId(toolIdVal);
            if (root.DomainState.Tools.TryGetValue(toolId, out var toolState) &&
                toolState.Location.Type == LocationType.StationSlot &&
                !IsStationWithinReach(senderId, (int)toolState.Location.HolderId, out string srcReachError, toolState.Location.SlotIndex))
            {
                NotifyErrorClientRpc(srcReachError, targetParams);
                return;
            }

            var loc = new LocationRef((LocationType)targetLocType, targetHolderId, targetSlotId);
            if (loc.Type == LocationType.Hand && loc.HolderId != senderId)
            {
                NotifyErrorClientRpc("Você só pode equipar utensílios nas suas próprias mãos.", targetParams);
                return;
            }

            if (loc.Type == LocationType.StationSlot &&
                !IsStationWithinReach(senderId, (int)loc.HolderId, out string dstReachError, loc.SlotId))
            {
                NotifyErrorClientRpc(dstReachError, targetParams);
                return;
            }

            if (loc.Type == LocationType.Hand &&
                (IsHandBlockedByWeaponServer(senderId, (HandSlotIndex)loc.SlotId, out _) || root.TransferService.IsLocationOccupied(loc)))
            {
                var otherHand = loc.SlotId == 0 ? HandSlotIndex.Right : HandSlotIndex.Left;
                var altLoc = new LocationRef(LocationType.Hand, senderId, (int)otherHand);
                if (!IsHandBlockedByWeaponServer(senderId, otherHand, out _) && !root.TransferService.IsLocationOccupied(altLoc))
                {
                    loc = altLoc;
                }
            }

            if (loc.Type == LocationType.Hand &&
                !TryPrepareHandForItemOrToolPickupServer(senderId, (HandSlotIndex)loc.SlotId, out string weaponToolErr))
            {
                NotifyErrorClientRpc(weaponToolErr, targetParams);
                return;
            }

            var res = root.TransferService.TransferTool(toolId, loc, root.Clock.Now, senderId, enforceRequesterOwnership: true);
            if (res.Success)
            {
                var toClose = new List<int>();
                foreach (var kvp in _storageSessions)
                {
                    if (kvp.Value.ClientId == senderId) toClose.Add(kvp.Key);
                }
                for (int i = 0; i < toClose.Count; i++)
                {
                    _storageSessions.Remove(toClose[i]);
                    SyncStationDoorStateClientRpc(toClose[i], false);
                }
                CloseStoragePickerClientRpc(targetParams);
                CommitAndReplicate();
            }
            else
            {
                NotifyErrorClientRpc(string.IsNullOrEmpty(res.Message) ? "Não foi possível mover o utensílio." : res.Message, targetParams);
            }
        }

        private static bool IsStationWithinReach(ulong clientId, int stationId, out string errorMsg, int slotId = -1)
        {
            errorMsg = null;
            if (!StationRegistry.TryGetStation(stationId, out var stationTf) || stationTf == null)
                return true;

            if (!PlayerContextResolver.TryGetPlayer(clientId, out var playerObj, out var head) || head == null)
                return true;

            Vector3 headPos = head.position;
            Vector3 bestPoint = stationTf.position;
            float bestDistSq = (headPos - stationTf.position).sqrMagnitude;

            var stationView = stationTf.GetComponent<Stations.StationView>();
            if (stationView != null)
            {
                if (slotId >= 0)
                {
                    Vector3 slotPos = stationView.GetSlotPosition(slotId);
                    float dSq = (headPos - slotPos).sqrMagnitude;
                    if (dSq < bestDistSq)
                    {
                        bestDistSq = dSq;
                        bestPoint = slotPos;
                    }
                }

                if (stationView.slotAnchors != null)
                {
                    for (int i = 0; i < stationView.slotAnchors.Length; i++)
                    {
                        var anchor = stationView.slotAnchors[i];
                        if (anchor != null)
                        {
                            float dSq = (headPos - anchor.position).sqrMagnitude;
                            if (dSq < bestDistSq)
                            {
                                bestDistSq = dSq;
                                bestPoint = anchor.position;
                            }
                        }
                    }
                }
            }

            var colliders = stationTf.GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                var col = colliders[i];
                if (col != null && col.enabled)
                {
                    Vector3 closestPt = col.ClosestPoint(headPos);
                    float dSq = (headPos - closestPt).sqrMagnitude;
                    if (dSq < bestDistSq)
                    {
                        bestDistSq = dSq;
                        bestPoint = closestPt;
                    }
                }
            }

            if (!InteractionValidator.ValidateDistanceAndSight(clientId, bestPoint, 4.5f, 2.0f, false))
            {
                errorMsg = "Você está longe demais desta bancada.";
                return false;
            }

            return true;
        }

        [ServerRpc(RequireOwnership = false)]
        public void StartWorkAtomicServerRpc(int stationId, int slotIndex, int processId, byte activeHand, ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (RejectIfCannotActServer(senderClientId, requireOpenShift: true)) return;
            Debug.Log($"[Server] StartWorkAtomicServerRpc recebido: stationId={stationId}, slotIndex={slotIndex}, processId={processId}, hand={activeHand}, client={senderClientId}");
            if (root == null || root.DomainState == null || slotIndex < 0)
            {
                Debug.LogWarning($"[Server] StartWorkAtomicServerRpc rejeitado: root={(root != null)}, domainState={(root?.DomainState != null)}, slotIndex={slotIndex}");
                return;
            }

            if (!IsStationWithinReach(senderClientId, stationId, out string reachErr, slotIndex))
            {
                NotifyErrorClientRpc(reachErr, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { senderClientId } } });
                return;
            }

            var slot = new StationSlotId(stationId, slotIndex);
            bool domainMutated = false;

            // Check if there is already an item in the slot
            ItemState itemInSlot = null;
            foreach (var kvp in root.DomainState.Items)
            {
                if (kvp.Value.Location == LocationRef.InStation(stationId, slotIndex))
                {
                    itemInSlot = kvp.Value;
                    break;
                }
            }

            if (itemInSlot == null)
            {
                // 1) Try to transfer item from player's ACTIVE hand first;
                // 2) If active hand holds a tool (or is empty), check the OTHER hand for a raw ingredient!
                ItemId itemToTransfer = new ItemId(0);
                int otherHand = activeHand == 0 ? 1 : 0;

                foreach (var kvp in root.DomainState.Items)
                {
                    if (kvp.Value.Location.Type == LocationType.Hand && kvp.Value.Location.HolderId == senderClientId && kvp.Value.Location.SlotId == activeHand)
                    {
                        itemToTransfer = kvp.Key;
                        break;
                    }
                }

                if (itemToTransfer.Value == 0)
                {
                    foreach (var kvp in root.DomainState.Items)
                    {
                        if (kvp.Value.Location.Type == LocationType.Hand && kvp.Value.Location.HolderId == senderClientId && kvp.Value.Location.SlotId == otherHand)
                        {
                            itemToTransfer = kvp.Key;
                            break;
                        }
                    }
                }

                if (itemToTransfer.Value != 0)
                {
                    var loc = LocationRef.InStation(stationId, slotIndex);
                    var transferRes = root.TransferService.TransferItem(itemToTransfer, loc);
                    if (transferRes.Success)
                    {
                        domainMutated = true;
                        root.DomainState.Items.TryGetValue(itemToTransfer, out itemInSlot);
                    }
                }

                if (itemInSlot == null)
                {
                    return; // No item in slot and no item in player's hands
                }
            }

            // Auto-detect processId from the item in the slot whenever processId <= 0 or does not match the item in the slot
            bool needsResolve = processId <= 0 ||
                                !root.Catalog.Processes.TryGetValue(processId, out var currentProc) ||
                                currentProc.InputIngredientId != itemInSlot.DefinitionId ||
                                currentProc.InputStage != itemInSlot.PrepStage;

            if (needsResolve)
            {
                processId = 0;
                foreach (var proc in root.Catalog.Processes.Values)
                {
                    if (proc.InputIngredientId == itemInSlot.DefinitionId && proc.InputStage == itemInSlot.PrepStage)
                    {
                        processId = proc.Id;
                        break;
                    }
                }
            }

            if (processId <= 0)
            {
                if (domainMutated)
                {
                    CommitAndReplicate();
                }
                var targetParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { senderClientId } } };
                string msg = itemInSlot.PrepStage == ProcessingStage.Prepared
                    ? "Este ingrediente já está preparado!"
                    : "Este item não pode ser processado nesta bancada.";
                NotifyErrorClientRpc(msg, targetParams);
                return;
            }

            var res = root.ProcessingService.StartWork(slot, processId, senderClientId, root.DomainState, root.Catalog, root.Clock);
            Debug.Log($"[Server] ProcessingService.StartWork resultado: Success={res.Success}, Error={res.Error}, Message=\"{res.Message}\"");
            if (res.Success)
            {
                CommitAndReplicate();
            }
            else
            {
                // Se o domínio foi mutado (item transferido), replicar mesmo com falha no StartWork
                if (domainMutated)
                {
                    CommitAndReplicate();
                }
                var targetParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { senderClientId } } };
                NotifyErrorClientRpc(string.IsNullOrEmpty(res.Message) ? "Não foi possível iniciar o trabalho." : res.Message, targetParams);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void HeartbeatWorkServerRpc(int stationId, int slotIndex, ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null) return;

            var slot = new StationSlotId(stationId, slotIndex);
            root.ProcessingService.HeartbeatWork(slot, senderClientId, root.DomainState, root.Clock);
        }

        [ServerRpc(RequireOwnership = false)]
        public void CancelWorkServerRpc(int stationId, int slotIndex, ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null) return;

            var slot = new StationSlotId(stationId, slotIndex);
            var res = root.ProcessingService.CancelWork(slot, senderClientId, root.DomainState, root.Clock);
            if (res.Success)
            {
                CommitAndReplicate();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void AddIngredientToPizzaServerRpc(ulong pizzaIdVal, ulong ingredientIdVal, ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null) return;
            if (RejectIfCannotActServer(senderClientId, requireOpenShift: true)) return;

            Debug.Log($"[Server] AddIngredientToPizzaServerRpc recebido: pizzaId={pizzaIdVal}, ingredientId={ingredientIdVal}, client={senderClientId}");
            var targetParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { senderClientId } } };
            var state = root.DomainState;

            if (!state.Items.TryGetValue(new ItemId(pizzaIdVal), out var pizza))
            {
                Debug.LogWarning($"[Server] AddIngredientToPizzaServerRpc: Pizza {pizzaIdVal} nao encontrada no estado!");
                NotifyErrorClientRpc("Pizza nao encontrada no servidor.", targetParams);
                return;
            }
            if (!state.Items.TryGetValue(new ItemId(ingredientIdVal), out var ingItem))
            {
                Debug.LogWarning($"[Server] AddIngredientToPizzaServerRpc: Ingrediente {ingredientIdVal} nao encontrado no estado!");
                NotifyErrorClientRpc("Ingrediente nao encontrado no servidor.", targetParams);
                return;
            }

            if (pizza.Category == ItemCategory.Dough && pizza.PrepStage == ProcessingStage.Raw)
            {
                NotifyErrorClientRpc("Abra a massa na bancada de preparo antes de montar a pizza!", targetParams);
                return;
            }

            if (ingItem.Category != ItemCategory.Sauce && ingItem.PrepStage == ProcessingStage.Raw)
            {
                NotifyErrorClientRpc("Prepare o ingrediente na bancada de preparo primeiro!", targetParams);
                return;
            }

            // Permissive add: Add ingredient composition to pizza
            if (pizza.AddIngredient(ingItem.DefinitionId, ingItem.PrepStage, 1))
            {
                // Consume ingredient item
                root.TransferService.DestroyItem(ingItem.Id);
                CommitAndReplicate();
            }
            else
            {
                NotifyErrorClientRpc("Não foi possível adicionar ingrediente", targetParams);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void InsertOvenServerRpc(ulong pizzaIdVal, int stationId, int slotIndex, ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null) return;
            if (RejectIfCannotActServer(senderClientId, requireOpenShift: true)) return;
            var targetParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { senderClientId } } };

            if (_activeOvenTransfers.ContainsKey(stationId))
            {
                NotifyErrorClientRpc("O forno está realizando uma transferência!", targetParams);
                return;
            }

            var slot = new StationSlotId(stationId, slotIndex);
            var ovenLocation = LocationRef.InStation(stationId, slotIndex);
            foreach (var kv in root.DomainState.Items)
            {
                if (kv.Value.Location == ovenLocation)
                {
                    NotifyErrorClientRpc("Slot do forno ocupado!", targetParams);
                    return;
                }
            }

            if (!root.DomainState.Items.TryGetValue(new ItemId(pizzaIdVal), out var pizza) ||
                pizza.Location.Type != LocationType.Hand ||
                pizza.Location.HolderId != senderClientId)
            {
                NotifyErrorClientRpc("Pizza não está na mão!", targetParams);
                return;
            }

            byte handSlot = (byte)pizza.Location.SlotId;
            _activeOvenTransfers[stationId] = (pizzaIdVal, slotIndex, senderClientId, true);

            const float doorOpenDuration = 0.25f;
            const float flightDuration = 0.40f;
            const float doorCloseDuration = 0.25f;

            StartOvenTransferClientRpc(stationId, pizzaIdVal, slotIndex, senderClientId, handSlot, true, doorOpenDuration, flightDuration, doorCloseDuration);
            StartCoroutine(CompleteOvenInsertionRoutine(stationId, pizzaIdVal, slotIndex, doorOpenDuration + flightDuration));
        }

        private System.Collections.IEnumerator CompleteOvenInsertionRoutine(int stationId, ulong pizzaIdVal, int slotIndex, float delay)
        {
            yield return new WaitForSeconds(delay);

            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null) yield break;

            if (_activeOvenTransfers.TryGetValue(stationId, out var transfer) && transfer.PizzaId == pizzaIdVal)
            {
                _activeOvenTransfers.Remove(stationId);
                var slot = new StationSlotId(stationId, slotIndex);
                var res = root.ProcessingService.InsertOven(new ItemId(pizzaIdVal), slot, root.DomainState, 15f, root.Clock);
                if (res.Success)
                {
                    CommitAndReplicate();
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void RemoveOvenServerRpc(int stationId, int slotIndex, byte targetHand, ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null) return;
            if (RejectIfCannotActServer(senderClientId, requireOpenShift: true)) return;
            var targetParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { senderClientId } } };

            if (_activeOvenTransfers.ContainsKey(stationId))
            {
                NotifyErrorClientRpc("O forno está realizando uma transferência!", targetParams);
                return;
            }

            var slot = new StationSlotId(stationId, slotIndex);
            var ovenLocation = LocationRef.InStation(stationId, slotIndex);
            ItemState pizzaInSlot = null;
            foreach (var kv in root.DomainState.Items)
            {
                if (kv.Value.Location == ovenLocation)
                {
                    pizzaInSlot = kv.Value;
                    break;
                }
            }

            if (pizzaInSlot == null)
            {
                NotifyErrorClientRpc("Forno vazio!", targetParams);
                return;
            }

            var targetHandIndex = (HandSlotIndex)targetHand;
            var targetLoc = LocationRef.InHand(senderClientId, targetHandIndex);

            if (root.TransferService.IsLocationOccupied(targetLoc) || IsHandBlockedByWeaponServer(senderClientId, targetHandIndex, out _))
            {
                var otherHand = targetHandIndex == HandSlotIndex.Left ? HandSlotIndex.Right : HandSlotIndex.Left;
                var otherLoc = LocationRef.InHand(senderClientId, otherHand);
                if (!root.TransferService.IsLocationOccupied(otherLoc) && !IsHandBlockedByWeaponServer(senderClientId, otherHand, out _))
                {
                    targetLoc = otherLoc;
                    targetHand = (byte)otherHand;
                    targetHandIndex = otherHand;
                }
            }

            if (!TryPrepareHandForItemOrToolPickupServer(senderClientId, targetHandIndex, out string ovenWeaponErr))
            {
                NotifyErrorClientRpc(ovenWeaponErr, targetParams);
                return;
            }

            if (root.TransferService.IsLocationOccupied(targetLoc))
            {
                NotifyErrorClientRpc("Ambas as mãos ocupadas! Libere uma mão antes de retirar do forno.", targetParams);
                return;
            }

            _activeOvenTransfers[stationId] = (pizzaInSlot.Id.Value, slotIndex, senderClientId, false);

            const float doorOpenDuration = 0.25f;
            const float flightDuration = 0.40f;
            const float doorCloseDuration = 0.25f;

            StartOvenTransferClientRpc(stationId, pizzaInSlot.Id.Value, slotIndex, senderClientId, targetHand, false, doorOpenDuration, flightDuration, doorCloseDuration);
            StartCoroutine(CompleteOvenRemovalRoutine(stationId, slotIndex, pizzaInSlot.Id.Value, targetLoc, doorOpenDuration, flightDuration));
        }

        private System.Collections.IEnumerator CompleteOvenRemovalRoutine(int stationId, int slotIndex, ulong pizzaIdVal, LocationRef targetLoc, float doorOpenDuration, float flightDuration)
        {
            var root = PizzeriaRoot.Instance;

            // 1. Aguarda a porta abrir
            yield return new WaitForSeconds(doorOpenDuration);

            // 2. Para a cocção no exato momento em que a porta abre e a pizza começa a sair
            if (root != null && root.DomainState != null)
            {
                var slot = new StationSlotId(stationId, slotIndex);
                if (root.DomainState.ActiveOperations.TryGetValue(slot, out var op))
                {
                    op.Progress.Materialize((float)root.Clock.Now);
                    if (root.DomainState.Items.TryGetValue(new ItemId(pizzaIdVal), out var pizza))
                    {
                        pizza.CookProgress = op.Progress.Normalized;
                    }
                    root.DomainState.ActiveOperations.Remove(slot);
                    CommitAndReplicate();
                }
            }

            // 3. Aguarda o voo até a mão
            yield return new WaitForSeconds(flightDuration);

            if (root != null && root.DomainState != null)
            {
                if (_activeOvenTransfers.TryGetValue(stationId, out var transfer) && transfer.PizzaId == pizzaIdVal)
                {
                    _activeOvenTransfers.Remove(stationId);
                    var slot = new StationSlotId(stationId, slotIndex);
                    var res = root.ProcessingService.RemoveOven(slot, targetLoc, root.DomainState, root.Clock);
                    if (res.Success)
                    {
                        CommitAndReplicate();
                    }
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void StartPackagingServerRpc(ulong pizzaIdVal, ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            var targetParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { senderId } } };
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null) return;
            if (RejectIfCannotActServer(senderId, requireOpenShift: true)) return;

            var pizzaId = new ItemId(pizzaIdVal);
            if (!root.DomainState.Items.TryGetValue(pizzaId, out var pizza))
            {
                NotifyErrorClientRpc("Pizza não encontrada para embalagem.", targetParams);
                return;
            }

            if (pizza.IsBoxed || !pizza.IsPizzaLike)
            {
                NotifyErrorClientRpc("Este item não pode ser embalado como pizza.", targetParams);
                return;
            }

            if (pizza.ReservedByHold != 0 && pizza.ReservedByHold != senderId)
            {
                NotifyErrorClientRpc("Item reservado por outro processo.", targetParams);
                return;
            }

            if (pizza.Location.Type == LocationType.Hand)
            {
                if (pizza.Location.HolderId != senderId)
                {
                    NotifyErrorClientRpc("Você não está segurando esta pizza.", targetParams);
                    return;
                }
            }
            else if (pizza.Location.Type == LocationType.StationSlot)
            {
                if (!IsStationWithinReach(senderId, (int)pizza.Location.HolderId, out string reachError, pizza.Location.SlotIndex))
                {
                    NotifyErrorClientRpc(reachError, targetParams);
                    return;
                }

                var slotKey = new StationSlotId((int)pizza.Location.HolderId, pizza.Location.SlotIndex);
                if (root.DomainState.ActiveOperations.TryGetValue(slotKey, out var activeOp) && activeOp.Phase == OperationPhase.Running)
                {
                    NotifyErrorClientRpc("Aguarde o processo na bancada terminar antes de embalar.", targetParams);
                    return;
                }
            }
            else
            {
                NotifyErrorClientRpc("Coloque a pizza na bancada ou em sua mão para embalar.", targetParams);
                return;
            }

            // A15: mantém permissão explícita para embalar crua/assada/queimada, informando claramente o resultado esperado.
            pizza.PackagingState = PackagingState.Boxed;
            pizza.Category = ItemCategory.Box;
            pizza.Revision++;
            root.DomainState.IncrementRevision();
            CommitAndReplicate();

            if (pizza.CookingStage != CookingStage.Baked)
            {
                NotifySuccessClientRpc("Pizza embalada! Atenção: pizza não está assada corretamente — o pagamento será reduzido.", targetParams);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void ConfirmStageBoxServerRpc(ulong boxIdVal, int stagingSlotIndex, int chosenDestinationId, ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            var targetParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { senderId } } };
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null) return;
            if (RejectIfCannotActServer(senderId, requireOpenShift: true)) return;

            if (chosenDestinationId <= 0)
            {
                NotifyErrorClientRpc("Selecione um endereço válido para etiquetar a caixa.", targetParams);
                return;
            }

            var boxId = new ItemId(boxIdVal);
            if (!root.DomainState.Items.TryGetValue(boxId, out var box))
            {
                NotifyErrorClientRpc("Caixa não encontrada no servidor.", targetParams);
                return;
            }

            if (!box.IsBoxed)
            {
                NotifyErrorClientRpc("Embale a pizza antes de definir o destino.", targetParams);
                return;
            }

            if (box.ReservedByHold != 0 && box.ReservedByHold != senderId)
            {
                NotifyErrorClientRpc("Caixa reservada por outro processo.", targetParams);
                return;
            }

            if (box.Location.Type == LocationType.Hand)
            {
                if (box.Location.HolderId != senderId)
                {
                    NotifyErrorClientRpc("Você não está segurando esta caixa.", targetParams);
                    return;
                }
            }
            else if (box.Location.Type == LocationType.StationSlot)
            {
                if (!IsStationWithinReach(senderId, (int)box.Location.HolderId, out string reachError, box.Location.SlotIndex))
                {
                    NotifyErrorClientRpc(reachError, targetParams);
                    return;
                }
            }
            else
            {
                NotifyErrorClientRpc("Caixa em local inválido para etiquetagem.", targetParams);
                return;
            }

            box.LabelDestinationId = chosenDestinationId;
            // Mantém a caixa repousando na bancada de expedição (StationSlot) para ser vista e recolhida
            box.Revision++;
            root.DomainState.IncrementRevision();
            CommitAndReplicate();
        }

        [ServerRpc(RequireOwnership = false)]
        public void AnswerPhoneServerRpc(int phoneStationId, ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            var targetParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { senderId } } };
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null) return;
            if (RejectIfCannotActServer(senderId, requireOpenShift: true)) return;

            if (!IsStationWithinReach(senderId, phoneStationId, out string reachError))
            {
                NotifyErrorClientRpc(reachError, targetParams);
                return;
            }

            // Se houver chamada pendente no CallService, atende a primeira
            if (root.DomainState.Calls.Count > 0)
            {
                var firstCallId = System.Linq.Enumerable.First(root.DomainState.Calls.Keys);
                var answerRes = root.CallService.AnswerCall(
                    root.DomainState,
                    firstCallId,
                    root.OrderService,
                    root.Catalog,
                    root.Clock,
                    root.Config.maxActiveOrders,
                    root.Config.deliveryTimeLimit);

                if (answerRes.Success)
                {
                    CommitAndReplicate();
                    Debug.Log($"[PizzeriaCommandHandler] Chamada #{firstCallId.Value} atendida por cliente {senderId}! Pedido #{answerRes.Value.Value} criado.");
                    return;
                }

                NotifyErrorClientRpc(string.IsNullOrEmpty(answerRes.Message) ? "Não foi possível atender o telefone." : answerRes.Message, targetParams);
                return;
            }

            // Fallback: gera pedido de pizza por composição de ingredientes
            var specs = new List<(PizzaSpecification spec, int qty)>
            {
                (PizzaOrderGenerator.GenerateRandomSpecification(root.Catalog), 1)
            };

            int destId = 4;
            var res = root.OrderService.CreatePhoneOrderBySpecifications(
                new AddressId(destId),
                specs,
                root.DomainState,
                root.Catalog,
                root.Clock,
                root.Config.maxActiveOrders,
                root.Config.deliveryTimeLimit);

            if (res.Success)
            {
                CommitAndReplicate();
                Debug.Log($"[PizzeriaCommandHandler] Telefone atendido! Pedido #{res.Value.Value} publicado para o destino #{destId}.");
            }
            else
            {
                NotifyErrorClientRpc(string.IsNullOrEmpty(res.Message) ? "Limite de pedidos ativos atingido." : res.Message, targetParams);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void SubmitDeliveryServerRpc(int destinationId, ulong[] selectedItemIds, ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            var targetParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { senderClientId } } };
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null) return;
            if (RejectIfCannotActServer(senderClientId, requireOpenShift: true)) return;

            ulong serverAttemptId = ((ulong)root.DomainState.Revision << 20)
                ^ ((ulong)(uint)destinationId << 8)
                ^ (senderClientId + 1UL);

            var res = LogisticsCommands.ExecuteServerDeliveryCore(root, senderClientId, destinationId, selectedItemIds, serverAttemptId);
            if (!res.Success)
            {
                NotifyErrorClientRpc(string.IsNullOrEmpty(res.Message) ? "Não foi possível concluir a entrega." : res.Message, targetParams);
                return;
            }

            var eval = res.Value;
            LogisticsCommands.Instance?.DispatchDeliveryClientFeedback(senderClientId, destinationId, eval);
            NotifySuccessClientRpc($"Entrega concluída! +R${eval.PersonalEarned}", targetParams);

            Debug.Log($"[PizzeriaCommandHandler] Entrega concluída no destino #{destinationId}. Pessoal: +R${eval.PersonalEarned}, Restaurante: +R${eval.RestaurantEarned}.");
        }

        [ServerRpc(RequireOwnership = false)]
        public void BeginAttendCustomerHoldServerRpc(ulong customerIdVal, ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null || root.PlayerHoldService == null) return;
            if (RejectIfCannotActServer(senderClientId, requireOpenShift: true)) return;

            var targetParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { senderClientId } } };
            float holdDur = root.Config != null && root.Config.customerAttendHoldDuration > 0.1f
                ? root.Config.customerAttendHoldDuration
                : 0.6f;
            float leaseSec = root.Config != null ? root.Config.workLeaseSeconds : 0.75f;
            int maxOrders = root.Config != null ? root.Config.maxActiveOrders : 6;

            var res = root.PlayerHoldService.BeginAttendCustomer(
                root.DomainState,
                senderClientId,
                new CustomerId(customerIdVal),
                holdDur,
                (float)root.Clock.Now,
                leaseSec,
                root.CustomerService?.TableService,
                maxOrders);

            if (!res.Success)
            {
                NotifyErrorClientRpc(res.Message, targetParams);
                return;
            }

            CommitAndReplicate();
        }

        [ServerRpc(RequireOwnership = false)]
        public void HeartbeatAttendCustomerHoldServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null || root.PlayerHoldService == null) return;

            float leaseSec = root.Config != null ? root.Config.workLeaseSeconds : 0.75f;
            root.PlayerHoldService.Heartbeat(root.DomainState, senderClientId, (float)root.Clock.Now, leaseSec);
            CommitAndReplicate();
        }

        [ServerRpc(RequireOwnership = false)]
        public void CancelAttendCustomerHoldServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null || root.PlayerHoldService == null) return;

            root.PlayerHoldService.Cancel(root.DomainState, senderClientId);
            CommitAndReplicate();
        }

        [ServerRpc(RequireOwnership = false)]
        public void ServeTablePizzaServerRpc(int tableId, ulong pizzaItemIdVal, ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null || root.CustomerService == null) return;
            if (RejectIfCannotActServer(senderClientId, requireOpenShift: true)) return;

            var targetParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { senderClientId } } };
            var res = root.CustomerService.ServeTableUnboxedPizza(
                root.DomainState,
                tableId,
                new ItemId(pizzaItemIdVal),
                senderClientId,
                root.Catalog,
                root.OrderService,
                root.Config != null ? root.Config.burnedPayoutFactor : 0f,
                root.Config != null ? root.Config.unbakedPayoutFactor : 0f,
                root.Config != null ? root.Config.restaurantBaseBonus : 10,
                root.Config != null ? root.Config.customerEatingDuration : 12f);

            if (!res.Success)
            {
                NotifyErrorClientRpc(res.Message, targetParams);
                return;
            }

            var eval = res.Value;
            if (root.DomainState.Items.TryGetValue(new ItemId(pizzaItemIdVal), out var servedPizza) &&
                servedPizza.Ingredients != null)
            {
                for (int i = 0; i < servedPizza.Ingredients.Count; i++)
                {
                    if (servedPizza.Ingredients[i].IngredientDefId == 10)
                    {
                        if (root.SpeciesHostilityService != null &&
                            root.SpeciesHostilityService.TryRegisterIngredientOffense(NpcSpecies.Vampire, 10))
                        {
                            BroadcastNoticeClientRpc("Cliente vampiro se ofendeu com Alho na pizza!", isError: true);
                        }
                        break;
                    }
                }
            }
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClients.TryGetValue(senderClientId, out var client))
            {
                if (client.PlayerObject != null)
                {
                    var playerState = client.PlayerObject.GetComponent<PlayerState>();
                    if (playerState != null)
                    {
                        playerState.Money.Value += eval.PersonalEarned;
                    }
                }
            }

            CommitAndReplicate();
            NotifySuccessClientRpc(eval.SummaryMessage, targetParams);
        }

        // ============================================================
        // Ciclo de Noites (O5), Quadro de Equipe (O7) e Recuperação (O20)
        // ============================================================

        private const float RecoveryCooldownSeconds = 60f;
        private readonly Dictionary<ulong, double> _lastPlayerRecoverTime = new Dictionary<ulong, double>();
        private readonly Dictionary<ulong, double> _lastMotoRecoverTime = new Dictionary<ulong, double>();

        public static event System.Func<bool> OnServerRecoverMotorcycleRequested;
        public static event System.Action OnServerResetNightCombatRequested;
        public static event System.Action<bool> OnServerResetNightCombatWithPolicyRequested;

        public static void RequestResetNightCombatServer(bool resetWeaponsAndAmmo)
        {
            OnServerResetNightCombatWithPolicyRequested?.Invoke(resetWeaponsAndAmmo);
            if (!resetWeaponsAndAmmo)
            {
                OnServerResetNightCombatRequested?.Invoke();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void ToggleClaimOrderServerRpc(ulong orderIdVal, ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            var targetParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { senderId } } };
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;
            if (RejectIfCannotActServer(senderId, requireOpenShift: true)) return;

            var res = NightCycleService.ToggleOrderClaim(root.DomainState, new OrderId(orderIdVal), senderId);
            if (!res.Success)
            {
                NotifyErrorClientRpc(res.Message, targetParams);
                return;
            }

            bool claimed = root.DomainState.Orders.TryGetValue(new OrderId(orderIdVal), out var order) &&
                           order.ClaimedByClientId == senderId;
            if (root.EmployeeTaskService != null)
            {
                var oid = new OrderId(orderIdVal);
                if (claimed && root.EmployeeTaskService.TryGetTaskForOrder(oid, Domain.Services.EmployeeRole.Pizzaiolo, out var empTask))
                {
                    root.EmployeeTaskService.HumanTakeoverTask(empTask.TaskId, senderId, root.Clock.Now);
                }
                else if (!claimed)
                {
                    root.EmployeeTaskService.ReleaseActorTasks(senderId, isHuman: true);
                }
            }

            CommitAndReplicate();
            NotifySuccessClientRpc(claimed ? $"Comanda #{orderIdVal} assumida por você." : $"Comanda #{orderIdVal} liberada para a equipe.", targetParams);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RetryNightServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            var targetParams = TargetSender(senderId);
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;

            if (senderId != NetworkManager.ServerClientId)
            {
                NotifyErrorClientRpc("Apenas o Host pode reiniciar.", targetParams);
                return;
            }

            bool allowDebugRetry = root.Config != null && root.Config.allowDebugNightRetryOnDefeat;
            if (!allowDebugRetry)
            {
                StartNewRunInternalServer();
                return;
            }

            int nightNum = root.DomainState.CurrentSnapshot != null
                ? root.DomainState.CurrentSnapshot.NightNumber
                : root.DomainState.CurrentNightNumber;

            ScaryParty.Combat.Monsters.MonsterPopulationDirector.Instance?.ClearNightMonstersServer();
            root.EmployeeTaskService?.OnNightEnded();
            var nightParams = root.Config != null ? root.Config.ResolveNightParameters(nightNum) : (NightGoalParameters?)null;
            NightCycleService.RetryCurrentNight(root.DomainState, nightParams);
            RequestResetNightCombatServer(resetWeaponsAndAmmo: false);

            ScaryParty.Pizzeria.Presentation.PizzeriaSceneLoadingCoordinator.Instance?.SetNightContext(
                root.DomainState.CurrentNightNumber,
                root.DomainState.NightRevenueGoal);

            CommitAndReplicate();
            BroadcastNoticeClientRpc($"Noite {root.DomainState.CurrentNightNumber} reiniciada (modo debug) pelo Host!", isError: false);
        }

        [ServerRpc(RequireOwnership = false)]
        public void StartNewRunServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;

            if (senderId != NetworkManager.ServerClientId)
            {
                NotifyErrorClientRpc("Apenas o Host pode iniciar uma nova RUN.", TargetSender(senderId));
                return;
            }

            StartNewRunInternalServer();
        }

        private void StartNewRunInternalServer()
        {
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;

            int newSeed = (root.Config != null && root.Config.runSeedOverride != 0)
                ? root.Config.runSeedOverride
                : (System.Environment.TickCount ^ GetInstanceID());
            int maxNights = root.Config != null ? Mathf.Clamp(root.Config.runMaxNights, 1, 7) : 3;
            var diff = root.Config != null ? root.Config.runDifficulty : RunDifficulty.Normal;
            int startNight = (root.Config != null && root.Config.enableTutorialNight0ByDefault) ? 0 : 1;

            root.ReinitializeForNewRunServer(newSeed, maxNights: maxNights, difficulty: diff, startNight: startNight);

            RequestResetNightCombatServer(resetWeaponsAndAmmo: true);

            CommitAndReplicate();
            string nightLabel = startNight == 0 ? "Noite 0 (Tutorial)" : $"Noite {root.DomainState.CurrentNightNumber}";
            BroadcastNoticeClientRpc($"Nova RUN iniciada! {nightLabel} — Meta: R$ {root.DomainState.NightRevenueGoal}.", isError: false);
        }

        [ServerRpc(RequireOwnership = false)]
        public void StartNight0TutorialServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            var targetParams = TargetSender(senderId);
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;

            if (senderId != NetworkManager.ServerClientId)
            {
                NotifyErrorClientRpc("Apenas o Host pode ativar a Noite 0 (Tutorial).", targetParams);
                return;
            }

            if (root.DomainState.CurrentNightNumber == 0) return;

            int newSeed = (root.Config != null && root.Config.runSeedOverride != 0)
                ? root.Config.runSeedOverride
                : (System.Environment.TickCount ^ GetInstanceID());
            int maxNights = root.Config != null ? Mathf.Clamp(root.Config.runMaxNights, 1, 7) : 3;
            var diff = root.Config != null ? root.Config.runDifficulty : RunDifficulty.Normal;

            root.ReinitializeForNewRunServer(newSeed, maxNights, diff, startNight: 0);
            RequestResetNightCombatServer(resetWeaponsAndAmmo: true);
            CommitAndReplicate();
            BroadcastNoticeClientRpc("Modo Tutorial (Noite 0) iniciado! Conclua 1 pizza ou clique em 'Ir p/ Noite 1'.", isError: false);
        }

        [ServerRpc(RequireOwnership = false)]
        public void SkipTutorialToNight1ServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            var targetParams = TargetSender(senderId);
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;

            if (senderId != NetworkManager.ServerClientId)
            {
                NotifyErrorClientRpc("Apenas o Host pode concluir o tutorial.", targetParams);
                return;
            }

            if (root.DomainState.CurrentNightNumber != 0)
            {
                return;
            }

            int newSeed = (root.Config != null && root.Config.runSeedOverride != 0)
                ? root.Config.runSeedOverride
                : (System.Environment.TickCount ^ GetInstanceID());
            int maxNights = root.Config != null ? Mathf.Clamp(root.Config.runMaxNights, 1, 7) : 3;
            var diff = root.Config != null ? root.Config.runDifficulty : RunDifficulty.Normal;

            root.ReinitializeForNewRunServer(newSeed, maxNights, diff, startNight: 1);
            RequestResetNightCombatServer(resetWeaponsAndAmmo: true);
            CommitAndReplicate();
            BroadcastNoticeClientRpc($"Tutorial concluído! Noite 1 iniciada — Meta: R$ {root.DomainState.NightRevenueGoal}.", isError: false);
        }

        [ServerRpc(RequireOwnership = false)]
        public void AdvanceToNextNightServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            var targetParams = TargetSender(senderId);
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;

            if (senderId != NetworkManager.ServerClientId)
            {
                NotifyErrorClientRpc("Apenas o Host pode avançar para a preparação/próxima noite.", targetParams);
                return;
            }

            if (root.DomainState.CurrentNightPhase != NightPhase.Results)
            {
                NotifyErrorClientRpc("A noite atual precisa ser vencida antes de avançar.", targetParams);
                return;
            }

            if (root.DomainState.CurrentNightNumber == 0)
            {
                SkipTutorialToNight1ServerRpc(rpcParams);
                return;
            }

            var run = root.RunProgressionService;
            if (run != null && root.DomainState.CurrentNightNumber >= run.MaxNights)
            {
                run.TryCompleteNightAndAdvance(root.DomainState);
                CommitAndReplicate();
                BroadcastNoticeClientRpc("VITÓRIA DA RUN! Todas as 3 noites foram concluídas com sucesso!", isError: false);
                return;
            }

            run?.ExpireTemporaryModifiersAtNightEnd();
            root.SpeciesHostilityService?.OnNightEnded();
            root.EmployeeTaskService?.OnNightEnded();
            root.ZombieConversionService?.Reset();
            ScaryParty.Combat.Monsters.MonsterPopulationDirector.Instance?.ClearNightMonstersServer();
            int nextNight = root.DomainState.CurrentNightNumber + 1;
            NightCycleService.ClearTransientNightEntities(root.DomainState, repairBackpacks: true);
            root.DomainState.CurrentNightPhase = NightPhase.PreparingNextNight;
            root.DomainState.IncrementRevision();
            run?.OpenChoiceVote(nextNight, 2);

            RequestResetNightCombatServer(resetWeaponsAndAmmo: false);
            CommitAndReplicate();
            BroadcastNoticeClientRpc($"Preparação para Noite {nextNight} aberta: votem, comprem melhorias e marquem Pronto!", isError: false);
        }

        [ServerRpc(RequireOwnership = false)]
        public void CastRunVoteServerRpc(Unity.Collections.FixedString32Bytes optionId, ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null || root.RunProgressionService == null) return;
            if (root.DomainState.CurrentNightPhase != NightPhase.PreparingNextNight) return;

            if (root.RunProgressionService.CastVote(senderId, optionId.ToString()))
            {
                root.DomainState.IncrementRevision();
                CommitAndReplicate();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetRunReadyServerRpc(bool ready, ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null || root.RunProgressionService == null) return;
            if (root.DomainState.CurrentNightPhase != NightPhase.PreparingNextNight) return;

            root.RunProgressionService.SetPlayerReady(senderId, ready);
            ResolveAndStartPreparedNightIfReadyServer();
            CommitAndReplicate();
        }

        public void ResolveAndStartPreparedNightIfReadyServer()
        {
            var root = PizzeriaRoot.Instance;
            if (root == null || !IsServer || root.DomainState == null || root.RunProgressionService == null) return;
            if (root.DomainState.CurrentNightPhase != NightPhase.PreparingNextNight) return;

            var connected = new List<ulong>();
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClientsIds.Count > 0)
            {
                foreach (var cid in NetworkManager.Singleton.ConnectedClientsIds) connected.Add(cid);
            }
            else
            {
                connected.Add(0UL);
            }

            var run = root.RunProgressionService;
            bool allReady = true;
            for (int i = 0; i < connected.Count; i++)
            {
                if (!ContainsClientId(run.ReadyPlayers, connected[i]))
                {
                    allReady = false;
                    break;
                }
            }

            if (!allReady) return;

            if (!run.IsVoteResolved)
            {
                run.ResolveVoteServer();
            }

            if (!run.CanStartNight(connected)) return;

            int nextNight = root.DomainState.CurrentNightNumber + 1;
            var nightParams = root.Config != null ? root.Config.ResolveNightParameters(nextNight) : (NightGoalParameters?)null;
            NightCycleService.StartNight(root.DomainState, nextNight, nightParams);
            run.FreezeNightStartParameters(root.DomainState, connected.Count);
            ApplyRunBackpackCapacityBonusServer(root);

            RequestResetNightCombatServer(resetWeaponsAndAmmo: false);
            ScaryParty.Pizzeria.Presentation.PizzeriaSceneLoadingCoordinator.Instance?.SetNightContext(
                root.DomainState.CurrentNightNumber,
                root.DomainState.NightRevenueGoal);

            BroadcastNoticeClientRpc($"Noite {root.DomainState.CurrentNightNumber} iniciada! Meta: R$ {root.DomainState.NightRevenueGoal}.", isError: false);
        }

        private static bool ContainsClientId(IReadOnlyCollection<ulong> set, ulong target)
        {
            foreach (var v in set)
            {
                if (v == target) return true;
            }
            return false;
        }

        private static void ApplyRunBackpackCapacityBonusServer(PizzeriaRoot root)
        {
            if (root == null || root.DomainState == null || root.RunProgressionService == null) return;
            int baseCap = root.Config != null ? Mathf.Max(2, root.Config.backpackCapacity) : 4;
            int bonus = root.RunProgressionService.RecalculateEffectiveStats().ExtraBackpackCapacity;
            int targetCap = baseCap + Mathf.Max(0, bonus);
            foreach (var bp in root.DomainState.Backpacks.Values)
            {
                if (bp.Capacity < targetCap)
                {
                    bp.Capacity = targetCap;
                    bp.Revision++;
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void PurchaseRunShopItemServerRpc(byte shopItemId, ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            var targetParams = TargetSender(senderId);
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null || root.RunProgressionService == null) return;
            if (root.DomainState.CurrentNightPhase != NightPhase.PreparingNextNight)
            {
                NotifyErrorClientRpc("A loja só está disponível durante a preparação inter-noites.", targetParams);
                return;
            }

            var run = root.RunProgressionService;
            var state = root.DomainState;
            bool ok = false;
            string purchasedMsg = null;

            switch (shopItemId)
            {
                case 1: // Timer do Forno (único)
                {
                    var item = new RunShopItem { Id = "oven_timer_shop", Name = "Timer do Forno", Cost = 90, IsUnique = true };
                    ok = run.TryPurchaseShopItem(state, item);
                    if (ok)
                    {
                        var def = run.GetModifierDefinition("oven_timer");
                        if (def != null) run.ApplyModifier(def);
                        purchasedMsg = "Timer do Forno adquirido (pizzas não queimam)!";
                    }
                    break;
                }
                case 2: // Bolsa Reforçada (+1 slot)
                {
                    var item = new RunShopItem { Id = "reinforced_bag_shop", Name = "Bolsa Reforçada", Cost = 75, IsUnique = false };
                    if (run.TryPurchaseShopItem(state, item))
                    {
                        var def = run.GetModifierDefinition("reinforced_bag");
                        if (def != null) run.ApplyModifier(def);
                        ApplyRunBackpackCapacityBonusServer(root);
                        ok = true;
                        purchasedMsg = "Bolsa Reforçada adquirida (+1 capacidade de carga)!";
                    }
                    break;
                }
                case 3: // Treinamento de Equipe
                {
                    var item = new RunShopItem { Id = "staff_training_shop", Name = "Treinamento Equipe", Cost = 85, IsUnique = false };
                    if (run.TryPurchaseShopItem(state, item))
                    {
                        var def = run.GetModifierDefinition("staff_training");
                        if (def != null) run.ApplyModifier(def);
                        ok = true;
                        purchasedMsg = "Treinamento aplicado (-25% chance de erro dos funcionários)!";
                    }
                    break;
                }
                case 4: // Suprimentos Base +5
                {
                    var item = new RunShopItem { Id = "extra_supplies_shop", Name = "Suprimentos Base +5", Cost = 45, IsUnique = false };
                    if (run.TryPurchaseShopItem(state, item))
                    {
                        foreach (var kvp in state.StorageSlots)
                        {
                            if (kvp.Key.ingredientDefId >= 1 && kvp.Key.ingredientDefId <= 3)
                            {
                                kvp.Value.Quantity = Mathf.Min(kvp.Value.MaxCapacity, kvp.Value.Quantity + 5);
                                kvp.Value.Revision++;
                            }
                        }
                        ok = true;
                        purchasedMsg = "+5 de Massa, Molho e Queijo adicionados ao estoque!";
                    }
                    break;
                }
                case 5: // Contratar Caixa
                {
                    if (root.EmployeeTaskService != null && root.EmployeeTaskService.TryHireEmployee(state, EmployeeRole.Cashier) != null)
                    {
                        ok = true;
                        purchasedMsg = "Caixa contratado para o atendimento!";
                    }
                    break;
                }
                case 6: // Contratar Pizzaiolo
                {
                    if (root.EmployeeTaskService != null && root.EmployeeTaskService.TryHireEmployee(state, EmployeeRole.Pizzaiolo) != null)
                    {
                        ok = true;
                        purchasedMsg = "Pizzaiolo contratado para a cozinha!";
                    }
                    break;
                }
            }

            if (!ok)
            {
                NotifyErrorClientRpc("Saldo insuficiente no caixa ou item único já adquirido.", targetParams);
                return;
            }

            CommitAndReplicate();
            BroadcastNoticeClientRpc(purchasedMsg ?? "Compra concluída!", isError: false);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestRecoverPlayerServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            var targetParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { senderId } } };
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer) return;

            double now = root.Clock != null ? root.Clock.Now : Time.timeAsDouble;
            if (_lastPlayerRecoverTime.TryGetValue(senderId, out double lastTime) && now - lastTime < RecoveryCooldownSeconds)
            {
                int remaining = Mathf.CeilToInt((float)(RecoveryCooldownSeconds - (now - lastTime)));
                NotifyErrorClientRpc($"Aguarde {remaining}s para recuperar o personagem novamente.", targetParams);
                return;
            }

            _lastPlayerRecoverTime[senderId] = now;
            Vector3 safePos = root.recoveryAnchor != null ? root.recoveryAnchor.position : root.GetPlayerSpawnPosition(0);
            Quaternion safeRot = root.recoveryAnchor != null ? root.recoveryAnchor.rotation : root.GetPlayerSpawnRotation(0);

            TeleportRiderSafelyServer(senderId, safePos, safeRot);
            NotifySuccessClientRpc("Personagem recuperado para área segura da pizzaria.", targetParams);
        }

        private void TeleportRiderSafelyServer(ulong clientId, Vector3 safePos, Quaternion safeRot)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) && client.PlayerObject != null)
            {
                var po = client.PlayerObject;
                var cc = po.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                po.transform.SetPositionAndRotation(safePos, safeRot);
                if (cc != null) cc.enabled = true;
            }

            TeleportPlayerClientRpc(clientId, safePos, safeRot);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestRecoverMotorcycleServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            var targetParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { senderId } } };
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer) return;

            double now = root.Clock != null ? root.Clock.Now : Time.timeAsDouble;
            if (_lastMotoRecoverTime.TryGetValue(senderId, out double lastTime) && now - lastTime < RecoveryCooldownSeconds)
            {
                int remaining = Mathf.CeilToInt((float)(RecoveryCooldownSeconds - (now - lastTime)));
                NotifyErrorClientRpc($"Aguarde {remaining}s para recuperar a moto novamente.", targetParams);
                return;
            }

            bool recovered = OnServerRecoverMotorcycleRequested != null && OnServerRecoverMotorcycleRequested.Invoke();
            if (!recovered)
            {
                NotifyErrorClientRpc("Nenhuma moto de entrega ativa encontrada no pátio.", targetParams);
                return;
            }

            _lastMotoRecoverTime[senderId] = now;
            BroadcastNoticeClientRpc("Moto de entrega devolvida ao estacionamento da pizzaria.", isError: false);
        }

        [ClientRpc]
        private void TeleportPlayerClientRpc(ulong targetClientId, Vector3 safePos, Quaternion safeRot)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.LocalClientId != targetClientId) return;

            var po = nm.SpawnManager?.GetLocalPlayerObject();
            if (po == null) return;

            var cc = po.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            po.transform.SetPositionAndRotation(safePos, safeRot);
            if (cc != null) cc.enabled = true;
        }

        [ClientRpc]
        private void BroadcastNoticeClientRpc(string message, bool isError)
        {
            ScaryParty.Pizzeria.Presentation.NotificationManager.Show(
                message,
                isError ? ScaryParty.Pizzeria.Presentation.NotificationType.Error : ScaryParty.Pizzeria.Presentation.NotificationType.Success);
        }

        // ============================================================
        // Combate, Armas no Rack (1P/2P) e Mochila Danificável
        // ============================================================

        private const float AssistDurationSeconds = 3.0f;
        private const float AssistHeartbeatTimeoutSeconds = 0.65f;
        private const float AssistMaxDistance = 2.85f;
        private const int AllyHealAmount = 25;
        private const double AllyHealCooldownSeconds = 6.0;

        private sealed class PlayerCombatRuntime
        {
            public int CurrentHp = 100;
            public int MaxHp = 100;
            public bool IsIncapacitated;
            public bool IsDead;
            public float RescueRemaining;
            public float RescueProgress;
            public byte RescueMode; // 0 = None, 1 = HealWounded, 2 = ReviveIncapacitated
            public ulong RescuedByClientId;
            public double AssistHeartbeatTime = -999.0;
            public double NextHealAllowedTime = -999.0;
            public ulong SpectatingTargetClientId;
            public bool IsAiming;
            public uint Revision = 1;
        }

        public bool CanPlayerPerformGameplayActionServer(ulong clientId, out string reason, bool requireOpenShift = false)
        {
            if (_playerCombat.TryGetValue(clientId, out var combat))
            {
                if (combat.IsDead)
                {
                    reason = "Você morreu nesta noite e está em modo espectador.";
                    return false;
                }
                if (combat.IsIncapacitated)
                {
                    reason = "Você está nocauteado! Aguarde um aliado segurar [E] (3s).";
                    return false;
                }
            }
            if (requireOpenShift)
            {
                var root = PizzeriaRoot.Instance;
                if (root != null && root.DomainState != null &&
                    root.DomainState.CurrentNightPhase != NightPhase.Active &&
                    root.DomainState.CurrentNightPhase != NightPhase.Preparing)
                {
                    reason = "O expediente desta noite já foi encerrado!";
                    return false;
                }
            }
            reason = null;
            return true;
        }

        private bool RejectIfCannotActServer(ulong clientId, bool requireOpenShift = false)
        {
            if (CanPlayerPerformGameplayActionServer(clientId, out string reason, requireOpenShift))
                return false;
            NotifyErrorClientRpc(reason, TargetSender(clientId));
            return true;
        }

        private readonly Dictionary<ulong, double> _customerAttackRepCooldown = new Dictionary<ulong, double>();

        private readonly Dictionary<ulong, PlayerCombatRuntime> _playerCombat = new Dictionary<ulong, PlayerCombatRuntime>();
        private readonly Dictionary<uint, double> _lastWeaponFireServerTime = new Dictionary<uint, double>();
        private readonly HashSet<ulong> _processedMotorcycleImpacts = new HashSet<ulong>();
        [SerializeField] private int motorcycleHitDamage = 40;
        [SerializeField] private int motorcycleRiderFallDamage = 0;

        private static readonly Vector2[] ShotgunPelletOffsetsDeg = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1.8f, 0.9f),
            new Vector2(-1.8f, 0.9f),
            new Vector2(1.8f, -1.1f),
            new Vector2(-1.8f, -1.1f),
            new Vector2(0f, 2.4f),
            new Vector2(3.1f, 0f),
            new Vector2(-3.1f, 0f)
        };

        public bool TryPrepareHandForItemOrToolPickupServer(ulong clientId, HandSlotIndex hand, out string errorMsg)
        {
            errorMsg = null;
            var netState = PizzeriaNetworkState.Instance;
            if (netState == null || netState.Weapons == null) return true;

            var moto = netState.MotorcycleState.Value;
            if (moto.IsActive && moto.IsMounted && moto.RiderClientId == clientId && hand == HandSlotIndex.Left)
            {
                errorMsg = "A mão esquerda está pilotando a moto! Use a mão direita [2].";
                return false;
            }

            // Se estiver empunhando a Escopeta (2 mãos), guarda automaticamente na bandoleira (0 mãos) para liberar a mão.
            for (int i = 0; i < netState.Weapons.Count; i++)
            {
                var w = netState.Weapons[i];
                if (!w.IsAvailableOnRack && !w.IsSlinged && w.OwnerClientId == clientId &&
                    w.WeaponType == (byte)ScaryParty.Combat.WeaponType.Shotgun)
                {
                    w.IsSlinged = true;
                    w.Revision++;
                    netState.Weapons[i] = w;
                    NotifySuccessClientRpc("Escopeta movida para a bandoleira.", TargetSender(clientId));
                    return true;
                }
            }

            if (IsHandBlockedByWeaponServer(clientId, hand, out errorMsg))
                return false;

            return true;
        }

        private static bool IsHandBlockedByWeaponServer(ulong clientId, HandSlotIndex hand, out string errorMsg)
        {
            errorMsg = null;
            var netState = PizzeriaNetworkState.Instance;
            if (netState == null) return false;
            if (!netState.IsHandOccupiedByWeapon(clientId, (int)hand)) return false;

            var moto = netState.MotorcycleState.Value;
            if (moto.IsActive && moto.IsMounted && moto.RiderClientId == clientId && hand == HandSlotIndex.Left)
            {
                errorMsg = "A mão esquerda está pilotando a moto! Use a mão direita [2].";
            }
            else if (netState.TryGetEquippedWeapon(clientId, out var w) && w.WeaponType == (byte)ScaryParty.Combat.WeaponType.Shotgun)
            {
                errorMsg = "Escopeta ocupa as duas mãos! Use [G] para colocar na bandoleira.";
            }
            else
            {
                errorMsg = "A mão direita está segurando a Pistola! Use a mão esquerda [1] ou devolva a arma.";
            }
            return true;
        }

        private static bool IsDomainHandOccupiedByItemOrToolServer(PizzeriaState state, ulong playerId, HandSlotIndex hand)
        {
            if (state == null) return false;
            var handLoc = LocationRef.InHand(playerId, hand);
            foreach (var it in state.Items.Values)
            {
                if (it.Location.Equals(handLoc)) return true;
            }
            foreach (var t in state.Tools.Values)
            {
                if (t.Location.Equals(handLoc)) return true;
            }
            return false;
        }

        public void EnsureDefaultWeaponsInitializedServer()
        {
            if (!IsServer) return;
            var netState = PizzeriaNetworkState.Instance;
            if (netState == null || netState.Weapons == null || netState.Weapons.Count > 0) return;

            // Instâncias distintas por WeaponId: 2 Pistolas (1 mão direita) + 1 Escopeta (2 mãos)
            netState.Weapons.Add(new NetWeaponStateDto
            {
                WeaponId = 1,
                WeaponType = (byte)ScaryParty.Combat.WeaponType.Pistol,
                IsAvailableOnRack = true,
                IsSlinged = false,
                OwnerClientId = 0,
                CurrentMagAmmo = 12,
                MaxMagAmmo = 12,
                RackReserveAmmo = 48,
                Revision = 1
            });

            netState.Weapons.Add(new NetWeaponStateDto
            {
                WeaponId = 2,
                WeaponType = (byte)ScaryParty.Combat.WeaponType.Pistol,
                IsAvailableOnRack = true,
                IsSlinged = false,
                OwnerClientId = 0,
                CurrentMagAmmo = 12,
                MaxMagAmmo = 12,
                RackReserveAmmo = 48,
                Revision = 1
            });

            netState.Weapons.Add(new NetWeaponStateDto
            {
                WeaponId = 3,
                WeaponType = (byte)ScaryParty.Combat.WeaponType.Shotgun,
                IsAvailableOnRack = true,
                IsSlinged = false,
                OwnerClientId = 0,
                CurrentMagAmmo = 6,
                MaxMagAmmo = 6,
                RackReserveAmmo = 24,
                Revision = 1
            });
        }

        private static ClientRpcParams TargetSender(ulong clientId)
        {
            return new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } };
        }

        private void HandleResetNightCombatRequestedServer()
        {
            HandleResetNightCombatServer(resetWeaponsAndAmmo: false);
        }

        private void HandleResetNightCombatServer(bool resetWeaponsAndAmmo = false)
        {
            if (!IsServer) return;

            var root = PizzeriaRoot.Instance;
            var clientsToRespawn = new List<ulong>();
            foreach (var kvp in _playerCombat)
            {
                bool wasDeadOrDown = kvp.Value.IsDead || kvp.Value.IsIncapacitated;
                kvp.Value.CurrentHp = kvp.Value.MaxHp;
                kvp.Value.IsIncapacitated = false;
                kvp.Value.IsDead = false;
                kvp.Value.RescueRemaining = 0f;
                kvp.Value.RescueProgress = 0f;
                kvp.Value.RescueMode = 0;
                kvp.Value.RescuedByClientId = 0;
                kvp.Value.AssistHeartbeatTime = -999.0;
                kvp.Value.NextHealAllowedTime = -999.0;
                kvp.Value.SpectatingTargetClientId = 0;
                kvp.Value.IsAiming = false;
                kvp.Value.Revision++;
                if (wasDeadOrDown)
                    clientsToRespawn.Add(kvp.Key);
            }
            SyncPlayerCombatListServer();
            _processedMotorcycleImpacts.Clear();
            _lastWeaponFireServerTime.Clear();

            if (root != null)
            {
                for (int i = 0; i < clientsToRespawn.Count; i++)
                {
                    ulong cid = clientsToRespawn[i];
                    TeleportRiderSafelyServer(cid, root.GetPlayerSpawnPosition((int)cid), root.GetPlayerSpawnRotation((int)cid));
                }
            }

            // A10: nunca repor munição gratuitamente entre noites; resetar armas apenas ao iniciar nova RUN
            if (resetWeaponsAndAmmo)
            {
                var netState = PizzeriaNetworkState.Instance;
                if (netState != null && netState.Weapons != null)
                {
                    for (int i = 0; i < netState.Weapons.Count; i++)
                    {
                        var w = netState.Weapons[i];
                        bool isShotgun = w.WeaponType == (byte)ScaryParty.Combat.WeaponType.Shotgun;
                        w.IsAvailableOnRack = true;
                        w.IsSlinged = false;
                        w.OwnerClientId = 0;
                        w.MaxMagAmmo = isShotgun ? 6 : 12;
                        w.CurrentMagAmmo = w.MaxMagAmmo;
                        w.RackReserveAmmo = isShotgun ? 24 : 48;
                        w.Revision++;
                        netState.Weapons[i] = w;
                    }
                }
            }

            ResetMotorcycleAndDismountRiderServer();
        }

        public void RegisterPlayerCombatServer(ulong clientId, int maxHp, bool _)
        {
            if (!IsServer) return;
            if (!_playerCombat.TryGetValue(clientId, out var runtime))
            {
                runtime = new PlayerCombatRuntime { MaxHp = Mathf.Max(1, maxHp), CurrentHp = Mathf.Max(1, maxHp) };
                _playerCombat[clientId] = runtime;
                SyncPlayerCombatListServer();
            }
        }

        private PlayerCombatRuntime GetOrCreateCombatRuntime(ulong clientId)
        {
            if (!_playerCombat.TryGetValue(clientId, out var runtime))
            {
                runtime = new PlayerCombatRuntime();
                _playerCombat[clientId] = runtime;
            }
            return runtime;
        }

        private void TickServerCombat(float dt)
        {
            if (_playerCombat.Count == 0) return;
            var root = PizzeriaRoot.Instance;
            var net = PizzeriaNetworkState.Instance;
            double now = NetworkManager.Singleton != null ? NetworkManager.Singleton.ServerTime.Time : Time.timeAsDouble;
            bool enableAllPlayersDeadDefeat = ResolveAllPlayersDeadDefeatPolicy(root);
            bool changed = false;

            var bleedoutExpired = new List<ulong>();
            foreach (var kvp in _playerCombat)
            {
                ulong targetId = kvp.Key;
                var c = kvp.Value;

                if (c.IsDead)
                {
                    ulong validSpec = ResolveValidSpectatorTargetServer(targetId, c.SpectatingTargetClientId, 0);
                    if (validSpec != c.SpectatingTargetClientId)
                    {
                        c.SpectatingTargetClientId = validSpec;
                        c.Revision++;
                        changed = true;
                    }
                    continue;
                }

                // 1. Sangramento quando nocauteado
                if (c.IsIncapacitated)
                {
                    c.RescueRemaining = Mathf.Max(0f, c.RescueRemaining - dt);
                    changed = true;
                    if (c.RescueRemaining <= 0f)
                    {
                        bleedoutExpired.Add(targetId);
                        continue;
                    }
                }

                // 2. Progresso autoritativo no servidor de cura/reanimação por [E]
                if (c.RescueMode != 0)
                {
                    bool modeValid = (c.RescueMode == 2 && c.IsIncapacitated) ||
                                     (c.RescueMode == 1 && !c.IsIncapacitated && c.CurrentHp > 0 && c.CurrentHp < c.MaxHp);
                    bool helperValid = _playerCombat.TryGetValue(c.RescuedByClientId, out var helper) &&
                                       !helper.IsIncapacitated && !helper.IsDead &&
                                       (net == null || !net.MotorcycleState.Value.IsMounted || net.MotorcycleState.Value.RiderClientId != c.RescuedByClientId);
                    bool heartbeatFresh = (now - c.AssistHeartbeatTime) <= AssistHeartbeatTimeoutSeconds;
                    bool inRange = IsClientsWithinDistanceServer(c.RescuedByClientId, targetId, AssistMaxDistance);

                    if (!modeValid || !helperValid || !heartbeatFresh || !inRange)
                    {
                        c.RescueMode = 0;
                        c.RescueProgress = 0f;
                        c.RescuedByClientId = 0;
                        c.Revision++;
                        changed = true;
                        continue;
                    }

                    c.RescueProgress = Mathf.Clamp01(c.RescueProgress + dt / AssistDurationSeconds);
                    c.Revision++;
                    changed = true;

                    if (c.RescueProgress >= 1.0f)
                    {
                        ulong helperId = c.RescuedByClientId;
                        byte completedMode = c.RescueMode;
                        c.RescueMode = 0;
                        c.RescueProgress = 0f;
                        c.RescuedByClientId = 0;

                        if (completedMode == 2)
                        {
                            c.IsIncapacitated = false;
                            c.CurrentHp = Mathf.Max(35, c.MaxHp / 2);
                            c.RescueRemaining = 0f;
                            NotifySuccessClientRpc($"Reanimado por aliado ({c.CurrentHp} HP)!", TargetSender(targetId));
                            NotifySuccessClientRpc($"Você reanimou o aliado #{targetId}!", TargetSender(helperId));
                        }
                        else
                        {
                            c.CurrentHp = Mathf.Min(c.MaxHp, c.CurrentHp + AllyHealAmount);
                            c.NextHealAllowedTime = now + AllyHealCooldownSeconds;
                            NotifySuccessClientRpc($"Curado por aliado (+{AllyHealAmount} HP -> {c.CurrentHp}/{c.MaxHp})!", TargetSender(targetId));
                            NotifySuccessClientRpc($"Aliado #{targetId} curado ({c.CurrentHp}/{c.MaxHp} HP)!", TargetSender(helperId));
                        }
                        c.Revision++;
                    }
                }
            }

            for (int i = 0; i < bleedoutExpired.Count; i++)
            {
                HandlePlayerTransitionToDeadServer(bleedoutExpired[i], enableAllPlayersDeadDefeat);
                changed = true;
            }

            if (changed)
            {
                SyncPlayerCombatListServer();
            }
        }

        private bool IsClientsWithinDistanceServer(ulong clientA, ulong clientB, float maxDistance)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return false;
            if (!nm.ConnectedClients.TryGetValue(clientA, out var a) || a.PlayerObject == null) return false;
            if (!nm.ConnectedClients.TryGetValue(clientB, out var b) || b.PlayerObject == null) return false;
            return (a.PlayerObject.transform.position - b.PlayerObject.transform.position).sqrMagnitude <= maxDistance * maxDistance;
        }

        private void ReleaseAssistSessionsForClientServer(ulong clientId)
        {
            bool changed = false;
            foreach (var kvp in _playerCombat)
            {
                if ((kvp.Key == clientId || kvp.Value.RescuedByClientId == clientId) &&
                    (kvp.Value.RescueMode != 0 || kvp.Value.RescueProgress > 0f))
                {
                    kvp.Value.RescueMode = 0;
                    kvp.Value.RescueProgress = 0f;
                    kvp.Value.RescuedByClientId = 0;
                    kvp.Value.Revision++;
                    changed = true;
                }
            }
            if (changed) SyncPlayerCombatListServer();
        }

        private void ForceEjectClientFromMotorcycleServer(ulong clientId)
        {
            var net = PizzeriaNetworkState.Instance;
            if (net == null) return;
            var st = net.MotorcycleState.Value;
            if (st.IsMounted && st.RiderClientId == clientId)
            {
                st.IsMounted = false;
                st.RiderClientId = 0;
                st.Speed = 0f;
                st.Revision++;
                net.MotorcycleState.Value = st;
            }
        }

        private void ReturnPlayerWeaponsToRackServer(ulong clientId)
        {
            var net = PizzeriaNetworkState.Instance;
            if (net == null || net.Weapons == null) return;
            for (int i = 0; i < net.Weapons.Count; i++)
            {
                var w = net.Weapons[i];
                if (!w.IsAvailableOnRack && w.OwnerClientId == clientId)
                {
                    w.IsAvailableOnRack = true;
                    w.IsSlinged = false;
                    w.OwnerClientId = 0;
                    w.Revision++;
                    net.Weapons[i] = w;
                }
            }
        }

        private ulong ResolveValidSpectatorTargetServer(ulong deadClientId, ulong currentTarget, int stepDirection)
        {
            var candidates = new List<ulong>();
            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                foreach (var cid in nm.ConnectedClientsIds)
                {
                    if (cid == deadClientId) continue;
                    if (_playerCombat.TryGetValue(cid, out var c) && c.IsDead) continue;
                    candidates.Add(cid);
                }
            }
            if (candidates.Count == 0) return 0;

            int idx = candidates.IndexOf(currentTarget);
            if (idx < 0) return candidates[0];
            if (stepDirection == 0) return candidates[idx];
            int nextIdx = ((idx + stepDirection) % candidates.Count + candidates.Count) % candidates.Count;
            return candidates[nextIdx];
        }

        private void HandlePlayerTransitionToDeadServer(ulong clientId, bool enableAllPlayersDeadDefeat)
        {
            if (!_playerCombat.TryGetValue(clientId, out var c) || c.IsDead) return;

            c.IsIncapacitated = false;
            c.IsDead = true;
            c.CurrentHp = 0;
            c.RescueRemaining = 0f;
            c.RescueProgress = 0f;
            c.RescueMode = 0;
            c.RescuedByClientId = 0;
            c.SpectatingTargetClientId = ResolveValidSpectatorTargetServer(clientId, 0, 0);
            c.IsAiming = false;
            c.Revision++;

            ReleaseAssistSessionsForClientServer(clientId);
            ForceEjectClientFromMotorcycleServer(clientId);
            ReturnPlayerWeaponsToRackServer(clientId);
            PlayerLifecycleBridge.CleanupPlayerWorldAndDomainStateServer(clientId);

            string specLabel = c.SpectatingTargetClientId != 0
                ? $" Espectando Aliado #{c.SpectatingTargetClientId}."
                : string.Empty;
            NotifyErrorClientRpc(
                $"Você morreu! Aguarde a equipe vencer a noite em modo espectador.{specLabel}",
                TargetSender(clientId));

            var root = PizzeriaRoot.Instance;
            if (enableAllPlayersDeadDefeat && root != null && root.DomainState != null && AreAllPlayersIncapacitatedServer())
            {
                NightCycleService.ForceOperationalDefeat(root.DomainState, NightDefeatReason.AllPlayersIncapacitated);
                CommitAndReplicate();
            }
        }

        private static bool ResolveAllPlayersDeadDefeatPolicy(PizzeriaRoot root)
        {
            if (root != null && root.Config != null)
            {
                if (!root.Config.enableAllPlayersDeadDefeat) return false;
                if (root.Config.nightConfig != null) return root.Config.nightConfig.enableAllPlayersDeadDefeat;
                return true;
            }
            return true;
        }

        [ServerRpc(RequireOwnership = false)]
        public void BeginAssistAllyServerRpc(ulong targetClientId, ServerRpcParams rpcParams = default)
        {
            TryBeginOrHeartbeatAssistServer(rpcParams.Receive.SenderClientId, targetClientId, isBegin: true);
        }

        [ServerRpc(RequireOwnership = false)]
        public void HeartbeatAssistAllyServerRpc(ulong targetClientId, ServerRpcParams rpcParams = default)
        {
            TryBeginOrHeartbeatAssistServer(rpcParams.Receive.SenderClientId, targetClientId, isBegin: false);
        }

        [ServerRpc(RequireOwnership = false)]
        public void CancelAssistAllyServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong helperId = rpcParams.Receive.SenderClientId;
            if (!IsServer) return;
            bool changed = false;
            foreach (var kvp in _playerCombat)
            {
                if (kvp.Value.RescuedByClientId == helperId && kvp.Value.RescueMode != 0)
                {
                    kvp.Value.RescueMode = 0;
                    kvp.Value.RescueProgress = 0f;
                    kvp.Value.RescuedByClientId = 0;
                    kvp.Value.Revision++;
                    changed = true;
                }
            }
            if (changed) SyncPlayerCombatListServer();
        }

        [ServerRpc(RequireOwnership = false)]
        public void AdvanceAllyRescueServerRpc(ulong targetClientId, float dt, ServerRpcParams rpcParams = default)
        {
            // Mantido por compatibilidade, sem confiar em dt do cliente para evitar acelerar timer por spam.
            TryBeginOrHeartbeatAssistServer(rpcParams.Receive.SenderClientId, targetClientId, isBegin: false);
        }

        private void TryBeginOrHeartbeatAssistServer(ulong helperId, ulong targetClientId, bool isBegin)
        {
            if (!IsServer || helperId == targetClientId) return;
            if (RejectIfCannotActServer(helperId)) return;

            var net = PizzeriaNetworkState.Instance;
            if (net != null && net.MotorcycleState.Value.IsMounted && net.MotorcycleState.Value.RiderClientId == helperId)
            {
                if (isBegin) NotifyErrorClientRpc("Desça da moto para ajudar um aliado.", TargetSender(helperId));
                return;
            }

            if (!_playerCombat.TryGetValue(targetClientId, out var target) || target.IsDead) return;
            if (!IsClientsWithinDistanceServer(helperId, targetClientId, AssistMaxDistance)) return;

            double now = NetworkManager.Singleton != null ? NetworkManager.Singleton.ServerTime.Time : Time.timeAsDouble;
            byte desiredMode = 0;
            if (target.IsIncapacitated)
            {
                desiredMode = 2;
            }
            else if (target.CurrentHp > 0 && target.CurrentHp < target.MaxHp)
            {
                if (now < target.NextHealAllowedTime)
                {
                    if (isBegin)
                    {
                        int wait = Mathf.CeilToInt((float)(target.NextHealAllowedTime - now));
                        NotifyErrorClientRpc($"Aguarde {wait}s antes de curar este aliado novamente.", TargetSender(helperId));
                    }
                    return;
                }
                desiredMode = 1;
            }

            if (desiredMode == 0) return;

            if (target.RescueMode != desiredMode || target.RescuedByClientId != helperId)
            {
                if (target.RescueMode != 0 && target.RescuedByClientId != helperId && (now - target.AssistHeartbeatTime) <= AssistHeartbeatTimeoutSeconds)
                    return; // Já está sendo ajudado por outro aliado neste instante
                target.RescueMode = desiredMode;
                target.RescueProgress = 0f;
                target.RescuedByClientId = helperId;
            }

            target.AssistHeartbeatTime = now;
            target.Revision++;
            SyncPlayerCombatListServer();
        }

        [ServerRpc(RequireOwnership = false)]
        public void CycleSpectatorTargetServerRpc(int direction, ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            if (!IsServer) return;
            if (!_playerCombat.TryGetValue(senderId, out var c) || !c.IsDead) return;

            int step = direction >= 0 ? 1 : -1;
            ulong nextTarget = ResolveValidSpectatorTargetServer(senderId, c.SpectatingTargetClientId, step);
            if (nextTarget != c.SpectatingTargetClientId)
            {
                c.SpectatingTargetClientId = nextTarget;
                c.Revision++;
                SyncPlayerCombatListServer();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void ReportCustomerAttackInPizzeriaServerRpc(ulong customerId, bool insidePizzeriaVolume, ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (!IsServer || root == null || root.DomainState == null) return;

            Vector3 victimPos = Vector3.zero;
            bool foundVictim = false;
            var views = FindObjectsByType<ScaryParty.Pizzeria.Presentation.CustomerNpcView>(FindObjectsSortMode.None);
            for (int i = 0; i < views.Length; i++)
            {
                if (views[i] != null && views[i].CustomerId == customerId)
                {
                    victimPos = views[i].transform.position;
                    foundVictim = true;
                    break;
                }
            }

            if (!foundVictim && NetworkManager.Singleton != null &&
                NetworkManager.Singleton.ConnectedClients.TryGetValue(senderId, out var senderClient) &&
                senderClient.PlayerObject != null)
            {
                victimPos = senderClient.PlayerObject.transform.position;
                foundVictim = true;
            }

            if (!foundVictim || !root.IsWorldPositionInsidePizzeria(victimPos))
                return;

            double now = NetworkManager.Singleton != null ? NetworkManager.Singleton.ServerTime.Time : Time.timeAsDouble;
            if (_customerAttackRepCooldown.TryGetValue(customerId, out double nextAllowed) && now < nextAllowed) return;
            _customerAttackRepCooldown[customerId] = now + 3.0;
            int penalty = root.Config != null ? root.Config.repPenaltyExpiredOrAttack : -15;
            NightCycleService.AdjustReputation(root.DomainState, penalty, "Cliente atacado dentro da pizzaria");
            CommitAndReplicate();
        }

        private void SyncPlayerCombatListServer()
        {
            var netState = PizzeriaNetworkState.Instance;
            if (netState == null || netState.PlayerCombatStates == null) return;

            foreach (var kvp in _playerCombat)
            {
                var r = kvp.Value;
                var dto = new NetPlayerCombatDto
                {
                    ClientId = kvp.Key,
                    CurrentHp = r.CurrentHp,
                    MaxHp = r.MaxHp,
                    IsIncapacitated = r.IsIncapacitated,
                    IsDead = r.IsDead,
                    RescueRemainingSeconds = r.RescueRemaining,
                    RescueProgress = r.RescueProgress,
                    RescuedByClientId = r.RescuedByClientId,
                    RescueMode = r.RescueMode,
                    SpectatingTargetClientId = r.SpectatingTargetClientId,
                    IsAiming = r.IsAiming,
                    Revision = r.Revision
                };

                int existingIdx = -1;
                for (int i = 0; i < netState.PlayerCombatStates.Count; i++)
                {
                    if (netState.PlayerCombatStates[i].ClientId == kvp.Key) { existingIdx = i; break; }
                }

                if (existingIdx >= 0)
                {
                    if (!netState.PlayerCombatStates[existingIdx].Equals(dto))
                        netState.PlayerCombatStates[existingIdx] = dto;
                }
                else
                {
                    netState.PlayerCombatStates.Add(dto);
                }
            }
        }

        public void ApplyDamageToPlayerServer(ulong targetClientId, ScaryParty.Combat.DamageEvent evt, bool enableAllPlayersDeadDefeat = false)
        {
            if (!IsServer || evt.Amount <= 0) return;

            var combat = GetOrCreateCombatRuntime(targetClientId);
            if (combat.IsDead || combat.IsIncapacitated) return;

            combat.CurrentHp = Mathf.Max(0, combat.CurrentHp - evt.Amount);
            combat.Revision++;

            var targetParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { targetClientId } } };
            var root = PizzeriaRoot.Instance;

            if (root != null && root.DomainState != null)
            {
                BackpackState equippedBag = null;
                foreach (var bp in root.DomainState.Backpacks.Values)
                {
                    if (bp.EquippedBy == targetClientId)
                    {
                        equippedBag = bp;
                        break;
                    }
                }

                if (equippedBag != null)
                {
                    int bagWear = Mathf.CeilToInt(evt.Amount * 0.5f);
                    Vector3 dropPos = Vector3.zero;
                    if (NetworkManager.Singleton != null &&
                        NetworkManager.Singleton.ConnectedClients.TryGetValue(targetClientId, out var client) &&
                        client.PlayerObject != null)
                    {
                        Transform pTf = client.PlayerObject.transform;
                        Vector3 candidate = pTf.position - pTf.forward * 0.75f + pTf.right * 0.25f;
                        if (Physics.Raycast(candidate + Vector3.up * 1.2f, Vector3.down, out var groundHit, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                            dropPos = groundHit.point + Vector3.up * 0.04f;
                        else
                            dropPos = pTf.position + Vector3.up * 0.04f;
                    }

                    bool droppedBox = BackpackService.ApplyBackpackDamage(
                        root.DomainState,
                        equippedBag.Id,
                        bagWear,
                        new DomainVector3(dropPos.x, dropPos.y, dropPos.z),
                        out var droppedBoxId);

                    CommitAndReplicate();

                    if (droppedBox)
                    {
                        NotifyErrorClientRpc(
                            $"Mochila rasgada! Caixa #{droppedBoxId.Value} caiu no chão. Pegue com [E] e segure [F] (3s) para reparar.",
                            targetParams);
                    }
                    else if (equippedBag.IsTorn)
                    {
                        NotifyErrorClientRpc("Mochila rasgada (0%)! Segure [F] (3s) para reparar antes de guardar caixas.", targetParams);
                    }
                    else
                    {
                        int pct = equippedBag.MaxIntegrity > 0 ? Mathf.RoundToInt(100f * equippedBag.Integrity / equippedBag.MaxIntegrity) : 0;
                        NotifyErrorClientRpc($"Você sofreu {evt.Amount} de dano! Integridade da mochila: {pct}%.", targetParams);
                    }
                }
                else
                {
                    NotifyErrorClientRpc($"Você sofreu {evt.Amount} de dano! (HP: {combat.CurrentHp}/{combat.MaxHp})", targetParams);
                }
            }

            // Interromper qualquer cura/resgate em andamento se o alvo ou o ajudante sofrer dano
            ReleaseAssistSessionsForClientServer(targetClientId);

            if (combat.CurrentHp <= 0)
            {
                combat.IsIncapacitated = true;
                combat.RescueRemaining = 45f;
                combat.RescueProgress = 0f;
                combat.RescueMode = 0;
                combat.RescuedByClientId = 0;
                combat.Revision++;
                ForceEjectClientFromMotorcycleServer(targetClientId);
                BroadcastNoticeClientRpc("Jogador nocauteado! Um aliado precisa segurar [E] por 3s para reanimá-lo.", isError: true);

                if ((enableAllPlayersDeadDefeat || ResolveAllPlayersDeadDefeatPolicy(root)) && root != null && root.DomainState != null && AreAllPlayersIncapacitatedServer())
                {
                    NightCycleService.ForceOperationalDefeat(root.DomainState, NightDefeatReason.AllPlayersIncapacitated);
                    CommitAndReplicate();
                }
            }

            SyncPlayerCombatListServer();
        }

        private bool AreAllPlayersIncapacitatedServer()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.ConnectedClientsIds.Count == 0) return false;
            foreach (var id in nm.ConnectedClientsIds)
            {
                if (!_playerCombat.TryGetValue(id, out var r) || (!r.IsIncapacitated && !r.IsDead))
                    return false;
            }
            return true;
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetWeaponAimServerRpc(bool aiming, ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            if (!IsServer) return;
            var c = GetOrCreateCombatRuntime(senderId);
            bool effective = aiming && !c.IsDead && !c.IsIncapacitated;
            if (c.IsAiming != effective)
            {
                c.IsAiming = effective;
                c.Revision++;
                SyncPlayerCombatListServer();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void InteractWeaponRackServerRpc(byte requestedWeaponType, ServerRpcParams rpcParams = default)
        {
            HandleWeaponRackInteractionServer(rpcParams.Receive.SenderClientId, requestedWeaponType, 0);
        }

        [ServerRpc(RequireOwnership = false)]
        public void InteractWeaponRackByIdServerRpc(byte requestedWeaponType, uint requestedWeaponId, ServerRpcParams rpcParams = default)
        {
            HandleWeaponRackInteractionServer(rpcParams.Receive.SenderClientId, requestedWeaponType, requestedWeaponId);
        }

        private void HandleWeaponRackInteractionServer(ulong senderId, byte requestedWeaponType, uint requestedWeaponId)
        {
            var targetParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { senderId } } };
            if (!IsServer) return;

            var rack = ScaryParty.Pizzeria.Stations.WeaponRackStation.Instance;
            if (rack != null && !InteractionValidator.ValidateDistanceAndSight(senderId, rack.transform.position, 4.5f, 2.0f, false))
            {
                NotifyErrorClientRpc("Aproxime-se do rack de armas.", targetParams);
                return;
            }

            if (RejectIfCannotActServer(senderId, requireOpenShift: false)) return;

            EnsureDefaultWeaponsInitializedServer();
            var netState = PizzeriaNetworkState.Instance;
            var root = PizzeriaRoot.Instance;
            if (netState == null || netState.Weapons == null || root == null || root.DomainState == null) return;

            // Sempre devolve a arma atualmente equipada pelo solicitante antes de equipar outra ou guardar.
            bool returnedAny = false;
            for (int i = 0; i < netState.Weapons.Count; i++)
            {
                var w = netState.Weapons[i];
                if (!w.IsAvailableOnRack && w.OwnerClientId == senderId)
                {
                    w.IsAvailableOnRack = true;
                    w.IsSlinged = false;
                    w.OwnerClientId = 0;
                    w.Revision++;
                    netState.Weapons[i] = w;
                    returnedAny = true;
                }
            }

            if (requestedWeaponType == (byte)ScaryParty.Combat.WeaponType.Unarmed)
            {
                if (returnedAny) NotifySuccessClientRpc("Arma devolvida ao rack.", targetParams);
                return;
            }

            int targetIdx = -1;
            int totalOfType = 0;
            for (int i = 0; i < netState.Weapons.Count; i++)
            {
                var w = netState.Weapons[i];
                if (w.WeaponType != requestedWeaponType) continue;
                totalOfType++;
                if (requestedWeaponId > 0 && w.WeaponId == requestedWeaponId)
                {
                    targetIdx = i;
                    break;
                }
                if (w.IsAvailableOnRack && targetIdx < 0)
                {
                    targetIdx = i;
                }
            }

            if (totalOfType == 0)
            {
                NotifyErrorClientRpc("Arma não encontrada no rack.", targetParams);
                return;
            }

            if (targetIdx < 0 || !netState.Weapons[targetIdx].IsAvailableOnRack)
            {
                NotifyErrorClientRpc("Todas as instâncias desta arma já estão sendo usadas.", targetParams);
                return;
            }

            var targetWeapon = netState.Weapons[targetIdx];

            if (requestedWeaponType == (byte)ScaryParty.Combat.WeaponType.Shotgun)
            {
                var moto = netState.MotorcycleState.Value;
                if (moto.IsActive && moto.IsMounted && moto.RiderClientId == senderId)
                {
                    NotifyErrorClientRpc("Desça da moto antes de empunhar a Escopeta (2 mãos)!", targetParams);
                    return;
                }
                if (IsDomainHandOccupiedByItemOrToolServer(root.DomainState, senderId, HandSlotIndex.Left) ||
                    IsDomainHandOccupiedByItemOrToolServer(root.DomainState, senderId, HandSlotIndex.Right))
                {
                    NotifyErrorClientRpc("A Escopeta exige as DUAS mãos vazias!", targetParams);
                    return;
                }
            }
            else if (requestedWeaponType == (byte)ScaryParty.Combat.WeaponType.Pistol)
            {
                if (IsDomainHandOccupiedByItemOrToolServer(root.DomainState, senderId, HandSlotIndex.Right))
                {
                    NotifyErrorClientRpc("Esvazie a mão direita [2] para equipar a Pistola.", targetParams);
                    return;
                }
            }

            targetWeapon.IsAvailableOnRack = false;
            targetWeapon.IsSlinged = false;
            targetWeapon.OwnerClientId = senderId;
            targetWeapon.Revision++;
            netState.Weapons[targetIdx] = targetWeapon;

            string msg = requestedWeaponType == (byte)ScaryParty.Combat.WeaponType.Shotgun
                ? "Escopeta equipada (2 mãos)."
                : "Pistola equipada (1 mão — mão esquerda [1] livre).";
            NotifySuccessClientRpc(msg, targetParams);
        }

        [ServerRpc(RequireOwnership = false)]
        public void ToggleSlingShotgunServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            if (!IsServer) return;
            if (RejectIfCannotActServer(senderId, requireOpenShift: false)) return;
            var targetParams = TargetSender(senderId);
            var root = PizzeriaRoot.Instance;
            var netState = PizzeriaNetworkState.Instance;
            if (netState == null || netState.Weapons == null || root == null || root.DomainState == null) return;

            int shotgunIdx = -1;
            for (int i = 0; i < netState.Weapons.Count; i++)
            {
                var w = netState.Weapons[i];
                if (!w.IsAvailableOnRack && w.OwnerClientId == senderId && w.WeaponType == (byte)ScaryParty.Combat.WeaponType.Shotgun)
                {
                    shotgunIdx = i;
                    break;
                }
            }

            if (shotgunIdx < 0)
            {
                NotifyErrorClientRpc("Você não possui uma Escopeta para guardar/sacar da bandoleira.", targetParams);
                return;
            }

            var shotgun = netState.Weapons[shotgunIdx];
            if (!shotgun.IsSlinged)
            {
                shotgun.IsSlinged = true;
                shotgun.Revision++;
                netState.Weapons[shotgunIdx] = shotgun;
                NotifySuccessClientRpc("Escopeta guardada na bandoleira (mãos livres).", targetParams);
                return;
            }

            // Sacar da bandoleira é transação atômica: exige não estar pilotando e ambas as mãos livres
            var moto = netState.MotorcycleState.Value;
            if (moto.IsActive && moto.IsMounted && moto.RiderClientId == senderId)
            {
                NotifyErrorClientRpc("Não é possível sacar a Escopeta (2 mãos) enquanto pilota a moto!", targetParams);
                return;
            }

            if (IsDomainHandOccupiedByItemOrToolServer(root.DomainState, senderId, HandSlotIndex.Left) ||
                IsDomainHandOccupiedByItemOrToolServer(root.DomainState, senderId, HandSlotIndex.Right) ||
                netState.TryGetEquippedWeapon(senderId, out _))
            {
                NotifyErrorClientRpc("Esvazie as DUAS mãos antes de sacar a Escopeta da bandoleira!", targetParams);
                return;
            }

            shotgun.IsSlinged = false;
            shotgun.Revision++;
            netState.Weapons[shotgunIdx] = shotgun;
            NotifySuccessClientRpc("Escopeta sacada da bandoleira (2 mãos).", targetParams);
        }

        [ServerRpc(RequireOwnership = false)]
        public void FireWeaponServerRpc(Vector3 rayOrigin, Vector3 rayDir, ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            if (!IsServer || rayDir.sqrMagnitude < 0.001f) return;
            if (!CanPlayerPerformGameplayActionServer(senderId, out _)) return;

            var netState = PizzeriaNetworkState.Instance;
            if (netState == null || netState.Weapons == null) return;

            int weaponIdx = -1;
            for (int i = 0; i < netState.Weapons.Count; i++)
            {
                if (!netState.Weapons[i].IsAvailableOnRack && !netState.Weapons[i].IsSlinged && netState.Weapons[i].OwnerClientId == senderId)
                {
                    weaponIdx = i;
                    break;
                }
            }
            if (weaponIdx < 0) return;

            var weapon = netState.Weapons[weaponIdx];
            if (weapon.CurrentMagAmmo <= 0) return;

            double now = NetworkManager.Singleton != null ? NetworkManager.Singleton.ServerTime.Time : Time.timeAsDouble;
            float minCooldown = weapon.WeaponType == (byte)ScaryParty.Combat.WeaponType.Shotgun ? 0.72f : 0.25f;
            uint weaponKey = weapon.WeaponId > 0 ? weapon.WeaponId : (uint)(weaponIdx + 1);
            if (_lastWeaponFireServerTime.TryGetValue(weaponKey, out double lastTime) && now - lastTime < minCooldown)
                return;

            _lastWeaponFireServerTime[weaponKey] = now;
            weapon.CurrentMagAmmo--;
            weapon.Revision++;
            netState.Weapons[weaponIdx] = weapon;

            Transform shooterRoot = null;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClients.TryGetValue(senderId, out var client) && client.PlayerObject != null)
            {
                shooterRoot = client.PlayerObject.transform;
            }

            Vector3 dir = rayDir.normalized;
            if (shooterRoot != null)
            {
                Vector3 chestOrigin = shooterRoot.position + Vector3.up * 1.45f;
                if (Physics.Linecast(chestOrigin, rayOrigin, out RaycastHit muzzleBlock, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    if (!muzzleBlock.collider.transform.IsChildOf(shooterRoot))
                    {
                        PlayWeaponFireEffectsClientRpc(senderId, weapon.WeaponType, rayOrigin, muzzleBlock.point, true);
                        return;
                    }
                }
            }
            float range = weapon.WeaponType == (byte)ScaryParty.Combat.WeaponType.Shotgun ? 38f : 65f;
            int rayCount = weapon.WeaponType == (byte)ScaryParty.Combat.WeaponType.Shotgun ? ShotgunPelletOffsetsDeg.Length : 1;
            int perRayDmg = weapon.WeaponType == (byte)ScaryParty.Combat.WeaponType.Shotgun ? 10 : 25;

            var accumulatedDamage = new Dictionary<ScaryParty.Combat.IDamageable, int>();
            var firstHitPoints = new Dictionary<ScaryParty.Combat.IDamageable, (Vector3 pt, Vector3 n)>();
            Vector3 fxEndPoint = rayOrigin + dir * Mathf.Min(range, 25f);
            bool anyFxHit = false;

            for (int r = 0; r < rayCount; r++)
            {
                Vector3 pelletDir = dir;
                if (weapon.WeaponType == (byte)ScaryParty.Combat.WeaponType.Shotgun)
                {
                    Vector2 off = ShotgunPelletOffsetsDeg[r];
                    Quaternion rot = Quaternion.LookRotation(dir);
                    pelletDir = (rot * Quaternion.Euler(off.y, off.x, 0f)) * Vector3.forward;
                }

                RaycastHit[] hits = Physics.RaycastAll(rayOrigin, pelletDir, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                for (int h = 0; h < hits.Length; h++)
                {
                    var hit = hits[h];
                    if (hit.collider == null) continue;
                    if (shooterRoot != null && hit.collider.transform.IsChildOf(shooterRoot)) continue;

                    var dmg = hit.collider.GetComponentInParent<ScaryParty.Combat.IDamageable>();
                    if (dmg != null && !(dmg is ScaryParty.Combat.PlayerHealth))
                    {
                        accumulatedDamage.TryGetValue(dmg, out int cur);
                        accumulatedDamage[dmg] = cur + perRayDmg;
                        if (!firstHitPoints.ContainsKey(dmg))
                            firstHitPoints[dmg] = (hit.point, hit.normal);
                        if (!anyFxHit)
                        {
                            anyFxHit = true;
                            fxEndPoint = hit.point;
                        }
                        break;
                    }

                    if (!hit.collider.isTrigger)
                    {
                        if (!anyFxHit)
                        {
                            anyFxHit = true;
                            fxEndPoint = hit.point;
                        }
                        break;
                    }
                }
            }

            foreach (var kvp in accumulatedDamage)
            {
                if (!kvp.Key.IsAlive) continue;
                var info = firstHitPoints[kvp.Key];
                kvp.Key.ApplyDamageServer(new ScaryParty.Combat.DamageEvent
                {
                    Amount = kvp.Value,
                    Source = ScaryParty.Combat.DamageSourceType.Firearm,
                    InstigatorClientId = senderId,
                    HitPoint = info.pt,
                    HitNormal = info.n
                });
            }

            PlayWeaponFireEffectsClientRpc(senderId, weapon.WeaponType, rayOrigin, fxEndPoint, anyFxHit);
        }

        [ServerRpc(RequireOwnership = false)]
        public void ReloadWeaponServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            if (!IsServer) return;
            if (!CanPlayerPerformGameplayActionServer(senderId, out _)) return;

            var netState = PizzeriaNetworkState.Instance;
            if (netState == null || netState.Weapons == null) return;

            for (int i = 0; i < netState.Weapons.Count; i++)
            {
                var w = netState.Weapons[i];
                if (w.IsAvailableOnRack || w.OwnerClientId != senderId) continue;

                // Se estiver próximo ao rack, reabastece reserva antes de recarregar o pente
                var rack = ScaryParty.Pizzeria.Stations.WeaponRackStation.Instance;
                if (rack != null && InteractionValidator.ValidateDistanceAndSight(senderId, rack.transform.position, 4.5f, 2.0f, false))
                {
                    int fullReserve = w.WeaponType == (byte)ScaryParty.Combat.WeaponType.Shotgun ? 24 : 48;
                    if (w.RackReserveAmmo < fullReserve) w.RackReserveAmmo = fullReserve;
                }

                int needed = w.MaxMagAmmo - w.CurrentMagAmmo;
                if (needed <= 0 || w.RackReserveAmmo <= 0) return;

                int take = Mathf.Min(needed, w.RackReserveAmmo);
                w.CurrentMagAmmo += take;
                w.RackReserveAmmo -= take;
                w.Revision++;
                netState.Weapons[i] = w;
                return;
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestTestPlayerDamageServerRpc(int amount, ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            if (!IsServer) return;

            var dummy = FindFirstObjectByType<ScaryParty.Combat.PracticeTargetDummy>();
            if (dummy != null && !InteractionValidator.ValidateDistanceAndSight(senderId, dummy.transform.position, 5.5f, 2.0f, false))
                return;

            ApplyDamageToPlayerServer(senderId, new ScaryParty.Combat.DamageEvent
            {
                Amount = Mathf.Clamp(amount, 1, 100),
                Source = ScaryParty.Combat.DamageSourceType.EnemyTest,
                InstigatorClientId = senderId,
                HitPoint = dummy != null ? dummy.transform.position : Vector3.zero,
                HitNormal = Vector3.up
            });
        }

        public void NotifyDummyHitServer(ulong instigatorClientId, string msg, int curHp, int maxHp)
        {
            if (!IsServer) return;
            SyncDummyVisualClientRpc(curHp);
            var targetParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { instigatorClientId } } };
            NotifySuccessClientRpc(msg, targetParams);
        }

        [ClientRpc]
        private void PlayWeaponFireEffectsClientRpc(ulong shooterClientId, byte weaponType, Vector3 origin, Vector3 hitPoint, bool didHit)
        {
            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.ConnectedClients.TryGetValue(shooterClientId, out var client) &&
                client.PlayerObject != null)
            {
                var wc = client.PlayerObject.GetComponent<WeaponController>();
                wc?.PlayRemoteOrLocalFireVisual(origin, hitPoint, didHit);
            }
        }

        [ClientRpc]
        private void SyncDummyVisualClientRpc(int serverCurrentHp)
        {
            var dummy = FindFirstObjectByType<ScaryParty.Combat.PracticeTargetDummy>();
            dummy?.ApplyLocalVisualPulse(serverCurrentHp);
        }

        #region Moto de Entregas (MOTO + O13)

        private double _lastHornServerTime = -10.0;

        private bool HandleRecoverMotorcycleServer() => ResetMotorcycleAndDismountRiderServer();

        private bool ResetMotorcycleAndDismountRiderServer()
        {
            if (!IsServer) return false;
            var bike = ScaryParty.City.DeliveryMotorcycleController.Instance;
            if (bike == null) return false;

            var net = PizzeriaNetworkState.Instance;
            if (net != null && net.MotorcycleState.Value.IsMounted)
            {
                ulong prevRider = net.MotorcycleState.Value.RiderClientId;
                Vector3 sideExit = ResolveMotorcycleExitPosition(bike);
                TeleportRiderSafelyServer(prevRider, sideExit, bike.ParkingSpawnRot);
            }

            bike.ResetServerPoseToParking();
            return true;
        }

        private static Vector3 ResolveMotorcycleExitPosition(ScaryParty.City.DeliveryMotorcycleController bike)
        {
            var candidates = new List<Vector3>(5);
            if (bike.LeftExitAnchor != null) candidates.Add(bike.LeftExitAnchor.position);
            if (bike.RightExitAnchor != null) candidates.Add(bike.RightExitAnchor.position);
            candidates.Add(bike.transform.position - bike.transform.right * 1.4f);
            candidates.Add(bike.transform.position + bike.transform.right * 1.4f);
            candidates.Add(bike.transform.position - bike.transform.forward * 1.8f);

            for (int i = 0; i < candidates.Count; i++)
            {
                Vector3 c = candidates[i];
                if (Physics.Raycast(c + Vector3.up * 2f, Vector3.down, out var hit, 5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    c.y = hit.point.y + 0.05f;
                else
                    c.y = bike.transform.position.y;

                bool blocked = Physics.CheckCapsule(
                    c + Vector3.up * 0.42f,
                    c + Vector3.up * 1.55f,
                    0.30f,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
                if (!blocked)
                    return c;
            }

            return bike.transform.position + Vector3.up * 0.8f;
        }

        [ServerRpc(RequireOwnership = false)]
        public void ToggleMotorcycleMountServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            var targetParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { senderId } } };
            var root = PizzeriaRoot.Instance;
            var net = PizzeriaNetworkState.Instance;
            var bike = ScaryParty.City.DeliveryMotorcycleController.Instance;
            if (!IsServer || root == null || root.DomainState == null || net == null || bike == null) return;
            if (RejectIfCannotActServer(senderId, requireOpenShift: false)) return;

            var st = net.MotorcycleState.Value;
            if (!st.IsActive)
            {
                bike.ResetServerPoseToParking();
                st = net.MotorcycleState.Value;
            }

            if (st.IsMounted)
            {
                if (st.RiderClientId != senderId)
                {
                    NotifyErrorClientRpc("A moto já está sendo pilotada por outro entregador.", targetParams);
                    return;
                }

                Vector3 exitPos = ResolveMotorcycleExitPosition(bike);
                Quaternion exitRot = Quaternion.LookRotation(Vector3.ProjectOnPlane(bike.transform.forward, Vector3.up).sqrMagnitude > 0.01f
                    ? Vector3.ProjectOnPlane(bike.transform.forward, Vector3.up).normalized
                    : Vector3.forward, Vector3.up);

                st.IsMounted = false;
                st.RiderClientId = 0;
                st.Speed = 0f;
                st.Revision++;
                net.MotorcycleState.Value = st;

                TeleportRiderSafelyServer(senderId, exitPos, exitRot);
                return;
            }

            if (_playerCombat.TryGetValue(senderId, out var c) && c.IsIncapacitated)
            {
                NotifyErrorClientRpc("Você está incapacitado e não pode pilotar a moto!", targetParams);
                return;
            }

            if (!InteractionValidator.ValidateDistanceAndSight(senderId, bike.transform.position, 4.5f, 3.0f, false))
            {
                NotifyErrorClientRpc("Aproxime-se da moto para montar.", targetParams);
                return;
            }

            if (IsDomainHandOccupiedByItemOrToolServer(root.DomainState, senderId, HandSlotIndex.Left))
            {
                NotifyErrorClientRpc("Esvazie a mão esquerda [1] para conduzir a moto (pistola na direita permitida).", targetParams);
                return;
            }

            if (net.TryGetEquippedWeapon(senderId, out var eqWeapon) && eqWeapon.WeaponType == (byte)ScaryParty.Combat.WeaponType.Shotgun && net.Weapons != null)
            {
                for (int i = 0; i < net.Weapons.Count; i++)
                {
                    var w = net.Weapons[i];
                    if (w.WeaponId == eqWeapon.WeaponId && w.OwnerClientId == senderId)
                    {
                        w.IsSlinged = true;
                        w.Revision++;
                        net.Weapons[i] = w;
                        NotifySuccessClientRpc("Escopeta movida para a bandoleira ao montar na moto.", targetParams);
                        break;
                    }
                }
            }

            st.IsActive = true;
            st.IsMounted = true;
            st.RiderClientId = senderId;
            st.Speed = 0f;
            st.Revision++;
            net.MotorcycleState.Value = st;
        }

        [ServerRpc(RequireOwnership = false)]
        public void ReportMotorcycleImpactServerRpc(ulong targetNetworkObjectId, uint impactEpisodeId, Vector3 impactPoint, ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            if (!IsServer) return;

            var net = PizzeriaNetworkState.Instance;
            var bike = ScaryParty.City.DeliveryMotorcycleController.Instance;
            if (net == null || bike == null) return;

            var st = net.MotorcycleState.Value;
            if (!st.IsActive || !st.IsMounted || st.RiderClientId != senderId) return;
            if (Mathf.Abs(st.Speed) < 3.5f) return;

            ulong dedupKey = ((ulong)st.Revision << 32) | impactEpisodeId;
            if (!_processedMotorcycleImpacts.Add(dedupKey)) return;

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out var targetObj) && targetObj != null)
            {
                Vector3 bikeCenter = bike.transform.position + Vector3.up * 0.7f;
                Vector3 targetCenter = targetObj.transform.position + Vector3.up * 0.9f;
                if ((targetCenter - bikeCenter).sqrMagnitude > 5.0f * 5.0f) return;

                if (Physics.Linecast(bikeCenter, targetCenter, out var losHit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    if (!losHit.collider.transform.IsChildOf(bike.transform) && !losHit.collider.transform.IsChildOf(targetObj.transform))
                        return;
                }

                var dmgTarget = targetObj.GetComponentInParent<ScaryParty.Combat.IDamageable>();
                if (dmgTarget != null && dmgTarget.IsAlive)
                {
                    dmgTarget.ApplyDamageServer(new ScaryParty.Combat.DamageEvent
                    {
                        Amount = Mathf.Max(1, motorcycleHitDamage),
                        Source = ScaryParty.Combat.DamageSourceType.Collision,
                        InstigatorClientId = senderId,
                        HitPoint = impactPoint,
                        HitNormal = (targetObj.transform.position - bike.transform.position).normalized
                    });
                }
            }

            // Derruba imediatamente o condutor no servidor (dano ao condutor configurável, padrão 0)
            st.IsMounted = false;
            st.RiderClientId = 0;
            st.Speed = 0f;
            st.Revision++;
            net.MotorcycleState.Value = st;

            if (motorcycleRiderFallDamage > 0)
            {
                ApplyDamageToPlayerServer(senderId, new ScaryParty.Combat.DamageEvent
                {
                    Amount = motorcycleRiderFallDamage,
                    Source = ScaryParty.Combat.DamageSourceType.Collision,
                    InstigatorClientId = senderId,
                    HitPoint = bike.transform.position,
                    HitNormal = Vector3.up
                }, ResolveAllPlayersDeadDefeatPolicy(PizzeriaRoot.Instance));
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClients.TryGetValue(senderId, out var client) && client.PlayerObject != null)
            {
                Vector3 safeExit = bike.transform.position + bike.transform.right * 1.4f;
                client.PlayerObject.transform.position = safeExit;
            }
        }

        [ServerRpc(RequireOwnership = false, Delivery = RpcDelivery.Unreliable)]
        public void UpdateMotorcyclePoseServerRpc(Vector3 clientPos, Quaternion clientRot, float speed, ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            if (!IsServer) return;
            var net = PizzeriaNetworkState.Instance;
            var bike = ScaryParty.City.DeliveryMotorcycleController.Instance;
            if (net == null || bike == null) return;

            var st = net.MotorcycleState.Value;
            if (!st.IsActive || !st.IsMounted || st.RiderClientId != senderId) return;

            if (float.IsNaN(clientPos.x) || float.IsNaN(clientPos.y) || float.IsNaN(clientPos.z) ||
                float.IsInfinity(clientPos.x) || float.IsInfinity(clientPos.y) || float.IsInfinity(clientPos.z) ||
                float.IsNaN(speed) || float.IsInfinity(speed))
                return;

            Vector3 prevPos = new Vector3(st.PosX, st.PosY, st.PosZ);
            Vector3 clamped = bike.ClampToAllowedSurface(prevPos, clientPos, out _);
            Vector3 safePos = Vector3.MoveTowards(prevPos, clamped, 3.5f);
            safePos.y = Mathf.Clamp(safePos.y, bike.ParkingSpawnPos.y - 1.5f, bike.ParkingSpawnPos.y + 4.0f);

            Quaternion flatRot = Quaternion.Euler(0f, clientRot.eulerAngles.y, 0f);
            bike.transform.SetPositionAndRotation(safePos, flatRot);

            st.PosX = safePos.x;
            st.PosY = safePos.y;
            st.PosZ = safePos.z;
            st.RotX = flatRot.x;
            st.RotY = flatRot.y;
            st.RotZ = flatRot.z;
            st.RotW = flatRot.w;
            st.Speed = Mathf.Clamp(speed, -10f, 20f);
            st.Revision++;
            net.MotorcycleState.Value = st;
        }

        [ServerRpc(RequireOwnership = false)]
        public void HonkMotorcycleServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            if (!IsServer) return;
            if (!CanPlayerPerformGameplayActionServer(senderId, out _)) return;
            var net = PizzeriaNetworkState.Instance;
            if (net == null) return;

            var st = net.MotorcycleState.Value;
            if (!st.IsActive || !st.IsMounted || st.RiderClientId != senderId) return;

            double now = NetworkManager.Singleton != null ? NetworkManager.Singleton.ServerTime.Time : Time.timeAsDouble;
            if (now - _lastHornServerTime < 1.5) return;
            _lastHornServerTime = now;

            st.HornCounter++;
            st.Revision++;
            net.MotorcycleState.Value = st;

            Vector3 bikePos = new Vector3(st.PosX, st.PosY, st.PosZ);
            var peds = FindObjectsByType<PedestrianNpc>(FindObjectsSortMode.None);
            for (int i = 0; i < peds.Length; i++)
            {
                if (peds[i] == null || !peds[i].gameObject.activeInHierarchy) continue;
                if ((peds[i].transform.position - bikePos).sqrMagnitude <= 16f * 16f)
                {
                    peds[i].ReactToHorn(bikePos);
                }
            }
        }

        #endregion
    }
}
