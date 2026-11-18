using System;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

namespace ScaryParty.UI.Piaria.Core
{
    public enum NightUIFeedbackId
    {
        IntermissionOpen,
        IntermissionClose,
        TabSwitch,
        SummaryHeroIn,
        SummaryCounterTick,
        CardHover,
        CardPress,
        CardDropIn,
        HindranceCardSelect,
        HindranceCardDeselect,
        OpportunityCardSelect,
        OpportunityCardDeselect,
        VoteTicketUpdate,
        ShopItemHover,
        ShopItemPurchasePending,
        ShopItemPurchaseSuccess,
        ShopItemPurchaseReject,
        ShopBudgetUpdate,
        ReadyToggleOn,
        ReadyToggleOff,
        ReadyBlockedMissingVotes,
        PlayerStatusUpdate,
        AllPlayersReadyLock,
        LotterySweepTick,
        LotteryHindranceReveal,
        LotteryOpportunityReveal,
        LotteryWinnerHighlight,
        LotteryNonWinnerDim,
        CountdownTick3,
        CountdownTick2,
        CountdownTick1,
        CountdownGo,
        TabOverlayOpen,
        TabOverlayClose,
        HudAlertPulse,
        ErrorShake
    }

    public enum FeedbackPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    /// <summary>
    /// Ponte centralizada de Feel (MMF_Player) e áudio 2D para a Intermissão 2.1 e Overlay TAB.
    /// Respeita ReduceMotion, OverallIntensity e limite de feedbacks simultâneos.
    /// </summary>
    [DisallowMultipleComponent]
    public class NightIntermissionFeelBridge : MonoBehaviour
    {
        public static NightIntermissionFeelBridge Instance { get; private set; }

        [Serializable]
        public class FeedbackBinding
        {
            public NightUIFeedbackId id;
            public FeedbackPriority priority = FeedbackPriority.Normal;
            public MMF_Player mmfPlayer;
            public AudioClip fallbackClip;
            [Range(0f, 1f)] public float volumeScale = 0.8f;
        }

        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private List<FeedbackBinding> _bindings = new List<FeedbackBinding>();

        private readonly Dictionary<NightUIFeedbackId, float> _lastPlayedTime = new Dictionary<NightUIFeedbackId, float>();
        private int _activeFrameCount;
        private int _lastFrameCounted = -1;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (_audioSource == null)
            {
                _audioSource = GetComponent<AudioSource>();
                if (_audioSource == null)
                {
                    _audioSource = gameObject.AddComponent<AudioSource>();
                    _audioSource.playOnAwake = false;
                    _audioSource.spatialBlend = 0f;
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static void Play(NightUIFeedbackId id, FeedbackPriority priority = FeedbackPriority.Normal)
        {
            if (Instance != null)
            {
                Instance.PlayInternal(id, priority);
            }
        }

        public void PlayInternal(NightUIFeedbackId id, FeedbackPriority priority = FeedbackPriority.Normal)
        {
            var theme = NightIntermissionTheme.ResolveActive();
            if (theme != null && !theme.enableUIJuice && priority < FeedbackPriority.Critical)
            {
                return;
            }

            if (Time.frameCount != _lastFrameCounted)
            {
                _lastFrameCounted = Time.frameCount;
                _activeFrameCount = 0;
            }

            int cap = theme != null ? Mathf.Max(1, theme.maxSimultaneousFeedbacks) : 6;
            if (_activeFrameCount >= cap && priority < FeedbackPriority.High)
            {
                return;
            }

            if (_lastPlayedTime.TryGetValue(id, out float prevTime) && Time.unscaledTime - prevTime < 0.04f && priority < FeedbackPriority.High)
            {
                return;
            }

            _lastPlayedTime[id] = Time.unscaledTime;
            _activeFrameCount++;

            for (int i = 0; i < _bindings.Count; i++)
            {
                var binding = _bindings[i];
                if (binding == null || binding.id != id) continue;

                if (binding.mmfPlayer != null)
                {
                    binding.mmfPlayer.PlayFeedbacks();
                    return;
                }

                if (binding.fallbackClip != null && _audioSource != null)
                {
                    float vol = binding.volumeScale * (theme != null ? theme.sfxVolume : 0.85f);
                    _audioSource.PlayOneShot(binding.fallbackClip, vol);
                    return;
                }
            }
        }
    }
}
