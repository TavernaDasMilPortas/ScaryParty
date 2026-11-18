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
        public EmployeeBlockReason LastBlockReason => _lastBlockReason;

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
        private OrderUnitKey _activeUnitKey;
        private OrderUnitKey _rolledUnitKey;
        private int _stepIndex;
        private float _stepTimer;
        private float _retryBackoffUntil;
        private EmployeeBlockReason _lastBlockReason = EmployeeBlockReason.None;
        private float _lastBlockLogAt = -999f;
        private ItemId _workingPizzaId = ItemId.None;
        private int _ovenStationId = -1;
        private int _ovenSlotIndex = -1;
        private int _prepStationId = -1;
        private int _prepSlotIndex = -1;
        private int _processStationId = -1;
        private int _processSlotIndex = -1;
        private int _packagingStationId = -1;
        private int _packagingSlotIndex = -1;
        private bool _hasStartedPrepOp;
        private bool _hasStartedIngProcessOp;
        private bool _hasStartedAssembleOp;
        private bool _hasStartedPackageOp;
        private ToolItemId _leasedToolId = ToolItemId.None;
        private int _expectedIngredientCountBeforeAsm;

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
                ReleaseEmployeeActiveWorkAndHandsServer(root, keepWorkingPizzaOnStation: true);
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
                if (_activeTaskId > 0 || _workingPizzaId.IsValid)
                {
                    ReleaseEmployeeActiveWorkAndHandsServer(root, keepWorkingPizzaOnStation: false);
                }
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
            ulong actorKey = EmployeeActorOffset + (ulong)EmployeeId;

            OrderUnitFulfillmentService.PruneInvalidatedGenerations(root.DomainState);
            OrderUnitFulfillmentService.PruneExpiredReservations(root.DomainState, now);

            // 1. Validar tarefa atual (ou se houve HumanTakeover / cancelamento / conclusão)
            SharedEmployeeTask currentTask = null;
            if (_activeTaskId > 0)
            {
                if (!root.EmployeeTaskService.Tasks.TryGetValue(_activeTaskId, out currentTask) ||
                    currentTask.IsReservedByHuman ||
                    currentTask.ReservedByActorId != (ulong)EmployeeId ||
                    currentTask.State == EmployeeTaskState.Completed ||
                    currentTask.State == EmployeeTaskState.Cancelled ||
                    !root.DomainState.Orders.TryGetValue(currentTask.TargetOrderId, out var curOrder) ||
                    curOrder.Lifecycle != OrderLifecycle.Accepted)
                {
                    ReleaseEmployeeActiveWorkAndHandsServer(root, keepWorkingPizzaOnStation: true);
                    ResetWorkState();
                    currentTask = null;
                }
                else
                {
                    root.EmployeeTaskService.TryReserveTask(_activeTaskId, (ulong)EmployeeId, isHuman: false, now, 20.0);
                }
            }

            if (Time.time < _retryBackoffUntil && currentTask != null)
            {
                MaintainActiveLeasesDuringBackoff(root, actorKey, now);
                return;
            }

            // 2. Se estiver ocioso, sincronizar unidades de pedidos e reservar próxima tarefa disponível
            if (currentTask == null)
            {
                OrderUnitFulfillmentService.SyncAllActiveOrderTasks(root.DomainState, root.EmployeeTaskService);

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
                ReleaseEmployeeActiveWorkAndHandsServer(root, keepWorkingPizzaOnStation: false);
                root.EmployeeTaskService.CancelTask(currentTask.TaskId);
                ResetWorkState();
                return;
            }

            // 3. Garantir uma unidade exclusiva (OrderUnitKey) para o funcionário trabalhar
            OrderUnitState activeUnit = null;
            if (_activeUnitKey.IsValid &&
                root.DomainState.OrderUnits.TryGetValue(_activeUnitKey, out var existingUnit) &&
                existingUnit.Key.GenerationId == root.DomainState.GenerationId &&
                existingUnit.Key.OrderId.Equals(order.Id) &&
                existingUnit.Status == OrderUnitStatus.InProgress &&
                existingUnit.AssignedEmployeeId == EmployeeId)
            {
                activeUnit = existingUnit;
            }
            else
            {
                if (!OrderUnitFulfillmentService.TryClaimNextPendingUnitForEmployee(
                    root.DomainState, order, EmployeeId, root.EmployeeTaskService, now, out activeUnit))
                {
                    // Todas as unidades já foram satisfeitas por pizzas prontas ou assumidas por outros
                    ReleaseEmployeeActiveWorkAndHandsServer(root, keepWorkingPizzaOnStation: true);
                    root.EmployeeTaskService.ReleaseActorTasks((ulong)EmployeeId, isHuman: false);
                    emp.AssignedTaskId = -1;
                    ResetWorkState();
                    PizzeriaCommandHandler.Instance?.CommitAndReplicate();
                    return;
                }
                _activeUnitKey = activeUnit.Key;
            }

            // Rolagem explícita de erro de habilidade ocorre uma única vez por unidade (nunca em bloqueio técnico)
            if (!_rolledUnitKey.Equals(_activeUnitKey))
            {
                _rolledUnitKey = _activeUnitKey;
                var stats = root.RunProgressionService != null
                    ? root.RunProgressionService.RecalculateEffectiveStats()
                    : new RunEffectiveStats();
                var rng = root.RunProgressionService != null ? root.RunProgressionService.EmployeeErrorRng : _fallbackRng;
                currentTask.IntentionalErrorTriggered = root.EmployeeTaskService.RollIntentionalSkillError(
                    EmployeeId, rng, stats.EmployeeErrorMultiplier);
            }

            var spec = activeUnit.Spec ?? PizzaSpecification.CreateDefaultCheese();

            // Etapa A: Adquirir e abrir massa na Bancada de Preparo usando ProcessingService real (sem atalhos)
            if (_stepIndex == (int)WorkStep.PizzaioloPrepareDough || _stepIndex == (int)WorkStep.Idle)
            {
                _stepIndex = (int)WorkStep.PizzaioloPrepareDough;
                Vector3 prepPos = ResolveStationPosition<PrepStation>(root.transform.position);
                if (!MoveToValidated(prepPos, out bool pathBlocked))
                {
                    if (pathBlocked) RecordBlock(root, currentTask, EmployeeBlockReason.PathBlocked, "Caminho até Bancada de Preparo bloqueado.");
                    return;
                }
                if ((transform.position - prepPos).sqrMagnitude > 2.6f * 2.6f) return;

                if (!_workingPizzaId.IsValid || !root.DomainState.Items.ContainsKey(_workingPizzaId))
                {
                    _hasStartedPrepOp = false;
                    if (!TryAcquireDoughIntoPrep(root, actorKey, _activeUnitKey, now, out _workingPizzaId, out _prepStationId, out _prepSlotIndex, out var doughBlock))
                    {
                        RecordBlock(root, currentTask, doughBlock, $"Sem massa ou bancada livre para iniciar unidade {_activeUnitKey}.");
                        return;
                    }
                }

                if (!root.DomainState.Items.TryGetValue(_workingPizzaId, out var dough))
                {
                    ResetUnitState();
                    return;
                }

                if (dough.PrepStage == ProcessingStage.Raw)
                {
                    var prepSlot = new StationSlotId(_prepStationId, _prepSlotIndex);
                    OrderUnitFulfillmentService.TryReserveSlot(root.DomainState, _prepStationId, _prepSlotIndex, actorKey, EmployeeId, _activeUnitKey, now, 12.0);

                    if (!_hasStartedPrepOp)
                    {
                        int doughProcessId = ResolveProcessIdForIngredient(root, dough.DefinitionId, dough.PrepStage, 1);
                        ToolItemId doughToolId = ToolItemId.None;
                        if (root.Catalog != null &&
                            root.Catalog.Processes.TryGetValue(doughProcessId, out var doughProc) &&
                            doughProc != null &&
                            doughProc.RequiredCapability != ToolCapability.None)
                        {
                            if (!TryFindAvailableStationTool(root.DomainState, doughProc.RequiredCapability, actorKey, now, out var freeTool))
                            {
                                RecordBlock(root, currentTask, EmployeeBlockReason.ToolInUse, "Ferramenta necessária ocupada por outro participante.");
                                return;
                            }
                            doughToolId = freeTool.Id;
                        }

                        var beginRes = root.ProcessingService.BeginOperation(
                            root.DomainState,
                            prepSlot,
                            OperationKind.Process,
                            ResumePolicy.Resumable,
                            dough.Id,
                            doughToolId,
                            doughProcessId,
                            actorKey,
                            root.Catalog,
                            (float)now,
                            ProcessingService.DefaultLeaseDuration);

                        if (!beginRes.Success)
                        {
                            RecordBlock(root, currentTask, EmployeeBlockReason.ToolInUse, $"BeginOperation de massa rejeitado: {beginRes.Message}");
                            return;
                        }

                        _leasedToolId = doughToolId;
                        _hasStartedPrepOp = true;
                        ClearBlock(root, currentTask);
                        PizzeriaCommandHandler.Instance?.CommitAndReplicate();
                        return;
                    }

                    if (root.DomainState.ActiveOperations.TryGetValue(prepSlot, out var prepOp))
                    {
                        root.ProcessingService.Heartbeat(root.DomainState, prepSlot, actorKey, (float)now, ProcessingService.DefaultLeaseDuration);
                        if (prepOp.ComputeCurrentProgress(now) < 1f)
                        {
                            return;
                        }

                        var compRes = root.ProcessingService.CompleteWork(prepSlot, root.DomainState, root.Catalog);
                        _leasedToolId = ToolItemId.None;
                        if (!compRes.Success)
                        {
                            _hasStartedPrepOp = false;
                            RecordBlock(root, currentTask, EmployeeBlockReason.ToolInUse, $"CompleteWork de massa falhou: {compRes.Message}");
                            return;
                        }
                    }

                    if (!root.DomainState.Items.TryGetValue(_workingPizzaId, out dough) || dough.PrepStage != ProcessingStage.Prepared)
                    {
                        _hasStartedPrepOp = false;
                        RecordBlock(root, currentTask, EmployeeBlockReason.NoDough, "Massa não atingiu estágio Prepared pelo serviço de domínio.");
                        return;
                    }
                }

                ClearBlock(root, currentTask);
                _hasStartedPrepOp = false;
                _hasStartedIngProcessOp = false;
                _hasStartedAssembleOp = false;
                _stepIndex = (int)WorkStep.PizzaioloAssembleToppings;
                PizzeriaCommandHandler.Instance?.CommitAndReplicate();
                return;
            }

            // Etapa B: Processar (via tempo real) e montar cada ingrediente exigido pelo Pedido
            if (_stepIndex == (int)WorkStep.PizzaioloAssembleToppings)
            {
                if (!root.DomainState.Items.TryGetValue(_workingPizzaId, out var pizza))
                {
                    ReleaseEmployeeActiveWorkAndHandsServer(root, keepWorkingPizzaOnStation: false);
                    ResetUnitState();
                    return;
                }

                Vector3 asmPos = ResolveStationPosition<AssemblyStation>(ResolveStationPosition<PrepStation>(root.transform.position));
                if (!MoveToValidated(asmPos, out bool pathBlocked))
                {
                    if (pathBlocked) RecordBlock(root, currentTask, EmployeeBlockReason.PathBlocked, "Caminho até Bancada de Montagem bloqueado.");
                    return;
                }
                if ((transform.position - asmPos).sqrMagnitude > 2.6f * 2.6f) return;

                if (pizza.Location.Type == LocationType.StationSlot)
                {
                    OrderUnitFulfillmentService.TryReserveSlot(
                        root.DomainState, (int)pizza.Location.HolderId, pizza.Location.SlotId, actorKey, EmployeeId, _activeUnitKey, now, 12.0);
                }

                var assembleResult = StepAssembleNextIngredientReal(root, pizza, spec, actorKey, now, currentTask.IntentionalErrorTriggered, out var missingReason, out string blockMsg);
                if (assembleResult == AssembleStepStatus.Blocked)
                {
                    RecordBlock(root, currentTask, missingReason, blockMsg);
                    return;
                }
                if (assembleResult == AssembleStepStatus.Waiting)
                {
                    ClearBlock(root, currentTask);
                    return;
                }

                ClearBlock(root, currentTask);
                _stepIndex = (int)WorkStep.PizzaioloInsertOven;
                PizzeriaCommandHandler.Instance?.CommitAndReplicate();
                return;
            }

            // Etapa C: Inserir no forno respeitando capacidade real
            if (_stepIndex == (int)WorkStep.PizzaioloInsertOven)
            {
                if (!root.DomainState.Items.TryGetValue(_workingPizzaId, out _))
                {
                    ResetUnitState();
                    return;
                }

                Vector3 ovenPos = ResolveStationPosition<OvenStation>(root.transform.position);
                if (!MoveToValidated(ovenPos, out bool pathBlocked))
                {
                    if (pathBlocked) RecordBlock(root, currentTask, EmployeeBlockReason.PathBlocked, "Caminho até Forno bloqueado.");
                    return;
                }
                if ((transform.position - ovenPos).sqrMagnitude > 2.6f * 2.6f) return;

                if (!TryFindFreeOvenSlot(root, actorKey, now, out int ovenId, out int slotIdx))
                {
                    RecordBlock(root, currentTask, EmployeeBlockReason.OvenBusy, "Forno sem slot livre dentro da capacidade real.");
                    return;
                }

                OrderUnitFulfillmentService.TryReserveSlot(root.DomainState, ovenId, slotIdx, actorKey, EmployeeId, _activeUnitKey, now, 25.0);
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
                    RecordBlock(root, currentTask, EmployeeBlockReason.OvenBusy, $"Falha ao inserir pizza no forno: {insertRes.Message}");
                    return;
                }

                ReleaseSpecificSlotReservation(root.DomainState, _prepStationId, _prepSlotIndex, actorKey);
                _prepStationId = -1;
                _prepSlotIndex = -1;
                _ovenStationId = ovenId;
                _ovenSlotIndex = slotIdx;
                ClearBlock(root, currentTask);
                _stepIndex = (int)WorkStep.PizzaioloWaitBake;
                PizzeriaCommandHandler.Instance?.CommitAndReplicate();
                return;
            }

            // Etapa D: Aguardar cocção real pelo Oven/ProcessingService
            if (_stepIndex == (int)WorkStep.PizzaioloWaitBake)
            {
                if (!root.DomainState.Items.TryGetValue(_workingPizzaId, out var bakingPizza))
                {
                    ReleaseSpecificSlotReservation(root.DomainState, _ovenStationId, _ovenSlotIndex, actorKey);
                    ResetUnitState();
                    return;
                }

                if (bakingPizza.CookingStage == CookingStage.Burned)
                {
                    // Pizza queimada é descartada corretamente e a unidade volta ao ciclo para ser refeita
                    ReleaseSpecificSlotReservation(root.DomainState, _ovenStationId, _ovenSlotIndex, actorKey);
                    TransferService.DestroyItem(root.DomainState, bakingPizza.Id, root.EmployeeTaskService);
                    ResetUnitState();
                    PizzeriaCommandHandler.Instance?.CommitAndReplicate();
                    return;
                }

                if (bakingPizza.CookingStage != CookingStage.Baked)
                    return;

                var ovenSlot = new StationSlotId(_ovenStationId, _ovenSlotIndex);
                var handLoc = LocationRef.InHand(actorKey, HandSlotIndex.Right);
                var remRes = root.OvenService.RemovePizza(root.DomainState, ovenSlot, handLoc, actorKey);
                if (!remRes.Success && !bakingPizza.Location.Equals(handLoc))
                {
                    RecordBlock(root, currentTask, EmployeeBlockReason.ToolInUse, $"Não foi possível retirar pizza do forno: {remRes.Message}");
                    return;
                }

                ReleaseSpecificSlotReservation(root.DomainState, _ovenStationId, _ovenSlotIndex, actorKey);
                _ovenStationId = -1;
                _ovenSlotIndex = -1;
                _hasStartedPackageOp = false;
                _packagingStationId = -1;
                _packagingSlotIndex = -1;
                ClearBlock(root, currentTask);
                _stepIndex = (int)WorkStep.PizzaioloPackageAndStage;
                PizzeriaCommandHandler.Instance?.CommitAndReplicate();
                return;
            }

            // Etapa E: Embalar via OperationKind.Package real (quando delivery) e posicionar em slot livre + vínculo exclusivo
            if (_stepIndex == (int)WorkStep.PizzaioloPackageAndStage)
            {
                if (!root.DomainState.Items.TryGetValue(_workingPizzaId, out var finishedPizza))
                {
                    ResetUnitState();
                    return;
                }

                bool requiresBox = !order.IsDineIn;
                Vector3 pkgOrStagePos = requiresBox && !_hasStartedPackageOp && !finishedPizza.IsBoxed
                    ? ResolveStationPosition<PackagingStation>(ResolveStationPosition<StagingStation>(root.transform.position))
                    : ResolveStationPosition<StagingStation>(ResolveStationPosition<PackagingStation>(root.transform.position));

                if (!MoveToValidated(pkgOrStagePos, out bool pathBlocked))
                {
                    if (pathBlocked) RecordBlock(root, currentTask, EmployeeBlockReason.PathBlocked, "Caminho até Embalagem/Expedição bloqueado.");
                    return;
                }
                if ((transform.position - pkgOrStagePos).sqrMagnitude > 2.6f * 2.6f) return;

                if (requiresBox && !finishedPizza.IsBoxed)
                {
                    if (finishedPizza.Location.Type != LocationType.StationSlot)
                    {
                        int pkgStationId = ResolvePackagingOnlyStationId(root, out var pkgView);
                        int maxPkgSlots = GetStationSlotCapacity(root, pkgStationId, pkgView);
                        int freePkgSlot = FindFirstFreeReservableSlot(root.DomainState, pkgStationId, maxPkgSlots, actorKey, now);
                        if (freePkgSlot < 0)
                        {
                            RecordBlock(root, currentTask, EmployeeBlockReason.OvenBusy, "Bancada de embalagem sem slot livre.");
                            return;
                        }

                        var placePkg = TransferService.TransferItem(
                            root.DomainState,
                            finishedPizza.Id,
                            LocationRef.InStation(pkgStationId, freePkgSlot),
                            actorKey,
                            bypassReservation: false);
                        if (!placePkg.Success)
                        {
                            RecordBlock(root, currentTask, EmployeeBlockReason.ToolInUse, $"Não foi possível apoiar pizza para embalagem: {placePkg.Message}");
                            return;
                        }
                        OrderUnitFulfillmentService.TryReserveSlot(root.DomainState, pkgStationId, freePkgSlot, actorKey, EmployeeId, _activeUnitKey, now, 10.0);
                        _packagingStationId = pkgStationId;
                        _packagingSlotIndex = freePkgSlot;
                    }

                    var pkgSlot = new StationSlotId((int)finishedPizza.Location.HolderId, finishedPizza.Location.SlotId);
                    if (!_hasStartedPackageOp)
                    {
                        float pkgDur = root.Config != null ? Mathf.Max(0.4f, root.Config.packagingDuration) : 1.5f;
                        var pkgBegin = root.ProcessingService.BeginOperation(
                            root.DomainState,
                            pkgSlot,
                            OperationKind.Package,
                            ResumePolicy.ResetOnCancel,
                            finishedPizza.Id,
                            ToolItemId.None,
                            0,
                            actorKey,
                            root.Catalog,
                            (float)now,
                            ProcessingService.DefaultLeaseDuration,
                            pkgDur);

                        if (!pkgBegin.Success)
                        {
                            RecordBlock(root, currentTask, EmployeeBlockReason.ToolInUse, $"BeginOperation(Package) rejeitado: {pkgBegin.Message}");
                            return;
                        }

                        _hasStartedPackageOp = true;
                        ClearBlock(root, currentTask);
                        PizzeriaCommandHandler.Instance?.CommitAndReplicate();
                        return;
                    }

                    if (root.DomainState.ActiveOperations.TryGetValue(pkgSlot, out var pkgOp))
                    {
                        root.ProcessingService.Heartbeat(root.DomainState, pkgSlot, actorKey, (float)now, ProcessingService.DefaultLeaseDuration);
                        if (pkgOp.ComputeCurrentProgress(now) < 1f)
                        {
                            return;
                        }

                        var compPkg = root.ProcessingService.CompleteWork(pkgSlot, root.DomainState, root.Catalog);
                        if (!compPkg.Success)
                        {
                            _hasStartedPackageOp = false;
                            RecordBlock(root, currentTask, EmployeeBlockReason.ToolInUse, $"CompleteWork(Package) falhou: {compPkg.Message}");
                            return;
                        }
                    }

                    if (!root.DomainState.Items.TryGetValue(_workingPizzaId, out finishedPizza) || !finishedPizza.IsBoxed)
                    {
                        _hasStartedPackageOp = false;
                        RecordBlock(root, currentTask, EmployeeBlockReason.ToolInUse, "Pizza não foi embalada pelo serviço de domínio.");
                        return;
                    }

                    finishedPizza.LabelAddress = activeUnit.LabelAddress;
                    finishedPizza.Revision++;
                }

                // Tenta mover para slot de expedição se ainda estiver em mão ou em outra bancada e houver slot livre na expedição
                int stageStationId = ResolvePackagingOrStagingStationId(root, out var stageView);
                int stageMaxSlots = GetStationSlotCapacity(root, stageStationId, stageView);
                bool alreadyAtTarget = finishedPizza.Location.Type == LocationType.StationSlot &&
                                       (int)finishedPizza.Location.HolderId == stageStationId;
                if (!alreadyAtTarget)
                {
                    int freeStageSlot = FindFirstFreeReservableSlot(root.DomainState, stageStationId, stageMaxSlots, actorKey, now);
                    if (freeStageSlot >= 0)
                    {
                        TransferService.TransferItem(
                            root.DomainState,
                            finishedPizza.Id,
                            LocationRef.InStation(stageStationId, freeStageSlot),
                            actorKey,
                            bypassReservation: false);
                    }
                    else if (finishedPizza.Location.Type != LocationType.StationSlot)
                    {
                        // Nenhuma vaga na expedição e ainda está na mão -> tenta bancada de embalagem como último slot válido (nunca InWorld!)
                        int fallbackId = ResolvePackagingOnlyStationId(root, out var pkgView2);
                        int fallbackMax = GetStationSlotCapacity(root, fallbackId, pkgView2);
                        int fallbackSlot = FindFirstFreeReservableSlot(root.DomainState, fallbackId, fallbackMax, actorKey, now);
                        if (fallbackSlot >= 0)
                        {
                            TransferService.TransferItem(
                                root.DomainState,
                                finishedPizza.Id,
                                LocationRef.InStation(fallbackId, fallbackSlot),
                                actorKey,
                                bypassReservation: false);
                        }
                        else
                        {
                            RecordBlock(root, currentTask, EmployeeBlockReason.OvenBusy, "Bancadas de embalagem e expedição cheias.");
                            return;
                        }
                    }
                }

                if (finishedPizza.Location.Type != LocationType.StationSlot)
                {
                    RecordBlock(root, currentTask, EmployeeBlockReason.ToolInUse, "Pizza precisa repousar em slot válido de bancada.");
                    return;
                }

                if (!OrderUnitFulfillmentService.TryBindSpecificPizzaToUnit(
                    root.DomainState, _activeUnitKey, finishedPizza.Id, root.EmployeeTaskService))
                {
                    RecordBlock(root, currentTask, EmployeeBlockReason.MissingIngredient, "Pizza pronta não atendeu exatamente aos requisitos da unidade.");
                    return;
                }

                OrderUnitFulfillmentService.ReleaseActorSlotReservations(root.DomainState, actorKey);
                ClearBlock(root, currentTask);

                if (OrderUnitFulfillmentService.TryClaimNextPendingUnitForEmployee(
                    root.DomainState, order, EmployeeId, root.EmployeeTaskService, now, out var nextUnit))
                {
                    _activeUnitKey = nextUnit.Key;
                    ResetUnitState();
                }
                else
                {
                    root.EmployeeTaskService.ReleaseActorTasks((ulong)EmployeeId, isHuman: false);
                    emp.AssignedTaskId = -1;
                    ResetWorkState();
                }

                PizzeriaCommandHandler.Instance?.CommitAndReplicate();
            }
        }

        private enum AssembleStepStatus
        {
            Waiting,
            Blocked,
            AllDone
        }

        private AssembleStepStatus StepAssembleNextIngredientReal(
            PizzeriaRoot root,
            ItemState pizza,
            PizzaSpecification spec,
            ulong actorKey,
            double now,
            bool intentionalError,
            out EmployeeBlockReason blockReason,
            out string blockMessage)
        {
            blockReason = EmployeeBlockReason.None;
            blockMessage = string.Empty;

            if (!TryGetNextMissingIngredientRequirement(pizza, spec, intentionalError, out int targetIngId, out ProcessingStage targetStage))
            {
                return AssembleStepStatus.AllDone;
            }

            if (pizza.Location.Type != LocationType.StationSlot)
            {
                blockReason = EmployeeBlockReason.ToolInUse;
                blockMessage = "Pizza não está em um slot de bancada para montagem.";
                return AssembleStepStatus.Blocked;
            }

            var pizzaSlot = new StationSlotId((int)pizza.Location.HolderId, pizza.Location.SlotId);

            // 1. Se já há uma operação Assemble em andamento nesta pizza por este funcionário, aguarda conclusão real
            if (root.DomainState.ActiveOperations.TryGetValue(pizzaSlot, out var existingOp))
            {
                if (existingOp.Kind != OperationKind.Assemble ||
                    (existingOp.WorkerId != actorKey && existingOp.InitiatorClientId != actorKey))
                {
                    blockReason = EmployeeBlockReason.ToolInUse;
                    blockMessage = "Slot da pizza ocupado por outra operação.";
                    return AssembleStepStatus.Blocked;
                }

                if (existingOp.ComputeCurrentProgress(now) < 1f)
                {
                    return AssembleStepStatus.Waiting;
                }

                if (!PizzaAssemblyService.Complete(root.DomainState, pizzaSlot))
                {
                    _hasStartedAssembleOp = false;
                    blockReason = EmployeeBlockReason.ToolInUse;
                    blockMessage = "PizzaAssemblyService.Complete retornou falso.";
                    return AssembleStepStatus.Blocked;
                }

                _hasStartedAssembleOp = false;
                if (GetIngredientCount(pizza, targetIngId) <= _expectedIngredientCountBeforeAsm)
                {
                    blockReason = EmployeeBlockReason.MissingIngredient;
                    blockMessage = $"Montagem de ingrediente {targetIngId} foi rejeitada na validação final.";
                    return AssembleStepStatus.Blocked;
                }

                PizzeriaCommandHandler.Instance?.CommitAndReplicate();
                return TryGetNextMissingIngredientRequirement(pizza, spec, intentionalError, out _, out _)
                    ? AssembleStepStatus.Waiting
                    : AssembleStepStatus.AllDone;
            }

            // Se a operação acabou de completar no PizzeriaRoot.Update neste mesmo frame:
            if (_hasStartedAssembleOp)
            {
                _hasStartedAssembleOp = false;
                if (!TryGetNextMissingIngredientRequirement(pizza, spec, intentionalError, out targetIngId, out targetStage))
                {
                    return AssembleStepStatus.AllDone;
                }
                return AssembleStepStatus.Waiting;
            }

            // 2. Se existe ingrediente em processamento na bancada auxiliar para este funcionário, conclui primeiro
            if (_hasStartedIngProcessOp && _processStationId > 0 && _processSlotIndex >= 0)
            {
                var procSlot = new StationSlotId(_processStationId, _processSlotIndex);
                if (root.DomainState.ActiveOperations.TryGetValue(procSlot, out var ingOp))
                {
                    root.ProcessingService.Heartbeat(root.DomainState, procSlot, actorKey, (float)now, ProcessingService.DefaultLeaseDuration);
                    if (ingOp.ComputeCurrentProgress(now) < 1f)
                    {
                        return AssembleStepStatus.Waiting;
                    }

                    var compIng = root.ProcessingService.CompleteWork(procSlot, root.DomainState, root.Catalog);
                    _leasedToolId = ToolItemId.None;
                    _hasStartedIngProcessOp = false;
                    if (!compIng.Success)
                    {
                        ReleaseSpecificSlotReservation(root.DomainState, _processStationId, _processSlotIndex, actorKey);
                        _processStationId = -1;
                        _processSlotIndex = -1;
                        blockReason = EmployeeBlockReason.ToolInUse;
                        blockMessage = $"Falha ao concluir preparo de ingrediente: {compIng.Message}";
                        return AssembleStepStatus.Blocked;
                    }

                    ItemState processedItem = FindItemInSlot(root.DomainState, _processStationId, _processSlotIndex);
                    if (processedItem != null)
                    {
                        var takeRes = TransferService.TransferItem(
                            root.DomainState, processedItem.Id, LocationRef.InHand(actorKey, HandSlotIndex.Right), actorKey, bypassReservation: false);
                        ReleaseSpecificSlotReservation(root.DomainState, _processStationId, _processSlotIndex, actorKey);
                        _processStationId = -1;
                        _processSlotIndex = -1;
                        if (!takeRes.Success)
                        {
                            blockReason = EmployeeBlockReason.ToolInUse;
                            blockMessage = $"Não foi possível pegar ingrediente preparado: {takeRes.Message}";
                            return AssembleStepStatus.Blocked;
                        }
                    }
                }
                else
                {
                    _hasStartedIngProcessOp = false;
                    ReleaseSpecificSlotReservation(root.DomainState, _processStationId, _processSlotIndex, actorKey);
                    _processStationId = -1;
                    _processSlotIndex = -1;
                }
            }

            // 3. Verificar se o funcionário já segura o ingrediente pronto compatível na mão direita
            ItemState handIngredient = FindReadyHandIngredient(root.DomainState, actorKey, targetIngId, targetStage);
            if (handIngredient == null)
            {
                // Primeiro tenta estoque já no estágio desejado (molho ou ingrediente pronto)
                if (CanDispenseIngredientFromAnyStorage(root, targetIngId))
                {
                    if (!IsEmployeeRightHandFree(root.DomainState, actorKey))
                    {
                        ReturnHandItemsOrPlaceOnCounter(root, actorKey, keepWorkingPizzaOnStation: true);
                        if (!IsEmployeeRightHandFree(root.DomainState, actorKey))
                        {
                            blockReason = EmployeeBlockReason.ToolInUse;
                            blockMessage = "Mão do funcionário ocupada.";
                            return AssembleStepStatus.Blocked;
                        }
                    }

                    if (TryDispenseIngredientFromAnyStorage(root, targetIngId, actorKey, out var directIngId) &&
                        root.DomainState.Items.TryGetValue(directIngId, out var directItem))
                    {
                        handIngredient = directItem;
                    }
                }
                else if (targetStage == ProcessingStage.Prepared &&
                         TryFindProcessProducingIngredient(root, targetIngId, out var procData))
                {
                    // Verifica TUDO antes de dispensar estoque cru: estoque + slot de preparo livre + ferramenta livre
                    if (!CanDispenseIngredientFromAnyStorage(root, procData.InputIngredientId))
                    {
                        blockReason = EmployeeBlockReason.MissingIngredient;
                        blockMessage = $"Sem estoque para ingrediente {targetIngId} (nem cru {procData.InputIngredientId}).";
                        return AssembleStepStatus.Blocked;
                    }

                    int procStationId = ResolvePrepOrAssemblyStationId(root, out var procView);
                    int procCapacity = GetStationSlotCapacity(root, procStationId, procView);
                    int freeProcSlot = FindFirstFreeReservableSlot(root.DomainState, procStationId, procCapacity, actorKey, now);
                    if (freeProcSlot < 0)
                    {
                        blockReason = EmployeeBlockReason.ToolInUse;
                        blockMessage = "Bancada de preparo sem slot livre para cortar ingrediente.";
                        return AssembleStepStatus.Blocked;
                    }

                    ToolItemId procToolId = ToolItemId.None;
                    if (procData.RequiredCapability != ToolCapability.None)
                    {
                        if (!TryFindAvailableStationTool(root.DomainState, procData.RequiredCapability, actorKey, now, out var availTool))
                        {
                            blockReason = EmployeeBlockReason.ToolInUse;
                            blockMessage = $"Ferramenta ({procData.RequiredCapability}) ocupada por humano ou outro processo.";
                            return AssembleStepStatus.Blocked;
                        }
                        procToolId = availTool.Id;
                    }

                    if (!IsEmployeeRightHandFree(root.DomainState, actorKey))
                    {
                        ReturnHandItemsOrPlaceOnCounter(root, actorKey, keepWorkingPizzaOnStation: true);
                        if (!IsEmployeeRightHandFree(root.DomainState, actorKey))
                        {
                            blockReason = EmployeeBlockReason.ToolInUse;
                            blockMessage = "Mão direita do funcionário ocupada.";
                            return AssembleStepStatus.Blocked;
                        }
                    }

                    if (!TryDispenseIngredientFromAnyStorage(root, procData.InputIngredientId, actorKey, out var rawItemId))
                    {
                        blockReason = EmployeeBlockReason.MissingIngredient;
                        blockMessage = $"Falha ao dispensar ingrediente cru {procData.InputIngredientId}.";
                        return AssembleStepStatus.Blocked;
                    }

                    var targetProcLoc = LocationRef.InStation(procStationId, freeProcSlot);
                    var moveRawRes = TransferService.TransferItem(root.DomainState, rawItemId, targetProcLoc, actorKey, bypassReservation: false);
                    if (!moveRawRes.Success)
                    {
                        RestoreRawItemToStorageOrDestroy(root, rawItemId);
                        blockReason = EmployeeBlockReason.ToolInUse;
                        blockMessage = $"Falha ao transferir ingrediente cru para bancada: {moveRawRes.Message}";
                        return AssembleStepStatus.Blocked;
                    }

                    OrderUnitFulfillmentService.TryReserveSlot(root.DomainState, procStationId, freeProcSlot, actorKey, EmployeeId, _activeUnitKey, now, 12.0);
                    var procSlotKey = new StationSlotId(procStationId, freeProcSlot);
                    var beginProcRes = root.ProcessingService.BeginOperation(
                        root.DomainState,
                        procSlotKey,
                        OperationKind.Process,
                        ResumePolicy.Resumable,
                        rawItemId,
                        procToolId,
                        procData.Id,
                        actorKey,
                        root.Catalog,
                        (float)now,
                        ProcessingService.DefaultLeaseDuration);

                    if (!beginProcRes.Success)
                    {
                        ReleaseSpecificSlotReservation(root.DomainState, procStationId, freeProcSlot, actorKey);
                        RestoreRawItemToStorageOrDestroy(root, rawItemId);
                        blockReason = EmployeeBlockReason.ToolInUse;
                        blockMessage = $"BeginOperation de ingrediente rejeitado: {beginProcRes.Message}";
                        return AssembleStepStatus.Blocked;
                    }

                    _processStationId = procStationId;
                    _processSlotIndex = freeProcSlot;
                    _leasedToolId = procToolId;
                    _hasStartedIngProcessOp = true;
                    PizzeriaCommandHandler.Instance?.CommitAndReplicate();
                    return AssembleStepStatus.Waiting;
                }
                else
                {
                    blockReason = EmployeeBlockReason.MissingIngredient;
                    blockMessage = $"Ingrediente {targetIngId} em falta no estoque.";
                    return AssembleStepStatus.Blocked;
                }
            }

            if (handIngredient == null)
            {
                blockReason = EmployeeBlockReason.MissingIngredient;
                blockMessage = $"Ingrediente {targetIngId} indisponível.";
                return AssembleStepStatus.Blocked;
            }

            // 4. Iniciar montagem temporizada real via PizzaAssemblyService.Begin
            _expectedIngredientCountBeforeAsm = GetIngredientCount(pizza, targetIngId);
            var beginAsm = PizzaAssemblyService.Begin(root.DomainState, pizza.Id, handIngredient.Id, actorKey, (float)now, 0.35f);
            if (!beginAsm.Success)
            {
                blockReason = EmployeeBlockReason.ToolInUse;
                blockMessage = $"PizzaAssemblyService.Begin rejeitado: {beginAsm.Message}";
                return AssembleStepStatus.Blocked;
            }

            _hasStartedAssembleOp = true;
            PizzeriaCommandHandler.Instance?.CommitAndReplicate();
            return AssembleStepStatus.Waiting;
        }

        private static bool TryGetNextMissingIngredientRequirement(
            ItemState pizza,
            PizzaSpecification spec,
            bool intentionalError,
            out int ingredientDefId,
            out ProcessingStage requiredStage)
        {
            ingredientDefId = 0;
            requiredStage = ProcessingStage.Prepared;
            if (pizza == null || spec == null) return false;

            if (spec.RequiresSauce && spec.SauceId > 0)
            {
                int targetSauce = Math.Max(1, spec.SaucePortions);
                if (GetIngredientCount(pizza, spec.SauceId) < targetSauce)
                {
                    ingredientDefId = spec.SauceId;
                    requiredStage = ProcessingStage.Prepared;
                    return true;
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
                    if (GetIngredientCount(pizza, top.IngredientDefId) < targetPortions)
                    {
                        ingredientDefId = top.IngredientDefId;
                        requiredStage = top.RequiredStage;
                        return true;
                    }
                }
            }

            return false;
        }

        private void MaintainActiveLeasesDuringBackoff(PizzeriaRoot root, ulong actorKey, double now)
        {
            if (root == null || root.DomainState == null) return;
            if (_hasStartedPrepOp && _prepStationId > 0 && _prepSlotIndex >= 0)
            {
                root.ProcessingService.Heartbeat(root.DomainState, new StationSlotId(_prepStationId, _prepSlotIndex), actorKey, (float)now, ProcessingService.DefaultLeaseDuration);
            }
            if (_hasStartedIngProcessOp && _processStationId > 0 && _processSlotIndex >= 0)
            {
                root.ProcessingService.Heartbeat(root.DomainState, new StationSlotId(_processStationId, _processSlotIndex), actorKey, (float)now, ProcessingService.DefaultLeaseDuration);
            }
            if (_hasStartedPackageOp && _packagingStationId > 0 && _packagingSlotIndex >= 0)
            {
                root.ProcessingService.Heartbeat(root.DomainState, new StationSlotId(_packagingStationId, _packagingSlotIndex), actorKey, (float)now, ProcessingService.DefaultLeaseDuration);
            }
        }

        private void RecordBlock(PizzeriaRoot root, SharedEmployeeTask task, EmployeeBlockReason reason, string diagnostic)
        {
            _lastBlockReason = reason;
            _retryBackoffUntil = Time.time + 0.75f;
            if (root != null && root.EmployeeTaskService != null && task != null)
            {
                root.EmployeeTaskService.MarkTaskBlocked(task.TaskId, reason);
            }
            if (Time.time - _lastBlockLogAt >= 6.0f)
            {
                _lastBlockLogAt = Time.time;
                Debug.Log($"[EmployeeAgentController #{EmployeeId}] Bloqueado ({reason}): {diagnostic}");
            }
        }

        private void ClearBlock(PizzeriaRoot root, SharedEmployeeTask task)
        {
            _lastBlockReason = EmployeeBlockReason.None;
            _retryBackoffUntil = 0f;
            if (root != null && root.EmployeeTaskService != null && task != null && task.State == EmployeeTaskState.Blocked)
            {
                root.EmployeeTaskService.ClearTaskBlocked(task.TaskId);
            }
        }

        public void ReleaseEmployeeActiveWorkAndHandsServer(PizzeriaRoot root, bool keepWorkingPizzaOnStation = true)
        {
            if (root == null || root.DomainState == null) return;
            ulong actorKey = EmployeeActorOffset + (ulong)EmployeeId;
            float now = root.Clock != null ? (float)root.Clock.Now : Time.time;

            var activeSlots = new List<StationSlotId>(root.DomainState.ActiveOperations.Keys);
            for (int i = 0; i < activeSlots.Count; i++)
            {
                var slot = activeSlots[i];
                if (!root.DomainState.ActiveOperations.TryGetValue(slot, out var op)) continue;
                if (op.WorkerId == actorKey || op.InitiatorClientId == actorKey)
                {
                    if (op.Kind == OperationKind.Assemble)
                        PizzaAssemblyService.Cancel(root.DomainState, slot);
                    else
                        root.ProcessingService.CancelOperation(root.DomainState, slot, op.WorkerId, now);
                }
            }

            foreach (var tool in root.DomainState.Tools.Values)
            {
                if (tool != null && tool.ReservedByWorkerId == actorKey)
                {
                    tool.ReleaseLease(actorKey);
                }
            }

            ReturnHandItemsOrPlaceOnCounter(root, actorKey, keepWorkingPizzaOnStation);
            OrderUnitFulfillmentService.ReleaseActorSlotReservations(root.DomainState, actorKey);
            OrderUnitFulfillmentService.ReleaseEmployeeAssignedUnits(root.DomainState, EmployeeId);
        }

        private static void ReturnHandItemsOrPlaceOnCounter(PizzeriaRoot root, ulong actorKey, bool keepWorkingPizzaOnStation)
        {
            if (root == null || root.DomainState == null) return;
            var handItemIds = new List<ItemId>();
            foreach (var kvp in root.DomainState.Items)
            {
                if (kvp.Value != null &&
                    kvp.Value.Location.Type == LocationType.Hand &&
                    kvp.Value.Location.HolderId == actorKey)
                {
                    handItemIds.Add(kvp.Key);
                }
            }

            double now = root.Clock != null ? root.Clock.Now : Time.timeAsDouble;
            for (int i = 0; i < handItemIds.Count; i++)
            {
                var itemId = handItemIds[i];
                if (!root.DomainState.Items.TryGetValue(itemId, out var item)) continue;
                item.ReservedByHold = 0;

                if (item.PrepStage == ProcessingStage.Raw && item.Category != ItemCategory.Pizza && item.Category != ItemCategory.PizzaBase)
                {
                    RestoreRawItemToStorageOrDestroy(root, itemId);
                    continue;
                }

                if (keepWorkingPizzaOnStation && (item.IsPizzaLike || item.Category == ItemCategory.Box))
                {
                    if (TryPlaceOnAnyFreeKitchenSlot(root, item, actorKey, now))
                    {
                        continue;
                    }
                }

                if (!TryPlaceOnAnyFreeKitchenSlot(root, item, actorKey, now))
                {
                    TransferService.DestroyItem(root.DomainState, itemId, root.EmployeeTaskService);
                }
            }
        }

        private static bool TryPlaceOnAnyFreeKitchenSlot(PizzeriaRoot root, ItemState item, ulong actorKey, double now)
        {
            int stgId = ResolvePackagingOrStagingStationId(root, out var stgView);
            int stgMax = GetStationSlotCapacity(root, stgId, stgView);
            int freeStg = FindFirstFreeReservableSlot(root.DomainState, stgId, stgMax, actorKey, now);
            if (freeStg >= 0)
            {
                return TransferService.TransferItem(root.DomainState, item.Id, LocationRef.InStation(stgId, freeStg), actorKey, bypassReservation: false).Success;
            }

            int prepId = ResolvePrepOrAssemblyStationId(root, out var prepView);
            int prepMax = GetStationSlotCapacity(root, prepId, prepView);
            int freePrep = FindFirstFreeReservableSlot(root.DomainState, prepId, prepMax, actorKey, now);
            if (freePrep >= 0)
            {
                return TransferService.TransferItem(root.DomainState, item.Id, LocationRef.InStation(prepId, freePrep), actorKey, bypassReservation: false).Success;
            }

            return false;
        }

        private static void RestoreRawItemToStorageOrDestroy(PizzeriaRoot root, ItemId itemId)
        {
            if (root == null || root.DomainState == null) return;
            if (root.DomainState.Items.TryGetValue(itemId, out var item))
            {
                item.ReservedByHold = 0;
                foreach (var kvp in root.DomainState.StorageSlots)
                {
                    if (kvp.Key.ingredientDefId == item.DefinitionId && kvp.Value != null)
                    {
                        if (kvp.Value.Quantity < kvp.Value.MaxCapacity)
                        {
                            kvp.Value.Quantity++;
                        }
                        break;
                    }
                }
            }
            TransferService.DestroyItem(root.DomainState, itemId, root.EmployeeTaskService);
        }

        private bool TryAcquireDoughIntoPrep(
            PizzeriaRoot root,
            ulong actorKey,
            OrderUnitKey unitKey,
            double now,
            out ItemId doughId,
            out int stationId,
            out int slotIdx,
            out EmployeeBlockReason failReason)
        {
            doughId = ItemId.None;
            failReason = EmployeeBlockReason.None;
            stationId = ResolvePrepOrAssemblyStationId(root, out var prepView);
            int maxSlots = GetStationSlotCapacity(root, stationId, prepView);
            slotIdx = FindFirstFreeReservableSlot(root.DomainState, stationId, maxSlots, actorKey, now);
            if (slotIdx < 0)
            {
                failReason = EmployeeBlockReason.ToolInUse;
                return false;
            }

            int selectedDefId = CanDispenseIngredientFromAnyStorage(root, PizzaSpecification.DefaultBaseDoughId)
                ? PizzaSpecification.DefaultBaseDoughId
                : (CanDispenseIngredientFromAnyStorage(root, 1) ? 1 : -1);

            if (selectedDefId < 0)
            {
                failReason = EmployeeBlockReason.NoDough;
                return false;
            }

            // Se precisar abrir massa crua (1), garante ferramenta livre ANTES de consumir do estoque
            if (selectedDefId == 1)
            {
                int procId = ResolveProcessIdForIngredient(root, 1, ProcessingStage.Raw, 1);
                if (root.Catalog != null &&
                    root.Catalog.Processes.TryGetValue(procId, out var procData) &&
                    procData != null &&
                    procData.RequiredCapability != ToolCapability.None &&
                    !TryFindAvailableStationTool(root.DomainState, procData.RequiredCapability, actorKey, now, out _))
                {
                    failReason = EmployeeBlockReason.ToolInUse;
                    return false;
                }
            }

            if (!IsEmployeeRightHandFree(root.DomainState, actorKey))
            {
                ReturnHandItemsOrPlaceOnCounter(root, actorKey, keepWorkingPizzaOnStation: true);
                if (!IsEmployeeRightHandFree(root.DomainState, actorKey))
                {
                    failReason = EmployeeBlockReason.ToolInUse;
                    return false;
                }
            }

            if (!TryDispenseIngredientFromAnyStorage(root, selectedDefId, actorKey, out doughId))
            {
                failReason = EmployeeBlockReason.NoDough;
                return false;
            }

            var targetLoc = LocationRef.InStation(stationId, slotIdx);
            var tr = TransferService.TransferItem(root.DomainState, doughId, targetLoc, actorKey, bypassReservation: false);
            if (!tr.Success)
            {
                RestoreRawItemToStorageOrDestroy(root, doughId);
                doughId = ItemId.None;
                failReason = EmployeeBlockReason.ToolInUse;
                return false;
            }

            OrderUnitFulfillmentService.TryReserveSlot(root.DomainState, stationId, slotIdx, actorKey, EmployeeId, unitKey, now, 12.0);
            return true;
        }

        private static int ResolveProcessIdForIngredient(PizzeriaRoot root, int inputIngredientId, ProcessingStage inputStage, int fallbackId)
        {
            if (root?.Catalog?.Processes != null)
            {
                foreach (var kvp in root.Catalog.Processes)
                {
                    var p = kvp.Value;
                    if (p != null && p.InputIngredientId == inputIngredientId && p.InputStage == inputStage)
                        return p.Id;
                }
            }
            return fallbackId;
        }

        private static bool TryFindProcessProducingIngredient(PizzeriaRoot root, int outputIngredientId, out ProcessData proc)
        {
            proc = null;
            if (root?.Catalog?.Processes == null) return false;
            foreach (var kvp in root.Catalog.Processes)
            {
                if (kvp.Value != null && kvp.Value.OutputIngredientId == outputIngredientId)
                {
                    proc = kvp.Value;
                    return true;
                }
            }
            return false;
        }

        private static bool TryFindAvailableStationTool(
            PizzeriaState state,
            ToolCapability requiredCapability,
            ulong actorKey,
            double now,
            out ToolItemState freeTool)
        {
            freeTool = null;
            if (state == null) return false;
            foreach (var kvp in state.Tools)
            {
                var tool = kvp.Value;
                if (tool == null || !tool.Capabilities.HasFlag(requiredCapability)) continue;
                // Nunca rouba ferramenta segurada na mão de um jogador humano
                if (tool.Location.Type == LocationType.Hand && tool.Location.HolderId != actorKey) continue;
                if (tool.IsReserved(now) && tool.ReservedByWorkerId != actorKey) continue;

                freeTool = tool;
                return true;
            }
            return false;
        }

        private static ItemState FindReadyHandIngredient(PizzeriaState state, ulong actorKey, int ingredientDefId, ProcessingStage stage)
        {
            if (state == null) return null;
            foreach (var kvp in state.Items)
            {
                var it = kvp.Value;
                if (it != null &&
                    it.Location.Type == LocationType.Hand &&
                    it.Location.HolderId == actorKey &&
                    it.DefinitionId == ingredientDefId &&
                    it.PrepStage == stage)
                {
                    return it;
                }
            }
            return null;
        }

        private static ItemState FindItemInSlot(PizzeriaState state, int stationId, int slotIdx)
        {
            var loc = LocationRef.InStation(stationId, slotIdx);
            foreach (var kvp in state.Items)
            {
                if (kvp.Value != null && kvp.Value.Location.Equals(loc))
                    return kvp.Value;
            }
            return null;
        }

        private static bool IsEmployeeRightHandFree(PizzeriaState state, ulong actorKey)
        {
            var loc = LocationRef.InHand(actorKey, HandSlotIndex.Right);
            return !TransferService.IsLocationOccupied(state, loc);
        }

        private static bool CanDispenseIngredientFromAnyStorage(PizzeriaRoot root, int ingredientDefId)
        {
            if (root?.DomainState == null) return false;
            foreach (var kvp in root.DomainState.StorageSlots)
            {
                if (kvp.Key.ingredientDefId == ingredientDefId && kvp.Value != null && kvp.Value.Quantity > 0)
                    return true;
            }
            return false;
        }

        private static void ReleaseSpecificSlotReservation(PizzeriaState state, int stationId, int slotIdx, ulong actorKey)
        {
            if (state == null || stationId <= 0 || slotIdx < 0) return;
            var key = (stationId, slotIdx);
            if (state.StationSlotReservations.TryGetValue(key, out var res) && (res == null || res.ActorKey == actorKey))
            {
                state.StationSlotReservations.Remove(key);
            }
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
            _activeUnitKey = default;
            _lastBlockReason = EmployeeBlockReason.None;
            _retryBackoffUntil = 0f;
            ResetUnitState();
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
            _processStationId = -1;
            _processSlotIndex = -1;
            _packagingStationId = -1;
            _packagingSlotIndex = -1;
            _hasStartedPrepOp = false;
            _hasStartedIngProcessOp = false;
            _hasStartedAssembleOp = false;
            _hasStartedPackageOp = false;
            _leasedToolId = ToolItemId.None;
            _expectedIngredientCountBeforeAsm = 0;
        }

        private static Vector3 ResolveStationPosition<T>(Vector3 fallback) where T : Component
        {
            var found = FindFirstObjectByType<T>();
            return found != null ? found.transform.position : fallback;
        }

        private static int ResolvePrepOrAssemblyStationId(PizzeriaRoot root, out StationView view)
        {
            var prep = FindFirstObjectByType<PrepStation>();
            if (prep != null && prep.TryGetComponent<StationView>(out var sv))
            {
                view = sv;
                return sv.stationId;
            }
            var asm = FindFirstObjectByType<AssemblyStation>();
            if (asm != null && asm.TryGetComponent<StationView>(out var sv2))
            {
                view = sv2;
                return sv2.stationId;
            }
            view = null;
            return 2;
        }

        private static int ResolvePackagingOnlyStationId(PizzeriaRoot root, out StationView view)
        {
            var pkg = FindFirstObjectByType<PackagingStation>();
            if (pkg != null && pkg.TryGetComponent<StationView>(out var sv))
            {
                view = sv;
                return sv.stationId;
            }
            return ResolvePackagingOrStagingStationId(root, out view);
        }

        private static int ResolvePackagingOrStagingStationId(PizzeriaRoot root, out StationView view)
        {
            var stg = FindFirstObjectByType<StagingStation>();
            if (stg != null && stg.TryGetComponent<StationView>(out var sv))
            {
                view = sv;
                return sv.stationId;
            }
            var pkg = FindFirstObjectByType<PackagingStation>();
            if (pkg != null && pkg.TryGetComponent<StationView>(out var sv2))
            {
                view = sv2;
                return sv2.stationId;
            }
            view = null;
            return 5;
        }

        private static int GetStationSlotCapacity(PizzeriaRoot root, int stationId, StationView view)
        {
            int catSlots = int.MaxValue;
            if (root?.Catalog?.Stations != null && root.Catalog.Stations.TryGetValue(stationId, out var stData) && stData != null && stData.SlotCount > 0)
            {
                catSlots = stData.SlotCount;
            }

            int viewSlots = view != null && view.slotAnchors != null && view.slotAnchors.Length > 0
                ? view.slotAnchors.Length
                : int.MaxValue;

            int min = Math.Min(catSlots, viewSlots);
            return min == int.MaxValue ? 4 : Math.Max(1, min);
        }

        private static bool TryFindFreeOvenSlot(PizzeriaRoot root, ulong actorKey, double now, out int ovenStationId, out int slotIndex)
        {
            ovenStationId = 4;
            StationView ovenView = null;
            var oven = FindFirstObjectByType<OvenStation>();
            if (oven != null && oven.TryGetComponent<StationView>(out var sv))
            {
                ovenView = sv;
                ovenStationId = sv.stationId;
            }

            int maxSlots = GetStationSlotCapacity(root, ovenStationId, ovenView);
            slotIndex = FindFirstFreeReservableSlot(root.DomainState, ovenStationId, maxSlots, actorKey, now);
            return slotIndex >= 0;
        }

        private static int FindFirstFreeReservableSlot(PizzeriaState state, int stationId, int maxSlots, ulong actorKey, double now)
        {
            for (int i = 0; i < maxSlots; i++)
            {
                if (OrderUnitFulfillmentService.IsSlotFreeForActor(state, stationId, i, actorKey, now))
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
                        BlockReason = (task != null && task.BlockReason != EmployeeBlockReason.None)
                            ? (byte)task.BlockReason
                            : (hasCtrl ? (byte)ctrl.LastBlockReason : (byte)EmployeeBlockReason.None),
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
                OrderUnitFulfillmentService.EnsureUnitsForOrder(root.DomainState, order);
                if (!root.EmployeeTaskService.TryGetTaskForOrder(order.Id, EmployeeRole.Pizzaiolo, out _))
                {
                    // Só cria se ainda não houver tarefa registrada para este pedido
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
            OrderUnitFulfillmentService.SyncAllActiveOrderTasks(root.DomainState, root.EmployeeTaskService);
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
