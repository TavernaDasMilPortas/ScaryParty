using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Unity.Netcode;
using ScaryParty.Player;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.UI.Piaria.Core;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ScaryParty.UI.Piaria.Presenters
{
    /// <summary>
    /// Menu de Pausa em jogo acionado por [ESC] sem conflitar com modais abertos (O2 / O20 / N3).
    /// Inclui recuperação de jogador e recuperação de moto com cooldown autoritativo de 60s.
    /// </summary>
    public class PauseEscapeMenuPresenter : MonoBehaviour, IModalView
    {
        public static PauseEscapeMenuPresenter Instance { get; private set; }

        public bool CanCloseWithEscape =>
            !_isOpen ||
            (_lastToggleFrame != Time.frameCount && _mainPanel != null && _mainPanel.style.display == DisplayStyle.Flex);
        public VisualElement RootElement { get; private set; }
        public bool IsOpen => _isOpen;

        public static PauseEscapeMenuPresenter EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindFirstObjectByType<PauseEscapeMenuPresenter>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            var go = new GameObject("PauseEscapeMenuPresenter");
            return go.AddComponent<PauseEscapeMenuPresenter>();
        }

        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _mainPanel;
        private VisualElement _controlsSubPanel;
        private VisualElement _settingsSubPanel;
        private VisualElement _confirmLeaveSubPanel;
        private Button _recoverPlayerBtn;
        private Button _recoverMotoBtn;

        private bool _isOpen = false;
        private bool _pushedToStack = false;
        private bool _pushedGateFallback = false;
        private bool _otherModalWasOpenLastFrame = false;
        private int _lastToggleFrame = -1;
        private float _openedUnscaledTime = -10f;

        private float _playerRecoverCooldownUntil = 0f;
        private float _motoRecoverCooldownUntil = 0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (_isOpen)
            {
                ReleaseLock();
            }
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            if (sceneName.IndexOf("Lobby", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sceneName.IndexOf("Ready", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                enabled = false;
            }
        }

        private void EnsureUIInitialized()
        {
            if (RootElement != null) return;

            var hud = FindFirstObjectByType<HudPresenter>();
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

            BuildUIHierarchy();
        }

        private void BuildUIHierarchy()
        {
            RootElement = new VisualElement();
            RootElement.name = "PauseEscapeOverlay";
            RootElement.style.position = Position.Absolute;
            RootElement.style.left = 0;
            RootElement.style.top = 0;
            RootElement.style.right = 0;
            RootElement.style.bottom = 0;
            RootElement.style.backgroundColor = new Color(0f, 0f, 0f, 0.72f);
            RootElement.style.justifyContent = Justify.Center;
            RootElement.style.alignItems = Align.Center;
            RootElement.style.display = DisplayStyle.None;

            var card = new VisualElement();
            card.style.width = 480;
            card.style.backgroundColor = new Color(0.10f, 0.11f, 0.15f, 0.98f);
            card.style.borderTopWidth = 2;
            card.style.borderBottomWidth = 2;
            card.style.borderLeftWidth = 2;
            card.style.borderRightWidth = 2;
            card.style.borderTopColor = new Color(0.88f, 0.62f, 0.25f);
            card.style.borderBottomColor = new Color(0.88f, 0.62f, 0.25f);
            card.style.borderLeftColor = new Color(0.88f, 0.62f, 0.25f);
            card.style.borderRightColor = new Color(0.88f, 0.62f, 0.25f);
            card.style.borderTopLeftRadius = 12;
            card.style.borderTopRightRadius = 12;
            card.style.borderBottomLeftRadius = 12;
            card.style.borderBottomRightRadius = 12;
            card.style.paddingLeft = 24;
            card.style.paddingRight = 24;
            card.style.paddingTop = 22;
            card.style.paddingBottom = 22;

            var title = new Label("PAUSA — PIZZARIA");
            title.style.fontSize = 24;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.98f, 0.8f, 0.35f);
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.marginBottom = 16;
            card.Add(title);

            _mainPanel = new VisualElement();
            AddMenuButton(_mainPanel, "Continuar [ESC]", Close, new Color(0.20f, 0.56f, 0.32f));
            AddMenuButton(_mainPanel, "Controles", () => ShowSubPanel(_controlsSubPanel), new Color(0.24f, 0.28f, 0.36f));
            AddMenuButton(_mainPanel, "Configurações", () => ShowSubPanel(_settingsSubPanel), new Color(0.24f, 0.28f, 0.36f));
            AddMenuButton(_mainPanel, "Ajuda / Tutorial", () =>
            {
                Close();
                ContextualTutorialPresenter.EnsureInstance().ShowTutorial();
            }, new Color(0.24f, 0.35f, 0.45f));

            _recoverPlayerBtn = AddMenuButton(_mainPanel, "Recuperar Jogador (60s)", RequestRecoverPlayer, new Color(0.55f, 0.40f, 0.18f));
            _recoverMotoBtn = AddMenuButton(_mainPanel, "Recuperar Moto (60s)", RequestRecoverMoto, new Color(0.55f, 0.40f, 0.18f));

            AddMenuButton(_mainPanel, "Sair da Partida", () =>
            {
                if (Time.unscaledTime - _openedUnscaledTime < 0.15f) return;
                ShowSubPanel(_confirmLeaveSubPanel);
            }, new Color(0.68f, 0.22f, 0.22f));
            card.Add(_mainPanel);

            // Subpainel Confirmar Saída
            _confirmLeaveSubPanel = new VisualElement();
            _confirmLeaveSubPanel.style.display = DisplayStyle.None;
            var confirmText = new Label("Deseja realmente sair da partida e voltar ao Lobby?");
            confirmText.style.fontSize = 16;
            confirmText.style.color = new Color(0.95f, 0.92f, 0.92f);
            confirmText.style.unityTextAlign = TextAnchor.MiddleCenter;
            confirmText.style.whiteSpace = WhiteSpace.Normal;
            confirmText.style.marginBottom = 16;
            _confirmLeaveSubPanel.Add(confirmText);
            AddMenuButton(_confirmLeaveSubPanel, "Sim, Sair da Partida", LeaveMatch, new Color(0.75f, 0.18f, 0.18f));
            AddMenuButton(_confirmLeaveSubPanel, "Cancelar [ESC]", () => ShowSubPanel(_mainPanel), new Color(0.32f, 0.34f, 0.40f));
            card.Add(_confirmLeaveSubPanel);

            // Subpainel Controles
            _controlsSubPanel = new VisualElement();
            _controlsSubPanel.style.display = DisplayStyle.None;
            var controlsText = new Label(
                "Movimento: WASD / Shift (Correr)\n" +
                "Interagir / Montar Moto / Equipar Arma: [E]\n" +
                "Reparar Mochila (3s) / Arquivar Comanda: [F]\n" +
                "Abrir Mochila: [B]\n" +
                "Mirar / Atirar: [Botão Direito] / [Botão Esquerdo]\n" +
                "Recarregar Arma: [R]\n" +
                "Guia de Receitas / Buzina na Moto: [H]\n" +
                "Trocar Mão Ativa: [1] / [2]\n" +
                "Minimapa Tela Cheia: [M] | Pausa: [ESC]");
            controlsText.style.fontSize = 15;
            controlsText.style.color = new Color(0.92f, 0.92f, 0.96f);
            controlsText.style.marginBottom = 14;
            _controlsSubPanel.Add(controlsText);
            AddMenuButton(_controlsSubPanel, "Voltar", () => ShowSubPanel(_mainPanel), new Color(0.32f, 0.32f, 0.38f));
            card.Add(_controlsSubPanel);

            // Subpainel Configurações Rápidas
            _settingsSubPanel = new VisualElement();
            _settingsSubPanel.style.display = DisplayStyle.None;

            float savedMaster = PlayerPrefs.GetFloat("MasterVolume", 1.0f);
            float savedSens = PlayerPrefs.GetFloat("LookSensitivity", 1.0f);

            var volSlider = new Slider("Volume Geral", 0f, 1f) { value = savedMaster };
            volSlider.style.color = Color.white;
            volSlider.style.marginBottom = 10;
            volSlider.RegisterValueChangedCallback(evt =>
            {
                AudioListener.volume = evt.newValue;
                PlayerPrefs.SetFloat("MasterVolume", evt.newValue);
            });
            _settingsSubPanel.Add(volSlider);

            var sensSlider = new Slider("Sensibilidade Câmera", 0.2f, 3.0f) { value = savedSens };
            sensSlider.style.color = Color.white;
            sensSlider.style.marginBottom = 16;
            sensSlider.RegisterValueChangedCallback(evt =>
            {
                PlayerPrefs.SetFloat("LookSensitivity", evt.newValue);
                var cams = FindObjectsByType<FirstPersonCameraController>(FindObjectsSortMode.None);
                for (int i = 0; i < cams.Length; i++)
                {
                    if (cams[i] != null) cams[i].mouseSensitivity = evt.newValue;
                }
            });
            _settingsSubPanel.Add(sensSlider);

            AddMenuButton(_settingsSubPanel, "Salvar e Voltar", () =>
            {
                PlayerPrefs.Save();
                ShowSubPanel(_mainPanel);
            }, new Color(0.22f, 0.55f, 0.32f));
            card.Add(_settingsSubPanel);

            RootElement.Add(card);
            _root.Add(RootElement);
        }

        private Button AddMenuButton(VisualElement parent, string text, Action onClick, Color bg)
        {
            var btn = new Button(onClick) { text = text };
            btn.style.height = 42;
            btn.style.marginBottom = 8;
            btn.style.fontSize = 16;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.backgroundColor = bg;
            btn.style.color = Color.white;
            btn.style.borderTopLeftRadius = 6;
            btn.style.borderTopRightRadius = 6;
            btn.style.borderBottomLeftRadius = 6;
            btn.style.borderBottomRightRadius = 6;
            parent.Add(btn);
            return btn;
        }

        private void ShowSubPanel(VisualElement target)
        {
            if (_mainPanel != null) _mainPanel.style.display = target == _mainPanel ? DisplayStyle.Flex : DisplayStyle.None;
            if (_controlsSubPanel != null) _controlsSubPanel.style.display = target == _controlsSubPanel ? DisplayStyle.Flex : DisplayStyle.None;
            if (_settingsSubPanel != null) _settingsSubPanel.style.display = target == _settingsSubPanel ? DisplayStyle.Flex : DisplayStyle.None;
            if (_confirmLeaveSubPanel != null) _confirmLeaveSubPanel.style.display = target == _confirmLeaveSubPanel ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private bool IsSubPanelVisible =>
            (_controlsSubPanel != null && _controlsSubPanel.style.display == DisplayStyle.Flex) ||
            (_settingsSubPanel != null && _settingsSubPanel.style.display == DisplayStyle.Flex) ||
            (_confirmLeaveSubPanel != null && _confirmLeaveSubPanel.style.display == DisplayStyle.Flex);

        private static bool WasEscapePressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        private bool IsAnyOtherModalOpen()
        {
            if (UIModalStack.Instance != null && UIModalStack.Instance.HasActiveModal)
                return true;
            if (TicketModalPresenter.Instance != null && TicketModalPresenter.Instance.IsOpen)
                return true;
            if (BackpackPanelPresenter.Instance != null && BackpackPanelPresenter.Instance.IsOpen)
                return true;
            if (RecipeBookPresenter.Instance != null && RecipeBookPresenter.Instance.IsOpen)
                return true;
            if (DeliverySelectionPresenter.Instance != null && DeliverySelectionPresenter.Instance.IsOpen)
                return true;
            if (DeliveryResultPresenter.Instance != null && DeliveryResultPresenter.Instance.IsOpen)
                return true;
            if (NightResultsPresenter.Instance != null && NightResultsPresenter.Instance.IsOpen)
                return true;
            return false;
        }

        private void Update()
        {
            bool otherModalOpen = IsAnyOtherModalOpen();
            bool escPressed = WasEscapePressedThisFrame();

            if (_isOpen)
            {
                UpdateRecoverButtonLabels();
                if (escPressed && Time.frameCount != _lastToggleFrame)
                {
                    if (IsSubPanelVisible)
                    {
                        _lastToggleFrame = Time.frameCount;
                        ShowSubPanel(_mainPanel);
                    }
                    else if (!_pushedToStack)
                    {
                        Close();
                    }
                }
                _otherModalWasOpenLastFrame = otherModalOpen;
                return;
            }

            if (escPressed && Time.frameCount != _lastToggleFrame && !otherModalOpen && !_otherModalWasOpenLastFrame)
            {
                Open();
            }

            _otherModalWasOpenLastFrame = otherModalOpen;
        }

        public void Open()
        {
            EnsureUIInitialized();
            if (RootElement == null || _isOpen) return;

            _isOpen = true;
            _lastToggleFrame = Time.frameCount;
            _openedUnscaledTime = Time.unscaledTime;
            ShowSubPanel(_mainPanel);
            UpdateRecoverButtonLabels();
            RootElement.style.display = DisplayStyle.Flex;

            if (UIModalStack.Instance != null)
            {
                UIModalStack.Instance.PushModal(this);
                _pushedToStack = true;
            }
            else
            {
                GameplayInputGate.PushLock();
                _pushedGateFallback = true;
            }
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            _lastToggleFrame = Time.frameCount;

            if (RootElement != null)
                RootElement.style.display = DisplayStyle.None;

            ReleaseLock();
        }

        private void ReleaseLock()
        {
            if (_pushedToStack)
            {
                _pushedToStack = false;
                UIModalStack.Instance?.PopModal(this);
            }
            if (_pushedGateFallback)
            {
                _pushedGateFallback = false;
                GameplayInputGate.PopLock();
            }
        }

        private void UpdateRecoverButtonLabels()
        {
            float now = Time.unscaledTime;
            if (_recoverPlayerBtn != null)
            {
                float rem = _playerRecoverCooldownUntil - now;
                _recoverPlayerBtn.text = rem > 0.5f
                    ? $"Recuperar Jogador ({Mathf.CeilToInt(rem)}s)"
                    : "Recuperar Jogador (Pronto)";
                _recoverPlayerBtn.SetEnabled(rem <= 0f);
            }

            if (_recoverMotoBtn != null)
            {
                float rem = _motoRecoverCooldownUntil - now;
                _recoverMotoBtn.text = rem > 0.5f
                    ? $"Recuperar Moto ({Mathf.CeilToInt(rem)}s)"
                    : "Recuperar Moto (Pronto)";
                _recoverMotoBtn.SetEnabled(rem <= 0f);
            }
        }

        private void RequestRecoverPlayer()
        {
            if (Time.unscaledTime < _playerRecoverCooldownUntil) return;
            _playerRecoverCooldownUntil = Time.unscaledTime + 60f;
            PizzeriaCommandHandler.Instance?.RequestRecoverPlayerServerRpc();
            Close();
        }

        private void RequestRecoverMoto()
        {
            if (Time.unscaledTime < _motoRecoverCooldownUntil) return;
            _motoRecoverCooldownUntil = Time.unscaledTime + 60f;
            PizzeriaCommandHandler.Instance?.RequestRecoverMotorcycleServerRpc();
            Close();
        }

        private void LeaveMatch()
        {
            Close();
            GameplayInputGate.Reset();

            if (RoomDiscoveryService.Instance != null)
                RoomDiscoveryService.Instance.StopHosting();

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();

            SceneManager.LoadScene("LobbyScene");
        }
    }
}
