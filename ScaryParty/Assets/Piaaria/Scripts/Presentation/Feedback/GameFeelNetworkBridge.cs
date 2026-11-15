using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Stations;
using Unity.Netcode;
using UnityEngine;

namespace ScaryParty.Pizzeria.Presentation.Feedback
{
    /// <summary>
    /// Observa transições autoritativas em PizzeriaNetworkState e traduz em GameFeelContext deduplicado.
    /// Protege late-joiners realizando um seed silencioso no primeiro frame sincronizado.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameFeelNetworkBridge : MonoBehaviour
    {
        private readonly Dictionary<ulong, NetItemDto> _items = new Dictionary<ulong, NetItemDto>();
        private readonly Dictionary<ulong, NetToolDto> _tools = new Dictionary<ulong, NetToolDto>();
        private readonly Dictionary<(int stationId, int slotIndex), NetStationSlotDto> _slots =
            new Dictionary<(int, int), NetStationSlotDto>();
        private readonly Dictionary<(int stationId, int slotIndex), float> _nextProcessTickAt =
            new Dictionary<(int, int), float>();
        private readonly Dictionary<ulong, NetOrderDto> _orders = new Dictionary<ulong, NetOrderDto>();
        private readonly HashSet<ulong> _warnedOrderDeadlines = new HashSet<ulong>();
        private readonly HashSet<ulong> _activeCalls = new HashSet<ulong>();
        private readonly Dictionary<ulong, NetStackDto> _stacks = new Dictionary<ulong, NetStackDto>();
        private readonly Dictionary<ulong, NetBackpackDto> _backpacks = new Dictionary<ulong, NetBackpackDto>();
        private readonly Dictionary<ulong, NetSupplyDto> _supplies = new Dictionary<ulong, NetSupplyDto>();
        private readonly Dictionary<ulong, byte> _customerPhases = new Dictionary<ulong, byte>();
        private readonly HashSet<(int stationId, int slotIndex)> _warnedOvenBurn = new HashSet<(int, int)>();

        private readonly List<ulong> _removedIds = new List<ulong>(32);
        private readonly HashSet<ulong> _seenIds = new HashSet<ulong>();
        private readonly HashSet<(int, int)> _seenSlots = new HashSet<(int, int)>();

        private bool _seeded;
        private PizzeriaNetworkState _boundState;

        private void Awake()
        {
            GameFeelPresenter.EnsureInstance();
        }

        private void ResetSnapshots()
        {
            _items.Clear();
            _tools.Clear();
            _slots.Clear();
            _nextProcessTickAt.Clear();
            _orders.Clear();
            _warnedOrderDeadlines.Clear();
            _activeCalls.Clear();
            _stacks.Clear();
            _backpacks.Clear();
            _supplies.Clear();
            _customerPhases.Clear();
            _warnedOvenBurn.Clear();
            _seeded = false;
            GameFeelPresenter.EnsureInstance()?.Deduplicator.ResetEpoch();
        }

        private void LateUpdate()
        {
            var netState = PizzeriaNetworkState.Instance;
            if (netState == null || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                if (_boundState != null)
                {
                    _boundState = null;
                    ResetSnapshots();
                }
                return;
            }

            if (_boundState != netState)
            {
                _boundState = netState;
                ResetSnapshots();
            }

            bool emit = _seeded;
            ulong localClientId = NetworkManager.Singleton.LocalClientId;
            double serverTime = NetworkManager.Singleton.ServerTime.Time;

            ProcessItems(netState, localClientId, emit);
            ProcessTools(netState, localClientId, emit);
            ProcessStationSlots(netState, localClientId, emit);
            ProcessOrders(netState, serverTime, emit);
            ProcessCalls(netState, emit);
            ProcessStacks(netState, emit);
            ProcessBackpacks(netState, localClientId, emit);
            ProcessSupplies(netState, emit);
            ProcessCustomers(netState, emit);

            _seeded = true;
        }

        private void ProcessItems(PizzeriaNetworkState state, ulong localClientId, bool emit)
        {
            _seenIds.Clear();
            var vis = PizzeriaItemVisualizer.Instance;

            for (int i = 0; i < state.Items.Count; i++)
            {
                var item = state.Items[i];
                _seenIds.Add(item.ItemId);

                if (!_items.TryGetValue(item.ItemId, out var prev))
                {
                    _items[item.ItemId] = item;
                    if (!emit) continue;

                    if (item.LocationType == (byte)LocationType.Hand)
                    {
                        EmitItemEvent(GameFeelEventId.ItemPickedUp, item, localClientId, vis);
                    }
                    else if (item.LocationType == (byte)LocationType.StationSlot || item.LocationType == (byte)LocationType.StagingSlot)
                    {
                        EmitItemEvent(GameFeelEventId.ItemPlaced, item, localClientId, vis);
                    }
                    continue;
                }

                if (emit && !prev.Equals(item))
                {
                    var wrapper = vis != null ? vis.GetItemVisual(item.ItemId) : null;
                    Transform target = wrapper != null ? wrapper.CosmeticPivot : null;
                    var family = GameFeelMaterialClassifier.ClassifyItem(
                        item.Category, item.DefinitionId,
                        item.PackagingState == (byte)PackagingState.Boxed || item.Category == (byte)ItemCategory.Box);

                    // Mudança de local
                    if (prev.LocationType != item.LocationType || prev.HolderId != item.HolderId || prev.SlotId != item.SlotId)
                    {
                        if (item.LocationType == (byte)LocationType.Hand)
                        {
                            if (prev.LocationType == (byte)LocationType.StationSlot && IsOvenStation((int)prev.HolderId))
                            {
                                EmitEvent(GameFeelEventId.OvenRemoved, item.ItemId, (int)prev.HolderId, item.SlotId, item.HolderId == localClientId, item.Revision, GameFeelMaterialFamily.RigidMetal, target);
                            }
                            else
                            {
                                EmitEvent(GameFeelEventId.ItemPickedUp, item.ItemId, (int)prev.HolderId, item.SlotId, item.HolderId == localClientId, item.Revision, family, target);
                            }
                        }
                        else if (item.LocationType == (byte)LocationType.StationSlot || item.LocationType == (byte)LocationType.StagingSlot || item.LocationType == (byte)LocationType.TablePlate)
                        {
                            bool fromLocal = prev.LocationType == (byte)LocationType.Hand && prev.HolderId == localClientId;
                            bool isOven = item.LocationType == (byte)LocationType.StationSlot && IsOvenStation((int)item.HolderId);
                            if (isOven)
                            {
                                EmitEvent(GameFeelEventId.OvenInserted, item.ItemId, (int)item.HolderId, item.SlotId, fromLocal, item.Revision, GameFeelMaterialFamily.RigidMetal, target);
                            }
                            else
                            {
                                var evt = family == GameFeelMaterialFamily.CardboardPaper ? GameFeelEventId.BoxPlaced : GameFeelEventId.ItemPlaced;
                                EmitEvent(evt, item.ItemId, (int)item.HolderId, item.SlotId, fromLocal, item.Revision, family, target);
                            }
                        }
                    }

                    // Embalagem e etiqueta
                    if (prev.PackagingState != item.PackagingState && item.PackagingState == (byte)PackagingState.Boxed)
                    {
                        EmitEvent(GameFeelEventId.BoxPackaged, item.ItemId, (int)item.HolderId, item.SlotId, item.HolderId == localClientId, item.Revision, GameFeelMaterialFamily.CardboardPaper, target);
                    }
                    if (prev.LabelAddressId < 0 && item.LabelAddressId >= 0)
                    {
                        EmitEvent(GameFeelEventId.BoxLabeled, item.ItemId, (int)item.HolderId, item.SlotId, item.HolderId == localClientId, item.Revision, GameFeelMaterialFamily.CardboardPaper, target);
                    }

                    // Cozimento no forno
                    if (prev.CookingStage != item.CookingStage)
                    {
                        if (item.CookingStage == (byte)CookingStage.Baked)
                        {
                            EmitEvent(GameFeelEventId.OvenReadyDing, item.ItemId, (int)item.HolderId, item.SlotId, true, item.Revision, GameFeelMaterialFamily.RigidMetal, target);
                        }
                        else if (item.CookingStage == (byte)CookingStage.Burned)
                        {
                            EmitEvent(GameFeelEventId.OvenBurned, item.ItemId, (int)item.HolderId, item.SlotId, true, item.Revision, GameFeelMaterialFamily.RigidMetal, target);
                        }
                    }
                    else if (item.BurnProgress >= 0.72f && prev.BurnProgress < 0.72f)
                    {
                        var ovenKey = ((int)item.HolderId, item.SlotId);
                        if (_warnedOvenBurn.Add(ovenKey))
                        {
                            EmitEvent(GameFeelEventId.OvenBurningWarning, item.ItemId, (int)item.HolderId, item.SlotId, true, item.Revision, GameFeelMaterialFamily.RigidMetal, target);
                        }
                    }

                    if (item.BurnProgress < 0.1f)
                    {
                        _warnedOvenBurn.Remove(((int)item.HolderId, item.SlotId));
                    }
                }

                _items[item.ItemId] = item;
            }

            _removedIds.Clear();
            foreach (var kv in _items)
            {
                if (!_seenIds.Contains(kv.Key)) _removedIds.Add(kv.Key);
            }
            for (int r = 0; r < _removedIds.Count; r++)
            {
                _items.Remove(_removedIds[r]);
            }
        }

        private void ProcessTools(PizzeriaNetworkState state, ulong localClientId, bool emit)
        {
            var vis = PizzeriaItemVisualizer.Instance;
            for (int i = 0; i < state.Tools.Count; i++)
            {
                var tool = state.Tools[i];
                if (!_tools.TryGetValue(tool.ToolId, out var prev))
                {
                    _tools[tool.ToolId] = tool;
                    continue;
                }

                if (emit && (prev.LocationType != tool.LocationType || prev.HolderId != tool.HolderId))
                {
                    var wrapper = vis != null ? vis.GetToolVisual(tool.ToolId) : null;
                    Transform target = wrapper != null ? wrapper.CosmeticPivot : null;
                    if (tool.LocationType == (byte)LocationType.Hand)
                    {
                        EmitEvent(GameFeelEventId.ToolPickedUp, tool.ToolId, 0, tool.SlotId, tool.HolderId == localClientId, tool.Revision, GameFeelMaterialFamily.RigidMetal, target);
                    }
                    else if (tool.LocationType == (byte)LocationType.StationSlot || tool.LocationType == (byte)LocationType.StagingSlot)
                    {
                        bool fromLocal = prev.LocationType == (byte)LocationType.Hand && prev.HolderId == localClientId;
                        EmitEvent(GameFeelEventId.ItemPlaced, tool.ToolId, (int)tool.HolderId, tool.SlotId, fromLocal, tool.Revision, GameFeelMaterialFamily.RigidMetal, target);
                    }
                }

                _tools[tool.ToolId] = tool;
            }
        }

        private void ProcessStationSlots(PizzeriaNetworkState state, ulong localClientId, bool emit)
        {
            _seenSlots.Clear();
            var vis = PizzeriaItemVisualizer.Instance;
            float now = Time.unscaledTime;

            for (int i = 0; i < state.StationSlots.Count; i++)
            {
                var slot = state.StationSlots[i];
                var key = (slot.StationId, slot.SlotIndex);
                _seenSlots.Add(key);

                if (!_slots.TryGetValue(key, out var prev))
                {
                    _slots[key] = slot;
                    if (emit && slot.Phase == (byte)OperationPhase.Running)
                    {
                        var wrapper = vis != null ? vis.GetItemVisual(slot.HeldItemId) : null;
                        EmitEvent(GameFeelEventId.ProcessStarted, slot.HeldItemId, slot.StationId, slot.SlotIndex,
                            slot.InitiatorClientId == localClientId, slot.Revision,
                            GameFeelMaterialClassifier.ClassifyProcess(slot.ProcessId),
                            wrapper != null ? wrapper.CosmeticPivot : null);
                    }
                    continue;
                }

                if (emit)
                {
                    var wrapper = vis != null ? vis.GetItemVisual(slot.HeldItemId) : null;
                    Transform target = wrapper != null ? wrapper.CosmeticPivot : null;
                    var family = GameFeelMaterialClassifier.ClassifyProcess(slot.ProcessId);
                    bool isLocal = slot.InitiatorClientId == localClientId || prev.InitiatorClientId == localClientId;

                    if (prev.Phase != slot.Phase)
                    {
                        if (slot.Phase == (byte)OperationPhase.Running)
                        {
                            EmitEvent(GameFeelEventId.ProcessStarted, slot.HeldItemId, slot.StationId, slot.SlotIndex, isLocal, slot.Revision, family, target);
                            _nextProcessTickAt[key] = now + 0.14f;
                        }
                        else if (slot.Phase == (byte)OperationPhase.Completed)
                        {
                            EmitEvent(GameFeelEventId.ProcessCompleted, slot.HeldItemId, slot.StationId, slot.SlotIndex, isLocal, slot.Revision, family, target);
                        }
                        else if (prev.Phase == (byte)OperationPhase.Running &&
                                 (slot.Phase == (byte)OperationPhase.Paused || slot.Phase == (byte)OperationPhase.Cancelled))
                        {
                            EmitEvent(GameFeelEventId.ProcessInterrupted, slot.HeldItemId, slot.StationId, slot.SlotIndex, isLocal, slot.Revision, family, target);
                        }
                    }

                    if (slot.Phase == (byte)OperationPhase.Running)
                    {
                        if (!_nextProcessTickAt.TryGetValue(key, out float nextAt) || now >= nextAt)
                        {
                            var stepEvt = MapProcessIdToStepEvent(slot.ProcessId);
                            float cycleInterval = stepEvt == GameFeelEventId.RollDoughStep ? 0.36f : 0.31f;
                            _nextProcessTickAt[key] = now + cycleInterval;
                            EmitEvent(stepEvt, slot.HeldItemId, slot.StationId, slot.SlotIndex, isLocal,
                                unchecked(slot.Revision + (uint)Mathf.FloorToInt(now * 10f)), family, target);
                        }
                    }
                }

                _slots[key] = slot;
            }
        }

        private void ProcessOrders(PizzeriaNetworkState state, double serverTime, bool emit)
        {
            for (int i = 0; i < state.Orders.Count; i++)
            {
                var order = state.Orders[i];
                if (!_orders.TryGetValue(order.OrderId, out var prev))
                {
                    _orders[order.OrderId] = order;
                    if (emit && order.Lifecycle == (byte)OrderLifecycle.Active)
                    {
                        EmitEvent(GameFeelEventId.OrderPosted, order.OrderId, 0, -1, true, order.Revision, GameFeelMaterialFamily.CardboardPaper, ResolveBoardPosition());
                    }
                    continue;
                }

                if (emit)
                {
                    if (prev.Lifecycle != order.Lifecycle && order.Lifecycle == (byte)OrderLifecycle.Delivered)
                    {
                        EmitEvent(GameFeelEventId.OrderCompleted, order.OrderId, 0, -1, true, order.Revision, GameFeelMaterialFamily.CardboardPaper, ResolveBoardPosition());
                    }

                    if (order.Lifecycle == (byte)OrderLifecycle.Active && order.DeadlineTime > 0d)
                    {
                        double remaining = order.DeadlineTime - serverTime;
                        if (remaining > 0d && remaining <= 45d && _warnedOrderDeadlines.Add(order.OrderId))
                        {
                            EmitEvent(GameFeelEventId.OrderDeadlineWarning, order.OrderId, 0, -1, true, order.Revision, GameFeelMaterialFamily.CardboardPaper, ResolveBoardPosition());
                        }
                    }
                }

                _orders[order.OrderId] = order;
            }
        }

        private void ProcessCalls(PizzeriaNetworkState state, bool emit)
        {
            _seenIds.Clear();
            for (int i = 0; i < state.Calls.Count; i++)
            {
                var call = state.Calls[i];
                _seenIds.Add(call.CallId);
                if (_activeCalls.Add(call.CallId) && emit)
                {
                    EmitEvent(GameFeelEventId.PhoneRingPulse, call.CallId, 0, -1, true, call.Revision, GameFeelMaterialFamily.RigidMetal, ResolvePhonePosition());
                }
            }

            _removedIds.Clear();
            foreach (var id in _activeCalls)
            {
                if (!_seenIds.Contains(id)) _removedIds.Add(id);
            }
            for (int r = 0; r < _removedIds.Count; r++)
            {
                ulong id = _removedIds[r];
                _activeCalls.Remove(id);
                if (emit)
                {
                    EmitEvent(GameFeelEventId.PhoneAnswered, id, 0, -1, true, 1, GameFeelMaterialFamily.RigidMetal, ResolvePhonePosition());
                }
            }
        }

        private void ProcessStacks(PizzeriaNetworkState state, bool emit)
        {
            for (int i = 0; i < state.Stacks.Count; i++)
            {
                var st = state.Stacks[i];
                if (!_stacks.TryGetValue(st.StackId, out var prev))
                {
                    _stacks[st.StackId] = st;
                    continue;
                }

                if (emit && prev.ItemCount != st.ItemCount)
                {
                    bool added = st.ItemCount > prev.ItemCount;
                    var evt = added ? GameFeelEventId.StackPlaced : GameFeelEventId.StackRemoved;
                    StationRegistry.TryGetStation(st.StationId, out var tr);
                    EmitEvent(evt, st.StackId, st.StationId, st.ItemCount, true, st.Revision, GameFeelMaterialFamily.CardboardPaper, tr);
                    PizzeriaItemVisualizer.Instance?.TriggerStackWave(st.StationId, added);
                }

                _stacks[st.StackId] = st;
            }
        }

        private void ProcessBackpacks(PizzeriaNetworkState state, ulong localClientId, bool emit)
        {
            for (int i = 0; i < state.Backpacks.Count; i++)
            {
                var bag = state.Backpacks[i];
                if (!_backpacks.TryGetValue(bag.BackpackId, out var prev))
                {
                    _backpacks[bag.BackpackId] = bag;
                    continue;
                }

                if (emit)
                {
                    bool isLocal = bag.EquippedBy == localClientId || prev.EquippedBy == localClientId;
                    Vector3 worldPos = new Vector3(bag.PosX, bag.PosY, bag.PosZ);

                    if (prev.EquippedBy != bag.EquippedBy && bag.EquippedBy != BackpackState.NoOwner)
                    {
                        EmitEvent(GameFeelEventId.BackpackEquipped, bag.BackpackId, 0, -1, isLocal, bag.Revision, GameFeelMaterialFamily.FabricCargo, worldPos);
                    }
                    else if (prev.ItemCount != bag.ItemCount)
                    {
                        var evt = bag.ItemCount > prev.ItemCount ? GameFeelEventId.BackpackStored : GameFeelEventId.BackpackRemoved;
                        EmitEvent(evt, bag.BackpackId, 0, -1, isLocal, bag.Revision, GameFeelMaterialFamily.FabricCargo, worldPos);
                    }
                }

                _backpacks[bag.BackpackId] = bag;
            }
        }

        private void ProcessSupplies(PizzeriaNetworkState state, bool emit)
        {
            for (int i = 0; i < state.SupplyOrders.Count; i++)
            {
                var sup = state.SupplyOrders[i];
                if (!_supplies.TryGetValue(sup.SupplyOrderId, out var prev))
                {
                    _supplies[sup.SupplyOrderId] = sup;
                    if (emit)
                    {
                        EmitEvent(GameFeelEventId.SupplyOrdered, sup.SupplyOrderId, 0, sup.ReceivingSlotIndex, true, sup.Revision, GameFeelMaterialFamily.CardboardPaper, transform.position);
                    }
                    continue;
                }

                if (emit && prev.State != sup.State && sup.State == (byte)SupplyOrderLifecycle.Arrived)
                {
                    EmitEvent(GameFeelEventId.SupplyArrived, sup.SupplyOrderId, 0, sup.ReceivingSlotIndex, true, sup.Revision, GameFeelMaterialFamily.CardboardPaper, transform.position);
                }

                _supplies[sup.SupplyOrderId] = sup;
            }
        }

        private void ProcessCustomers(PizzeriaNetworkState state, bool emit)
        {
            for (int i = 0; i < state.Customers.Count; i++)
            {
                var c = state.Customers[i];
                if (!_customerPhases.TryGetValue(c.CustomerId, out var prevPhase))
                {
                    _customerPhases[c.CustomerId] = c.Phase;
                    continue;
                }

                if (emit && prevPhase != c.Phase)
                {
                    GameFeelEventId evt = (CustomerPhase)c.Phase switch
                    {
                        CustomerPhase.WaitingInQueue => GameFeelEventId.NpcCounterArrived,
                        CustomerPhase.WalkingToTable => GameFeelEventId.NpcOrderRegistered,
                        CustomerPhase.SeatedWaitingFood => GameFeelEventId.NpcSeatedSettle,
                        CustomerPhase.Eating => GameFeelEventId.NpcOrderGood,
                        CustomerPhase.LeavingSatisfied => GameFeelEventId.DeliverySuccess,
                        CustomerPhase.LeavingAngry => GameFeelEventId.NpcLeavingAngry,
                        _ => GameFeelEventId.None
                    };

                    if (evt != GameFeelEventId.None)
                    {
                        EmitEvent(evt, c.CustomerId, c.AssignedTableId, c.QueueSlotIndex, false, c.Revision, GameFeelMaterialFamily.CharacterBody, transform.position);
                    }
                }

                _customerPhases[c.CustomerId] = c.Phase;
            }
        }

        private static GameFeelEventId MapProcessIdToStepEvent(int processId)
        {
            return processId switch
            {
                1 => GameFeelEventId.RollDoughStep,
                2 => GameFeelEventId.GrateCheeseStep,
                3 => GameFeelEventId.SliceTomatoPepperoniStep,
                4 => GameFeelEventId.SliceMushroomStep,
                _ => GameFeelEventId.ProcessTick
            };
        }

        private static bool IsOvenStation(int stationId)
        {
            if (StationRegistry.TryGetStation(stationId, out var tr) && tr != null)
            {
                return tr.GetComponent<OvenStation>() != null;
            }
            return false;
        }

        private Vector3 ResolvePhonePosition()
        {
            var phone = FindFirstObjectByType<PhoneStation>();
            return phone != null ? phone.transform.position : transform.position;
        }

        private Vector3 ResolveBoardPosition()
        {
            var board = FindFirstObjectByType<TicketBoardPresenter>();
            return board != null ? board.transform.position : transform.position;
        }

        private void EmitItemEvent(GameFeelEventId eventId, NetItemDto item, ulong localClientId, PizzeriaItemVisualizer vis)
        {
            var wrapper = vis != null ? vis.GetItemVisual(item.ItemId) : null;
            var family = GameFeelMaterialClassifier.ClassifyItem(
                item.Category, item.DefinitionId,
                item.PackagingState == (byte)PackagingState.Boxed || item.Category == (byte)ItemCategory.Box);
            EmitEvent(eventId, item.ItemId, (int)item.HolderId, item.SlotId, item.HolderId == localClientId, item.Revision, family, wrapper != null ? wrapper.CosmeticPivot : null);
        }

        private static void EmitEvent(
            GameFeelEventId eventId,
            ulong entityId,
            int stationId,
            int slotIndex,
            bool isLocal,
            uint revision,
            GameFeelMaterialFamily family,
            Transform target)
        {
            var presenter = GameFeelPresenter.EnsureInstance();
            if (presenter == null) return;

            var ctx = GameFeelContext.Create(eventId, entityId, stationId, revision, family);
            ctx.SlotIndex = slotIndex;
            ctx.IsLocalOwner = isLocal;
            ctx.TargetTransform = target;
            ctx.WorldPosition = target != null ? target.position : Vector3.zero;
            presenter.Trigger(in ctx);
        }

        private static void EmitEvent(
            GameFeelEventId eventId,
            ulong entityId,
            int stationId,
            int slotIndex,
            bool isLocal,
            uint revision,
            GameFeelMaterialFamily family,
            Vector3 worldPos)
        {
            var presenter = GameFeelPresenter.EnsureInstance();
            if (presenter == null) return;

            var ctx = GameFeelContext.Create(eventId, entityId, stationId, revision, family);
            ctx.SlotIndex = slotIndex;
            ctx.IsLocalOwner = isLocal;
            ctx.WorldPosition = worldPos;
            presenter.Trigger(in ctx);
        }
    }
}
