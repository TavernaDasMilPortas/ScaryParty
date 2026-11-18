using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using ScaryParty.Combat;
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
    public class EmployeeAgentController : MonoBehaviour, IDamageable
    {
        public const ulong EmployeeActorOffset = 1_000_000UL;

        public int EmployeeId { get; private set; }
        public EmployeeRole Role { get; private set; }
        public bool IsAlive => _isClientProxy ? _clientIsAlive : _serverIsAlive;
        public int CurrentHpInt => Mathf.CeilToInt(_hp);
        public int MaxHpInt => 100;
        public uint Revision { get; private set; } = 1;

        private NavMeshAgent _agent;
        private Animator _animator;
        private Collider _mainCollider;
        private Transform _visualRoot;
        private Renderer[] _bodyRenderers;
        private MaterialPropertyBlock _flashMpb;
        private float _damageFlashTimer;
        private readonly System.Random _fallbackRng = new System.Random();

        private bool _isClientProxy;
        private bool _serverIsAlive = true;
        private Vector3 _clientTargetPos;
        private float _clientTargetYaw;
        private bool _hasClientTarget;
        private bool _clientIsAlive = true;
        private int _clientLastHp = 100;

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

        public void Initialize(int employeeId, EmployeeRole role, bool isClientProxy = false)
        {
            EmployeeId = employeeId;
            Role = role;
            _isClientProxy = isClientProxy;
            _serverIsAlive = true;
            _clientIsAlive = true;
            _hp = 100f;
            _clientLastHp = 100;
            _mainCollider = GetComponent<Collider>();
            _visualRoot = transform.Find("VisualRoot") ?? transform;
            _animator = GetComponentInChildren<Animator>(true);
            _bodyRenderers = _visualRoot.GetComponentsInChildren<Renderer>(true);
            if (!isClientProxy)
            {
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
        }

        public void ApplyReplicatedDto(NetEmployeeDto dto, bool snapImmediately = false)
        {
            if (_hasClientTarget && dto.CurrentHp < _clientLastHp)
            {
                _damageFlashTimer = 0.18f;
            }
            _clientLastHp = dto.CurrentHp;
            _clientIsAlive = dto.IsAlive;
            _clientTargetPos = new Vector3(dto.PosX, dto.PosY, dto.PosZ);
            _clientTargetYaw = dto.Yaw;
            Revision = dto.Revision;
            if (snapImmediately || !_hasClientTarget || (_clientTargetPos - transform.position).sqrMagnitude > 36f)
            {
                transform.position = _clientTargetPos;
                transform.rotation = Quaternion.Euler(0f, _clientTargetYaw, 0f);
            }
            _hasClientTarget = true;
            if (_mainCollider != null && _mainCollider.enabled != _clientIsAlive)
            {
                _mainCollider.enabled = _clientIsAlive;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            UpdateVisualFeedback(dt, IsAlive);

            if (!_isClientProxy || !_hasClientTarget) return;
            if (!_clientIsAlive)
            {
                DriveAnimatorLocomotion(0f);
                if (_visualRoot == transform)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(-90f, _clientTargetYaw, 0f), Mathf.Clamp01(dt * 8f));
                }
                return;
            }

            Vector3 prev = transform.position;
            Vector3 next = Vector3.Lerp(prev, _clientTargetPos, Mathf.Clamp01(dt * 12f));
            Vector3 delta = next - prev;
            delta.y = 0f;
            transform.position = next;
            if (delta.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(delta.normalized, Vector3.up), Mathf.Clamp01(dt * 12f));
            }
            else
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, _clientTargetYaw, 0f), Mathf.Clamp01(dt * 10f));
            }
            float estSpeed = dt > 0.0001f ? delta.magnitude / dt : 0f;
            DriveAnimatorLocomotion(estSpeed);
        }

        private void UpdateVisualFeedback(float dt, bool alive)
        {
            if (_visualRoot != null && _visualRoot != transform)
            {
                float targetPitch = !alive ? -90f : 0f;
                Vector3 targetLocalPos = !alive ? new Vector3(0f, 0.25f, 0f) : Vector3.zero;
                _visualRoot.localRotation = Quaternion.Slerp(_visualRoot.localRotation, Quaternion.Euler(targetPitch, 0f, 0f), Mathf.Clamp01(dt * 10f));
                _visualRoot.localPosition = Vector3.Lerp(_visualRoot.localPosition, targetLocalPos, Mathf.Clamp01(dt * 14f));
            }

            if (_damageFlashTimer > 0f)
            {
                _damageFlashTimer = Mathf.Max(0f, _damageFlashTimer - dt);
                if (_bodyRenderers != null)
                {
                    _flashMpb ??= new MaterialPropertyBlock();
                    Color emit = _damageFlashTimer > 0f ? new Color(0.85f, 0.15f, 0.15f, 1f) : Color.black;
                    for (int i = 0; i < _bodyRenderers.Length; i++)
                    {
                        var r = _bodyRenderers[i];
                        if (r == null) continue;
                        r.GetPropertyBlock(_flashMpb);
                        _flashMpb.SetColor("_EmissionColor", emit);
                        r.SetPropertyBlock(_flashMpb);
                    }
                }
            }
        }

        private void DriveAnimatorLocomotion(float speed)
        {
            if (_animator == null) return;
            _animator.SetFloat("Speed", speed);
            _animator.SetFloat("MotionSpeed", speed > 0.1f ? 1f : 0f);
            _animator.SetBool("Grounded", true);
            _animator.SetBool("IsMoving", speed > 0.15f);
        }

        public void ApplyDamageServer(DamageEvent evt)
        {
            TakeDamageServer(evt.Amount);
        }

        public void TakeDamageServer(float damage)
        {
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.EmployeeTaskService == null) return;
            if (!root.EmployeeTaskService.Employees.TryGetValue(EmployeeId, out var emp) || !emp.IsAlive) return;

            _hp = Mathf.Max(0f, _hp - Mathf.Max(0f, damage));
            _damageFlashTimer = 0.18f;
            Revision++;
            if (_hp <= 0f)
            {
                _hp = 0f;
                emp.IsAlive = false;
                _serverIsAlive = false;
                emp.AssignedTaskId = -1;
                root.EmployeeTaskService.ReleaseActorTasks((ulong)EmployeeId, isHuman: false);
                ResetWorkState();
                if (_mainCollider != null) _mainCollider.enabled = false;
                if (_agent != null && _agent.enabled)
                {
                    if (_agent.isOnNavMesh) _agent.isStopped = true;
                    _agent.enabled = false;
                }
            }
        }

        public void TickServer(PizzeriaRoot root, float dt)
        {
            if (root == null || !root.IsServer || root.DomainState == null || root.EmployeeTaskService == null) return;
            if (!root.EmployeeTaskService.Employees.TryGetValue(EmployeeId, out var emp)) return;

            _serverIsAlive = emp.IsAlive;
            if (!emp.IsAlive)
            {
                if (_mainCollider != null && _mainCollider.enabled) _mainCollider.enabled = false;
                if (_agent != null && _agent.enabled) _agent.enabled = false;
                DriveAnimatorLocomotion(0f);
                if (_visualRoot == transform)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(-90f, transform.eulerAngles.y, 0f), Mathf.Clamp01(dt * 8f));
                }
                return;
            }

            if (_hp <= 0f) _hp = 100f;
            if (_mainCollider != null && !_mainCollider.enabled) _mainCollider.enabled = true;
            if (_agent != null && !_agent.enabled) _agent.enabled = true;
            if (_agent != null && _agent.enabled && _agent.isStopped) _agent.isStopped = false;
            DriveAnimatorLocomotion(_agent != null && _agent.enabled ? _agent.velocity.magnitude : 0f);

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
                float baseAttend = root.Config != null ? root.Config.cashierAttendBaseSeconds : 2.2f;
                _stepTimer = Mathf.Max(0.8f, baseAttend - (emp.Level - 1) * 0.35f);
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
                root.EmployeeTaskService.CancelTask(currentTask.TaskId);
                ResetWorkState();
                return;
            }

            if (_rolledTaskId != currentTask.TaskId)
            {
                _rolledTaskId = currentTask.TaskId;
                currentTask.TotalUnitsRequired = EmployeeTaskService.ComputeTotalOrderUnits(order);
                var stats = root.RunProgressionService != null
                    ? root.RunProgressionService.RecalculateEffectiveStats()
                    : new RunEffectiveStats();
                var rng = root.RunProgressionService != null ? root.RunProgressionService.EmployeeErrorRng : _fallbackRng;
                currentTask.IntentionalErrorTriggered = root.EmployeeTaskService.RollIntentionalSkillError(
                    EmployeeId, rng, stats.EmployeeErrorMultiplier);
            }

            if (currentTask.CurrentLineIndex >= order.Lines.Count)
            {
                root.EmployeeTaskService.CompleteTask(currentTask.TaskId);
                ResetWorkState();
                return;
            }

            var activeLine = order.Lines[currentTask.CurrentLineIndex];
            var spec = activeLine.Specification ?? PizzaSpecification.CreateDefaultCheese();
            ulong actorKey = EmployeeActorOffset + (ulong)EmployeeId;

            if (_stepIndex == (int)WorkStep.PizzaioloPrepareDough || _stepIndex == (int)WorkStep.Idle)
            {
                // Não duplica unidades que humanos já finalizaram e disponibilizaram na bancada para esta linha
                if (!_workingPizzaId.IsValid && CountReadyMatchingPizzasForLine(root, order, spec) >= activeLine.Quantity)
                {
                    if (root.EmployeeTaskService.AdvanceUnitProgress(currentTask.TaskId, order))
                    {
                        ResetWorkState();
                    }
                    else
                    {
                        ResetUnitState();
                    }
                    PizzeriaCommandHandler.Instance?.CommitAndReplicate();
                    return;
                }

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
                    var prepSlot = new StationSlotId(_prepStationId, _prepSlotIndex);
                    if (root.DomainState.Items.TryGetValue(_workingPizzaId, out var rawDough) &&
                        rawDough.PrepStage == ProcessingStage.Raw)
                    {
                        var workRes = root.ProcessingService.StartWork(prepSlot, 1, actorKey, root.DomainState, root.Catalog, root.Clock);
                        if (!workRes.Success)
                        {
                            root.EmployeeTaskService.MarkTaskBlocked(currentTask.TaskId, EmployeeBlockReason.ToolInUse);
                            return;
                        }
                    }
                    root.EmployeeTaskService.ClearTaskBlocked(currentTask.TaskId);
                    float baseStep = root.Config != null ? root.Config.pizzaioloStepBaseSeconds : 1.8f;
                    _stepTimer = Mathf.Max(0.75f, baseStep - (emp.Level - 1) * 0.25f);
                }

                _stepTimer -= dt;
                if (_stepTimer > 0f) return;

                if (root.DomainState.Items.TryGetValue(_workingPizzaId, out var dough))
                {
                    if (dough.PrepStage == ProcessingStage.Raw)
                    {
                        var prepSlot = new StationSlotId(_prepStationId, _prepSlotIndex);
                        var compRes = root.ProcessingService.CompleteWork(prepSlot, root.DomainState, root.Catalog);
                        if (!compRes.Success)
                        {
                            dough.PrepStage = ProcessingStage.Prepared;
                            dough.BaseDoughStage = ProcessingStage.Prepared;
                            dough.Revision++;
                        }
                    }
                    _stepIndex = (int)WorkStep.PizzaioloAssembleToppings;
                    float baseStep2 = root.Config != null ? root.Config.pizzaioloStepBaseSeconds : 1.8f;
                    _stepTimer = Mathf.Max(0.75f, baseStep2 - (emp.Level - 1) * 0.25f);
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
                float bakeDur = spec.BakeDuration > 0.1f ? spec.BakeDuration : (root.Config != null ? root.Config.defaultBakeDuration : 15f);
                var insertRes = root.ProcessingService.InsertOven(
                    _workingPizzaId,
                    ovenSlot,
                    root.DomainState,
                    bakeDur,
                    root.Clock);

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

                if (root.EmployeeTaskService.AdvanceUnitProgress(currentTask.TaskId, order))
                {
                    ResetWorkState();
                }
                else
                {
                    ResetUnitState();
                }
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
            if (spec.RequiresSauce && spec.SauceId > 0)
            {
                int targetSauce = Math.Max(1, spec.SaucePortions);
                while (GetIngredientCount(pizza, spec.SauceId) < targetSauce)
                {
                    if (!TryDispenseAndProcessIngredient(root, pizza, spec.SauceId, ProcessingStage.Prepared, actorKey, out blockReason))
                        return false;
                }
            }

            if (spec.Toppings != null)
            {
                int count = spec.Toppings.Count;
                for (int i = 0; i < count; i++)
                {
                    var top = spec.Toppings[i];
                    int targetPortions = Math.Max(1, top.PortionCount);
                    if (intentionalError && i == count - 1)
                    {
                        targetPortions = Math.Max(0, targetPortions - 1);
                    }
                    while (GetIngredientCount(pizza, top.IngredientDefId) < targetPortions)
                    {
                        if (!TryDispenseAndProcessIngredient(root, pizza, top.IngredientDefId, top.RequiredStage, actorKey, out blockReason))
                            return false;
                    }
                }
            }

            return true;
        }

        private static int GetIngredientCount(ItemState pizza, int defId)
        {
            if (pizza.Ingredients == null) return 0;
            int total = 0;
            for (int i = 0; i < pizza.Ingredients.Count; i++)
            {
                if (pizza.Ingredients[i].IngredientDefId == defId)
                    total += Math.Max(1, pizza.Ingredients[i].Count);
            }
            return total;
        }

        private static bool TryDispenseAndProcessIngredient(
            PizzeriaRoot root,
            ItemState pizza,
            int ingredientDefId,
            ProcessingStage stage,
            ulong actorKey,
            out EmployeeBlockReason failReason)
        {
            failReason = EmployeeBlockReason.None;

            // 1. Tenta dispensar o ingrediente diretamente no estágio esperado
            if (TryDispenseIngredientFromAnyStorage(root, ingredientDefId, actorKey, out var ingItemId))
            {
                return ApplyIngredientViaAssembly(root, pizza, ingItemId, ingredientDefId, stage, actorKey);
            }

            // 2. Se requer estágio Prepared, procura processo que transforme ingrediente cru (ex: Queijo 4->5, Calabresa 6->7, Cogumelo 8->9)
            if (stage == ProcessingStage.Prepared && root.Catalog != null)
            {
                ProcessData matchingProc = null;
                foreach (var kvp in root.Catalog.Processes)
                {
                    if (kvp.Value != null && kvp.Value.OutputIngredientId == ingredientDefId)
                    {
                        matchingProc = kvp.Value;
                        break;
                    }
                }

                if (matchingProc != null &&
                    TryDispenseIngredientFromAnyStorage(root, matchingProc.InputIngredientId, actorKey, out var rawItemId))
                {
                    int prepStationId = ResolvePrepOrAssemblyStationId(root);
                    int prepSlotIdx = FindFirstFreeStationSlot(root.DomainState, prepStationId, 4);
                    if (prepSlotIdx < 0)
                    {
                        TransferService.DestroyItem(root.DomainState, rawItemId);
                        failReason = EmployeeBlockReason.ToolInUse;
                        return false;
                    }

                    // Reserva ferramenta necessária se o processo exigir
                    ToolItemState leasedTool = null;
                    if (matchingProc.RequiredCapability != ToolCapability.None)
                    {
                        double now = root.Clock != null ? root.Clock.Now : Time.timeAsDouble;
                        foreach (var tKvp in root.DomainState.Tools)
                        {
                            var tool = tKvp.Value;
                            if (tool != null && tool.Capabilities.HasFlag(matchingProc.RequiredCapability) &&
                                tool.TryAcquireLease(actorKey, now, 10.0))
                            {
                                leasedTool = tool;
                                break;
                            }
                        }
                        if (leasedTool == null)
                        {
                            failReason = EmployeeBlockReason.ToolInUse;
                            return false;
                        }
                    }

                    var prepSlot = new StationSlotId(prepStationId, prepSlotIdx);
                    TransferService.TransferItem(root.DomainState, rawItemId, LocationRef.InStation(prepStationId, prepSlotIdx), actorKey, bypassReservation: true);
                    var beginRes = root.ProcessingService.BeginOperation(
                        root.DomainState,
                        prepSlot,
                        OperationKind.Process,
                        ResumePolicy.Resumable,
                        rawItemId,
                        leasedTool != null ? leasedTool.Id : ToolItemId.None,
                        matchingProc.Id,
                        actorKey,
                        root.Catalog,
                        (float)(root.Clock != null ? root.Clock.Now : Time.timeAsDouble),
                        10f);

                    if (beginRes.Success)
                    {
                        root.ProcessingService.CompleteWork(prepSlot, root.DomainState, root.Catalog);
                    }
                    leasedTool?.ReleaseLease(actorKey);

                    TransferService.TransferItem(root.DomainState, rawItemId, LocationRef.InHand(actorKey, HandSlotIndex.Right), actorKey, bypassReservation: true);
                    return ApplyIngredientViaAssembly(root, pizza, rawItemId, ingredientDefId, stage, actorKey);
                }
            }

            failReason = EmployeeBlockReason.MissingIngredient;
            return false;
        }

        private static bool ApplyIngredientViaAssembly(
            PizzeriaRoot root,
            ItemState pizza,
            ItemId ingItemId,
            int fallbackDefId,
            ProcessingStage fallbackStage,
            ulong actorKey)
        {
            if (pizza.Location.Type == LocationType.StationSlot)
            {
                var slot = new StationSlotId((int)pizza.Location.HolderId, pizza.Location.SlotId);
                float now = (float)(root.Clock != null ? root.Clock.Now : Time.timeAsDouble);
                if (PizzaAssemblyService.Begin(root.DomainState, pizza.Id, ingItemId, actorKey, now, 0.2f).Success &&
                    PizzaAssemblyService.Complete(root.DomainState, slot))
                {
                    return true;
                }
            }

            pizza.AddIngredient(fallbackDefId, fallbackStage, 1);
            TransferService.DestroyItem(root.DomainState, ingItemId);
            return true;
        }

        private static int CountReadyMatchingPizzasForLine(PizzeriaRoot root, OrderState order, PizzaSpecification spec)
        {
            if (root.DomainState == null) return 0;
            int targetStationId = ResolvePackagingOrStagingStationId(root);
            int matches = 0;
            var specEvaluator = new PizzaSpecificationEvaluator();
            foreach (var kvp in root.DomainState.Items)
            {
                var it = kvp.Value;
                if (it == null || !it.IsPizzaLike) continue;
                if (it.Location.Type != LocationType.StationSlot || (int)it.Location.HolderId != targetStationId) continue;
                if (it.CookingStage != CookingStage.Baked) continue;
                if (!order.IsDineIn && it.PackagingState != PackagingState.Boxed) continue;
                var eval = specEvaluator.Evaluate(it, spec);
                if (eval.IsExactMatch) matches++;
            }
            return matches;
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

        private void ResetUnitState()
        {
            _stepIndex = (int)WorkStep.PizzaioloPrepareDough;
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
        private static readonly Dictionary<int, EmployeeAgentController> _clientProxies = new Dictionary<int, EmployeeAgentController>();
        private static readonly List<NetEmployeeDto> _clientSyncBuffer = new List<NetEmployeeDto>();
        private static PizzeriaNetworkState _subscribedNetState;
        private static bool _hooksRegistered;

        public static int ServerSpawnedCount => _spawned.Count;
        public static int ClientProxyCount => _clientProxies.Count;
        public static bool TryGetClientProxy(int employeeId, out EmployeeAgentController proxy) => _clientProxies.TryGetValue(employeeId, out proxy);

        private static void DestroyObjectSafe(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(obj);
            else
                UnityEngine.Object.DestroyImmediate(obj);
        }

        public static void TickAndProjectServer(PizzeriaRoot root, PizzeriaNetworkState netState, float dt)
        {
            if (root == null || !root.IsServer || root.EmployeeTaskService == null) return;

            SyncOrderTasksFromDomain(root);

            var deadKeys = new List<int>();
            foreach (var kvp in _spawned)
            {
                if (kvp.Value == null || !root.EmployeeTaskService.Employees.ContainsKey(kvp.Key))
                {
                    if (kvp.Value != null) DestroyObjectSafe(kvp.Value.gameObject);
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

            if (netState != null && netState.IsSpawned && netState.Employees != null)
            {
                var runStats = root.RunProgressionService != null ? root.RunProgressionService.RecalculateEffectiveStats() : new RunEffectiveStats();
                var activeIds = new HashSet<int>();
                foreach (var kvp in root.EmployeeTaskService.Employees)
                {
                    var emp = kvp.Value;
                    activeIds.Add(emp.EmployeeId);
                    SharedEmployeeTask task = null;
                    if (emp.AssignedTaskId > 0)
                    {
                        root.EmployeeTaskService.Tasks.TryGetValue(emp.AssignedTaskId, out task);
                    }
                    bool hasCtrl = _spawned.TryGetValue(emp.EmployeeId, out var ctrl) && ctrl != null;
                    Vector3 pos = hasCtrl ? ctrl.transform.position : root.transform.position;
                    float yaw = hasCtrl ? ctrl.transform.eulerAngles.y : 0f;
                    int curHp = hasCtrl ? ctrl.CurrentHpInt : (emp.IsAlive ? 100 : 0);
                    int maxHp = hasCtrl ? ctrl.MaxHpInt : 100;
                    uint rev = hasCtrl ? ctrl.Revision : 1u;

                    float errProb = root.EmployeeTaskService.ComputeEffectiveErrorProbability(emp, runStats.EmployeeErrorMultiplier);
                    var dto = new NetEmployeeDto
                    {
                        EmployeeId = emp.EmployeeId,
                        Role = (byte)emp.Role,
                        Level = (byte)Mathf.Clamp(emp.Level, 1, 255),
                        IsAlive = emp.IsAlive,
                        CurrentHp = curHp,
                        MaxHp = maxHp,
                        TaskState = task != null ? (byte)task.State : (byte)EmployeeTaskState.Available,
                        BlockReason = task != null ? (byte)task.BlockReason : (byte)EmployeeBlockReason.None,
                        AssignedTaskId = emp.AssignedTaskId,
                        TargetOrderId = task != null ? (int)task.TargetOrderId.Value : 0,
                        CurrentLineIndex = task != null ? (byte)Mathf.Clamp(task.CurrentLineIndex, 0, 255) : (byte)0,
                        UnitsCompleted = task != null ? (byte)Mathf.Clamp(task.TotalUnitsCompleted, 0, 255) : (byte)0,
                        UnitsRequired = task != null ? (byte)Mathf.Clamp(task.TotalUnitsRequired, 0, 255) : (byte)0,
                        SkillErrorPercent = (byte)Mathf.Clamp(Mathf.RoundToInt(errProb * 100f), 0, 100),
                        IntentionalErrorTriggered = task != null && task.IntentionalErrorTriggered,
                        PosX = pos.x,
                        PosY = pos.y,
                        PosZ = pos.z,
                        Yaw = yaw,
                        Revision = rev
                    };

                    int idx = -1;
                    for (int j = 0; j < netState.Employees.Count; j++)
                    {
                        if (netState.Employees[j].EmployeeId == dto.EmployeeId)
                        {
                            idx = j;
                            break;
                        }
                    }
                    if (idx >= 0)
                    {
                        if (!netState.Employees[idx].Equals(dto))
                        {
                            netState.Employees[idx] = dto;
                        }
                    }
                    else
                    {
                        netState.Employees.Add(dto);
                    }
                }

                for (int j = netState.Employees.Count - 1; j >= 0; j--)
                {
                    if (!activeIds.Contains(netState.Employees[j].EmployeeId))
                    {
                        netState.Employees.RemoveAt(j);
                    }
                }
            }
        }

        public static void ClearAll()
        {
            UnsubscribeClient();
            foreach (var kvp in _spawned)
            {
                if (kvp.Value != null) DestroyObjectSafe(kvp.Value.gameObject);
            }
            _spawned.Clear();
            ClearClientProxiesOnly();

            var ns = PizzeriaNetworkState.Instance;
            if (ns != null && ns.IsSpawned && ns.Employees != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                ns.Employees.Clear();
            }
        }

        private static void ClearClientProxiesOnly()
        {
            foreach (var kvp in _clientProxies)
            {
                if (kvp.Value != null) DestroyObjectSafe(kvp.Value.gameObject);
            }
            _clientProxies.Clear();
        }

        public static void EnsureClientSubscribedAndTick(PizzeriaNetworkState netState)
        {
            var root = PizzeriaRoot.Instance;
            if (root != null && root.IsServer)
            {
                ClearClientProxiesOnly();
                return;
            }

            if (netState == null || !netState.IsSpawned || netState.Employees == null) return;
            if (_subscribedNetState != netState)
            {
                UnsubscribeClient();
                _subscribedNetState = netState;
                _subscribedNetState.Employees.OnListChanged += OnClientEmployeesListChanged;
                if (!_hooksRegistered)
                {
                    _hooksRegistered = true;
                    PizzeriaNetworkState.OnNetworkStateDespawning += HandleNetStateDespawning;
                }
                SyncClientProxiesFromList(netState);
            }
        }

        public static void TickClientProxies(PizzeriaNetworkState netState)
        {
            EnsureClientSubscribedAndTick(netState);
        }

        private static void UnsubscribeClient()
        {
            if (_subscribedNetState != null && _subscribedNetState.Employees != null)
            {
                _subscribedNetState.Employees.OnListChanged -= OnClientEmployeesListChanged;
            }
            _subscribedNetState = null;
            if (_hooksRegistered)
            {
                _hooksRegistered = false;
                PizzeriaNetworkState.OnNetworkStateDespawning -= HandleNetStateDespawning;
            }
        }

        private static void HandleNetStateDespawning(PizzeriaNetworkState ns)
        {
            if (ns == _subscribedNetState)
            {
                UnsubscribeClient();
                ClearClientProxiesOnly();
            }
        }

        private static void OnClientEmployeesListChanged(NetworkListEvent<NetEmployeeDto> changeEvent)
        {
            var root = PizzeriaRoot.Instance;
            if (root != null && root.IsServer) return;
            if (_subscribedNetState != null && _subscribedNetState.IsSpawned)
            {
                SyncClientProxiesFromList(_subscribedNetState);
            }
        }

        public static void SyncClientProxiesFromList(PizzeriaNetworkState netState)
        {
            var root = PizzeriaRoot.Instance;
            if (root != null && root.IsServer)
            {
                ClearClientProxiesOnly();
                return;
            }
            if (netState == null || !netState.IsSpawned || netState.Employees == null)
            {
                ClearClientProxiesOnly();
                return;
            }

            _clientSyncBuffer.Clear();
            for (int i = 0; i < netState.Employees.Count; i++)
            {
                _clientSyncBuffer.Add(netState.Employees[i]);
            }
            SyncClientProxiesFromSnapshot(_clientSyncBuffer);
        }

        /// <summary>
        /// Sincroniza proxies client-side de funcionários a partir de um snapshot replicado.
        /// </summary>
        public static void SyncClientProxiesFromSnapshot(IReadOnlyList<NetEmployeeDto> snapshot)
        {
            var root = PizzeriaRoot.Instance;
            if (root != null && root.IsServer)
            {
                ClearClientProxiesOnly();
                return;
            }
            if (snapshot == null || snapshot.Count == 0)
            {
                ClearClientProxiesOnly();
                return;
            }

            var aliveIds = new HashSet<int>();
            for (int i = 0; i < snapshot.Count; i++)
            {
                var dto = snapshot[i];
                aliveIds.Add(dto.EmployeeId);
                if (_clientProxies.TryGetValue(dto.EmployeeId, out var proxy) && proxy != null)
                {
                    proxy.ApplyReplicatedDto(dto);
                }
                else
                {
                    _clientProxies[dto.EmployeeId] = SpawnEmployeeClientProxy(dto);
                }
            }

            var toRemove = new List<int>();
            foreach (var kvp in _clientProxies)
            {
                if (kvp.Value == null || !aliveIds.Contains(kvp.Key))
                    toRemove.Add(kvp.Key);
            }
            for (int i = 0; i < toRemove.Count; i++)
            {
                if (_clientProxies.TryGetValue(toRemove[i], out var p) && p != null)
                    DestroyObjectSafe(p.gameObject);
                _clientProxies.Remove(toRemove[i]);
            }
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

            var go = new GameObject($"Employee_{emp.Role}_{emp.EmployeeId}");
            go.transform.position = spawnPos;
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.9f, 0f);
            capsule.height = 1.8f;
            capsule.radius = 0.35f;

            BuildEmployeeVisual(go, emp.Role);

            var ctrl = go.AddComponent<EmployeeAgentController>();
            ctrl.Initialize(emp.EmployeeId, emp.Role, isClientProxy: false);
            return ctrl;
        }

        private static EmployeeAgentController SpawnEmployeeClientProxy(NetEmployeeDto dto)
        {
            var role = (EmployeeRole)dto.Role;
            var go = new GameObject($"EmployeeProxy_{role}_{dto.EmployeeId}");
            go.transform.position = new Vector3(dto.PosX, dto.PosY, dto.PosZ);
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.9f, 0f);
            capsule.height = 1.8f;
            capsule.radius = 0.35f;

            BuildEmployeeVisual(go, role);

            var ctrl = go.AddComponent<EmployeeAgentController>();
            ctrl.Initialize(dto.EmployeeId, role, isClientProxy: true);
            ctrl.ApplyReplicatedDto(dto, snapImmediately: true);
            return ctrl;
        }

        private static void BuildEmployeeVisual(GameObject go, EmployeeRole role)
        {
            var visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(go.transform, false);
            visualRoot.transform.localPosition = Vector3.zero;
            visualRoot.transform.localRotation = Quaternion.identity;

            Color tint = role == EmployeeRole.Cashier
                ? new Color(0.20f, 0.55f, 0.85f)
                : new Color(0.92f, 0.90f, 0.82f);

            GameObject visualPrefab = ScaryParty.VisualStyles.VisualStyleManager.Active?.activeLibrary?.styledCharacterVariantPrefab;
            if (visualPrefab == null) visualPrefab = Resources.Load<GameObject>("CharacterVisual");
            if (visualPrefab != null)
            {
                var inst = UnityEngine.Object.Instantiate(visualPrefab, visualRoot.transform, false);
                inst.name = "CharacterVisual";
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                var smr = inst.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (smr != null)
                {
                    var mpb = new MaterialPropertyBlock();
                    smr.GetPropertyBlock(mpb);
                    mpb.SetColor("_Color", tint);
                    mpb.SetColor("_BaseColor", tint);
                    smr.SetPropertyBlock(mpb);
                }
            }
            else
            {
                var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                cap.name = "BodyVisual";
                DestroyObjectSafe(cap.GetComponent<Collider>());
                cap.transform.SetParent(visualRoot.transform, false);
                cap.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                var renderer = cap.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var mpb = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(mpb);
                    mpb.SetColor("_Color", tint);
                    mpb.SetColor("_BaseColor", tint);
                    renderer.SetPropertyBlock(mpb);
                }
            }
        }
    }
}
