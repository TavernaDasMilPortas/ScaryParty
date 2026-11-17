using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Stations;

namespace ScaryParty.Pizzeria.Presentation
{
    /// <summary>
    /// Controlador autoritativo no servidor para funcionários contratados (Balconista e Pizzaiolo).
    /// Navega via NavMesh pelas estações reais, executa serviços de domínio e respeita reservas/takeover de humanos.
    /// </summary>
    public class EmployeeAgentController : MonoBehaviour
    {
        public const ulong EmployeeActorOffset = 1_000_000UL;

        public int EmployeeId { get; private set; }
        public EmployeeRole Role { get; private set; }

        private NavMeshAgent _agent;
        private readonly System.Random _fallbackRng = new System.Random();

        private float _hp = 100f;
        private int _activeTaskId = -1;
        private int _rolledTaskId = -1;
        private int _stepIndex;
        private float _stepTimer;
        private ItemId _workingPizzaId = ItemId.None;
        private int _ovenStationId = -1;
        private int _ovenSlotIndex = -1;
        private int _prepStationId = -1;
        private int _prepSlotIndex = -1;

        private enum WorkStep
        {
            Idle = 0,
            CashierMoveToCounter = 1,
            CashierAttendWait = 2,
            PizzaioloPrepareDough = 3,
            PizzaioloAssembleToppings = 4,
            PizzaioloInsertOven = 5,
            PizzaioloWaitBake = 6,
            PizzaioloPackageAndStage = 7
        }

        public void Initialize(int employeeId, EmployeeRole role)
        {
            EmployeeId = employeeId;
            Role = role;
            _agent = GetComponent<NavMeshAgent>();
            if (_agent == null)
            {
                _agent = gameObject.AddComponent<NavMeshAgent>();
                _agent.radius = 0.35f;
                _agent.height = 1.75f;
                _agent.speed = 3.6f;
                _agent.angularSpeed = 540f;
                _agent.stoppingDistance = 0.9f;
            }
        }

        public void TakeDamageServer(float damage)
        {
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.EmployeeTaskService == null) return;
            if (!root.EmployeeTaskService.Employees.TryGetValue(EmployeeId, out var emp) || !emp.IsAlive) return;

            _hp -= Mathf.Max(0f, damage);
            if (_hp <= 0f)
            {
                _hp = 0f;
                emp.IsAlive = false;
                emp.AssignedTaskId = -1;
                root.EmployeeTaskService.ReleaseActorTasks((ulong)EmployeeId, isHuman: false);
                ResetWorkState();
                if (_agent != null && _agent.enabled) _agent.isStopped = true;
            }
        }

        public void TickServer(PizzeriaRoot root, float dt)
        {
            if (root == null || !root.IsServer || root.DomainState == null || root.EmployeeTaskService == null) return;
            if (!root.EmployeeTaskService.Employees.TryGetValue(EmployeeId, out var emp)) return;

            if (!emp.IsAlive)
            {
                if (_agent != null && _agent.enabled) _agent.isStopped = true;
                return;
            }

            if (_hp <= 0f) _hp = 100f;
            if (_agent != null && _agent.enabled && _agent.isStopped) _agent.isStopped = false;

            if (root.DomainState.CurrentNightPhase != NightPhase.Active)
            {
                ResetWorkState();
                return;
            }

            if (Role == EmployeeRole.Cashier)
            {
                TickCashier(root, emp, dt);
            }
            else
            {
                TickPizzaiolo(root, emp, dt);
            }
        }

        private void TickCashier(PizzeriaRoot root, HiredEmployeeState emp, float dt)
        {
            CustomerState targetCustomer = null;
            foreach (var kvp in root.DomainState.Customers)
            {
                var c = kvp.Value;
                if (c != null && c.IsInQueue && c.QueueSlotIndex == 0 && c.AttendedByPlayerId == 0)
                {
                    targetCustomer = c;
                    break;
                }
            }

            if (targetCustomer == null)
            {
                _stepIndex = (int)WorkStep.Idle;
                _stepTimer = 0f;
                return;
            }

            Vector3 counterPos = ResolveStationPosition<CustomerCounterStation>(root.transform.position);
            if (!MoveToValidated(counterPos, out bool pathBlocked))
            {
                if (pathBlocked && _activeTaskId > 0)
                {
                    root.EmployeeTaskService.MarkTaskBlocked(_activeTaskId, EmployeeBlockReason.PathBlocked);
                }
                return;
            }

            if ((transform.position - counterPos).sqrMagnitude > 2.4f * 2.4f)
            {
                _stepIndex = (int)WorkStep.CashierMoveToCounter;
                return;
            }

            if (_stepIndex != (int)WorkStep.CashierAttendWait)
            {
                _stepIndex = (int)WorkStep.CashierAttendWait;
                _stepTimer = Mathf.Max(0.8f, 2.2f - (emp.Level - 1) * 0.35f);
            }

            _stepTimer -= dt;
            if (_stepTimer > 0f) return;

            var res = root.CustomerService.CompleteAttendCustomer(
                root.DomainState,
                targetCustomer.Id,
                root.OrderService,
                root.Clock,
                root.Config != null ? root.Config.maxActiveOrders : 6);

            if (res.Success)
            {
                root.EmployeeTaskService.EnqueueOrderTask(res.Value, EmployeeRole.Pizzaiolo);
                PizzeriaCommandHandler.Instance?.CommitAndReplicate();
            }

            _stepIndex = (int)WorkStep.Idle;
            _stepTimer = 0f;
        }

        private void TickPizzaiolo(PizzeriaRoot root, HiredEmployeeState emp, float dt)
        {
            double now = root.Clock != null ? root.Clock.Now : Time.timeAsDouble;

            // Valida se a tarefa atual ainda pertence a este funcionário (ou sofreu HumanTakeover)
            SharedEmployeeTask currentTask = null;
            if (_activeTaskId > 0)
            {
                if (!root.EmployeeTaskService.Tasks.TryGetValue(_activeTaskId, out currentTask) ||
                    currentTask.IsReservedByHuman ||
                    currentTask.ReservedByActorId != (ulong)EmployeeId ||
                    currentTask.State == EmployeeTaskState.Completed ||
                    currentTask.State == EmployeeTaskState.Cancelled)
                {
                    ResetWorkState();
                    currentTask = null;
                }
                else
                {
                    // Renova lease enquanto trabalha
                    root.EmployeeTaskService.TryReserveTask(_activeTaskId, (ulong)EmployeeId, isHuman: false, now, 20.0);
                }
            }

            if (currentTask == null)
            {
                foreach (var kvp in root.EmployeeTaskService.Tasks)
                {
                    var t = kvp.Value;
                    if (t.RequiredRole != EmployeeRole.Pizzaiolo) continue;
                    if (t.State != EmployeeTaskState.Available &&
                        !(t.State == EmployeeTaskState.Blocked && now >= t.LeaseExpiresAt)) continue;
                    if (!root.DomainState.Orders.TryGetValue(t.TargetOrderId, out var ord) ||
                        ord.Lifecycle != OrderLifecycle.Accepted) continue;

                    if (root.EmployeeTaskService.TryReserveTask(t.TaskId, (ulong)EmployeeId, isHuman: false, now, 20.0))
                    {
                        _activeTaskId = t.TaskId;
                        emp.AssignedTaskId = t.TaskId;
                        currentTask = t;
                        _stepIndex = (int)WorkStep.PizzaioloPrepareDough;
                        _stepTimer = 0f;
                        break;
                    }
                }
            }

            if (currentTask == null) return;

            if (!root.DomainState.Orders.TryGetValue(currentTask.TargetOrderId, out var order) ||
                order.Lifecycle != OrderLifecycle.Accepted ||
                order.Lines == null || order.Lines.Count == 0)
            {
                root.EmployeeTaskService.CompleteTask(currentTask.TaskId);
                ResetWorkState();
                return;
            }

            if (_rolledTaskId != currentTask.TaskId)
            {
                _rolledTaskId = currentTask.TaskId;
                var stats = root.RunProgressionService != null
                    ? root.RunProgressionService.RecalculateEffectiveStats()
                    : new RunEffectiveStats();
                var rng = root.RunProgressionService != null ? root.RunProgressionService.EmployeeErrorRng : _fallbackRng;
                currentTask.IntentionalErrorTriggered = root.EmployeeTaskService.RollIntentionalSkillError(
                    EmployeeId, rng, stats.EmployeeErrorMultiplier);
            }

            var spec = order.Lines[0].Specification ?? PizzaSpecification.CreateDefaultCheese();
            ulong actorKey = EmployeeActorOffset + (ulong)EmployeeId;

            if (_stepIndex == (int)WorkStep.PizzaioloPrepareDough || _stepIndex == (int)WorkStep.Idle)
            {
                Vector3 prepPos = ResolveStationPosition<PrepStation>(root.transform.position);
                if (!MoveToValidated(prepPos, out bool pathBlocked))
                {
                    if (pathBlocked) root.EmployeeTaskService.MarkTaskBlocked(currentTask.TaskId, EmployeeBlockReason.PathBlocked);
                    return;
                }
                if ((transform.position - prepPos).sqrMagnitude > 2.5f * 2.5f) return;

                if (!_workingPizzaId.IsValid || !root.DomainState.Items.ContainsKey(_workingPizzaId))
                {
                    if (!TryAcquireDoughIntoPrep(root, spec, actorKey, out _workingPizzaId, out _prepStationId, out _prepSlotIndex))
                    {
                        root.EmployeeTaskService.MarkTaskBlocked(currentTask.TaskId, EmployeeBlockReason.NoDough);
                        return;
                    }
                    root.EmployeeTaskService.ClearTaskBlocked(currentTask.TaskId);
                    _stepTimer = Mathf.Max(0.75f, 1.8f - (emp.Level - 1) * 0.25f);
                }

                _stepTimer -= dt;
                if (_stepTimer > 0f) return;

                if (root.DomainState.Items.TryGetValue(_workingPizzaId, out var dough))
                {
                    if (dough.PrepStage == ProcessingStage.Raw)
                    {
                        dough.PrepStage = ProcessingStage.Prepared;
                        dough.BaseDoughStage = ProcessingStage.Prepared;
                        dough.Revision++;
                    }
                    _stepIndex = (int)WorkStep.PizzaioloAssembleToppings;
                    _stepTimer = Mathf.Max(0.75f, 1.8f - (emp.Level - 1) * 0.25f);
                    PizzeriaCommandHandler.Instance?.CommitAndReplicate();
                }
                return;
            }

            if (_stepIndex == (int)WorkStep.PizzaioloAssembleToppings)
            {
                Vector3 asmPos = ResolveStationPosition<AssemblyStation>(ResolveStationPosition<PrepStation>(root.transform.position));
                if (!MoveToValidated(asmPos, out bool pathBlocked))
                {
                    if (pathBlocked) root.EmployeeTaskService.MarkTaskBlocked(currentTask.TaskId, EmployeeBlockReason.PathBlocked);
                    return;
                }
                if ((transform.position - asmPos).sqrMagnitude > 2.5f * 2.5f) return;

                _stepTimer -= dt;
                if (_stepTimer > 0f) return;

                if (!root.DomainState.Items.TryGetValue(_workingPizzaId, out var pizza))
                {
                    ResetWorkState();
                    return;
                }

                if (!TryApplySpecIngredients(root, pizza, spec, actorKey, currentTask.IntentionalErrorTriggered, out var missingReason))
                {
                    root.EmployeeTaskService.MarkTaskBlocked(currentTask.TaskId, missingReason);
                    return;
                }

                root.EmployeeTaskService.ClearTaskBlocked(currentTask.TaskId);
                _stepIndex = (int)WorkStep.PizzaioloInsertOven;
                _stepTimer = 0.5f;
                PizzeriaCommandHandler.Instance?.CommitAndReplicate();
                return;
            }

            if (_stepIndex == (int)WorkStep.PizzaioloInsertOven)
            {
                Vector3 ovenPos = ResolveStationPosition<OvenStation>(root.transform.position);
                if (!MoveToValidated(ovenPos, out bool pathBlocked))
                {
                    if (pathBlocked) root.EmployeeTaskService.MarkTaskBlocked(currentTask.TaskId, EmployeeBlockReason.PathBlocked);
                    return;
                }
                if ((transform.position - ovenPos).sqrMagnitude > 2.6f * 2.6f) return;

                if (!TryFindFreeOvenSlot(root, out int ovenId, out int slotIdx))
                {
                    root.EmployeeTaskService.MarkTaskBlocked(currentTask.TaskId, EmployeeBlockReason.OvenBusy);
                    return;
                }

                var ovenSlot = new StationSlotId(ovenId, slotIdx);
                var insertRes = root.OvenService.InsertPizza(
                    root.DomainState,
                    ovenSlot,
                    _workingPizzaId,
                    actorKey,
                    root.Catalog,
                    root.Config != null ? root.Config.defaultBakeDuration : 15f);

                if (!insertRes.Success)
                {
                    root.EmployeeTaskService.MarkTaskBlocked(currentTask.TaskId, EmployeeBlockReason.OvenBusy);
                    return;
                }

                root.EmployeeTaskService.ClearTaskBlocked(currentTask.TaskId);
                _ovenStationId = ovenId;
                _ovenSlotIndex = slotIdx;
                _stepIndex = (int)WorkStep.PizzaioloWaitBake;
                PizzeriaCommandHandler.Instance?.CommitAndReplicate();
                return;
            }

            if (_stepIndex == (int)WorkStep.PizzaioloWaitBake)
            {
                if (!root.DomainState.Items.TryGetValue(_workingPizzaId, out var bakingPizza))
                {
                    ResetWorkState();
                    return;
                }

                if (bakingPizza.CookingStage != CookingStage.Baked && bakingPizza.CookingStage != CookingStage.Burned)
                    return;

                var ovenSlot = new StationSlotId(_ovenStationId, _ovenSlotIndex);
                var handLoc = LocationRef.InHand(actorKey, HandSlotIndex.Right);
                var remRes = root.OvenService.RemovePizza(root.DomainState, ovenSlot, handLoc, actorKey);
                if (!remRes.Success)
                {
                    // Caso forno já esvaziado ou mão ocupada
                    if (!bakingPizza.Location.Equals(handLoc))
                    {
                        root.EmployeeTaskService.MarkTaskBlocked(currentTask.TaskId, EmployeeBlockReason.ToolInUse);
                        return;
                    }
                }

                _stepIndex = (int)WorkStep.PizzaioloPackageAndStage;
                _stepTimer = Mathf.Max(0.5f, 1.4f - (emp.Level - 1) * 0.2f);
                PizzeriaCommandHandler.Instance?.CommitAndReplicate();
                return;
            }

            if (_stepIndex == (int)WorkStep.PizzaioloPackageAndStage)
            {
                Vector3 stagePos = ResolveStationPosition<StagingStation>(
                    ResolveStationPosition<PackagingStation>(root.transform.position));
                if (!MoveToValidated(stagePos, out bool pathBlocked))
                {
                    if (pathBlocked) root.EmployeeTaskService.MarkTaskBlocked(currentTask.TaskId, EmployeeBlockReason.PathBlocked);
                    return;
                }
                if ((transform.position - stagePos).sqrMagnitude > 2.6f * 2.6f) return;

                _stepTimer -= dt;
                if (_stepTimer > 0f) return;

                if (root.DomainState.Items.TryGetValue(_workingPizzaId, out var finishedPizza))
                {
                    if (!order.IsDineIn)
                    {
                        finishedPizza.PackagingState = PackagingState.Boxed;
                    }

                    int targetStationId = ResolvePackagingOrStagingStationId(root);
                    int freeSlot = FindFirstFreeStationSlot(root.DomainState, targetStationId, 6);
                    if (freeSlot >= 0)
                    {
                        TransferService.TransferItem(
                            root.DomainState,
                            _workingPizzaId,
                            LocationRef.InStation(targetStationId, freeSlot),
                            actorKey,
                            bypassReservation: true);
                    }
                    else
                    {
                        TransferService.TransferItem(
                            root.DomainState,
                            _workingPizzaId,
                            LocationRef.InWorld(stagePos.x, stagePos.y, stagePos.z),
                            actorKey,
                            bypassReservation: true);
                    }
                    finishedPizza.Revision++;
                }

                root.EmployeeTaskService.CompleteTask(currentTask.TaskId);
                ResetWorkState();
                PizzeriaCommandHandler.Instance?.CommitAndReplicate();
            }
        }

        private bool TryAcquireDoughIntoPrep(
            PizzeriaRoot root,
            PizzaSpecification spec,
            ulong actorKey,
            out ItemId doughId,
            out int stationId,
            out int slotIdx)
        {
            doughId = ItemId.None;
            stationId = ResolvePrepOrAssemblyStationId(root);
            slotIdx = FindFirstFreeStationSlot(root.DomainState, stationId, 4);
            if (slotIdx < 0) return false;

            // Tenta dispensar massa aberta ou massa crua de qualquer StorageSlot com estoque > 0
            int candidateDefId = spec.BaseDoughId > 0 ? spec.BaseDoughId : PizzaSpecification.DefaultBaseDoughId;
            if (!TryDispenseIngredientFromAnyStorage(root, candidateDefId, actorKey, out doughId))
            {
                if (!TryDispenseIngredientFromAnyStorage(root, 1, actorKey, out doughId))
                    return false;
            }

            var targetLoc = LocationRef.InStation(stationId, slotIdx);
            var tr = TransferService.TransferItem(root.DomainState, doughId, targetLoc, actorKey, bypassReservation: true);
            return tr.Success;
        }

        private static bool TryApplySpecIngredients(
            PizzeriaRoot root,
            ItemState pizza,
            PizzaSpecification spec,
            ulong actorKey,
            bool intentionalError,
            out EmployeeBlockReason blockReason)
        {
            blockReason = EmployeeBlockReason.None;
            if (spec.RequiresSauce && spec.SauceId > 0 && !HasIngredient(pizza, spec.SauceId))
            {
                if (!TryDispenseAndApply(root, pizza, spec.SauceId, ProcessingStage.Prepared, actorKey))
                {
                    blockReason = EmployeeBlockReason.MissingIngredient;
                    return false;
                }
            }

            if (spec.Toppings != null)
            {
                int count = spec.Toppings.Count;
                for (int i = 0; i < count; i++)
                {
                    var top = spec.Toppings[i];
                    if (intentionalError && i == count - 1)
                    {
                        // Erro intencional: omite o último ingrediente quando RNG aciona
                        continue;
                    }
                    if (!HasIngredient(pizza, top.IngredientDefId))
                    {
                        if (!TryDispenseAndApply(root, pizza, top.IngredientDefId, top.RequiredStage, actorKey))
                        {
                            blockReason = EmployeeBlockReason.MissingIngredient;
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private static bool HasIngredient(ItemState pizza, int defId)
        {
            if (pizza.Ingredients == null) return false;
            for (int i = 0; i < pizza.Ingredients.Count; i++)
            {
                if (pizza.Ingredients[i].IngredientDefId == defId) return true;
            }
            return false;
        }

        private static bool TryDispenseAndApply(
            PizzeriaRoot root,
            ItemState pizza,
            int ingredientDefId,
            ProcessingStage stage,
            ulong actorKey)
        {
            if (!TryDispenseIngredientFromAnyStorage(root, ingredientDefId, actorKey, out var ingItemId))
                return false;

            pizza.AddIngredient(ingredientDefId, stage, 1);
            TransferService.DestroyItem(root.DomainState, ingItemId);
            return true;
        }

        private static bool TryDispenseIngredientFromAnyStorage(
            PizzeriaRoot root,
            int ingredientDefId,
            ulong actorKey,
            out ItemId spawnedId)
        {
            spawnedId = ItemId.None;
            foreach (var kvp in root.DomainState.StorageSlots)
            {
                if (kvp.Key.ingredientDefId == ingredientDefId && kvp.Value != null && kvp.Value.Quantity > 0)
                {
                    var res = TransferService.DispenseFromStorage(
                        root.DomainState,
                        root.Catalog,
                        kvp.Key.storageId,
                        ingredientDefId,
                        actorKey,
                        HandSlotIndex.Right);
                    if (res.Success)
                    {
                        spawnedId = res.Value;
                        return true;
                    }
                }
            }
            return false;
        }

        private bool MoveToValidated(Vector3 destination, out bool pathBlocked)
        {
            pathBlocked = false;
            if (_agent == null || !_agent.enabled) return true;

            if (!NavMesh.SamplePosition(destination, out var hit, 4.0f, NavMesh.AllAreas))
            {
                pathBlocked = true;
                return false;
            }

            if (!_agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(transform.position, out var selfHit, 6.0f, NavMesh.AllAreas))
                {
                    _agent.Warp(selfHit.position);
                }
                else
                {
                    pathBlocked = true;
                    return false;
                }
            }

            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(_agent.transform.position, hit.position, NavMesh.AllAreas, path) ||
                path.status != NavMeshPathStatus.PathComplete)
            {
                pathBlocked = true;
                return false;
            }

            _agent.SetDestination(hit.position);
            return true;
        }

        private void ResetWorkState()
        {
            _activeTaskId = -1;
            _stepIndex = (int)WorkStep.Idle;
            _stepTimer = 0f;
            _workingPizzaId = ItemId.None;
            _ovenStationId = -1;
            _ovenSlotIndex = -1;
            _prepStationId = -1;
            _prepSlotIndex = -1;
        }

        private static Vector3 ResolveStationPosition<T>(Vector3 fallback) where T : Component
        {
            var found = FindFirstObjectByType<T>();
            return found != null ? found.transform.position : fallback;
        }

        private static int ResolvePrepOrAssemblyStationId(PizzeriaRoot root)
        {
            var prep = FindFirstObjectByType<PrepStation>();
            if (prep != null && prep.TryGetComponent<StationView>(out var sv)) return sv.stationId;
            var asm = FindFirstObjectByType<AssemblyStation>();
            if (asm != null && asm.TryGetComponent<StationView>(out var sv2)) return sv2.stationId;
            return 2;
        }

        private static int ResolvePackagingOrStagingStationId(PizzeriaRoot root)
        {
            var stg = FindFirstObjectByType<StagingStation>();
            if (stg != null && stg.TryGetComponent<StationView>(out var sv)) return sv.stationId;
            var pkg = FindFirstObjectByType<PackagingStation>();
            if (pkg != null && pkg.TryGetComponent<StationView>(out var sv2)) return sv2.stationId;
            return 5;
        }

        private static bool TryFindFreeOvenSlot(PizzeriaRoot root, out int ovenStationId, out int slotIndex)
        {
            ovenStationId = 4;
            var oven = FindFirstObjectByType<OvenStation>();
            if (oven != null && oven.TryGetComponent<StationView>(out var sv))
            {
                ovenStationId = sv.stationId;
            }

            int maxSlots = 4;
            if (root.Catalog != null && root.Catalog.Stations.TryGetValue(ovenStationId, out var stData))
            {
                maxSlots = Math.Max(1, stData.SlotCount);
            }

            slotIndex = FindFirstFreeStationSlot(root.DomainState, ovenStationId, maxSlots);
            return slotIndex >= 0;
        }

        private static int FindFirstFreeStationSlot(PizzeriaState state, int stationId, int maxSlots)
        {
            for (int i = 0; i < maxSlots; i++)
            {
                if (!TransferService.IsLocationOccupied(state, LocationRef.InStation(stationId, i)))
                    return i;
            }
            return -1;
        }
    }

    /// <summary>
    /// Sincroniza GameObjects visuais/NavMesh de funcionários contratados e projeta seu estado para NetEmployeeDto.
    /// </summary>
    public static class EmployeePopulationDirector
    {
        private static readonly Dictionary<int, EmployeeAgentController> _spawned = new Dictionary<int, EmployeeAgentController>();

        public static void TickAndProjectServer(PizzeriaRoot root, PizzeriaNetworkState netState, float dt)
        {
            if (root == null || !root.IsServer || root.EmployeeTaskService == null) return;

            SyncOrderTasksFromDomain(root);

            var deadKeys = new List<int>();
            foreach (var kvp in _spawned)
            {
                if (kvp.Value == null || !root.EmployeeTaskService.Employees.ContainsKey(kvp.Key))
                {
                    if (kvp.Value != null) UnityEngine.Object.Destroy(kvp.Value.gameObject);
                    deadKeys.Add(kvp.Key);
                }
            }
            for (int i = 0; i < deadKeys.Count; i++) _spawned.Remove(deadKeys[i]);

            foreach (var kvp in root.EmployeeTaskService.Employees)
            {
                var emp = kvp.Value;
                if (!_spawned.TryGetValue(emp.EmployeeId, out var ctrl) || ctrl == null)
                {
                    ctrl = SpawnEmployeeBody(root, emp);
                    _spawned[emp.EmployeeId] = ctrl;
                }
                ctrl.TickServer(root, dt);
            }

            if (netState != null && netState.Employees != null)
            {
                var desired = new List<NetEmployeeDto>();
                foreach (var kvp in root.EmployeeTaskService.Employees)
                {
                    var emp = kvp.Value;
                    SharedEmployeeTask task = null;
                    if (emp.AssignedTaskId > 0)
                    {
                        root.EmployeeTaskService.Tasks.TryGetValue(emp.AssignedTaskId, out task);
                    }
                    Vector3 pos = _spawned.TryGetValue(emp.EmployeeId, out var ctrl) && ctrl != null
                        ? ctrl.transform.position
                        : root.transform.position;

                    desired.Add(new NetEmployeeDto
                    {
                        EmployeeId = emp.EmployeeId,
                        Role = (byte)emp.Role,
                        Level = (byte)Mathf.Clamp(emp.Level, 1, 255),
                        IsAlive = emp.IsAlive,
                        TaskState = task != null ? (byte)task.State : (byte)EmployeeTaskState.Available,
                        BlockReason = task != null ? (byte)task.BlockReason : (byte)EmployeeBlockReason.None,
                        AssignedTaskId = emp.AssignedTaskId,
                        TargetOrderId = task != null ? (int)task.TargetOrderId.Value : 0,
                        IntentionalErrorTriggered = task != null && task.IntentionalErrorTriggered,
                        PosX = pos.x,
                        PosY = pos.y,
                        PosZ = pos.z
                    });
                }

                bool differs = netState.Employees.Count != desired.Count;
                if (!differs)
                {
                    for (int i = 0; i < desired.Count; i++)
                    {
                        if (!netState.Employees[i].Equals(desired[i]))
                        {
                            differs = true;
                            break;
                        }
                    }
                }
                if (differs)
                {
                    netState.Employees.Clear();
                    for (int i = 0; i < desired.Count; i++) netState.Employees.Add(desired[i]);
                }
            }
        }

        public static void ClearAll()
        {
            foreach (var kvp in _spawned)
            {
                if (kvp.Value != null) UnityEngine.Object.Destroy(kvp.Value.gameObject);
            }
            _spawned.Clear();
        }

        private static void SyncOrderTasksFromDomain(PizzeriaRoot root)
        {
            if (root.DomainState == null) return;
            foreach (var kvp in root.DomainState.Orders)
            {
                var order = kvp.Value;
                if (order == null || order.Lifecycle != OrderLifecycle.Accepted) continue;
                if (!root.EmployeeTaskService.TryGetTaskForOrder(order.Id, EmployeeRole.Pizzaiolo, out _))
                {
                    // Só cria se ainda não houver tarefa concluída/ativa para este pedido
                    bool alreadyTracked = false;
                    foreach (var t in root.EmployeeTaskService.Tasks.Values)
                    {
                        if (t.TargetOrderId.Equals(order.Id) && t.RequiredRole == EmployeeRole.Pizzaiolo)
                        {
                            alreadyTracked = true;
                            break;
                        }
                    }
                    if (!alreadyTracked)
                    {
                        root.EmployeeTaskService.EnqueueOrderTask(order.Id, EmployeeRole.Pizzaiolo);
                    }
                }
            }
        }

        private static EmployeeAgentController SpawnEmployeeBody(PizzeriaRoot root, HiredEmployeeState emp)
        {
            Vector3 spawnPos = root.transform.position + new Vector3((emp.EmployeeId % 3 - 1) * 1.3f, 0f, 1.0f);
            if (NavMesh.SamplePosition(spawnPos, out var hit, 5f, NavMesh.AllAreas))
            {
                spawnPos = hit.position;
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = $"Employee_{emp.Role}_{emp.EmployeeId}";
            go.transform.position = spawnPos;
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null && renderer.material != null)
            {
                renderer.material.color = emp.Role == EmployeeRole.Cashier
                    ? new Color(0.20f, 0.55f, 0.85f)
                    : new Color(0.92f, 0.90f, 0.82f);
            }

            var ctrl = go.AddComponent<EmployeeAgentController>();
            ctrl.Initialize(emp.EmployeeId, emp.Role);
            return ctrl;
        }
    }
}
