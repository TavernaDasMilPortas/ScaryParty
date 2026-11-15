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
        [SerializeField] private bool enableAllPlayersDeadDefeat = false;

        private NetworkObject _netObj;
        private ThirdPersonController _tpc;
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
                return state != null && state.TryGetPlayerCombatState(ClientId, out var dto) && dto.IsIncapacitated;
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

        public bool IsAlive => !IsIncapacitated && CurrentHp > 0;

        private void Awake()
        {
            _netObj = GetComponent<NetworkObject>();
            _tpc = GetComponent<ThirdPersonController>();
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
            bool incap = hasState && dto.IsIncapacitated;
            float rescue = hasState ? dto.RescueRemainingSeconds : 0f;

            if (_netObj != null && _netObj.IsOwner)
            {
                HudPresenter.Instance?.SetCombatHealthHud(hp, totalMax, incap, rescue);

                if (_tpc != null)
                {
                    // Enquanto nocauteado, pausa o deslocamento local sem bloquear câmera/pause.
                    if (_tpc.enabled == incap)
                        _tpc.enabled = !incap;
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
