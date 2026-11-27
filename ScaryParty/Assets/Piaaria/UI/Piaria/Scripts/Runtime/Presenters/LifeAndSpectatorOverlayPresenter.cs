using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.UI.Piaria.Core;

namespace ScaryParty.UI.Piaria.Presenters
{
    /// <summary>
    /// Overlay de estado de vida em UI Toolkit (P4 — Fluxo 11):
    /// mostra barra de resgate quando incapacitado e controles [Q]/[E] de espectador quando morto.
    /// Permanece pass-through (PickingMode.Ignore) enquanto o jogador estiver vivo.
    /// </summary>
    public class LifeAndSpectatorOverlayPresenter : MonoBehaviour
    {
        public static LifeAndSpectatorOverlayPresenter Instance { get; private set; }

        public static LifeAndSpectatorOverlayPresenter EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindFirstObjectByType<LifeAndSpectatorOverlayPresenter>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            var hud = FindFirstObjectByType<HudPresenter>();
            if (hud != null)
                return hud.gameObject.AddComponent<LifeAndSpectatorOverlayPresenter>();

            var go = new GameObject("LifeAndSpectatorOverlayPresenter");
            return go.AddComponent<LifeAndSpectatorOverlayPresenter>();
        }

        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _banner;
        private Label _titleLabel;
        private Label _detailLabel;
        private VisualElement _progressBg;
        private VisualElement _progressFill;
        private VisualElement _spectatorBtnRow;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            var sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName.IndexOf("Lobby", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sceneName.IndexOf("Ready", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                enabled = false;
                return;
            }
            GameplayIntroCutsceneFlow.OnStageChanged -= HandlePresentationStageChanged;
            GameplayIntroCutsceneFlow.OnStageChanged += HandlePresentationStageChanged;
        }

        private void HandlePresentationStageChanged(IntroPresentationStage stage)
        {
            if (stage != IntroPresentationStage.NightActive && _banner != null)
            {
                _banner.style.display = DisplayStyle.None;
            }
        }

        private void OnDestroy()
        {
            GameplayIntroCutsceneFlow.OnStageChanged -= HandlePresentationStageChanged;
            if (Instance == this) Instance = null;
        }

        private void EnsureUIInitialized()
        {
            if (_banner != null) return;
            var hud = FindFirstObjectByType<HudPresenter>();
            _doc = hud != null ? hud.uiDocument : GetComponent<UIDocument>();
            if (_doc == null) return;
            _root = _doc.rootVisualElement;
            if (_root == null) return;

            _banner = new VisualElement { name = "LifeAndSpectatorBanner" };
            _banner.pickingMode = PickingMode.Ignore;
            _banner.AddToClassList("life-spectator-banner");
            _banner.style.display = DisplayStyle.None;

            _titleLabel = new Label();
            _titleLabel.pickingMode = PickingMode.Ignore;
            _titleLabel.AddToClassList("life-spectator-title");
            _banner.Add(_titleLabel);

            _detailLabel = new Label();
            _detailLabel.pickingMode = PickingMode.Ignore;
            _detailLabel.AddToClassList("life-spectator-detail");
            _banner.Add(_detailLabel);

            _progressBg = new VisualElement();
            _progressBg.pickingMode = PickingMode.Ignore;
            _progressBg.AddToClassList("life-progress-bg");
            _progressFill = new VisualElement();
            _progressFill.pickingMode = PickingMode.Ignore;
            _progressFill.AddToClassList("life-progress-fill");
            _progressBg.Add(_progressFill);
            _banner.Add(_progressBg);

            _spectatorBtnRow = new VisualElement();
            _spectatorBtnRow.AddToClassList("spectator-btn-row");

            var prevBtn = new Button(() => PizzeriaCommandHandler.Instance?.CycleSpectatorTargetServerRpc(-1))
            {
                text = "Aliado Anterior [Q]"
            };
            prevBtn.AddToClassList("spectator-nav-btn");
            UIToolkitTweenService.BindInteractiveFeedback(prevBtn, 1.04f, 0.96f);
            _spectatorBtnRow.Add(prevBtn);

            var nextBtn = new Button(() => PizzeriaCommandHandler.Instance?.CycleSpectatorTargetServerRpc(1))
            {
                text = "Próximo Aliado [E]"
            };
            nextBtn.AddToClassList("spectator-nav-btn");
            UIToolkitTweenService.BindInteractiveFeedback(nextBtn, 1.04f, 0.96f);
            _spectatorBtnRow.Add(nextBtn);

            _banner.Add(_spectatorBtnRow);
            _root.Add(_banner);
        }

        private void Update()
        {
            if (GameplayIntroCutsceneFlow.Instance != null &&
                GameplayIntroCutsceneFlow.CurrentStage != IntroPresentationStage.NightActive)
            {
                if (_banner != null)
                    _banner.style.display = DisplayStyle.None;
                return;
            }

            var net = PizzeriaNetworkState.Instance;
            if (net == null) return;

            ulong localId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0UL;
            if (!net.TryGetPlayerCombatState(localId, out var dto) || (!dto.IsIncapacitated && !dto.IsDead))
            {
                if (_banner != null)
                    _banner.style.display = DisplayStyle.None;
                return;
            }

            EnsureUIInitialized();
            if (_banner == null) return;
            _banner.style.display = DisplayStyle.Flex;

            if (dto.IsDead)
            {
                _titleLabel.text = "VOCÊ MORREU — MODO ESPECTADOR";
                bool hasTarget = dto.SpectatingTargetClientId != ulong.MaxValue;
                string allyName = hasTarget ? ResolvePlayerName(dto.SpectatingTargetClientId) : null;
                _detailLabel.text = hasTarget
                    ? $"Observando: {allyName ?? $"Jogador #{dto.SpectatingTargetClientId}"} ([Q]/[E] ou clique abaixo). Retorno automático na próxima noite."
                    : "Aguardando resolução do turno ou retorno na próxima noite.";
                _progressBg.style.display = DisplayStyle.None;
                _spectatorBtnRow.style.display = hasTarget ? DisplayStyle.Flex : DisplayStyle.None;
            }
            else
            {
                _titleLabel.text = $"VOCÊ ESTÁ NOCAUTEADO! ({Mathf.CeilToInt(dto.RescueRemainingSeconds)}s)";
                string helperInfo = dto.RescueProgress > 0.01f
                    ? $"Aliado #{dto.RescuedByClientId} resgatando: {Mathf.RoundToInt(dto.RescueProgress * 100f)}%"
                    : "Aproxime-se ou peça a um aliado para segurar [F] perto de você!";
                _detailLabel.text = helperInfo;
                _progressBg.style.display = DisplayStyle.Flex;
                _progressFill.style.width = new Length(Mathf.Clamp01(dto.RescueProgress) * 100f, LengthUnit.Percent);
                _spectatorBtnRow.style.display = DisplayStyle.None;
            }
        }

        private static string ResolvePlayerName(ulong clientId)
        {
            if (clientId == ulong.MaxValue) return null;
            if (PlayerState.TryGetByClientId(clientId, out var matched) && matched != null)
            {
                string n = matched.PlayerName.Value.ToString();
                return string.IsNullOrWhiteSpace(n) ? $"Jogador #{clientId}" : n;
            }
            return null;
        }
    }
}
