using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using ScaryParty.UI.Piaria.Core;

namespace ScaryParty.Pizzeria.Presentation
{
    /// <summary>
    /// Serviço unificado de feedback visual, sonoro e cinemático para a pizzaria (Fase 5).
    /// Coordena efeitos no mundo (DOTween para transforms/escalas, partículas, áudio e punchs),
    /// respeitando configurações de acessibilidade (ReduceMotion, cooldowns, orçamentos e culling).
    /// </summary>
    [DisallowMultipleComponent]
    public class PizzeriaFeedbackService : MonoBehaviour
    {
        public static PizzeriaFeedbackService Instance { get; private set; }

        public enum FeedbackType
        {
            PickupItem,
            PlaceItem,
            DoughKnead,
            CutIngredient,
            AddTopping,
            AddSauceOrCheese,
            OvenDoorOpen,
            OvenDoorClose,
            OvenDingReady,
            OvenBurnSmoke,
            BoxClose,
            BoxStackSettle,
            BackpackStore,
            BackpackRetrieve,
            PhoneRing,
            CustomerHappy,
            CustomerAngry,
            DeliverySuccess,
            DeliveryFail
        }

        [Header("Áudio e Mixer")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _pickupClip;
        [SerializeField] private AudioClip _placeClip;
        [SerializeField] private AudioClip _kneadClip;
        [SerializeField] private AudioClip _cutClip;
        [SerializeField] private AudioClip _toppingClip;
        [SerializeField] private AudioClip _ovenDingClip;
        [SerializeField] private AudioClip _ovenBurnClip;
        [SerializeField] private AudioClip _boxCloseClip;
        [SerializeField] private AudioClip _stackClip;
        [SerializeField] private AudioClip _backpackClip;
        [SerializeField] private AudioClip _happyCustomerClip;
        [SerializeField] private AudioClip _angryCustomerClip;
        [SerializeField] private AudioClip _deliverySuccessClip;
        [SerializeField] private AudioClip _deliveryFailClip;

        [Header("Partículas Opcionais")]
        [SerializeField] private ParticleSystem _flourDustParticles;
        [SerializeField] private ParticleSystem _confettiParticles;
        [SerializeField] private ParticleSystem _burnSmokeParticles;

        private readonly Dictionary<Transform, Tween> _activeTransformTweens = new Dictionary<Transform, Tween>();
        private readonly Dictionary<FeedbackType, float> _lastPlayTime = new Dictionary<FeedbackType, float>();

        private const float DefaultCooldown = 0.08f;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (_audioSource == null)
                {
                    _audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
                    _audioSource.playOnAwake = false;
                    _audioSource.spatialBlend = 0.5f; // Semi-posicional para manter boa audibilidade
                }
            }
            else if (Instance != this)
            {
                Destroy(this);
            }
        }

        private void OnDestroy()
        {
            KillAllTweens();
            if (Instance == this) Instance = null;
        }

        public void KillAllTweens()
        {
            foreach (var kvp in _activeTransformTweens)
            {
                if (kvp.Value != null && kvp.Value.IsActive())
                {
                    kvp.Value.Kill();
                }
            }
            _activeTransformTweens.Clear();
        }

        public bool CanTrigger(FeedbackType type, float customCooldown = DefaultCooldown)
        {
            if (_lastPlayTime.TryGetValue(type, out float lastTime))
            {
                if (Time.time - lastTime < customCooldown) return false;
            }
            _lastPlayTime[type] = Time.time;
            return true;
        }

        /// <summary>
        /// Aciona feedback completo de adição de ingrediente à pizza na bancada (micro-punch e som satisfatório).
        /// </summary>
        public void PlayAddToppingFeedback(Transform pizzaTransform)
        {
            if (!CanTrigger(FeedbackType.AddTopping)) return;

            PlaySound(_toppingClip != null ? _toppingClip : _placeClip, 0.75f);

            if (pizzaTransform != null && !UIToolkitTweenService.ReduceMotion)
            {
                PunchScaleSafe(pizzaTransform, new Vector3(0.08f, -0.06f, 0.08f), 0.18f);
            }
        }

        /// <summary>
        /// Feedback de corte ou ralo de ingrediente na tábua de corte.
        /// </summary>
        public void PlayCutFeedback(Transform cuttingBoardTransform)
        {
            if (!CanTrigger(FeedbackType.CutIngredient, 0.12f)) return;

            PlaySound(_cutClip != null ? _cutClip : _kneadClip, 0.8f);

            if (cuttingBoardTransform != null && !UIToolkitTweenService.ReduceMotion)
            {
                PunchPositionSafe(cuttingBoardTransform, new Vector3(0f, -0.015f, 0f), 0.14f);
            }
        }

        /// <summary>
        /// Feedback de abertura de massa (amassar / cilindrar).
        /// </summary>
        public void PlayDoughKneadFeedback(Transform doughTransform)
        {
            if (!CanTrigger(FeedbackType.DoughKnead, 0.15f)) return;

            PlaySound(_kneadClip != null ? _kneadClip : _placeClip, 0.75f);

            if (_flourDustParticles != null && doughTransform != null)
            {
                _flourDustParticles.transform.position = doughTransform.position + Vector3.up * 0.05f;
                _flourDustParticles.Play();
            }

            if (doughTransform != null && !UIToolkitTweenService.ReduceMotion)
            {
                PunchScaleSafe(doughTransform, new Vector3(0.12f, -0.1f, 0.12f), 0.22f);
            }
        }

        /// <summary>
        /// Feedback de forno pronto (Ding sonoro e aviso).
        /// </summary>
        public void PlayOvenReadyFeedback(Vector3 ovenPos)
        {
            if (!CanTrigger(FeedbackType.OvenDingReady, 1.5f)) return;

            PlaySound(_ovenDingClip, 0.9f);
        }

        /// <summary>
        /// Feedback de pizza queimando / queimada.
        /// </summary>
        public void PlayOvenBurnFeedback(Vector3 ovenPos)
        {
            if (!CanTrigger(FeedbackType.OvenBurnSmoke, 2.0f)) return;

            PlaySound(_ovenBurnClip, 0.85f);
            if (_burnSmokeParticles != null)
            {
                _burnSmokeParticles.transform.position = ovenPos + Vector3.up * 0.5f;
                _burnSmokeParticles.Play();
            }
        }

        /// <summary>
        /// Feedback ao embalar na caixa de pizza.
        /// </summary>
        public void PlayBoxCloseFeedback(Transform boxTransform)
        {
            if (!CanTrigger(FeedbackType.BoxClose)) return;

            PlaySound(_boxCloseClip != null ? _boxCloseClip : _placeClip, 0.85f);

            if (boxTransform != null && !UIToolkitTweenService.ReduceMotion)
            {
                PunchScaleSafe(boxTransform, new Vector3(0.06f, -0.06f, 0.06f), 0.16f);
            }
        }

        /// <summary>
        /// Feedback ao empilhar caixas.
        /// </summary>
        public void PlayBoxStackFeedback(Transform stackTransform)
        {
            if (!CanTrigger(FeedbackType.BoxStackSettle)) return;

            PlaySound(_stackClip != null ? _stackClip : _placeClip, 0.75f);

            if (stackTransform != null && !UIToolkitTweenService.ReduceMotion)
            {
                PunchPositionSafe(stackTransform, new Vector3(0f, -0.02f, 0f), 0.15f);
            }
        }

        /// <summary>
        /// Feedback de guardar ou retirar da mochila térmica.
        /// </summary>
        public void PlayBackpackFeedback(bool isStoring)
        {
            var type = isStoring ? FeedbackType.BackpackStore : FeedbackType.BackpackRetrieve;
            if (!CanTrigger(type)) return;

            PlaySound(_backpackClip != null ? _backpackClip : _pickupClip, 0.8f);
        }

        /// <summary>
        /// Feedback comemoração de entrega com sucesso (+gorjeta / cliente feliz).
        /// </summary>
        public void PlayDeliverySuccessFeedback(Vector3 customerPos)
        {
            if (!CanTrigger(FeedbackType.DeliverySuccess, 0.5f)) return;

            PlaySound(_deliverySuccessClip != null ? _deliverySuccessClip : _happyCustomerClip, 0.95f);

            if (_confettiParticles != null)
            {
                _confettiParticles.transform.position = customerPos + Vector3.up * 1.2f;
                _confettiParticles.Play();
            }

            UIFeedbackService.Instance?.PlaySuccess();
        }

        /// <summary>
        /// Feedback de entrega incorreta / cliente furioso.
        /// </summary>
        public void PlayDeliveryFailFeedback(Vector3 customerPos)
        {
            if (!CanTrigger(FeedbackType.DeliveryFail, 0.5f)) return;

            PlaySound(_deliveryFailClip != null ? _deliveryFailClip : _angryCustomerClip, 0.95f);
            UIFeedbackService.Instance?.PlayError();
        }

        public void PlayPickupFeedback(Transform itemTransform)
        {
            if (!CanTrigger(FeedbackType.PickupItem)) return;

            PlaySound(_pickupClip, 0.7f);

            if (itemTransform != null && !UIToolkitTweenService.ReduceMotion)
            {
                PunchScaleSafe(itemTransform, new Vector3(-0.06f, 0.08f, -0.06f), 0.15f);
            }
        }

        public void PlayPlaceFeedback(Transform itemTransform)
        {
            if (!CanTrigger(FeedbackType.PlaceItem)) return;

            PlaySound(_placeClip, 0.7f);

            if (itemTransform != null && !UIToolkitTweenService.ReduceMotion)
            {
                PunchScaleSafe(itemTransform, new Vector3(0.08f, -0.08f, 0.08f), 0.15f);
            }
        }

        private void PunchScaleSafe(Transform target, Vector3 punch, float duration)
        {
            if (target == null) return;
            CancelExistingTween(target);

            Vector3 origScale = target.localScale;
            var tw = target.DOPunchScale(punch, duration, 4, 0.5f)
                .SetUpdate(true)
                .OnKill(() => {
                    if (target != null) target.localScale = origScale;
                    _activeTransformTweens.Remove(target);
                });

            _activeTransformTweens[target] = tw;
        }

        private void PunchPositionSafe(Transform target, Vector3 punch, float duration)
        {
            if (target == null) return;
            CancelExistingTween(target);

            Vector3 origPos = target.localPosition;
            var tw = target.DOPunchPosition(punch, duration, 4, 0.5f)
                .SetUpdate(true)
                .OnKill(() => {
                    if (target != null) target.localPosition = origPos;
                    _activeTransformTweens.Remove(target);
                });

            _activeTransformTweens[target] = tw;
        }

        private void CancelExistingTween(Transform target)
        {
            if (_activeTransformTweens.TryGetValue(target, out var tw) && tw != null && tw.IsActive())
            {
                tw.Kill(true); // Completa e reseta para a pose base antes de novo tween
            }
            _activeTransformTweens.Remove(target);
        }

        private void PlaySound(AudioClip clip, float volume = 0.8f)
        {
            if (clip != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(clip, volume);
            }
        }
    }
}
