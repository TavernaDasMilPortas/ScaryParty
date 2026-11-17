using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Services;
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

        public ulong MonsterId { get; private set; }
        public uint Revision { get; private set; } = 1;
        public NpcSpecies Species => species;
        public int MaxHealth => maxHealth;
        public int CurrentHealth => _localHealth;
        public NpcBehaviorState CurrentBehaviorState => _localBehaviorState;
        public bool IsAlive => CurrentHealth > 0 && CurrentBehaviorState != NpcBehaviorState.Dead;
        private bool IsAuthoritativeServer => !_isClientProxy && _configuredOnServer && (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer);

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponentInChildren<Animator>(true);
        }

        public void ConfigureClientProxy(ulong monsterId, NpcSpecies npcSpecies, Renderer[] eyes, NetMonsterDto initialDto)
        {
            MonsterId = monsterId;
            species = npcSpecies;
            _isClientProxy = true;
            _configuredOnServer = false;
            if (eyes != null && eyes.Length > 0) eyeRenderers = eyes;
            _animator = GetComponentInChildren<Animator>(true);
            ApplyReplicatedDto(initialDto, snapImmediately: true);
        }

        public void ApplyReplicatedDto(NetMonsterDto dto, bool snapImmediately = false)
        {
            maxHealth = Mathf.Max(1, dto.MaxHp);
            _localHealth = Mathf.Max(0, dto.CurrentHp);
            _localBehaviorState = (NpcBehaviorState)dto.BehaviorState;
            Revision = dto.Revision;
            _clientTargetPos = new Vector3(dto.PosX, dto.PosY, dto.PosZ);
            _clientTargetYaw = dto.Yaw;
            if (snapImmediately || !_hasClientTarget || (_clientTargetPos - transform.position).sqrMagnitude > 36f)
            {
                transform.position = _clientTargetPos;
                transform.rotation = Quaternion.Euler(0f, _clientTargetYaw, 0f);
            }
            _hasClientTarget = true;
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
                disengageDistance = profile.DisengageDistance;
                disengageSeconds = profile.DisengageSeconds;
            }
            _isPerchedOnRooftop = perchedOnRooftop && species == NpcSpecies.Vampire;
            if (eyes != null && eyes.Length > 0) eyeRenderers = eyes;
            _configuredOnServer = true;
            _isClientProxy = false;
            _localHealth = maxHealth;
            _animator = GetComponentInChildren<Animator>(true);
            SetBehaviorStateInternal(NpcBehaviorState.Routine);
            if (_agent != null && _isPerchedOnRooftop)
                _agent.enabled = false;
            UpdateEyePresentation();
        }

        private void SetBehaviorStateInternal(NpcBehaviorState next)
        {
            _localBehaviorState = next;
            Revision++;
        }

        private void SetHealthInternal(int nextHp)
        {
            _localHealth = Mathf.Max(0, nextHp);
            Revision++;
        }

        private void Update()
        {
            var curState = CurrentBehaviorState;
            if (curState != _lastVisualState)
            {
                _lastVisualState = curState;
                UpdateEyePresentation();
            }

            if (_isClientProxy)
            {
                float cdt = Time.deltaTime;
                if (_hasClientTarget && IsAlive)
                {
                    Vector3 prev = transform.position;
                    transform.position = Vector3.Lerp(prev, _clientTargetPos, Mathf.Clamp01(cdt * 12f));
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, _clientTargetYaw, 0f), Mathf.Clamp01(cdt * 12f));
                    float estSpeed = cdt > 0.0001f ? (transform.position - prev).magnitude / cdt : 0f;
                    DriveAnimatorLocomotion(estSpeed);
                }
                else if (!IsAlive)
                {
                    DriveAnimatorLocomotion(0f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(-90f, _clientTargetYaw, 0f), Mathf.Clamp01(cdt * 8f));
                }
                return;
            }

            if (!IsAuthoritativeServer) return;

            if (!IsAlive)
            {
                DriveAnimatorLocomotion(0f);
                _deadHideTimer += Time.deltaTime;
                return;
            }

            DriveAnimatorLocomotion(_agent != null && _agent.enabled ? _agent.velocity.magnitude : 0f);

            float dt = Time.deltaTime;
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
                case NpcBehaviorState.Customer:
                    if (ShouldEnterHostileStateServer())
                    {
                        TryAcquireNearestTarget();
                    }
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
                        SetBehaviorStateInternal(_currentTarget != null ? NpcBehaviorState.Aggressive : NpcBehaviorState.Routine);
                    }
                    break;

                case NpcBehaviorState.Disengaging:
                    _currentTarget = null;
                    _outOfRangeTimer = 0f;
                    SetBehaviorStateInternal(NpcBehaviorState.Routine);
                    break;
            }
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
            _currentTarget = target;
            _outOfRangeTimer = 0f;
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
            _isDirectlyProvoked = true;
            SetHealthInternal(CurrentHealth - Mathf.Max(1, evt.Amount));

            if (CurrentHealth <= 0)
            {
                SetBehaviorStateInternal(NpcBehaviorState.Dead);
                if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
                {
                    _agent.isStopped = true;
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
            float bestDistSq = acquisitionRange * acquisitionRange;
            Transform bestTarget = null;

            foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
            {
                var po = kvp.Value?.PlayerObject;
                if (po == null) continue;
                var health = po.GetComponent<PlayerHealth>();
                if (health != null && (health.IsIncapacitated || health.IsDead)) continue;

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

            if (bestTarget != null)
            {
                TriggerAggression(bestTarget);
            }
        }

        private void TickChase(float dt)
        {
            if (_currentTarget == null || !_currentTarget.gameObject.activeInHierarchy)
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
