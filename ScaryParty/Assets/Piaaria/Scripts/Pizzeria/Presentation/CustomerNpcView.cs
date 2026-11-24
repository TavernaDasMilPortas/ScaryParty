using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Presentation.Feedback;
using ScaryParty.Pizzeria.Stations;
using ScaryParty.Player;
using UnityEngine;
using UnityEngine.AI;

namespace ScaryParty.Pizzeria.Presentation
{
    /// <summary>
    /// Controlador visual e de navegação de um cliente presencial (NPC).
    /// - Reutiliza o mesmo rig/ CharacterVisualController / CharacterAppearance do player (sem scripts de input ou câmera).
    /// - Integração completa com as 4 âncoras da mesa (Approach, Seat, SeatSurface, Exit).
    /// - Caminha até a âncora de aproximação fora da área esculpida pelo NavMeshObstacle da mesa.
    /// - Desacopla suavemente o NavMeshAgent para sentar-se na cadeira com animação SitDown/SitIdle.
    /// - Ao finalizar, executa animação StandUp, desloca-se até o ponto de saída e retoma o NavMeshAgent para sair do estabelecimento.
    /// - Ao mirar diretamente no NPC na frente da fila, segurar [F] por 0,6 s registra o pedido.
    /// </summary>
    public class CustomerNpcView : MonoBehaviour, IInteractable
    {
        public ulong CustomerId { get; private set; }
        public NetCustomerDto CurrentState { get; private set; }

        public CharacterVisualController VisualController { get; private set; }
        public CharacterAppearance Appearance { get; private set; }
        public NavMeshAgent Agent { get; private set; }

        private Vector3 _targetPosition;
        private Vector3 _lastFramePosition;
        private bool _hasLastFramePosition;
        private Quaternion _targetRotation = Quaternion.identity;
        private float _walkSpeed = 2.4f;
        private bool _hasReachedApproach;
        private bool _hasReachedSeat;
        private bool _wasSeated;
        private bool _isExitingSeat;
        private float _exitTimer;
        private bool _hasLastFeelState;
        private CustomerPhase _lastFeelPhase;
        private bool _impatienceTriggered;
        private float _nextMicroFidgetTime;
        private DiningTableStation _chairOccupantTable;
        private DiningTableStation _lastSeatedTable;
        private bool _loggedNoNavMeshWarning;

        private void OnDisable()
        {
            ReleaseClaimedChair();
            _hasReachedApproach = false;
            _hasReachedSeat = false;
            _isExitingSeat = false;
        }

        private void OnDestroy()
        {
            ReleaseClaimedChair();
        }

        private void UpdateChairOccupancyVisual(DiningTableStation table, bool occupied)
        {
            if (occupied)
            {
                if (_chairOccupantTable != null && _chairOccupantTable != table)
                {
                    _chairOccupantTable.SetChairOccupiedVisual(0, false);
                }
                _chairOccupantTable = table;
                if (_chairOccupantTable != null)
                {
                    _chairOccupantTable.SetChairOccupiedVisual(0, true);
                }
            }
            else if (_chairOccupantTable != null)
            {
                _chairOccupantTable.SetChairOccupiedVisual(0, false);
                _chairOccupantTable = null;
            }
        }

        private void ReleaseClaimedChair()
        {
            if (_chairOccupantTable != null)
            {
                _chairOccupantTable.SetChairOccupiedVisual(0, false);
                _chairOccupantTable = null;
            }
        }

        public bool HasReachedSeat => _hasReachedSeat;

        private static readonly Color[] NpcPalette = new[]
        {
            new Color(0.85f, 0.32f, 0.28f),
            new Color(0.25f, 0.58f, 0.85f),
            new Color(0.32f, 0.75f, 0.42f),
            new Color(0.88f, 0.68f, 0.22f),
            new Color(0.68f, 0.38f, 0.82f),
            new Color(0.22f, 0.75f, 0.72f),
            new Color(0.90f, 0.48f, 0.25f),
            new Color(0.75f, 0.35f, 0.58f)
        };

        public void Initialize(NetCustomerDto dto, GameObject visualInstance, float walkSpeedMultiplier = 1f)
        {
            CustomerId = dto.CustomerId;
            CurrentState = dto;
            _walkSpeed = 2.4f * Mathf.Clamp(walkSpeedMultiplier, 0.7f, 1.5f);
            _hasLastFramePosition = false;

            if (visualInstance != null)
            {
                visualInstance.transform.SetParent(transform, false);
                visualInstance.transform.localPosition = Vector3.zero;
                visualInstance.transform.localRotation = Quaternion.identity;
                VisualController = visualInstance.GetComponent<CharacterVisualController>()
                                ?? visualInstance.AddComponent<CharacterVisualController>();
                Appearance = visualInstance.GetComponent<CharacterAppearance>()
                          ?? visualInstance.AddComponent<CharacterAppearance>();

                VisualController.ResolveReferences();
                if (VisualController.Animator != null)
                {
                    VisualController.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    VisualController.Animator.applyRootMotion = false;
                }
                VisualController.SetPreparing(false);
                VisualController.SetSitting(false, false);
                VisualController.SetLocomotion(0f, 1f, true);
                VisualController.EnsureFeelPivot();
                VisualController.DesynchronizeIdleCycle(dto.CustomerId);
            }

            _hasLastFeelState = true;
            _lastFeelPhase = (CustomerPhase)dto.Phase;
            _impatienceTriggered = false;
            _nextMicroFidgetTime = Time.time + 2.5f + ((dto.CustomerId % 7UL) * 0.45f);

            if (visualInstance != null)
            {
                ScaryParty.VisualStyles.VisualStyleManager.ApplyHierarchyOrFallback(
                    visualInstance,
                    ScaryParty.VisualStyles.VisualSemanticRole.NpcBody,
                    ScaryParty.VisualStyles.VisualCategoryIds.Characters);
                ScaryParty.Lighting.PizzeriaLightingController.Active?.ApplyZoneFillToHierarchy(visualInstance);
            }

            if (Appearance != null)
            {
                Color c = NpcPalette[Mathf.Abs(dto.VisualColorIndex) % NpcPalette.Length];
                Appearance.ApplyPrimaryColor(c);
            }

            // Rigidbody cinemático para física consistente e prevenção de ghosting
            var rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
            rb.isKinematic = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // CapsuleCollider para raycast de interação (atender segurando F diretamente no NPC)
            var col = GetComponent<CapsuleCollider>();
            if (col == null)
            {
                col = gameObject.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0f, 0.9f, 0f);
                col.radius = 0.35f;
                col.height = 1.8f;
            }

            if (GetComponent<ScaryParty.Interaction.OutlineTarget>() == null)
            {
                gameObject.AddComponent<ScaryParty.Interaction.OutlineTarget>();
            }

            Agent = GetComponent<NavMeshAgent>();
            if (Agent != null)
            {
                Agent.speed = _walkSpeed;
                Agent.angularSpeed = 360f;
                Agent.acceleration = 12f;
                Agent.stoppingDistance = 0.12f;
            }
        }

        public void ApplyState(NetCustomerDto dto, Vector3 targetPos, Quaternion targetRot, float walkSpeedMultiplier = 1f)
        {
            CurrentState = dto;
            _targetPosition = targetPos;
            _targetRotation = Quaternion.Euler(0f, targetRot.eulerAngles.y, 0f);
            _walkSpeed = 2.4f * Mathf.Clamp(walkSpeedMultiplier, 0.7f, 1.5f);

            var phase = (CustomerPhase)dto.Phase;
            ProcessCustomerFeelTransitions(dto, phase);
            bool shouldBeSeated = phase == CustomerPhase.SeatedWaitingFood ||
                                  phase == CustomerPhase.Eating;

            if (Agent != null && Agent.isOnNavMesh)
            {
                Agent.speed = _walkSpeed;
                if (!shouldBeSeated && !_isExitingSeat && !(phase == CustomerPhase.WalkingToTable && _hasReachedApproach))
                {
                    Agent.updatePosition = true;
                    Agent.updateRotation = true;
                    Agent.isStopped = false;
                    if (Vector3.Distance(Agent.destination, _targetPosition) > 0.15f)
                    {
                        if (UnityEngine.AI.NavMesh.SamplePosition(_targetPosition, out var navHit, 2.5f, UnityEngine.AI.NavMesh.AllAreas))
                        {
                            var path = new UnityEngine.AI.NavMeshPath();
                            if (UnityEngine.AI.NavMesh.CalculatePath(transform.position, navHit.position, UnityEngine.AI.NavMesh.AllAreas, path) &&
                                path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete)
                            {
                                Agent.SetDestination(navHit.position);
                            }
                        }
                    }
                }
            }
        }

        private void Update()
        {
            var phase = (CustomerPhase)CurrentState.Phase;
            bool isSeatedPhase = phase == CustomerPhase.SeatedWaitingFood ||
                                 phase == CustomerPhase.Eating;
            bool isWalkingToSeat = phase == CustomerPhase.WalkingToTable;
            bool isLeaving = phase == CustomerPhase.LeavingSatisfied ||
                             phase == CustomerPhase.LeavingAngry;

            DiningTableStation table = null;
            if (CurrentState.AssignedTableId > 0)
            {
                if (!DiningTableStation.TryGetTable(CurrentState.AssignedTableId, out table))
                {
                    var allTables = FindObjectsByType<DiningTableStation>(FindObjectsSortMode.None);
                    for (int i = 0; i < allTables.Length; i++)
                    {
                        if (allTables[i] != null && allTables[i].tableId == CurrentState.AssignedTableId)
                        {
                            table = allTables[i];
                            break;
                        }
                    }
                }
                if (table != null) _lastSeatedTable = table;
            }
            else if (isLeaving && _lastSeatedTable != null)
            {
                table = _lastSeatedTable;
            }

            float currentSpeed = 0f;

            if (isLeaving)
            {
                if (_wasSeated)
                {
                    if (!_isExitingSeat)
                    {
                        _isExitingSeat = true;
                        _exitTimer = 0.8f; // Duração correspondente à animação StandUp reconstruída
                        _hasReachedSeat = false;
                        _hasReachedApproach = false;
                        if (VisualController != null)
                        {
                            VisualController.SetSitting(false, triggerTransition: true);
                        }
                    }

                    if (_isExitingSeat)
                    {
                        _exitTimer -= Time.deltaTime;
                        Vector3 exitPos = table != null ? table.GetExitPosition() : transform.position;
                        if (table == null && UnityEngine.AI.NavMesh.SamplePosition(transform.position, out var safeExitHit, 3.0f, UnityEngine.AI.NavMesh.AllAreas))
                        {
                            exitPos = safeExitHit.position;
                        }
                        transform.position = Vector3.MoveTowards(transform.position, exitPos, Time.deltaTime * 1.3f);
                        Vector3 exitDir = exitPos - transform.position;
                        exitDir.y = 0f;
                        if (exitDir.sqrMagnitude > 0.001f)
                        {
                            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(exitDir.normalized, Vector3.up), Time.deltaTime * 240f);
                        }

                        if (_exitTimer <= 0f || Vector3.Distance(transform.position, exitPos) <= 0.20f)
                        {
                            _isExitingSeat = false;
                            _wasSeated = false;
                            _lastSeatedTable = null;
                            ReleaseClaimedChair();
                            if (Agent != null)
                            {
                                if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out var warpHit, 3.0f, UnityEngine.AI.NavMesh.AllAreas))
                                {
                                    Agent.enabled = true;
                                    Agent.Warp(warpHit.position);
                                }
                                if (Agent.isOnNavMesh)
                                {
                                    Agent.updatePosition = true;
                                    Agent.updateRotation = true;
                                    Agent.isStopped = false;
                                    Agent.SetDestination(_targetPosition);
                                }
                            }
                        }
                    }
                }
                else
                {
                    ReleaseClaimedChair();
                    if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
                    {
                        Agent.updatePosition = true;
                        Agent.updateRotation = true;
                        Agent.isStopped = false;
                        currentSpeed = Agent.velocity.magnitude;
                    }
                }
            }
            else if (isWalkingToSeat)
            {
                Vector3 approachPos = table != null ? table.GetApproachPosition() : _targetPosition;
                Vector3 seatPos = table != null ? table.GetSeatPosition() : _targetPosition;
                Quaternion seatRot = table != null ? table.GetSeatRotation() : _targetRotation;

                if (!_hasReachedApproach)
                {
                    if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
                    {
                        Agent.updatePosition = true;
                        Agent.updateRotation = true;
                        Agent.isStopped = false;
                        currentSpeed = Agent.velocity.magnitude;

                        if (Vector3.Distance(Agent.destination, approachPos) > 0.2f)
                        {
                            Agent.SetDestination(approachPos);
                        }

                        bool arrivedAtApproach = Vector3.Distance(transform.position, approachPos) <= 0.35f ||
                                                (!Agent.pathPending && Agent.hasPath && Agent.remainingDistance <= 0.25f);

                        if (arrivedAtApproach)
                        {
                            _hasReachedApproach = true;
                        }
                    }
                    else
                    {
                        // A07: Tentar reinserção no NavMesh antes de qualquer fallback; nunca atravessar obstáculos em linha reta
                        if (Agent != null && UnityEngine.AI.NavMesh.SamplePosition(transform.position, out var navHit, 2.5f, UnityEngine.AI.NavMesh.AllAreas))
                        {
                            Agent.enabled = true;
                            Agent.Warp(navHit.position);
                            Agent.SetDestination(approachPos);
                            _loggedNoNavMeshWarning = false;
                        }
                        else
                        {
                            float distToApproach = Vector3.Distance(transform.position, approachPos);
                            Vector3 rayStart = transform.position + Vector3.up * 0.5f;
                            Vector3 rayEnd = approachPos + Vector3.up * 0.5f;
                            bool obstructed = Physics.Linecast(rayStart, rayEnd, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

                            if (!obstructed && distToApproach <= 1.5f)
                            {
                                transform.position = Vector3.MoveTowards(transform.position, approachPos, Time.deltaTime * _walkSpeed);
                                if (Vector3.Distance(transform.position, approachPos) <= 0.35f)
                                {
                                    _hasReachedApproach = true;
                                }
                            }
                            else if (!_loggedNoNavMeshWarning)
                            {
                                _loggedNoNavMeshWarning = true;
                                Debug.LogWarning($"[CustomerNpcView] NPC {CustomerId} sem NavMesh válido para alcançar {approachPos} (obstructed={obstructed}, dist={distToApproach:F2}m). Movimento bloqueado para evitar atravessar obstáculos.");
                            }
                        }
                    }
                }

                if (_hasReachedApproach)
                {
                    UpdateChairOccupancyVisual(table, true);
                    if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
                    {
                        Agent.updatePosition = false;
                        Agent.updateRotation = false;
                        Agent.isStopped = true;
                    }

                    transform.position = Vector3.MoveTowards(transform.position, seatPos, Time.deltaTime * 1.4f);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, seatRot, Time.deltaTime * 360f);

                    float distToSeat = Vector3.Distance(transform.position, seatPos);
                    float angleToSeat = Quaternion.Angle(transform.rotation, seatRot);

                    if (distToSeat <= 0.05f && angleToSeat <= 6f)
                    {
                        _hasReachedSeat = true;
                        transform.position = seatPos;
                        transform.rotation = seatRot;
                    }
                }
            }
            else if (isSeatedPhase)
            {
                UpdateChairOccupancyVisual(table, true);
                Vector3 seatPos = table != null ? table.GetSeatPosition() : _targetPosition;
                Quaternion seatRot = table != null ? table.GetSeatRotation() : _targetRotation;

                if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
                {
                    Agent.updatePosition = false;
                    Agent.updateRotation = false;
                    Agent.isStopped = true;
                }

                _hasReachedApproach = true;
                _hasReachedSeat = true;
                transform.position = Vector3.Lerp(transform.position, seatPos, Time.deltaTime * 8f);
                transform.rotation = Quaternion.Slerp(transform.rotation, seatRot, Time.deltaTime * 10f);
            }
            else
            {
                // Em fila no balcão ou entrando
                _hasReachedSeat = false;
                _hasReachedApproach = false;

                if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
                {
                    Agent.updatePosition = true;
                    Agent.updateRotation = true;
                    Agent.isStopped = false;
                    currentSpeed = Agent.velocity.magnitude;

                    float distToTarget = Vector3.Distance(transform.position, _targetPosition);
                    if (distToTarget <= 0.25f)
                    {
                        transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, Time.deltaTime * 8f);
                    }
                }
                else
                {
                    // Fallback para reinserção no NavMesh
                    if (Agent != null && UnityEngine.AI.NavMesh.SamplePosition(transform.position, out var hit, 2.5f, UnityEngine.AI.NavMesh.AllAreas))
                    {
                        Agent.enabled = true;
                        Agent.Warp(hit.position);
                        Agent.SetDestination(_targetPosition);
                    }
                }
            }

            // Atualização do Animator e estados visuais
            Vector3 currentFramePos = transform.position;
            if (_hasLastFramePosition && Time.deltaTime > 0.0001f)
            {
                float horizontalDisplacement = Vector2.Distance(
                    new Vector2(currentFramePos.x, currentFramePos.z),
                    new Vector2(_lastFramePosition.x, _lastFramePosition.z));
                float frameSpeed = horizontalDisplacement / Time.deltaTime;
                if (frameSpeed < 12f)
                {
                    currentSpeed = Mathf.Max(currentSpeed, frameSpeed);
                }
            }
            _lastFramePosition = currentFramePos;
            _hasLastFramePosition = true;

            if (VisualController != null)
            {
                bool seatedNow = isSeatedPhase || (isWalkingToSeat && _hasReachedSeat);

                if (seatedNow != _wasSeated && !_isExitingSeat)
                {
                    _wasSeated = seatedNow;
                    VisualController.SetSitting(seatedNow, triggerTransition: true);
                    if (seatedNow && IsWithinMicroReactionDistance())
                    {
                        VisualController.PlayBodyReaction(
                            GameFeelEventId.NpcSeatedSettle,
                            0.9f,
                            (int)CurrentState.Revision,
                            CurrentState.CustomerId);
                    }
                }

                if (Time.time >= _nextMicroFidgetTime)
                {
                    _nextMicroFidgetTime = Time.time + 3.6f + ((CustomerId % 5UL) * 0.5f);
                    var currentPhase = (CustomerPhase)CurrentState.Phase;
                    if (!GameFeelSettings.ReduceMotion && IsWithinMicroReactionDistance())
                    {
                        if (currentPhase == CustomerPhase.Eating)
                        {
                            VisualController.PlayBodyReaction(GameFeelEventId.NpcEatingLoop, 0.7f, (int)Time.frameCount, CustomerId);
                        }
                        else if ((currentPhase == CustomerPhase.WaitingInQueue || currentPhase == CustomerPhase.SeatedWaitingFood) &&
                                 CurrentState.ActivePatienceNormalized > 0f && CurrentState.ActivePatienceNormalized <= 0.45f)
                        {
                            VisualController.PlayBodyReaction(GameFeelEventId.NpcImpatient, 0.8f, (int)Time.frameCount, CustomerId);
                        }
                    }
                }

                VisualController.SetPreparing(false);

                if (seatedNow)
                {
                    VisualController.SetLocomotion(0f, 1f, true, false, false);
                }
                else if (_isExitingSeat)
                {
                    VisualController.SetLocomotion(0.5f, 1f, true, false, false);
                }
                else
                {
                    float animSpeed = 0f;
                    if (currentSpeed > 0.08f)
                    {
                        float speedRatio = Mathf.Clamp01(currentSpeed / Mathf.Max(0.1f, _walkSpeed));
                        animSpeed = Mathf.Lerp(1.6f, 2.4f, speedRatio);
                    }
                    VisualController.SetLocomotion(animSpeed, 1f, true, false, false);
                }
            }
        }

        public string InteractPrompt
        {
            get
            {
                var catalog = ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance?.Catalog;
                var spec = CurrentState.RequestedSpec.ToDomain();
                string pizzaText = PizzaOrderFormatter.FormatShortSummary(spec, catalog);

                string posTraitName = catalog != null && catalog.CustomerTraits.TryGetValue(CurrentState.PositiveTraitId, out var pt) ? pt.Name : "Paciente";
                string negTraitName = catalog != null && catalog.CustomerTraits.TryGetValue(CurrentState.NegativeTraitId, out var nt) ? nt.Name : "Apressado";
                string traitsTag = $"[{posTraitName} / {negTraitName}]";

                if (CurrentState.IsInQueue)
                {
                    if (CurrentState.QueueSlotIndex == 0)
                    {
                        return $"[Segure F] Atender Cliente (Fila {CurrentState.QueueIndex + 1}) {traitsTag} — {pizzaText}";
                    }
                    return $"Cliente na Fila {CurrentState.QueueIndex + 1} (#{CurrentState.QueueSlotIndex + 1}) {traitsTag}";
                }

                if (CurrentState.IsWaitingForFood && CurrentState.AssignedTableId > 0)
                {
                    return $"[E] Servir Mesa {CurrentState.AssignedTableId} (Pizza sem caixa) {traitsTag} — {pizzaText}";
                }

                if (CurrentState.Phase == (byte)CustomerPhase.Eating)
                {
                    return $"Cliente comendo na Mesa {CurrentState.AssignedTableId} {traitsTag}";
                }

                return $"Cliente {traitsTag}";
            }
        }

        public void OnInteract(GameObject interactor)
        {
            if (CurrentState.IsInQueue && CurrentState.QueueSlotIndex == 0)
            {
                NotificationManager.Show("Segure [F] por 0,6 s para anotar o pedido deste cliente!", NotificationType.Info);
                return;
            }

            if (CurrentState.IsWaitingForFood && CurrentState.AssignedTableId > 0)
            {
                // Se o jogador apertou [E] olhando diretamente para o NPC sentado na mesa, delegar para a DiningTableStation correspondente
                if (DiningTableStation.TryGetTable(CurrentState.AssignedTableId, out var table))
                {
                    table.TryServeTableFromPlayer(interactor);
                    return;
                }
                var tables = FindObjectsByType<DiningTableStation>(FindObjectsSortMode.None);
                for (int i = 0; i < tables.Length; i++)
                {
                    if (tables[i] != null && tables[i].tableId == CurrentState.AssignedTableId)
                    {
                        tables[i].TryServeTableFromPlayer(interactor);
                        return;
                    }
                }
            }
        }

        private void ProcessCustomerFeelTransitions(in NetCustomerDto dto, CustomerPhase phase)
        {
            if (VisualController == null) return;

            if (!_hasLastFeelState)
            {
                _hasLastFeelState = true;
                _lastFeelPhase = phase;
                return;
            }

            if (phase != _lastFeelPhase)
            {
                _lastFeelPhase = phase;
                _impatienceTriggered = false;
                switch (phase)
                {
                    case CustomerPhase.WaitingInQueue:
                        if (IsWithinMicroReactionDistance())
                        {
                            VisualController.PlayBodyReaction(GameFeelEventId.NpcCounterArrived, 0.9f, (int)dto.Revision, dto.CustomerId);
                        }
                        break;
                    case CustomerPhase.SeatedWaitingFood:
                        if (IsWithinMicroReactionDistance())
                        {
                            VisualController.PlayBodyReaction(GameFeelEventId.NpcSeatedSettle, 0.85f, (int)dto.Revision, dto.CustomerId);
                        }
                        break;
                    case CustomerPhase.Eating:
                        VisualController.PlayBodyReaction(GameFeelEventId.NpcOrderGood, 1.05f, (int)dto.Revision, dto.CustomerId);
                        break;
                    case CustomerPhase.LeavingSatisfied:
                        VisualController.PlayBodyReaction(GameFeelEventId.NpcOrderGood, 1.15f, (int)dto.Revision, dto.CustomerId);
                        break;
                    case CustomerPhase.LeavingAngry:
                        VisualController.PlayBodyReaction(GameFeelEventId.NpcLeavingAngry, 1.15f, (int)dto.Revision, dto.CustomerId);
                        break;
                }
            }

            if ((phase == CustomerPhase.WaitingInQueue || phase == CustomerPhase.SeatedWaitingFood) &&
                !_impatienceTriggered && dto.ActivePatienceNormalized > 0f && dto.ActivePatienceNormalized <= 0.30f)
            {
                _impatienceTriggered = true;
                if (IsWithinMicroReactionDistance())
                {
                    VisualController.PlayBodyReaction(GameFeelEventId.NpcImpatient, 0.9f, (int)dto.Revision, dto.CustomerId);
                }
            }
            else if (dto.ActivePatienceNormalized > 0.38f)
            {
                _impatienceTriggered = false;
            }
        }

        private bool IsWithinMicroReactionDistance()
        {
            var cam = Camera.main;
            if (cam == null) return true;
            return (cam.transform.position - transform.position).sqrMagnitude <= 400f;
        }

        public void OnFocus() { }
        public void OnLoseFocus() { }
    }
}
