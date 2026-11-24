using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Presentation;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace ScaryParty.Combat.Monsters
{
    /// <summary>
    /// Base comum de estado e combate para NPCs/Monstros (Humano agressivo, Vampiro, Lobisomem, Fantasma e Zumbi).
    /// Separa autoridade de decisão (servidor) da apresentação visual interpolada (host e clientes).
    /// </summary>
    public class MonsterNpcController : MonoBehaviour, IDamageable
    {
        [Header("Espécie e Capacidades")]
        [SerializeField] private NpcSpecies species = NpcSpecies.Vampire;
        [SerializeField] private int maxHealth = 130;
        [SerializeField] private int attackDamage = 20;
        [SerializeField] private float attackRange = 1.9f;
        [SerializeField] private float attackCooldown = 1.2f;
        [SerializeField] private float acquisitionRange = 15f;
        private float _baseAcquisitionRange = -1f;
        [SerializeField] private float disengageDistance = 22f;
        [SerializeField] private float disengageSeconds = 5f;

        [Header("Capacidades Específicas")]
        [SerializeField] private float lungeSpeed = 11f;
        [SerializeField] private float lungePrepSeconds = 0.55f;
        [SerializeField] private float lungeDuration = 0.45f;
        [SerializeField] private float lungeRecoverySeconds = 1.1f;
        [SerializeField] private float blinkCooldown = 4.5f;
        [SerializeField] private float blinkDistance = 5.5f;
        [SerializeField] private float blinkTelegraphSeconds = 0.45f;
        [SerializeField] private float ghostChaseFloatOffset = 1.15f;
        [SerializeField] private LayerMask ghostObstacleMask = 0;

        [Header("Apresentação")]
        [SerializeField] private Renderer[] eyeRenderers;
        [SerializeField] private Color calmEyeColor = Color.white;
        [SerializeField] private Color aggressiveEyeColor = Color.red;

        private readonly System.Random _rng = new System.Random();

        private Animator _animator;
        private NavMeshAgent _agent;
        private Collider _mainCollider;
        private Transform _visualRoot;
        private Renderer[] _bodyRenderers;
        private MaterialPropertyBlock _flashMpb;
        private float _damageFlashTimer;
        private byte _clientTelegraphKind;
        private Transform _currentTarget;
        private float _outOfRangeTimer;
        private float _attackCooldownTimer;
        private float _lungePrepTimer;
        private float _lungeTimer;
        private float _recoveryTimer;
        private float _blinkTimer;
        private float _blinkTelegraphTimer;
        private Vector3 _pendingBlinkDest;
        private bool _hasPendingBlink;
        private float _dropTelegraphTimer;
        private bool _isPerchedOnRooftop;
        private bool _isDirectlyProvoked;
        private bool _configuredOnServer;
        private Vector3 _lungeDirection;
        private bool _lungeHitApplied;
        private NpcBehaviorState _localBehaviorState = NpcBehaviorState.Routine;
        private int _localHealth = 130;
        private NpcBehaviorState _lastVisualState = NpcBehaviorState.Routine;
        private bool _isClientProxy;
        private Vector3 _clientTargetPos;
        private float _clientTargetYaw;
        private bool _hasClientTarget;
        private float _deadHideTimer;
        private float _routineDecisionTimer;
        private float _customerVisitCooldown = 12f;
        private bool _hasRoutineDest;
        private Vector3 _routineDest;
        private CustomerId _boundCustomerId = CustomerId.None;

        public ulong MonsterId { get; private set; }
        public uint Revision { get; private set; } = 1;
        public NpcSpecies Species => species;
        public int MaxHealth => maxHealth;
        public int CurrentHealth => _localHealth;
        public NpcBehaviorState CurrentBehaviorState => _localBehaviorState;
        public bool IsAlive => CurrentHealth > 0 && CurrentBehaviorState != NpcBehaviorState.Dead;
        public byte CurrentTelegraphKind
        {
            get
            {
                if (_isClientProxy) return _clientTelegraphKind;
                if (_dropTelegraphTimer > 0f) return 1;
                if (_localBehaviorState == NpcBehaviorState.Attacking && _lungePrepTimer > 0f) return 2;
                if (_hasPendingBlink && _blinkTelegraphTimer > 0f) return 3;
                if (_localBehaviorState == NpcBehaviorState.Customer && _boundCustomerId.IsValid &&
                    PizzeriaRoot.Instance != null && PizzeriaRoot.Instance.DomainState != null &&
                    PizzeriaRoot.Instance.DomainState.Customers.TryGetValue(_boundCustomerId, out var cust) &&
                    (cust.Phase == CustomerPhase.SeatedWaitingFood || cust.Phase == CustomerPhase.Eating))
                {
                    return 4;
                }
                return 0;
            }
        }
        private bool IsAuthoritativeServer => !_isClientProxy && _configuredOnServer && (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer);

        public static float ComputeEffectiveAcquisitionRange(float baseRange, float aggressionMultiplier, bool isEligibleAggressiveVampire)
        {
            return isEligibleAggressiveVampire
                ? Mathf.Max(1f, baseRange * Mathf.Max(0.2f, aggressionMultiplier))
                : baseRange;
        }

        public float EffectiveAcquisitionRange
        {
            get
            {
                float baseRange = _baseAcquisitionRange > 0f ? _baseAcquisitionRange : acquisitionRange;
                bool eligible = species == NpcSpecies.Vampire && !_boundCustomerId.IsValid && (ShouldEnterHostileStateServer() || _localBehaviorState == NpcBehaviorState.Aggressive);
                float mult = PizzeriaRoot.Instance != null && PizzeriaRoot.Instance.RunProgressionService != null
                    ? PizzeriaRoot.Instance.RunProgressionService.RecalculateEffectiveStats().VampireAggressionMultiplier
                    : 1f;
                return ComputeEffectiveAcquisitionRange(baseRange, mult, eligible);
            }
        }

        private void Awake()
        {
            _baseAcquisitionRange = acquisitionRange;
            _agent = GetComponent<NavMeshAgent>();
            _mainCollider = GetComponent<Collider>();
            _visualRoot = transform.Find("VisualRoot") ?? transform;
            _animator = GetComponentInChildren<Animator>(true);
            CacheBodyRenderers();
        }

        private void CacheBodyRenderers()
        {
            _visualRoot = transform.Find("VisualRoot") ?? transform;
            var all = _visualRoot.GetComponentsInChildren<Renderer>(true);
            var list = new System.Collections.Generic.List<Renderer>(all.Length);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name != "Eyes")
                    list.Add(all[i]);
            }
            _bodyRenderers = list.ToArray();
        }

        public void ConfigureClientProxy(ulong monsterId, NpcSpecies npcSpecies, Renderer[] eyes, NetMonsterDto initialDto)
        {
            MonsterId = monsterId;
            species = npcSpecies;
            _isClientProxy = true;
            _configuredOnServer = false;
            _mainCollider = GetComponent<Collider>();
            if (eyes != null && eyes.Length > 0) eyeRenderers = eyes;
            _animator = GetComponentInChildren<Animator>(true);
            CacheBodyRenderers();
            ApplyReplicatedDto(initialDto, snapImmediately: true);
        }

        public void ApplyReplicatedDto(NetMonsterDto dto, bool snapImmediately = false)
        {
            int previousHp = _localHealth;
            maxHealth = Mathf.Max(1, dto.MaxHp);
            _localHealth = Mathf.Max(0, dto.CurrentHp);
            _localBehaviorState = (NpcBehaviorState)dto.BehaviorState;
            _clientTelegraphKind = dto.TelegraphKind;
            Revision = dto.Revision;
            _clientTargetPos = new Vector3(dto.PosX, dto.PosY, dto.PosZ);
            _clientTargetYaw = dto.Yaw;
            if (_hasClientTarget && _localHealth < previousHp)
            {
                TriggerDamageFlash();
            }
            if (snapImmediately || !_hasClientTarget || (_clientTargetPos - transform.position).sqrMagnitude > 36f)
            {
                transform.position = _clientTargetPos;
                transform.rotation = Quaternion.Euler(0f, _clientTargetYaw, 0f);
            }
            _hasClientTarget = true;
            if (!IsAlive && _mainCollider != null && _mainCollider.enabled)
            {
                _mainCollider.enabled = false;
            }
            UpdateEyePresentation();
        }

        public void ConfigureServer(
            ulong monsterId,
            NpcSpecies npcSpecies,
            SpeciesCapabilityProfile profile,
            bool perchedOnRooftop,
            Renderer[] eyes)
        {
            MonsterId = monsterId;
            species = npcSpecies;
            if (profile != null)
            {
                maxHealth = Mathf.Max(1, profile.MaxHealth);
                attackDamage = Mathf.Max(1, profile.AttackDamage);
                attackCooldown = Mathf.Max(0.3f, profile.AttackCooldown);
                acquisitionRange = profile.AcquisitionRange;
                _baseAcquisitionRange = acquisitionRange;
                disengageDistance = profile.DisengageDistance;
                disengageSeconds = profile.DisengageSeconds;
            }
            _isPerchedOnRooftop = perchedOnRooftop && species == NpcSpecies.Vampire;
            if (eyes != null && eyes.Length > 0) eyeRenderers = eyes;
            _configuredOnServer = true;
            _isClientProxy = false;
            _localHealth = maxHealth;
            _mainCollider = GetComponent<Collider>();
            _animator = GetComponentInChildren<Animator>(true);
            CacheBodyRenderers();
            SetBehaviorStateInternal(NpcBehaviorState.Routine);
            if (_agent != null && _isPerchedOnRooftop)
                _agent.enabled = false;
            UpdateEyePresentation();
        }

        private void SetBehaviorStateInternal(NpcBehaviorState next)
        {
            var prev = _localBehaviorState;
            _localBehaviorState = next;
            Revision++;

            // BUG-CMB-02: Se o bote do Lobisomem foi interrompido externamente (dano/mudança de alvo), restaura controle do NavMeshAgent
            if (prev == NpcBehaviorState.Attacking && next != NpcBehaviorState.Attacking && next != NpcBehaviorState.Dead)
            {
                _lungePrepTimer = 0f;
                _lungeTimer = 0f;
                if (_agent != null && _agent.enabled)
                {
                    if (!_agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out var navHit, 4.0f, NavMesh.AllAreas))
                        _agent.Warp(navHit.position);
                    _agent.updatePosition = true;
                    if (_agent.isOnNavMesh) _agent.isStopped = false;
                }
            }
        }

        private void SetHealthInternal(int nextHp)
        {
            _localHealth = Mathf.Max(0, nextHp);
            Revision++;
        }

        private void TriggerDamageFlash()
        {
            _damageFlashTimer = 0.18f;
        }

        private void UpdateVisualFeedback(float dt)
        {
            if (_visualRoot != null && _visualRoot != transform)
            {
                float targetPitch = !IsAlive ? -90f : 0f;
                Vector3 targetLocalPos = !IsAlive ? new Vector3(0f, 0.25f, 0f) : Vector3.zero;
                byte tg = CurrentTelegraphKind;
                if (IsAlive && tg == 1) targetLocalPos.y += Mathf.Sin(Time.time * 18f) * 0.08f; // Roof drop telegraph
                else if (IsAlive && tg == 2) targetLocalPos.z -= 0.18f; // Lunge prep crouch
                else if (IsAlive && tg == 3) targetLocalPos.y += 0.22f + Mathf.Sin(Time.time * 24f) * 0.10f; // Ghost blink

                _visualRoot.localRotation = Quaternion.Slerp(_visualRoot.localRotation, Quaternion.Euler(targetPitch, 0f, 0f), Mathf.Clamp01(dt * 10f));
                _visualRoot.localPosition = Vector3.Lerp(_visualRoot.localPosition, targetLocalPos, Mathf.Clamp01(dt * 14f));
            }

            if (_animator != null)
            {
                _animator.SetBool("IsSitting", IsAlive && CurrentTelegraphKind == 4);
            }

            if (_damageFlashTimer > 0f)
            {
                _damageFlashTimer = Mathf.Max(0f, _damageFlashTimer - dt);
                ApplyDamageTint(_damageFlashTimer > 0f);
            }
        }

        private void ApplyDamageTint(bool flashing)
        {
            if (_bodyRenderers == null || _bodyRenderers.Length == 0) return;
            _flashMpb ??= new MaterialPropertyBlock();
            for (int i = 0; i < _bodyRenderers.Length; i++)
            {
                var r = _bodyRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_flashMpb);
                if (flashing)
                {
                    _flashMpb.SetColor("_EmissionColor", new Color(0.85f, 0.12f, 0.12f, 1f));
                }
                else
                {
                    _flashMpb.SetColor("_EmissionColor", Color.black);
                }
                r.SetPropertyBlock(_flashMpb);
            }
        }

        private void Update()
        {
            var curState = CurrentBehaviorState;
            if (curState != _lastVisualState)
            {
                _lastVisualState = curState;
                UpdateEyePresentation();
            }

            float dt = Time.deltaTime;
            UpdateVisualFeedback(dt);

            if (_isClientProxy)
            {
                if (_hasClientTarget && IsAlive)
                {
                    Vector3 prev = transform.position;
                    transform.position = Vector3.Lerp(prev, _clientTargetPos, Mathf.Clamp01(dt * 12f));
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, _clientTargetYaw, 0f), Mathf.Clamp01(dt * 12f));
                    float estSpeed = dt > 0.0001f ? (transform.position - prev).magnitude / dt : 0f;
                    DriveAnimatorLocomotion(estSpeed);
                }
                else if (!IsAlive)
                {
                    DriveAnimatorLocomotion(0f);
                    if (_visualRoot == transform)
                    {
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(-90f, _clientTargetYaw, 0f), Mathf.Clamp01(dt * 8f));
                    }
                }
                return;
            }

            if (!IsAuthoritativeServer) return;

            if (!IsAlive)
            {
                DriveAnimatorLocomotion(0f);
                if (_mainCollider != null && _mainCollider.enabled) _mainCollider.enabled = false;
                if (_agent != null && _agent.enabled) _agent.enabled = false;
                if (_visualRoot == transform)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(-90f, transform.eulerAngles.y, 0f), Mathf.Clamp01(dt * 8f));
                }
                _deadHideTimer += dt;
                return;
            }

            DriveAnimatorLocomotion(_agent != null && _agent.enabled ? _agent.velocity.magnitude : 0f);
            if (_attackCooldownTimer > 0f) _attackCooldownTimer -= dt;
            if (_blinkTimer > 0f) _blinkTimer -= dt;

            // Flutuação seletiva do Fantasma apenas em perseguição/ataque (T47)
            if (species == NpcSpecies.Ghost && _agent != null)
            {
                _agent.baseOffset = (curState == NpcBehaviorState.Aggressive || curState == NpcBehaviorState.Attacking)
                    ? ghostChaseFloatOffset
                    : 0f;
            }

            // Telemetria de queda do Vampiro do telhado (T45)
            if (_dropTelegraphTimer > 0f)
            {
                _dropTelegraphTimer -= dt;
                if (_dropTelegraphTimer <= 0f)
                {
                    Vector3 hint = _currentTarget != null ? _currentTarget.position - _currentTarget.forward * 2.5f : transform.position;
                    TryDropFromRooftop(hint);
                }
                return;
            }

            switch (curState)
            {
                case NpcBehaviorState.Routine:
                    if (ShouldEnterHostileStateServer())
                    {
                        TryAcquireNearestTarget();
                        break;
                    }
                    TickPeacefulRoutine(dt);
                    break;

                case NpcBehaviorState.Customer:
                    if (ShouldEnterHostileStateServer())
                    {
                        InterruptBoundCustomerServer();
                        TryAcquireNearestTarget();
                        break;
                    }
                    TickCustomerRole(dt);
                    break;

                case NpcBehaviorState.Aggressive:
                    TickChase(dt);
                    break;

                case NpcBehaviorState.Attacking:
                    TickCommittedLunge(dt);
                    break;

                case NpcBehaviorState.Recovering:
                    _recoveryTimer -= dt;
                    if (_recoveryTimer <= 0f)
                    {
                        SetBehaviorStateInternal(IsTargetAlive(_currentTarget) ? NpcBehaviorState.Aggressive : NpcBehaviorState.Routine);
                    }
                    break;

                case NpcBehaviorState.Disengaging:
                    _currentTarget = null;
                    _outOfRangeTimer = 0f;
                    _isDirectlyProvoked = false;
                    _hasRoutineDest = false;
                    SetBehaviorStateInternal(NpcBehaviorState.Routine);
                    break;
            }
        }

        private void TickPeacefulRoutine(float dt)
        {
            if (_isPerchedOnRooftop || _agent == null || !_agent.enabled) return;

            _customerVisitCooldown -= dt;
            var root = PizzeriaRoot.Instance;
            if (_customerVisitCooldown <= 0f && species != NpcSpecies.Zombie &&
                root != null && root.DomainState != null &&
                root.DomainState.CurrentNightPhase == NightPhase.Active &&
                root.CustomerService != null)
            {
                _customerVisitCooldown = 28f + (float)_rng.NextDouble() * 18f;
                var hostility = root.SpeciesHostilityService;
                if (hostility == null || hostility.GetHostility(species) < 50)
                {
                    if (TryEnterAsCustomerServer(root)) return;
                }
            }

            _routineDecisionTimer -= dt;
            if (!_hasRoutineDest || _routineDecisionTimer <= 0f ||
                (!_agent.pathPending && _agent.remainingDistance <= 1.2f))
            {
                _routineDecisionTimer = 6f + (float)_rng.NextDouble() * 6f;
                Vector3 center = root != null ? root.transform.position : transform.position;
                float angle = (float)(_rng.NextDouble() * Mathf.PI * 2.0);
                float dist = 10f + (float)_rng.NextDouble() * 16f;
                Vector3 candidate = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * dist;
                if (NavMesh.SamplePosition(candidate, out var hit, 6f, NavMesh.AllAreas))
                {
                    var path = new NavMeshPath();
                    if (_agent.isOnNavMesh && NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, path) &&
                        path.status == NavMeshPathStatus.PathComplete)
                    {
                        _routineDest = hit.position;
                        _hasRoutineDest = true;
                        _agent.speed = 2.2f;
                        _agent.SetDestination(_routineDest);
                    }
                }
            }
        }

        private bool TryEnterAsCustomerServer(PizzeriaRoot root)
        {
            var res = root.CustomerService.SpawnWalkInCustomer(
                root.DomainState,
                root.Catalog,
                root.Clock,
                rng: _rng);
            if (!res.Success) return false;

            if (root.DomainState.Customers.TryGetValue(res.Value, out var cust))
            {
                cust.IsMonsterCustomer = true;
                cust.Revision++;
            }
            _boundCustomerId = res.Value;
            SetBehaviorStateInternal(NpcBehaviorState.Customer);
            PizzeriaCommandHandler.Instance?.CommitAndReplicate();
            return true;
        }

        private void TickCustomerRole(float dt)
        {
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null || !_boundCustomerId.IsValid ||
                !root.DomainState.Customers.TryGetValue(_boundCustomerId, out var cust) ||
                cust.Phase == CustomerPhase.Despawned)
            {
                _boundCustomerId = CustomerId.None;
                _hasRoutineDest = false;
                SetBehaviorStateInternal(NpcBehaviorState.Routine);
                return;
            }

            if (_agent == null || !_agent.enabled) return;

            var spawner = CustomerSpawner.Instance;
            if (spawner != null)
            {
                var dto = new NetCustomerDto
                {
                    CustomerId = cust.Id.Value,
                    Phase = (byte)cust.Phase,
                    QueueIndex = cust.QueueIndex,
                    QueueSlotIndex = cust.QueueSlotIndex,
                    AssignedTableId = cust.AssignedTableId,
                    OrderId = cust.OrderId.Value,
                    IsMonsterCustomer = true
                };
                spawner.ResolveTargetPose(dto, out Vector3 targetPos, out Quaternion targetRot);
                if (NavMesh.SamplePosition(targetPos, out var navHit, 3.0f, NavMesh.AllAreas))
                {
                    _agent.speed = 2.4f;
                    if (_agent.isOnNavMesh) _agent.SetDestination(navHit.position);

                    if (cust.Phase == CustomerPhase.WalkingToTable &&
                        (transform.position - navHit.position).sqrMagnitude <= 1.2f * 1.2f)
                    {
                        if (root.CustomerService.ConfirmCustomerSeated(root.DomainState, _boundCustomerId))
                        {
                            transform.rotation = targetRot;
                            PizzeriaCommandHandler.Instance?.CommitAndReplicate();
                        }
                    }
                }
            }
        }

        private void InterruptBoundCustomerServer()
        {
            if (!_boundCustomerId.IsValid) return;
            var root = PizzeriaRoot.Instance;
            if (root != null && root.DomainState != null && root.CustomerService != null)
            {
                if (root.CustomerService.InterruptCustomerForCombat(root.DomainState, _boundCustomerId, root.Clock.Now))
                {
                    PizzeriaCommandHandler.Instance?.CommitAndReplicate();
                }
            }
            _boundCustomerId = CustomerId.None;
        }

        private bool ShouldEnterHostileStateServer()
        {
            if (species == NpcSpecies.Zombie || _isDirectlyProvoked) return true;
            var hostility = PizzeriaRoot.Instance != null ? PizzeriaRoot.Instance.SpeciesHostilityService : null;
            return hostility != null && hostility.GetHostility(species) >= 50;
        }

        public void TriggerAggression(Transform target)
        {
            if (!IsAuthoritativeServer || !IsAlive || target == null) return;
            InterruptBoundCustomerServer();
            _currentTarget = target;
            _outOfRangeTimer = 0f;
            if (_agent != null && _agent.enabled)
            {
                _agent.speed = species == NpcSpecies.Werewolf ? 4.8f : (species == NpcSpecies.Zombie ? 2.4f : 3.6f);
            }
            if (_isPerchedOnRooftop && species == NpcSpecies.Vampire && _dropTelegraphTimer <= 0f)
            {
                _dropTelegraphTimer = 0.85f;
            }
            SetBehaviorStateInternal(NpcBehaviorState.Aggressive);
        }

        /// <summary>Vampiro: desce de anchor elevado para posição segura válida no NavMesh.</summary>
        public bool TryDropFromRooftop(Vector3 landingHint)
        {
            if (!IsAuthoritativeServer || species != NpcSpecies.Vampire) return false;
            _isPerchedOnRooftop = false;
            if (NavMesh.SamplePosition(landingHint, out var hit, 8f, NavMesh.AllAreas))
            {
                if (_agent != null)
                {
                    if (!_agent.enabled) _agent.enabled = true;
                    _agent.Warp(hit.position);
                }
                else transform.position = hit.position;
                return true;
            }
            if (_agent != null && !_agent.enabled) _agent.enabled = true;
            return false;
        }

        public void ApplyDamageServer(DamageEvent evt)
        {
            if (!IsAuthoritativeServer || !IsAlive) return;
            bool wasCustomer = _boundCustomerId.IsValid;
            InterruptBoundCustomerServer();
            _isDirectlyProvoked = true;
            SetHealthInternal(CurrentHealth - Mathf.Max(1, evt.Amount));
            TriggerDamageFlash();

            if (wasCustomer && PizzeriaRoot.Instance != null && PizzeriaRoot.Instance.DomainState != null &&
                PizzeriaRoot.Instance.IsWorldPositionInsidePizzeria(transform.position))
            {
                NightCycleService.AdjustReputation(PizzeriaRoot.Instance.DomainState, -15, "Cliente atacado dentro da pizzaria");
                PizzeriaCommandHandler.Instance?.CommitAndReplicate();
            }

            if (CurrentHealth <= 0)
            {
                SetBehaviorStateInternal(NpcBehaviorState.Dead);
                if (_mainCollider != null) _mainCollider.enabled = false;
                if (_agent != null && _agent.enabled)
                {
                    if (_agent.isOnNavMesh) _agent.isStopped = true;
                    _agent.enabled = false;
                }
                return;
            }

            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.ConnectedClients.TryGetValue(evt.InstigatorClientId, out var attacker) &&
                attacker.PlayerObject != null)
            {
                TriggerAggression(attacker.PlayerObject.transform);
            }
        }

        private void TryAcquireNearestTarget()
        {
            if (NetworkManager.Singleton == null) return;
            float range = EffectiveAcquisitionRange;
            float bestDistSq = range * range;
            Transform bestTarget = null;

            foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
            {
                var po = kvp.Value?.PlayerObject;
                if (po == null || !po.gameObject.activeInHierarchy) continue;
                var dmg = po.GetComponentInParent<IDamageable>();
                if (dmg != null && !dmg.IsAlive) continue;

                float dSq = (po.transform.position - transform.position).sqrMagnitude;
                if (dSq <= bestDistSq)
                {
                    bestDistSq = dSq;
                    bestTarget = po.transform;
                }
            }

            if (species == NpcSpecies.Zombie)
            {
                var peds = FindObjectsByType<PedestrianNpc>(FindObjectsSortMode.None);
                for (int i = 0; i < peds.Length; i++)
                {
                    if (peds[i] == null || !peds[i].gameObject.activeInHierarchy) continue;
                    float dSq = (peds[i].transform.position - transform.position).sqrMagnitude;
                    if (dSq < bestDistSq)
                    {
                        bestDistSq = dSq;
                        bestTarget = peds[i].transform;
                    }
                }
            }

            if (bestTarget == null)
            {
                var employees = FindObjectsByType<EmployeeAgentController>(FindObjectsSortMode.None);
                for (int i = 0; i < employees.Length; i++)
                {
                    var emp = employees[i];
                    if (emp == null || !emp.IsAlive) continue;
                    float dSq = (emp.transform.position - transform.position).sqrMagnitude;
                    if (dSq <= bestDistSq)
                    {
                        bestDistSq = dSq;
                        bestTarget = emp.transform;
                    }
                }
            }

            if (bestTarget != null)
            {
                TriggerAggression(bestTarget);
            }
        }

        private static bool IsTargetAlive(Transform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy) return false;
            var dmg = target.GetComponentInParent<IDamageable>();
            return dmg == null || dmg.IsAlive;
        }

        private void TickChase(float dt)
        {
            if (!IsTargetAlive(_currentTarget))
            {
                SetBehaviorStateInternal(NpcBehaviorState.Disengaging);
                return;
            }

            float dist = Vector3.Distance(transform.position, _currentTarget.position);
            if (dist > disengageDistance)
            {
                _outOfRangeTimer += dt;
                if (_outOfRangeTimer >= disengageSeconds)
                {
                    SetBehaviorStateInternal(NpcBehaviorState.Disengaging);
                    return;
                }
            }
            else
            {
                _outOfRangeTimer = 0f;
            }

            // Teleporte com telégrafo do Fantasma somente quando LOS bloqueada ou distância > 7m (T47)
            if (species == NpcSpecies.Ghost)
            {
                if (_hasPendingBlink)
                {
                    _blinkTelegraphTimer -= dt;
                    if (_blinkTelegraphTimer <= 0f)
                    {
                        _hasPendingBlink = false;
                        ExecuteValidatedGhostBlink();
                    }
                    return;
                }

                if (_blinkTimer <= 0f)
                {
                    bool losBlocked = Physics.Linecast(
                        transform.position + Vector3.up * 1.1f,
                        _currentTarget.position + Vector3.up * 1.1f,
                        Physics.DefaultRaycastLayers,
                        QueryTriggerInteraction.Ignore);
                    if (losBlocked || dist > 7.0f)
                    {
                        TryPrepareGhostBlink(_currentTarget.position);
                    }
                }
            }

            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            {
                _agent.SetDestination(_currentTarget.position);
            }

            if (species == NpcSpecies.Werewolf && dist <= attackRange * 2.5f && _attackCooldownTimer <= 0f)
            {
                _lungeDirection = (_currentTarget.position - transform.position);
                _lungeDirection.y = 0f;
                if (_lungeDirection.sqrMagnitude > 0.01f) _lungeDirection.Normalize();
                else _lungeDirection = transform.forward;

                if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
                {
                    _agent.isStopped = true;
                    _agent.updatePosition = false;
                }

                _lungePrepTimer = lungePrepSeconds;
                _lungeTimer = lungeDuration;
                _lungeHitApplied = false;
                _attackCooldownTimer = attackCooldown;
                SetBehaviorStateInternal(NpcBehaviorState.Attacking);
                return;
            }

            if (dist <= attackRange && _attackCooldownTimer <= 0f)
            {
                _attackCooldownTimer = attackCooldown;
                ApplyMeleeHit(_currentTarget);
            }
        }

        private void TickCommittedLunge(float dt)
        {
            if (_lungePrepTimer > 0f)
            {
                _lungePrepTimer -= dt;
                return;
            }

            _lungeTimer -= dt;
            float stepDist = lungeSpeed * dt;
            bool blockedByWall = Physics.SphereCast(
                transform.position + Vector3.up * 0.85f,
                0.40f,
                _lungeDirection,
                out _,
                stepDist + 0.35f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            if (!blockedByWall)
            {
                transform.position += _lungeDirection * stepDist;
            }
            else
            {
                _lungeTimer = 0f;
            }

            if (!_lungeHitApplied && _currentTarget != null && Vector3.Distance(transform.position, _currentTarget.position) <= attackRange * 1.15f)
            {
                _lungeHitApplied = true;
                ApplyMeleeHit(_currentTarget);
            }

            if (_lungeTimer <= 0f)
            {
                if (_agent != null && _agent.enabled)
                {
                    if (NavMesh.SamplePosition(transform.position, out var navHit, 4.0f, NavMesh.AllAreas))
                        _agent.Warp(navHit.position);
                    _agent.updatePosition = true;
                    if (_agent.isOnNavMesh) _agent.isStopped = false;
                }
                _recoveryTimer = lungeRecoverySeconds;
                SetBehaviorStateInternal(NpcBehaviorState.Recovering);
            }
        }

        private void TryPrepareGhostBlink(Vector3 targetPos)
        {
            Vector3 dir = (targetPos - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;
            dir.Normalize();

            Vector3 candidate = transform.position + dir * blinkDistance;
            if (Physics.Raycast(candidate + Vector3.up * 3f, Vector3.down, out var groundHit, 10f, ~ghostObstacleMask, QueryTriggerInteraction.Ignore))
            {
                if (NavMesh.SamplePosition(groundHit.point, out var navHit, 2.5f, NavMesh.AllAreas))
                {
                    bool occupied = Physics.OverlapSphere(
                        navHit.position + Vector3.up * 0.9f,
                        0.40f,
                        Physics.DefaultRaycastLayers,
                        QueryTriggerInteraction.Ignore).Length > 0;
                    if (!occupied)
                    {
                        _pendingBlinkDest = navHit.position;
                        _hasPendingBlink = true;
                        _blinkTelegraphTimer = blinkTelegraphSeconds;
                        _blinkTimer = blinkCooldown;
                    }
                }
            }
        }

        private void ExecuteValidatedGhostBlink()
        {
            if (_agent != null && _agent.enabled)
                _agent.Warp(_pendingBlinkDest);
            else
                transform.position = _pendingBlinkDest;
        }

        private void ApplyMeleeHit(Transform target)
        {
            if (target == null) return;

            Vector3 eyeFrom = transform.position + Vector3.up * 1.15f;
            Vector3 eyeTo = target.position + Vector3.up * 1.0f;
            if (Physics.Linecast(eyeFrom, eyeTo, out RaycastHit losHit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (!losHit.collider.transform.IsChildOf(transform) && !losHit.collider.transform.IsChildOf(target))
                    return;
            }

            var dmg = target.GetComponentInParent<IDamageable>();
            if (dmg != null && dmg.IsAlive)
            {
                dmg.ApplyDamageServer(new DamageEvent
                {
                    Amount = Mathf.Max(1, attackDamage),
                    Source = DamageSourceType.EnemyTest,
                    InstigatorClientId = 0UL,
                    HitPoint = target.position,
                    HitNormal = (target.position - transform.position).normalized
                });
                return;
            }

            // Zumbi infecta pedestre/NPC civil com conversão única e teto (T48)
            if (species == NpcSpecies.Zombie)
            {
                var ped = target.GetComponentInParent<PedestrianNpc>();
                if (ped != null && ped.gameObject.activeInHierarchy)
                {
                    var root = PizzeriaRoot.Instance;
                    var zSvc = root != null ? root.ZombieConversionService : null;
                    var director = MonsterPopulationDirector.Instance;
                    if (zSvc != null && director != null &&
                        zSvc.TryConvertTarget((ulong)ped.GetInstanceID(), director.CountActiveZombies(), Time.timeAsDouble, _rng))
                    {
                        Vector3 spawnPos = ped.transform.position;
                        ped.gameObject.SetActive(false);
                        director.SpawnConvertedZombieServer(spawnPos);
                        _currentTarget = null;
                    }
                }
            }
        }

        private void UpdateEyePresentation()
        {
            if (eyeRenderers == null) return;
            var s = CurrentBehaviorState;
            bool angry = s == NpcBehaviorState.Aggressive || s == NpcBehaviorState.Attacking;
            Color targetColor = angry ? aggressiveEyeColor : calmEyeColor;
            var mpb = new MaterialPropertyBlock();
            for (int i = 0; i < eyeRenderers.Length; i++)
            {
                if (eyeRenderers[i] == null) continue;
                eyeRenderers[i].GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", targetColor);
                mpb.SetColor("_Color", targetColor);
                eyeRenderers[i].SetPropertyBlock(mpb);
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
    }
}
