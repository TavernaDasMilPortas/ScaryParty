using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;
using ScaryParty.UI.Piaria.Core;

namespace ScaryParty.Pizzeria.Presentation
{
    /// <summary>
    /// Modal de resultados de fim de noite e derrota operacional (O5 / N1).
    /// Exibido automaticamente ao entrar em NightPhase.Results ou NightPhase.Defeat.
    /// </summary>
    public class NightResultsPresenter : MonoBehaviour
    {
        public static NightResultsPresenter Instance { get; private set; }

        public static NightResultsPresenter EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindFirstObjectByType<NightResultsPresenter>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            var go = new GameObject("NightResultsPresenter");
            return go.AddComponent<NightResultsPresenter>();
        }

        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _overlay;
        private VisualElement _card;

        private Label _titleLabel;
        private Label _subtitleLabel;
        private Label _statsLabel;
        private Label _clientWaitLabel;
        private Button _nextNightBtn;
        private Button _retryNightBtn;

        private bool _isOpen = false;
        private bool _hasLock = false;
        private byte _lastPhase = 255;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            RunPreparationPresenter.EnsureInstance();
        }

        private void OnDestroy()
        {
            if (_hasLock)
            {
                GameplayInputGate.PopLock();
                _hasLock = false;
            }
            if (Instance == this) Instance = null;
        }

        private void EnsureUIInitialized()
        {
            if (_overlay != null) return;

            var hud = FindFirstObjectByType<ScaryParty.UI.Piaria.Presenters.HudPresenter>();
            _doc = hud != null ? hud.uiDocument : GetComponent<UIDocument>();
            if (_doc == null)
                _doc = gameObject.AddComponent<UIDocument>();

            if (_doc.panelSettings == null)
            {
                var panelSettingsList = Resources.FindObjectsOfTypeAll<PanelSettings>();
                if (panelSettingsList != null && panelSettingsList.Length > 0)
                    _doc.panelSettings = panelSettingsList[0];
            }

            _root = _doc.rootVisualElement;
            if (_root == null) return;

            _overlay = new VisualElement();
            _overlay.name = "NightResultsOverlay";
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = 0;
            _overlay.style.top = 0;
            _overlay.style.right = 0;
            _overlay.style.bottom = 0;
            _overlay.style.backgroundColor = new Color(0.03f, 0.03f, 0.06f, 0.84f);
            _overlay.style.justifyContent = Justify.Center;
            _overlay.style.alignItems = Align.Center;
            _overlay.style.display = DisplayStyle.None;

            _card = new VisualElement();
            _card.name = "NightResultsCard";
            _card.style.width = 580;
            _card.style.backgroundColor = new Color(0.11f, 0.11f, 0.14f, 0.98f);
            _card.style.borderTopWidth = 3;
            _card.style.borderBottomWidth = 3;
            _card.style.borderLeftWidth = 3;
            _card.style.borderRightWidth = 3;
            _card.style.borderTopLeftRadius = 14;
            _card.style.borderTopRightRadius = 14;
            _card.style.borderBottomLeftRadius = 14;
            _card.style.borderBottomRightRadius = 14;
            _card.style.paddingLeft = 28;
            _card.style.paddingRight = 28;
            _card.style.paddingTop = 26;
            _card.style.paddingBottom = 24;

            _titleLabel = new Label("FIM DA NOITE");
            _titleLabel.style.fontSize = 28;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _titleLabel.style.marginBottom = 8;
            _card.Add(_titleLabel);

            _subtitleLabel = new Label("");
            _subtitleLabel.style.fontSize = 17;
            _subtitleLabel.style.color = new Color(0.9f, 0.88f, 0.84f);
            _subtitleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _subtitleLabel.style.marginBottom = 16;
            _card.Add(_subtitleLabel);

            _statsLabel = new Label("");
            _statsLabel.style.fontSize = 18;
            _statsLabel.style.color = new Color(0.95f, 0.95f, 0.98f);
            _statsLabel.style.backgroundColor = new Color(0.06f, 0.06f, 0.08f, 0.9f);
            _statsLabel.style.borderTopLeftRadius = 8;
            _card.Add(_statsLabel);
            _statsLabel.style.borderTopRightRadius = 8;
            _statsLabel.style.borderBottomLeftRadius = 8;
            _statsLabel.style.borderBottomRightRadius = 8;
            _statsLabel.style.paddingLeft = 16;
            _statsLabel.style.paddingRight = 16;
            _statsLabel.style.paddingTop = 14;
            _statsLabel.style.paddingBottom = 14;
            _statsLabel.style.marginBottom = 18;

            _clientWaitLabel = new Label("Aguardando decisão do Host...");
            _clientWaitLabel.style.fontSize = 16;
            _clientWaitLabel.style.color = new Color(0.9f, 0.78f, 0.4f);
            _clientWaitLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _clientWaitLabel.style.marginBottom = 12;
            _clientWaitLabel.style.display = DisplayStyle.None;
            _card.Add(_clientWaitLabel);

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.justifyContent = Justify.Center;

            _retryNightBtn = new Button(() =>
            {
                PizzeriaCommandHandler.Instance?.StartNewRunServerRpc();
            });
            _retryNightBtn.text = "Iniciar Nova RUN (Noite 1)";
            _retryNightBtn.style.height = 46;
            _retryNightBtn.style.flexGrow = 1;
            _retryNightBtn.style.marginRight = 8;
            _retryNightBtn.style.fontSize = 17;
            _retryNightBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            _retryNightBtn.style.backgroundColor = new Color(0.72f, 0.35f, 0.18f);
            _retryNightBtn.style.color = Color.white;
            btnRow.Add(_retryNightBtn);

            _nextNightBtn = new Button(() =>
            {
                PizzeriaCommandHandler.Instance?.AdvanceToNextNightServerRpc();
            });
            _nextNightBtn.text = "Próxima Noite";
            _nextNightBtn.style.height = 46;
            _nextNightBtn.style.flexGrow = 1;
            _nextNightBtn.style.marginLeft = 8;
            _nextNightBtn.style.fontSize = 17;
            _nextNightBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            _nextNightBtn.style.backgroundColor = new Color(0.18f, 0.58f, 0.32f);
            _nextNightBtn.style.color = Color.white;
            btnRow.Add(_nextNightBtn);

            var menuBtn = new Button(ReturnToMainMenu)
            {
                text = "Voltar ao Menu"
            };
            menuBtn.style.height = 46;
            menuBtn.style.minWidth = 150;
            menuBtn.style.marginLeft = 8;
            menuBtn.style.fontSize = 15;
            menuBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            menuBtn.style.backgroundColor = new Color(0.32f, 0.34f, 0.42f);
            menuBtn.style.color = Color.white;
            btnRow.Add(menuBtn);

            _card.Add(btnRow);
            _overlay.Add(_card);
            _root.Add(_overlay);
        }

        private void Update()
        {
            var netState = PizzeriaNetworkState.Instance;
            if (netState == null) return;

            var dto = netState.NightState.Value;
            if (dto.NightNumber < 0) return;
            RunPreparationPresenter.EnsureInstance();

            bool shouldBeOpen = dto.Phase == (byte)NightPhase.Results || dto.Phase == (byte)NightPhase.Defeat;
            if (shouldBeOpen)
            {
                if (!_isOpen || _lastPhase != dto.Phase)
                {
                    OpenWithState(dto, netState.RestaurantBudget.Value);
                }
                else
                {
                    RefreshLabels(dto, netState.RestaurantBudget.Value);
                }
            }
            else if (_isOpen)
            {
                Close();
            }

            _lastPhase = dto.Phase;
        }

        private void OpenWithState(NetNightStateDto dto, int totalBudget)
        {
            EnsureUIInitialized();
            if (_overlay == null) return;

            _isOpen = true;
            _overlay.style.display = DisplayStyle.Flex;
            if (!_hasLock)
            {
                GameplayInputGate.PushLock();
                _hasLock = true;
            }

            RefreshLabels(dto, totalBudget);
        }

        private void RefreshLabels(NetNightStateDto dto, int totalBudget)
        {
            var netState = PizzeriaNetworkState.Instance;
            var runDto = netState != null ? netState.RunState.Value : default;
            int maxNights = runDto.MaxRunNights > 0 ? runDto.MaxRunNights : 3;
            bool isTutorialNight = dto.NightNumber == 0;
            bool isVictory = dto.Phase == (byte)NightPhase.Results;
            bool isRunFinalVictory = !isTutorialNight && isVictory && (runDto.IsRunVictory || dto.NightNumber >= maxNights);
            Color accent = isVictory ? new Color(0.3f, 0.85f, 0.45f) : new Color(0.9f, 0.28f, 0.25f);

            if (_card != null)
            {
                _card.style.borderTopColor = accent;
                _card.style.borderBottomColor = accent;
                _card.style.borderLeftColor = accent;
                _card.style.borderRightColor = accent;
            }

            if (_titleLabel != null)
            {
                if (isTutorialNight)
                    _titleLabel.text = "NOITE 0 (TUTORIAL) CONCLUÍDA!";
                else if (isRunFinalVictory)
                    _titleLabel.text = $"VITÓRIA DA RUN! ({dto.NightNumber}/{maxNights})";
                else if (isVictory)
                    _titleLabel.text = $"NOITE {dto.NightNumber}/{maxNights} CONCLUÍDA!";
                else
                    _titleLabel.text = $"DERROTA OPERACIONAL — NOITE {dto.NightNumber}";
                _titleLabel.style.color = accent;
            }

            if (_subtitleLabel != null)
            {
                if (isTutorialNight)
                {
                    _subtitleLabel.text = "Tutorial concluído sem consumir as 3 noites da RUN. Avance para a Noite 1!";
                }
                else if (isRunFinalVictory)
                {
                    _subtitleLabel.text = "Parabéns! A equipe sobreviveu e cumpriu todas as metas da RUN!";
                }
                else if (isVictory)
                {
                    _subtitleLabel.text = "Meta atingida! Avance para a preparação inter-noites (votação e loja).";
                }
                else
                {
                    _subtitleLabel.text = dto.DefeatReason switch
                    {
                        (byte)NightDefeatReason.RevenueBelowGoal => "A receita obtida nesta noite ficou abaixo da meta estipulada.",
                        (byte)NightDefeatReason.AllPlayersIncapacitated => "Toda a equipe foi incapacitada durante o turno.",
                        (byte)NightDefeatReason.ReputationZeroed => "A reputação da pizzaria zerou durante o expediente.",
                        _ => "Falha crítica na operação da pizzaria."
                    };
                }
            }

            if (_statsLabel != null)
            {
                int netNightBalance = dto.EarnedRevenue - (dto.NightSupplySpent + dto.NightShopSpent + dto.NightWagesSpent);
                _statsLabel.text =
                    $"Faturamento da Noite: R$ {dto.EarnedRevenue} / Meta: R$ {dto.RevenueGoal} (Humanos congelados: {Mathf.Max(1, dto.FrozenHumanCount)})\n" +
                    $"Gastos Separados -> Reposição: R$ {dto.NightSupplySpent} | Loja/Upgrades: R$ {dto.NightShopSpent} | Equipe: R$ {dto.NightWagesSpent}\n" +
                    $"Saldo Líquido do Turno: R$ {netNightBalance} | Caixa Coletivo Atual: R$ {totalBudget}\n" +
                    $"Reputação: {dto.Reputation}/100 | Pedidos Concluídos: {dto.OrdersCompleted} | Falhados: {dto.OrdersFailed} | Perdidos: {dto.LostCustomers}";
            }

            bool isHost = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;
            if (_clientWaitLabel != null)
                _clientWaitLabel.style.display = isHost ? DisplayStyle.None : DisplayStyle.Flex;

            if (_retryNightBtn != null)
            {
                bool showNewRunOnEnd = !isTutorialNight && (!isVictory || isRunFinalVictory);
                _retryNightBtn.text = "Iniciar Nova RUN (Noite 1)";
                _retryNightBtn.style.display = (isHost && showNewRunOnEnd) ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_nextNightBtn != null)
            {
                _nextNightBtn.text = isTutorialNight ? "Iniciar Noite 1 (3 Noites)" : "Ir para Votação e Loja";
                _nextNightBtn.style.display = (isHost && isVictory && !isRunFinalVictory) ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;

            if (_overlay != null)
                _overlay.style.display = DisplayStyle.None;

            if (_hasLock)
            {
                GameplayInputGate.PopLock();
                _hasLock = false;
            }
        }

        private void ReturnToMainMenu()
        {
            Close();
            if (RoomDiscoveryService.Instance != null)
                RoomDiscoveryService.Instance.StopHosting();
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();
            GameplayInputGate.Reset();
            UnityEngine.SceneManagement.SceneManager.LoadScene("LobbyScene");
        }
    }
}
