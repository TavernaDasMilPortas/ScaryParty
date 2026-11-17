using ScaryParty.Combat;
using ScaryParty.Pizzeria.Domain.Services;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace ScaryParty.Combat.Monsters
{
    /// <summary>
    /// Base comum de estado e combate para NPCs/Monstros (Humano agressivo, Vampiro, Lobisomem, Fantasma e Zumbi).
    /// Separa autoridade de decisão (servidor) da apresentação visual (olhos vermelhos, telemetria e efeitos).
    /// </summary>
    public class MonsterNpcController : NetworkBehaviour, IDamageable
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
        [SerializeField] private float lungeDuration = 0.45f;
        [SerializeField] private float lungeRecoverySeconds = 1.1f;
        [SerializeField] private float blinkCooldown = 4.5f;
        [SerializeField] private float blinkDistance = 5.5f;
        [SerializeField] private LayerMask ghostObstacleMask = 0;

        [Header("Apresentação")]
        [SerializeField] private Renderer[] eyeRenderers;
        [SerializeField] private Color calmEyeColor = Color.white;
        [SerializeField] private Color aggressiveEyeColor = Color.red;

        private readonly NetworkVariable<byte> _netState = new NetworkVariable<byte>((byte)NpcBehaviorState.Routine);
        private readonly NetworkVariable<int> _netHealth = new NetworkVariable<int>(130);

        private NavMeshAgent _agent;
        private Transform _currentTarget;
        private float _outOfRangeTimer;
        private float _attackCooldownTimer;
        private float _lungeTimer;
        private float _recoveryTimer;
        private float _blinkTimer;
        private Vector3 _lungeDirection;
        private bool _lungeHitApplied;
        private NpcBehaviorState _lastVisualState = NpcBehaviorState.Routine;

        public NpcSpecies Species => species;
        public NpcBehaviorState CurrentBehaviorState => (NpcBehaviorState)_netState.Value;
        public bool IsAlive => _netHealth.Value > 0 && CurrentBehaviorState != NpcBehaviorState.Dead;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _netHealth.Value = Mathf.Max(1, maxHealth);
                _netState.Value = (byte)NpcBehaviorState.Routine;
            }
            UpdateEyePresentation();
        }

        private void Update()
        {
            if ((NpcBehaviorState)_netState.Value != _lastVisualState)
            {
                _lastVisualState = (NpcBehaviorState)_netState.Value;
                UpdateEyePresentation();
            }

            if (!IsServer || !IsAlive) return;

            float dt = Time.deltaTime;
            if (_attackCooldownTimer > 0f) _attackCooldownTimer -= dt;
            if (_blinkTimer > 0f) _blinkTimer -= dt;

            switch (CurrentBehaviorState)
            {
                case NpcBehaviorState.Routine:
                case NpcBehaviorState.Customer:
                    TryAcquireNearestPlayer();
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
                        _netState.Value = (byte)(_currentTarget != null ? NpcBehaviorState.Aggressive : NpcBehaviorState.Routine);
                    }
                    break;

                case NpcBehaviorState.Disengaging:
                    _currentTarget = null;
                    _outOfRangeTimer = 0f;
                    _netState.Value = (byte)NpcBehaviorState.Routine;
                    break;
            }
        }

        public void TriggerAggression(Transform target)
        {
            if (!IsServer || !IsAlive || target == null) return;
            _currentTarget = target;
            _outOfRangeTimer = 0f;
            _netState.Value = (byte)NpcBehaviorState.Aggressive;
        }

        /// <summary>Vampiro: desce de anchor elevado para posição segura válida no NavMesh.</summary>
        public bool TryDropFromRooftop(Vector3 landingHint)
        {
            if (!IsServer || species != NpcSpecies.Vampire) return false;
            if (NavMesh.SamplePosition(landingHint, out var hit, 4f, NavMesh.AllAreas))
            {
                if (_agent != null && _agent.enabled) _agent.Warp(hit.position);
                else transform.position = hit.position;
                return true;
            }
            return false;
        }

        public void ApplyDamageServer(DamageEvent evt)
        {
            if (!IsServer || !IsAlive) return;
            _netHealth.Value = Mathf.Max(0, _netHealth.Value - Mathf.Max(1, evt.Amount));
            if (_netHealth.Value <= 0)
            {
                _netState.Value = (byte)NpcBehaviorState.Dead;
                if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
                {
                    _agent.isStopped = true;
                }
            }
        }

        private void TryAcquireNearestPlayer()
        {
            if (NetworkManager.Singleton == null) return;
            float bestDistSq = acquisitionRange * acquisitionRange;
            Transform bestTarget = null;

            foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
            {
                var po = kvp.Value?.PlayerObject;
                if (po == null) continue;
                var health = po.GetComponent<PlayerHealth>();
                if (health != null && health.IsIncapacitated) continue;

                float dSq = (po.transform.position - transform.position).sqrMagnitude;
                if (dSq <= bestDistSq)
                {
                    bestDistSq = dSq;
                    bestTarget = po.transform;
                }
            }

            if (bestTarget != null)
            {
                TriggerAggression(bestTarget);
            }
        }

        private void TickChase(float dt)
        {
            if (_currentTarget == null)
            {
                _netState.Value = (byte)NpcBehaviorState.Disengaging;
                return;
            }

            float dist = Vector3.Distance(transform.position, _currentTarget.position);
            if (dist > disengageDistance)
            {
                _outOfRangeTimer += dt;
                if (_outOfRangeTimer >= disengageSeconds)
                {
                    _netState.Value = (byte)NpcBehaviorState.Disengaging;
                    return;
                }
            }
            else
            {
                _outOfRangeTimer = 0f;
            }

            if (species == NpcSpecies.Ghost && _blinkTimer <= 0f && dist > attackRange * 2.2f)
            {
                TryGhostBlinkTowards(_currentTarget.position);
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
                _lungeTimer = lungeDuration;
                _lungeHitApplied = false;
                _attackCooldownTimer = attackCooldown;
                _netState.Value = (byte)NpcBehaviorState.Attacking;
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
            _lungeTimer -= dt;
            transform.position += _lungeDirection * (lungeSpeed * dt);

            if (!_lungeHitApplied && _currentTarget != null && Vector3.Distance(transform.position, _currentTarget.position) <= attackRange * 1.15f)
            {
                _lungeHitApplied = true;
                ApplyMeleeHit(_currentTarget);
            }

            if (_lungeTimer <= 0f)
            {
                _recoveryTimer = lungeRecoverySeconds;
                _netState.Value = (byte)NpcBehaviorState.Recovering;
            }
        }

        private void TryGhostBlinkTowards(Vector3 targetPos)
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
                    transform.position = navHit.position;
                    _blinkTimer = blinkCooldown;
                }
            }
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
            }
        }

        private void UpdateEyePresentation()
        {
            if (eyeRenderers == null) return;
            bool angry = _lastVisualState == NpcBehaviorState.Aggressive || _lastVisualState == NpcBehaviorState.Attacking;
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
    }
}
