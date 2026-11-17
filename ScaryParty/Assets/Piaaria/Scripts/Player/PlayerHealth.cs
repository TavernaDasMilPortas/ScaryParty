using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.UI.Piaria.Presenters;
using StarterAssets;
using Unity.Netcode;
using UnityEngine;

namespace ScaryParty.Combat
{
    /// <summary>
    /// Componente de vida e estado de nocaute do jogador apoiado pela autoridade de servidor
    /// em PizzeriaCommandHandler / PizzeriaNetworkState (sem mutar índices de NetworkBehaviour do prefab).
    /// </summary>
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        public const int DefaultMaxHp = 100;
        public const float DefaultAutoRescueSeconds = 10f;

        [SerializeField] private int maxHp = DefaultMaxHp;
        [SerializeField] private bool enableAllPlayersDeadDefeat = true;

        private NetworkObject _netObj;
        private ThirdPersonController _tpc;
        private float _baseMoveSpeed = 2f;
        private float _baseSprintSpeed = 5.335f;
        private float _baseJumpHeight = 1.2f;
        private int _lastObservedHp = DefaultMaxHp;
        private bool _hasObservedInitial;

        public int MaxHp => maxHp;
        public bool EnableAllPlayersDeadDefeat => enableAllPlayersDeadDefeat;

        public ulong ClientId
        {
            get
            {
                if (_netObj == null) _netObj = GetComponent<NetworkObject>();
                return _netObj != null ? _netObj.OwnerClientId : 0;
            }
        }

        public int CurrentHp
        {
            get
            {
                var state = PizzeriaNetworkState.Instance;
                if (state != null && state.TryGetPlayerCombatState(ClientId, out var dto))
                    return dto.CurrentHp;
                return maxHp;
            }
        }

        public bool IsIncapacitated
        {
            get
            {
                var state = PizzeriaNetworkState.Instance;
                return state != null && state.TryGetPlayerCombatState(ClientId, out var dto) && dto.IsIncapacitated && !dto.IsDead;
            }
        }

        public bool IsDead
        {
            get
            {
                var state = PizzeriaNetworkState.Instance;
                return state != null && state.TryGetPlayerCombatState(ClientId, out var dto) && dto.IsDead;
            }
        }

        public bool IsSpectating => IsDead;

        public ulong SpectatingTargetClientId
        {
            get
            {
                var state = PizzeriaNetworkState.Instance;
                return state != null && state.TryGetPlayerCombatState(ClientId, out var dto) ? dto.SpectatingTargetClientId : 0;
            }
        }

        public float RescueRemainingSeconds
        {
            get
            {
                var state = PizzeriaNetworkState.Instance;
                if (state != null && state.TryGetPlayerCombatState(ClientId, out var dto))
                    return dto.RescueRemainingSeconds;
                return 0f;
            }
        }

        public float RescueProgress
        {
            get
            {
                var state = PizzeriaNetworkState.Instance;
                if (state != null && state.TryGetPlayerCombatState(ClientId, out var dto))
                    return dto.RescueProgress;
                return 0f;
            }
        }

        public bool IsAlive => !IsIncapacitated && !IsDead && CurrentHp > 0;
        public bool CanPerformGameplayActions => IsAlive;

        private void Awake()
        {
            _netObj = GetComponent<NetworkObject>();
            _tpc = GetComponent<ThirdPersonController>();
            if (_tpc != null)
            {
                _baseMoveSpeed = _tpc.MoveSpeed;
                _baseSprintSpeed = _tpc.SprintSpeed;
                _baseJumpHeight = _tpc.JumpHeight;
            }
            if (GetComponent<PlayerAllyHelpInteractable>() == null)
                gameObject.AddComponent<PlayerAllyHelpInteractable>();
        }

        private void Start()
        {
            var cmd = PizzeriaCommandHandler.Instance;
            if (cmd != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                cmd.RegisterPlayerCombatServer(ClientId, maxHp, enableAllPlayersDeadDefeat);
            }
        }

        private void Update()
        {
            var netState = PizzeriaNetworkState.Instance;
            if (netState == null) return;

            bool hasState = netState.TryGetPlayerCombatState(ClientId, out var dto);
            int hp = hasState ? dto.CurrentHp : maxHp;
            int totalMax = hasState && dto.MaxHp > 0 ? dto.MaxHp : maxHp;
            bool isDead = hasState && dto.IsDead;
            bool incap = hasState && dto.IsIncapacitated && !isDead;
            float rescue = hasState ? dto.RescueRemainingSeconds : 0f;

            if (_netObj != null && _netObj.IsOwner)
            {
                HudPresenter.Instance?.SetCombatHealthHud(
                    hp,
                    totalMax,
                    incap,
                    rescue,
                    isDead,
                    hasState ? dto.RescueProgress : 0f,
                    hasState ? dto.RescueMode : (byte)0,
                    hasState ? dto.SpectatingTargetClientId : 0UL);

                if (_tpc != null)
                {
                    if (isDead)
                    {
                        if (_tpc.enabled) _tpc.enabled = false;
                    }
                    else if (incap)
                    {
                        // Enquanto nocauteado, permite rastejo lento e bloqueia corrida/pulo
                        if (!_tpc.enabled) _tpc.enabled = true;
                        _tpc.MoveSpeed = 0.70f;
                        _tpc.SprintSpeed = 0.70f;
                        _tpc.JumpHeight = 0f;
                    }
                    else
                    {
                        if (!_tpc.enabled) _tpc.enabled = true;
                        _tpc.MoveSpeed = _baseMoveSpeed;
                        _tpc.SprintSpeed = _baseSprintSpeed;
                        _tpc.JumpHeight = _baseJumpHeight;
                    }
                }

                if (_hasObservedInitial && hp < _lastObservedHp)
                {
                    ContextualTutorialPresenter.NotifyContextualTip(
                        "combat_damage",
                        "Dano em Combate",
                        "Ao sofrer dano, sua mochila também se desgasta. Se rasgar, recupere a caixa caída [E] e segure [F] (3s) para reparar.");
                }

                _lastObservedHp = hp;
                _hasObservedInitial = true;
            }
        }

        public void ApplyDamageServer(DamageEvent evt)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
            PizzeriaCommandHandler.Instance?.ApplyDamageToPlayerServer(ClientId, evt, enableAllPlayersDeadDefeat);
        }
    }
}
