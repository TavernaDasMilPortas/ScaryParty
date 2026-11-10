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
    /// - Move-se até os slots da fila, caminha até a cadeira da mesa reservada, senta (SitIdle) e sai ao terminar.
    /// - Ao mirar diretamente no NPC (ou no CustomerCounterStation) na frente da fila, segurar [F] por 0,6 s registra o pedido.
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
        private bool _hasReachedSeat;
        private bool _wasSeated;

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

            // Garantir Rigidbody cinemático para física consistente e prevenção de ghosting
            var rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
            rb.isKinematic = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Garantir CapsuleCollider para raycast de interação (atender segurando F diretamente no NPC)
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

            if (Agent != null && Agent.isOnNavMesh)
            {
                Agent.speed = _walkSpeed;
                bool shouldBeSeated = dto.Phase == (byte)CustomerPhase.SeatedWaitingFood ||
                                      dto.Phase == (byte)CustomerPhase.Eating;
                if (!shouldBeSeated)
                {
                    Agent.updatePosition = true;
                    Agent.updateRotation = true;
                    _hasReachedSeat = false;
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

            float distToTarget = Vector3.Distance(transform.position, _targetPosition);
            float currentSpeed = 0f;

            if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
            {
                currentSpeed = Agent.velocity.magnitude;

                if (isWalkingToSeat)
                {
                    // Navegando até a cadeira
                    bool hasValidPath = !Agent.pathPending && Agent.hasPath && Agent.pathStatus == NavMeshPathStatus.PathComplete;
                    bool arrivedAtSeat = distToTarget <= 0.25f || (hasValidPath && Agent.remainingDistance <= 0.25f);
                    if (arrivedAtSeat)
                    {
                        Agent.updatePosition = false;
                        Agent.updateRotation = false;
                        Agent.isStopped = true;
                        _hasReachedSeat = true;
                        transform.position = Vector3.Lerp(transform.position, _targetPosition, Time.deltaTime * 6f);
                        transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, Time.deltaTime * 10f);
                    }
                    else
                    {
                        Agent.updatePosition = true;
                        Agent.updateRotation = true;
                        Agent.isStopped = false;
                        _hasReachedSeat = false;
                        if (distToTarget <= 0.40f)
                        {
                            transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, Time.deltaTime * 6f);
                        }
                    }
                }
                else if (isSeatedPhase)
                {
                    // Já na mesa/comendo
                    Agent.updatePosition = false;
                    Agent.updateRotation = false;
                    Agent.isStopped = true;
                    _hasReachedSeat = true;
                    transform.position = Vector3.Lerp(transform.position, _targetPosition, Time.deltaTime * 8f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, Time.deltaTime * 10f);
                }
                else
                {
                    // Em fila ou saindo
                    Agent.updatePosition = true;
                    Agent.updateRotation = true;
                    Agent.isStopped = false;
                    _hasReachedSeat = false;
                    if (distToTarget <= 0.25f)
                    {
                        transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, Time.deltaTime * 8f);
                    }
                }
            }
            else
            {
                // Tenta recuperar o NavMeshAgent se estiver desabilitado ou fora de malha
                if (Agent != null && UnityEngine.AI.NavMesh.SamplePosition(transform.position, out var hit, 2.5f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    Agent.enabled = true;
                    Agent.Warp(hit.position);
                    Agent.SetDestination(_targetPosition);
                }
                else if (isSeatedPhase || (isWalkingToSeat && distToTarget <= 0.35f))
                {
                    transform.position = Vector3.Lerp(transform.position, _targetPosition, Time.deltaTime * 6f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, Time.deltaTime * 10f);
                    if (distToTarget <= 0.25f) _hasReachedSeat = true;
                }
                else
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, Time.deltaTime * 10f);
                }
            }

            if (VisualController != null)
            {
                bool seatedNow = isSeatedPhase || (isWalkingToSeat && _hasReachedSeat && distToTarget <= 0.25f);
                if (seatedNow != _wasSeated)
                {
                    _wasSeated = seatedNow;
                    VisualController.SetSitting(seatedNow, triggerTransition: true);
                }

                VisualController.SetPreparing(false);

                if (seatedNow)
                {
                    VisualController.SetLocomotion(0f, 1f, true, false, false);
                }
                else
                {
                    // Remapear para a velocidade do BlendTree (Speed: 0 = Idle, 2.0 = Walk, 5.335 = Run)
                    float animSpeed = 0f;
                    if (currentSpeed > 0.08f)
                    {
                        // Escala velocidade real do NavMeshAgent (~2.0 - 2.5 m/s) para o patamar de Walk (2.0f)
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
