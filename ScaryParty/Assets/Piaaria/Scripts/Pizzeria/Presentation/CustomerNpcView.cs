using UnityEngine;
using UnityEngine.AI;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Stations;
using ScaryParty.Player;

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
        private Quaternion _targetRotation = Quaternion.identity;
        private float _walkSpeed = 2.4f;
        private bool _hasReachedApproach;
        private bool _hasReachedSeat;
        private bool _wasSeated;
        private bool _isExitingSeat;
        private float _exitTimer;

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
            bool shouldBeSeated = phase == CustomerPhase.SeatedWaitingFood ||
                                  phase == CustomerPhase.Eating;

            if (Agent != null && Agent.isOnNavMesh)
            {
                Agent.speed = _walkSpeed;
                if (!shouldBeSeated && !_isExitingSeat)
                {
                    Agent.updatePosition = true;
                    Agent.updateRotation = true;
                    Agent.isStopped = false;
                    if (Vector3.Distance(Agent.destination, _targetPosition) > 0.15f)
                    {
                        Agent.SetDestination(_targetPosition);
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
                        Vector3 exitPos = table != null ? table.GetExitPosition() : _targetPosition;
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
                            if (Agent != null && Agent.isOnNavMesh)
                            {
                                Agent.updatePosition = true;
                                Agent.updateRotation = true;
                                Agent.isStopped = false;
                                Agent.SetDestination(_targetPosition);
                            }
                        }
                    }
                }
                else
                {
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
                        transform.position = Vector3.MoveTowards(transform.position, approachPos, Time.deltaTime * _walkSpeed);
                        if (Vector3.Distance(transform.position, approachPos) <= 0.35f)
                        {
                            _hasReachedApproach = true;
                        }
                    }
                }

                if (_hasReachedApproach)
                {
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
            if (VisualController != null)
            {
                bool seatedNow = isSeatedPhase || (isWalkingToSeat && _hasReachedSeat);

                if (seatedNow != _wasSeated && !_isExitingSeat)
                {
                    _wasSeated = seatedNow;
                    VisualController.SetSitting(seatedNow, triggerTransition: true);
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

        public void OnFocus() { }
        public void OnLoseFocus() { }
    }
}
