using ScaryParty.Pizzeria.Network;
using Unity.Netcode;
using UnityEngine;

namespace ScaryParty.Combat
{
    /// <summary>
    /// Interagível anexado aos avatares de jogadores para permitir que aliados reanimem nocauteados (3s)
    /// ou curem aliados feridos mantendo [E] pressionado, sem acelerar por spam de RPC.
    /// </summary>
    public class PlayerAllyHelpInteractable : MonoBehaviour, IInteractable
    {
        private PlayerHealth _health;
        private NetworkObject _netObj;

        public PlayerHealth TargetHealth
        {
            get
            {
                if (_health == null) _health = GetComponent<PlayerHealth>();
                return _health;
            }
        }

        public ulong TargetClientId
        {
            get
            {
                if (_netObj == null) _netObj = GetComponent<NetworkObject>();
                if (_netObj != null) return _netObj.OwnerClientId;
                return TargetHealth != null ? TargetHealth.ClientId : 0;
            }
        }

        public string InteractPrompt
        {
            get
            {
                var target = TargetHealth;
                if (target == null || target.IsDead) return string.Empty;

                var netState = PizzeriaNetworkState.Instance;
                ulong localId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : ulong.MaxValue;
                if (TargetClientId == localId) return string.Empty;

                if (netState != null && netState.TryGetPlayerCombatState(localId, out var localCombat))
                {
                    if (localCombat.IsIncapacitated || localCombat.IsDead) return string.Empty;
                }

                if (target.IsIncapacitated)
                {
                    int sec = Mathf.CeilToInt(target.RescueRemainingSeconds);
                    return $"Segure [E] para Reanimar Aliado (3s) [{sec}s rest.]";
                }

                if (target.CurrentHp > 0 && target.CurrentHp < target.MaxHp)
                {
                    return $"Segure [E] para Curar Aliado ({target.CurrentHp}/{target.MaxHp} HP)";
                }

                return string.Empty;
            }
        }

        private void Awake()
        {
            _health = GetComponent<PlayerHealth>();
            _netObj = GetComponent<NetworkObject>();
        }

        public void OnInteract(GameObject interactor)
        {
            if (interactor == null || TargetHealth == null || TargetHealth.IsDead) return;
            PizzeriaCommandHandler.Instance?.BeginAssistAllyServerRpc(TargetClientId);
        }

        public void OnFocus() { }
        public void OnLoseFocus() { }
    }
}
