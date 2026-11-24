using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Presentation;
using Unity.Netcode;
using UnityEngine;

namespace ScaryParty.Combat
{
    /// <summary>
    /// Alvo de prática no pátio/estacionamento para validar disparos (Pistola/Escopeta)
    /// e testar dano controlado em jogador/mochila sem depender de IA completa de inimigos.
    /// </summary>
    public class PracticeTargetDummy : MonoBehaviour, IDamageable, IInteractable
    {
        [Header("Configuração do Alvo")]
        [SerializeField] private int maxHp = 100;
        [SerializeField] private int testHitDamageOnPlayer = 25;
        [SerializeField] private float resetDelaySeconds = 2.5f;

        private int _currentHp;
        private float _resetTimer;
        private float _hitFlashTimer;
        private Vector3 _baseLocalScale;
        private Quaternion _baseLocalRotation;
        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;

        public int CurrentHp => _currentHp;
        public bool IsAlive => _currentHp > 0;

        public string InteractPrompt => $"[E] Testar Dano em Si ({testHitDamageOnPlayer} HP / desgasta mochila) | Atire no alvo ({_currentHp}/{maxHp} HP)";

        private void Awake()
        {
            _currentHp = maxHp;
            _baseLocalScale = transform.localScale;
            _baseLocalRotation = transform.localRotation;
            _renderers = GetComponentsInChildren<Renderer>(true);
            _mpb = new MaterialPropertyBlock();
        }

        private void Start()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            {
                PizzeriaCommandHandler.Instance?.RequestSyncDummyVisualServerRpc();
            }
        }

        private void Update()
        {
            if (_currentHp <= 0)
            {
                _resetTimer -= Time.deltaTime;
                transform.localRotation = Quaternion.Slerp(
                    transform.localRotation,
                    _baseLocalRotation * Quaternion.Euler(78f, 0f, 0f),
                    Time.deltaTime * 10f);

                if (_resetTimer <= 0f)
                {
                    _currentHp = maxHp;
                }
            }
            else
            {
                transform.localRotation = Quaternion.Slerp(
                    transform.localRotation,
                    _baseLocalRotation,
                    Time.deltaTime * 8f);
            }

            if (_hitFlashTimer > 0f)
            {
                _hitFlashTimer -= Time.deltaTime;
                ApplyTint(Color.Lerp(new Color(0.85f, 0.25f, 0.2f), new Color(1f, 0.78f, 0.25f), Mathf.Clamp01(_hitFlashTimer * 4f)));
            }
            else if (_currentHp <= 0)
            {
                ApplyTint(new Color(0.35f, 0.35f, 0.38f));
            }
            else
            {
                ApplyTint(new Color(0.85f, 0.25f, 0.2f));
            }
        }

        public void ApplyDamageServer(DamageEvent evt)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
            if (!IsAlive || evt.Amount <= 0) return;

            _currentHp = Mathf.Max(0, _currentHp - evt.Amount);
            _hitFlashTimer = 0.25f;
            if (_currentHp <= 0)
            {
                _resetTimer = resetDelaySeconds;
            }

            string msg = _currentHp > 0
                ? $"Alvo atingido (-{evt.Amount} HP | {_currentHp}/{maxHp})."
                : "Alvo derrubado! Reerguendo em 2,5s.";

            PizzeriaCommandHandler.Instance?.NotifyDummyHitServer(evt.InstigatorClientId, msg, _currentHp, maxHp);
        }

        public void ApplyLocalVisualPulse(int serverCurrentHp)
        {
            if (serverCurrentHp >= 0)
                _currentHp = serverCurrentHp;
            if (_currentHp <= 0)
                _resetTimer = resetDelaySeconds;
            _hitFlashTimer = 0.25f;
        }

        public void OnInteract(GameObject interactor)
        {
            PizzeriaCommandHandler.Instance?.RequestTestPlayerDamageServerRpc(testHitDamageOnPlayer);
        }

        public void OnFocus() { }
        public void OnLoseFocus() { }

        private void ApplyTint(Color color)
        {
            if (_renderers == null || _mpb == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor("_BaseColor", color);
                _mpb.SetColor("_Color", color);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
