using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Models
{
    public class NightStartSnapshot
    {
        public int NightNumber { get; }
        public int RestaurantBudget { get; }
        public Dictionary<(int storageId, int ingredientDefId), int> SavedStock { get; } = new Dictionary<(int, int), int>();
        public Dictionary<int, int> SavedUpgrades { get; } = new Dictionary<int, int>();

        public NightStartSnapshot(int nightNumber, int restaurantBudget)
        {
            NightNumber = nightNumber;
            RestaurantBudget = restaurantBudget;
        }
    }

    public class PizzeriaState
    {
        public uint GenerationId { get; set; } = 1;
        public uint Revision { get; set; } = 1;
        public int RestaurantBudget { get; set; } = 500;

        // Estado autoritativo do ciclo de noites (O5 / N1)
        public int CurrentNightNumber { get; set; } = 1;
        public int TargetNightNumber { get; set; } = 1;
        public NightPhase CurrentNightPhase { get; set; } = NightPhase.Preparing;
        public NightDefeatReason NightDefeatReason { get; set; } = NightDefeatReason.None;
        public float NightDurationSeconds { get; set; } = 360f;
        public float NightRemainingSeconds { get; set; } = 360f;
        public float NightGraceRemainingSeconds { get; set; } = 0f;
        public float NightPrepRemainingSeconds { get; set; } = 4f;
        public int NightEarnedRevenue { get; set; } = 0;
        public int RunTotalRevenue { get; set; } = 0;
        public int NightRevenueGoal { get; set; } = 60;
        public int Reputation { get; set; } = 100;
        public int NightReputationMinGoal { get; set; } = 0;
        public int LastReputationDelta { get; set; } = 0;
        public string LastReputationReason { get; set; } = string.Empty;
        public int NightPizzasDelivered { get; set; } = 0;
        public int NightPizzasMinGoal { get; set; } = 0;
        public int NightOrdersCompleted { get; set; } = 0;
        public int NightOrdersFailed { get; set; } = 0;
        public int NightLostCustomers { get; set; } = 0;
        public bool IsNonLethalTutorialNight { get; set; } = false;
        public int FrozenHumanCount { get; set; } = 1;
        public int ResolvedBaseGoal { get; set; } = 60;
        public float ResolvedHumanScaleFactor { get; set; } = 1f;
        public float ResolvedModifierMultiplier { get; set; } = 1f;
        public byte ResolvedGoalSource { get; set; } = 0;
        public string ActivePresetId { get; set; } = "Default";
        public bool AllowDebugPanel { get; set; } = true;
        public bool IsDebugTaintedRun { get; set; } = false;
        public int NightSupplySpent { get; set; } = 0;
        public int NightShopSpent { get; set; } = 0;
        public int NightWagesSpent { get; set; } = 0;
        public int ToolRackStationId { get; set; } = 3;
        public NightStartSnapshot CurrentSnapshot { get; set; }

        private ulong _nextItemId = 1;
        private ulong _nextToolId = 1;
        private ulong _nextOrderId = 1;
        private ulong _nextOrderLineId = 1;
        private ulong _nextOperationId = 1;
        private ulong _nextSupplyOrderId = 1;
        private ulong _nextCallId = 1;
        private ulong _nextStackId = 1;
        private ulong _nextBackpackId = 1;
        private ulong _nextCustomerId = 1;

        public readonly Dictionary<ItemId, ItemState> Items = new Dictionary<ItemId, ItemState>();
        public readonly Dictionary<ToolItemId, ToolItemState> Tools = new Dictionary<ToolItemId, ToolItemState>();
        public readonly Dictionary<OrderId, OrderState> Orders = new Dictionary<OrderId, OrderState>();
        public readonly Dictionary<StationSlotId, OperationState> ActiveOperations = new Dictionary<StationSlotId, OperationState>();
        public readonly Dictionary<(int storageId, int ingredientDefId), StorageSlotState> StorageSlots = new Dictionary<(int, int), StorageSlotState>();
        public readonly Dictionary<SupplyOrderId, SupplyOrderState> SupplyOrders = new Dictionary<SupplyOrderId, SupplyOrderState>();
        public readonly Dictionary<int, int> UpgradeLevels = new Dictionary<int, int>();

        public readonly Dictionary<StackId, StackState> Stacks = new Dictionary<StackId, StackState>();
        public readonly Dictionary<BackpackId, BackpackState> Backpacks = new Dictionary<BackpackId, BackpackState>();
        public readonly Dictionary<CallId, PendingCall> Calls = new Dictionary<CallId, PendingCall>();
        public readonly Dictionary<ulong, PlayerHold> PlayerHolds = new Dictionary<ulong, PlayerHold>();
        public readonly DeliveryAttemptLog DeliveryAttempts = new DeliveryAttemptLog();

        // Clientes presenciais e Mesas do salão
        public readonly Dictionary<CustomerId, CustomerState> Customers = new Dictionary<CustomerId, CustomerState>();
        public readonly Dictionary<int, TableState> Tables = new Dictionary<int, TableState>();
        public readonly Dictionary<int, double> QueueLastAdvanceTime = new Dictionary<int, double>();

        // P2: Vínculo exclusivo Pizza <-> Unidade de Pedido e reservas de slot por funcionário
        public readonly Dictionary<OrderUnitKey, OrderUnitState> OrderUnits = new Dictionary<OrderUnitKey, OrderUnitState>();
        public readonly Dictionary<ItemId, OrderUnitKey> ItemToOrderUnit = new Dictionary<ItemId, OrderUnitKey>();
        public readonly Dictionary<(int stationId, int slotIndex), StationSlotReservation> StationSlotReservations = new Dictionary<(int, int), StationSlotReservation>();

        public ItemId GenerateItemId() => new ItemId(_nextItemId++);
        public ToolItemId GenerateToolId() => new ToolItemId(_nextToolId++);
        public OrderId GenerateOrderId() => new OrderId(_nextOrderId++);
        public OrderLineId GenerateOrderLineId() => new OrderLineId(_nextOrderLineId++);
        public OperationId GenerateOperationId() => new OperationId(_nextOperationId++);
        public SupplyOrderId GenerateSupplyOrderId() => new SupplyOrderId(_nextSupplyOrderId++);
        public CallId GenerateCallId() => new CallId(_nextCallId++);
        public StackId GenerateStackId() => new StackId(_nextStackId++);
        public BackpackId GenerateBackpackId() => new BackpackId(_nextBackpackId++);
        public CustomerId GenerateCustomerId() => new CustomerId(_nextCustomerId++);

        public void IncrementRevision() => Revision++;

        public void Reset(uint newGenerationId, int startingBudget = 500, int initialReputation = 100)
        {
            GenerationId = newGenerationId;
            Revision = 1;
            RestaurantBudget = startingBudget;
            Reputation = System.Math.Clamp(initialReputation, 0, 100);
            NightReputationMinGoal = 0;
            LastReputationDelta = 0;
            LastReputationReason = string.Empty;
            RunTotalRevenue = 0;
            NightPizzasDelivered = 0;
            NightPizzasMinGoal = 0;
            CurrentNightNumber = 1;
            TargetNightNumber = 1;
            CurrentNightPhase = NightPhase.Preparing;
            NightDefeatReason = NightDefeatReason.None;
            NightDurationSeconds = 360f;
            NightRemainingSeconds = 360f;
            NightGraceRemainingSeconds = 0f;
            NightPrepRemainingSeconds = 4f;
            NightEarnedRevenue = 0;
            NightRevenueGoal = 60;
            NightOrdersCompleted = 0;
            NightOrdersFailed = 0;
            NightLostCustomers = 0;
            IsNonLethalTutorialNight = false;
            FrozenHumanCount = 1;
            ResolvedBaseGoal = 60;
            ResolvedHumanScaleFactor = 1f;
            ResolvedModifierMultiplier = 1f;
            ResolvedGoalSource = 0;
            ActivePresetId = "Default";
            AllowDebugPanel = true;
            IsDebugTaintedRun = false;
            NightSupplySpent = 0;
            NightShopSpent = 0;
            NightWagesSpent = 0;
            CurrentSnapshot = null;
            _nextItemId = 1;
            _nextToolId = 1;
            _nextOrderId = 1;
            _nextOrderLineId = 1;
            _nextOperationId = 1;
            _nextSupplyOrderId = 1;
            _nextCallId = 1;
            _nextStackId = 1;
            _nextBackpackId = 1;
            _nextCustomerId = 1;

            Items.Clear();
            Tools.Clear();
            Orders.Clear();
            ActiveOperations.Clear();
            StorageSlots.Clear();
            SupplyOrders.Clear();
            UpgradeLevels.Clear();
            Stacks.Clear();
            Backpacks.Clear();
            Calls.Clear();
            PlayerHolds.Clear();
            DeliveryAttempts.Clear();
            Customers.Clear();
            Tables.Clear();
            QueueLastAdvanceTime.Clear();
            OrderUnits.Clear();
            ItemToOrderUnit.Clear();
            StationSlotReservations.Clear();
        }
    }
}
