using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Presentation;

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
            }
        }

        private void OnDestroy()
        {
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
            _banner.style.position = Position.Absolute;
            _banner.style.left = Length.Percent(28);
            _banner.style.width = Length.Percent(44);
            _banner.style.bottom = 128;
            _banner.style.backgroundColor = new Color(0.18f, 0.04f, 0.05f, 0.92f);
            _banner.style.borderTopWidth = 2;
            _banner.style.borderBottomWidth = 2;
            _banner.style.borderLeftWidth = 2;
            _banner.style.borderRightWidth = 2;
            var border = new Color(0.95f, 0.30f, 0.25f);
            _banner.style.borderTopColor = border;
            _banner.style.borderBottomColor = border;
            _banner.style.borderLeftColor = border;
            _banner.style.borderRightColor = border;
            _banner.style.borderTopLeftRadius = 10;
            _banner.style.borderTopRightRadius = 10;
            _banner.style.borderBottomLeftRadius = 10;
            _banner.style.borderBottomRightRadius = 10;
            _banner.style.paddingLeft = 16;
            _banner.style.paddingRight = 16;
            _banner.style.paddingTop = 10;
            _banner.style.paddingBottom = 10;
            _banner.style.alignItems = Align.Center;
            _banner.style.display = DisplayStyle.None;

            _titleLabel = new Label();
            _titleLabel.pickingMode = PickingMode.Ignore;
            _titleLabel.style.fontSize = 18;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.color = new Color(1f, 0.45f, 0.40f);
            _banner.Add(_titleLabel);

            _detailLabel = new Label();
            _detailLabel.pickingMode = PickingMode.Ignore;
            _detailLabel.style.fontSize = 13;
            _detailLabel.style.color = Color.white;
            _detailLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _detailLabel.style.whiteSpace = WhiteSpace.Normal;
            _detailLabel.style.marginTop = 4;
            _banner.Add(_detailLabel);

            _progressBg = new VisualElement();
            _progressBg.pickingMode = PickingMode.Ignore;
            _progressBg.style.width = Length.Percent(90);
            _progressBg.style.height = 10;
            _progressBg.style.backgroundColor = new Color(0.08f, 0.08f, 0.10f, 0.9f);
            _progressBg.style.marginTop = 6;
            _progressFill = new VisualElement();
            _progressFill.pickingMode = PickingMode.Ignore;
            _progressFill.style.height = Length.Percent(100);
            _progressFill.style.width = Length.Percent(0);
            _progressFill.style.backgroundColor = new Color(0.32f, 0.88f, 0.45f);
            _progressBg.Add(_progressFill);
            _banner.Add(_progressBg);

            _spectatorBtnRow = new VisualElement();
            _spectatorBtnRow.style.flexDirection = FlexDirection.Row;
            _spectatorBtnRow.style.marginTop = 8;

            var prevBtn = new Button(() => PizzeriaCommandHandler.Instance?.CycleSpectatorTargetServerRpc(-1))
            {
                text = "◀ Aliado Anterior [Q]"
            };
            prevBtn.style.height = 30;
            prevBtn.style.marginRight = 8;
            prevBtn.style.backgroundColor = new Color(0.24f, 0.30f, 0.42f);
            prevBtn.style.color = Color.white;
            _spectatorBtnRow.Add(prevBtn);

            var nextBtn = new Button(() => PizzeriaCommandHandler.Instance?.CycleSpectatorTargetServerRpc(1))
            {
                text = "Próximo Aliado [E] ▶"
            };
            nextBtn.style.height = 30;
            nextBtn.style.backgroundColor = new Color(0.24f, 0.30f, 0.42f);
            nextBtn.style.color = Color.white;
            _spectatorBtnRow.Add(nextBtn);

            _banner.Add(_spectatorBtnRow);
            _root.Add(_banner);
        }

        private void Update()
        {
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
                string allyName = ResolvePlayerName(dto.SpectatingTargetClientId);
                _detailLabel.text = dto.SpectatingTargetClientId > 0 || allyName != null
                    ? $"Observando: {allyName ?? $"Jogador #{dto.SpectatingTargetClientId}"} ([Q]/[E] ou clique abaixo). Retorno automático na próxima noite."
                    : "Aguardando resolução do turno ou retorno na próxima noite.";
                _progressBg.style.display = DisplayStyle.None;
                _spectatorBtnRow.style.display = DisplayStyle.Flex;
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
            var players = FindObjectsByType<PlayerState>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                if (p != null && p.OwnerClientId == clientId)
                {
                    string n = p.PlayerName.Value.ToString();
                    return string.IsNullOrWhiteSpace(n) ? $"Jogador #{clientId}" : n;
                }
            }
            return null;
        }
    }
}
